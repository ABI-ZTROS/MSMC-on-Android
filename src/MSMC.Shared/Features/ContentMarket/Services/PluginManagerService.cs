// -----------------------------------------------------------------------------
// 文件名: PluginManagerService.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: 插件管理服务 —— 安装/更新/卸载 + SHA1 校验 + 安全备份
// 设计模式: 三链原则 - 执行链：文件备份 + Hash 校验；返回链：安装审计日志
// -----------------------------------------------------------------------------

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

/// <summary>
/// 插件管理服务 —— 负责下载、安装、更新、卸载
/// </summary>
public class PluginManagerService
{
    private readonly ILogger<PluginManagerService> _logger;
    private readonly IMarketProvider _provider;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // P7 资源诚信：直链下载共享 static HttpClient（P4 诚实链的 downloadUrl fallback 必用它）
    // 不要每安装一个就 new HttpClient——旧 Provider 每次 new HttpClient 已经在各 Provider 里修了，
    // 这里再加一套给「version.DownloadUrl 直链下载」兜底路径用。
    private static readonly HttpClient _sharedDownloadClient = new()
    {
        Timeout = TimeSpan.FromSeconds(120)
    };
    static PluginManagerService()
    {
        _sharedDownloadClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MSMC", "1.0"));
        _sharedDownloadClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("*/*"));
    }

    public PluginManagerService(ILogger<PluginManagerService> logger, IMarketProvider provider)
    {
        _logger = logger;
        _provider = provider;
    }

    /// <summary>
    /// 安装插件到服务器目录
    /// </summary>
    public async Task<InstallResult> InstallAsync(MarketVersion version, string serverPath, CancellationToken ct = default)
    {
        _logger.LogInformation("[PluginMgr] Starting installation: {VersionName} (Id={VersionId})",
            version.Name, version.Id);

        // 1. 校验输入
        if (string.IsNullOrEmpty(serverPath))
            return InstallResult.Failed(version.ProjectId, "Server path cannot be empty");
        if (string.IsNullOrEmpty(version.DownloadUrl))
            return InstallResult.Failed(version.ProjectId, "No download URL available for this version");

        var pluginsDir = Path.Combine(serverPath, "plugins");
        try
        {
            Directory.CreateDirectory(pluginsDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Failed to create plugins directory: {Path}", pluginsDir);
            return InstallResult.Failed(version.ProjectId, $"Cannot create plugins directory: {ex.Message}");
        }

        // 2. 计算目标文件名
        string safeName = SanitizeFileName(version.Name);
        string destPath = Path.Combine(pluginsDir, $"{safeName}.jar");

        // 3. 安全备份
        string? backupPath = null;
        if (File.Exists(destPath))
        {
            try
            {
                string backupDir = Path.Combine(pluginsDir, ".msmc_backups");
                Directory.CreateDirectory(backupDir);
                backupPath = Path.Combine(backupDir, $"{safeName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.jar.bak");
                File.Copy(destPath, backupPath, overwrite: true);
                _logger.LogInformation("[PluginMgr] Backup created: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PluginMgr] Backup failed, proceeding without backup");
                backupPath = null;
            }
        }

        // 4. 下载文件
        // P4 诚实返回链 + 返回链修复：
        //   HangarProvider / SpigetProvider 现在会抛 NotSupportedException（而不是假成功的空数组）。
        //   如果 Provider 拒绝下载或抛 NotSupported，回退用 version.DownloadUrl 直链 HTTP GET。
        //   无论哪条路径，最终拿到的 fileBytes 长度为 0 → 诚实报错，不写入空文件不记录"成功"。
        byte[] fileBytes;
        try
        {
            try
            {
                fileBytes = await _provider.DownloadVersionAsync(version.Id, progress: null, ct);

                // P4 最高优先级：诚实 —— Provider 返回空数组 = 假成功残留，必须阻止。
                // 之前的 Bug：Hangar/Spiget 返回 Array.Empty<byte>() → 写入 0 字节 .jar → 安装标记成功。
                if (fileBytes.Length == 0)
                {
                    _logger.LogWarning(
                        "[PluginMgr] Provider.DownloadVersionAsync(VersionId={Id}) 返回 0 字节（假成功残留），" +
                        "尝试 version.DownloadUrl 直链下载兜底。", version.Id);
                    if (!string.IsNullOrEmpty(version.DownloadUrl))
                    {
                        fileBytes = await DownloadDirectAsync(version.DownloadUrl, ct);
                    }
                }
            }
            catch (NotSupportedException)
            {
                // Hangar/Spiget 诚实告知：仅凭 versionId 不能下载。预期行为。
                if (string.IsNullOrEmpty(version.DownloadUrl))
                {
                    _logger.LogError(
                        "[PluginMgr] Provider 拒绝下载(NotSupportedException)，且 version.DownloadUrl 为空，" +
                        "ProjectId={ProjectId} VersionNumber={Version}，无法继续安装。",
                        version.ProjectId, version.VersionNumber);
                    RestoreFromBackup(backupPath, destPath);
                    return InstallResult.Failed(version.ProjectId,
                        $"此版本没有可用的直链下载地址（通常是外部发布链接，需要手动安装）。");
                }
                _logger.LogInformation(
                    "[PluginMgr] Provider 明确拒绝该版本的专用下载，回退到 version.DownloadUrl 直链下载。");
                fileBytes = await DownloadDirectAsync(version.DownloadUrl, ct);
            }

            // P4 最终防线：任何路径拿到 0 字节都视为失败，绝不写进 plugins/ 也不记安装成功。
            if (fileBytes.Length == 0)
            {
                _logger.LogError(
                    "[PluginMgr] 两条下载路径都返回 0 字节。ProjectId={ProjectId} VersionNumber={Version}",
                    version.ProjectId, version.VersionNumber);
                RestoreFromBackup(backupPath, destPath);
                return InstallResult.Failed(version.ProjectId,
                    "下载内容为空（可能该版本发布在外部链接，MSMC 暂不支持自动安装）。请手动下载后放入 plugins/ 目录。");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Download failed for version {VersionId}", version.Id);
            RestoreFromBackup(backupPath, destPath);
            return InstallResult.Failed(version.ProjectId, $"Download failed: {ex.Message}");
        }

        // 5. SHA1 校验
        if (!string.IsNullOrEmpty(version.Sha1Hash))
        {
            var actualHash = ComputeSha1Hash(fileBytes);
            if (!actualHash.Equals(version.Sha1Hash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[PluginMgr] SHA1 hash mismatch. Expected={Expected}, Actual={Actual}",
                    version.Sha1Hash, actualHash);
                RestoreFromBackup(backupPath, destPath);
                return InstallResult.Failed(version.ProjectId, $"SHA1 hash mismatch: expected {version.Sha1Hash}, got {actualHash}");
            }
            _logger.LogInformation("[PluginMgr] SHA1 hash verified: {Hash}", actualHash);
        }

        // 6. 写入目标文件
        try
        {
            // 写入临时文件，成功后再原子替换
            string tempPath = destPath + ".tmp";
            await File.WriteAllBytesAsync(tempPath, fileBytes, ct);
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(tempPath, destPath);
            _logger.LogInformation("[PluginMgr] Plugin installed: {DestPath}", destPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Failed to write plugin file");
            RestoreFromBackup(backupPath, destPath);
            return InstallResult.Failed(version.ProjectId, $"File write failed: {ex.Message}");
        }

        // 7. 记录安装信息
        await SaveInstallRecordAsync(serverPath, new InstalledPlugin
        {
            Id = version.Id,
            ProjectId = version.ProjectId,
            ProjectName = version.Name,
            Version = version.VersionNumber,
            InstalledAt = DateTimeOffset.UtcNow,
            BackupPath = backupPath,
            ServerPath = serverPath,
            FileName = $"{safeName}.jar"
        });

        _logger.LogInformation("[PluginMgr] Installation complete: {Name} v{Version}", version.Name, version.VersionNumber);
        return InstallResult.Succeeded(
            projectId: version.ProjectId,
            projectName: version.Name,
            version: version.VersionNumber,
            backupPath: backupPath);
    }

    /// <summary>
    /// 卸载插件
    /// </summary>
    public async Task<bool> UninstallAsync(string serverPath, string fileName)
    {
        string pluginsDir = Path.Combine(serverPath, "plugins");
        string destPath = Path.Combine(pluginsDir, fileName);

        if (!File.Exists(destPath))
        {
            _logger.LogWarning("[PluginMgr] Plugin file not found: {Path}", destPath);
            return false;
        }

        try
        {
            // 先备份再删除
            string backupDir = Path.Combine(pluginsDir, ".msmc_backups");
            Directory.CreateDirectory(backupDir);
            string backupPath = Path.Combine(backupDir, $"{fileName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.jar.bak");
            File.Copy(destPath, backupPath, overwrite: true);

            File.Delete(destPath);
            _logger.LogInformation("[PluginMgr] Plugin uninstalled: {FileName} (backup at {Backup})", fileName, backupPath);

            await RemoveInstallRecordAsync(serverPath, fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Uninstall failed for: {FileName}", fileName);
            return false;
        }
    }

    /// <summary>
    /// 获取已安装插件列表
    /// </summary>
    public IReadOnlyList<InstalledPlugin> GetInstalledPlugins(string serverPath)
    {
        string metaPath = GetInstalledPluginsPath(serverPath);
        if (!File.Exists(metaPath))
            return new List<InstalledPlugin>();

        try
        {
            var json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<List<InstalledPlugin>>(json, _jsonOptions) ?? new List<InstalledPlugin>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Failed to read installed plugins list");
            return new List<InstalledPlugin>();
        }
    }

    private static string GetInstalledPluginsPath(string serverPath)
    {
        return Path.Combine(serverPath, "plugins", ".msmc", "installed-plugins.json");
    }

    private async Task SaveInstallRecordAsync(string serverPath, InstalledPlugin record)
    {
        try
        {
            string metaDir = Path.Combine(serverPath, "plugins", ".msmc");
            Directory.CreateDirectory(metaDir);

            string metaPath = GetInstalledPluginsPath(serverPath);
            var list = GetInstalledPlugins(serverPath).ToList();

            // 更新或添加
            var existing = list.FirstOrDefault(p => p.FileName == record.FileName);
            if (existing != null)
            {
                list.Remove(existing);
            }
            list.Add(record);

            // 原子写：先写临时文件再替换，防止写入中途崩溃损坏安装记录
            string tmpPath = metaPath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, JsonSerializer.Serialize(list, _jsonOptions));
            if (File.Exists(metaPath))
                File.Replace(tmpPath, metaPath, null);
            else
                File.Move(tmpPath, metaPath);

            _logger.LogDebug("[PluginMgr] Install record saved: {File}", record.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Failed to save install record: {File}", record.FileName);
            throw;
        }
    }

    private async Task RemoveInstallRecordAsync(string serverPath, string fileName)
    {
        try
        {
            string metaPath = GetInstalledPluginsPath(serverPath);
            if (!File.Exists(metaPath)) return;

            string metaDir = Path.Combine(serverPath, "plugins", ".msmc");
            Directory.CreateDirectory(metaDir);

            var list = GetInstalledPlugins(serverPath).ToList();
            list.RemoveAll(p => p.FileName == fileName);

            // 原子写：先写临时文件再替换，防止写入中途崩溃损坏安装记录
            string tmpPath = metaPath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, JsonSerializer.Serialize(list, _jsonOptions));
            File.Replace(tmpPath, metaPath, null);

            _logger.LogDebug("[PluginMgr] Install record removed: {File}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Failed to remove install record: {File}", fileName);
            throw;
        }
    }

    private static void RestoreFromBackup(string? backupPath, string destPath)
    {
        if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath)) return;

        try
        {
            File.Copy(backupPath, destPath, overwrite: true);
        }
        catch
        {
            // 恢复失败不阻塞流程
        }
    }

    /// <summary>
    /// P4 诚实 + 返回链修复新增：直链下载（作为 Provider 不能下载时的兜底）。
    /// 场景：
    ///   1) HangarProvider / SpigetProvider 抛 NotSupportedException（没有足够信息从 API 下载）
    ///   2) Provider 假成功地返回 0 字节（兼容性残留）
    /// 两种情况下我们尝试使用 version.DownloadUrl 直连 HTTP GET。
    /// 若 URL 本身无效或返回失败，本方法如实抛异常给上层，绝不吞错误。
    /// </summary>
    private async Task<byte[]> DownloadDirectAsync(string downloadUrl, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(downloadUrl))
            throw new ArgumentException("DownloadUrl 为空，无法直链下载。");

        // 规范化：有些 API 返回相对路径（如 Hangar v1 旧 downloadName 拼接结果）
        // 如果是完整 URL，直接用；如果是相对路径就不猜了直接报错。
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"下载地址不是完整 URL: {downloadUrl}");

        _logger.LogInformation("[PluginMgr] 直链下载: {Url}", downloadUrl);

        using var response = await _sharedDownloadClient.GetAsync(
            uri, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "[PluginMgr] 直链下载失败: {Url} HTTP {StatusCode} {Reason}",
                downloadUrl, (int)response.StatusCode, response.ReasonPhrase);
            throw new HttpRequestException(
                $"直链下载失败: HTTP {(int)response.StatusCode} ({response.ReasonPhrase})。URL: {downloadUrl}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memoryStream = new MemoryStream();
        var buffer = new byte[65536];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            await memoryStream.WriteAsync(buffer, 0, bytesRead, ct);

        _logger.LogInformation("[PluginMgr] 直链下载完成: {Bytes} bytes", memoryStream.Length);
        return memoryStream.ToArray();
    }

    private static string ComputeSha1Hash(byte[] data)
    {
        using var sha1 = SHA1.Create();
        byte[] hashBytes = sha1.ComputeHash(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return safe.Length > 60 ? safe[..60] : safe;
    }
}
