// -----------------------------------------------------------------------------
// 文件名: HangarProvider.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: PaperMC Hangar API 提供器 —— 搜索/版本/下载
// 文档: https://hangar.papermc.io/api-docs
// -----------------------------------------------------------------------------

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

/// <summary>
/// PaperMC Hangar API v1 客户端
/// Hangar 是 PaperMC 官方插件市场，主要收录 Paper/Purpur/Folia 生态的插件
/// </summary>
public class HangarProvider : IMarketProvider
{
    private const string BaseUrl = "https://hangar.papermc.io/api/v1";
    // P7 资源诚信：禁止每方法 new HttpClient（Socket 泄漏），改为 static 单例
    private static readonly HttpClient _sharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    static HangarProvider()
    {
        _sharedHttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MSMC", "1.0"));
    }
    private readonly ILogger<HangarProvider> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MarketSource Source => MarketSource.Hangar;

    public HangarProvider(ILogger<HangarProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<MarketProject>> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString["query"] = request.Query;
        queryString["limit"] = request.Limit.ToString();
        queryString["offset"] = request.Offset.ToString();

        if (!string.IsNullOrEmpty(request.Category))
            queryString["category"] = request.Category;

        var url = $"{BaseUrl}/projects?{queryString}";
        _logger.LogDebug("Hangar 搜索: {Url}", url);

        try
        {
            var json = await _sharedHttpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var results = new List<MarketProject>();
            if (root.TryGetProperty("result", out var resultArr) && resultArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resultArr.EnumerateArray())
                {
                    results.Add(ParseProject(item));
                }
            }
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Hangar 搜索 HTTP 错误");
            return Array.Empty<MarketProject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hangar 搜索异常");
            return Array.Empty<MarketProject>();
        }
    }

    public async Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(string projectId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/projects/{Uri.EscapeDataString(projectId)}/versions?limit=50";
        _logger.LogDebug("Hangar 版本查询: {Url}", url);

        try
        {
            var json = await _sharedHttpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var versions = new List<MarketVersion>();
            if (root.TryGetProperty("result", out var resultArr) && resultArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resultArr.EnumerateArray())
                {
                    versions.Add(ParseVersion(item, projectId));
                }
            }
            return versions;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Hangar 版本查询 HTTP 错误");
            return Array.Empty<MarketVersion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hangar 版本查询异常");
            return Array.Empty<MarketVersion>();
        }
    }

    // P4 诚实返回链：HangarProvider 不支持仅凭 versionId 下载，抛异常而非假成功空数组
    // （之前返回 Array.Empty<byte>()，导致 PluginManagerService 记录安装成功但写入 0 字节空文件，典型"假成功"）
    public async Task<byte[]> DownloadVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        _logger.LogError(
            "[Hangar] DownloadVersionAsync({VersionId}) 不支持：Hangar API 下载需要 projectSlug + versionName + 下载文件名，" +
            "仅版本 id 无法定位下载地址。请使用 MarketVersion.DownloadUrl（若有值）走直链下载，" +
            "或在安装前通过 GetVersionsAsync 获取版本时确认 DownloadUrl 不为 null。",
            versionId);
        await Task.CompletedTask; // 保持异步签名（与接口一致）
        throw new NotSupportedException(
            $"HangarProvider.DownloadVersionAsync 不能仅靠 versionId ({versionId}) 下载。" +
            "若版本的 DownloadUrl 有值，请让 PluginManagerService 走直链下载；若无值说明该项目使用外部发布链接（GitHub Release 等），MSMC 侧无法直接下载。");
    }

    private static MarketProject ParseProject(JsonElement item)
    {
        var project = new MarketProject
        {
            Source = MarketSource.Hangar,
            Id = item.TryGetProperty("id", out var idEl) ? idEl.GetInt32().ToString() : "",
            Name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
            Description = item.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "",
            UpdatedAt = item.TryGetProperty("lastUpdated", out var updEl) && updEl.TryGetDateTimeOffset(out var upd) ? upd : null,
        };

        // namespace.slug / namespace.owner
        if (item.TryGetProperty("namespace", out var nsEl))
        {
            if (nsEl.TryGetProperty("slug", out var slugEl))
                project.Slug = slugEl.GetString() ?? "";
            if (nsEl.TryGetProperty("owner", out var ownerEl))
                project.Author = ownerEl.GetString() ?? "";
        }

        // stats
        if (item.TryGetProperty("stats", out var statsEl))
        {
            if (statsEl.TryGetProperty("downloads", out var dlEl))
                project.Downloads = dlEl.GetInt64();
            if (statsEl.TryGetProperty("watchers", out var watchEl))
                project.Followers = watchEl.GetInt64();
        }

        // category
        if (item.TryGetProperty("category", out var catEl) && catEl.GetString() is { } cat)
            project.Categories.Add(cat);

        // supportedPlatforms (字典: PAPER -> versions[])
        if (item.TryGetProperty("supportedPlatforms", out var spEl) && spEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in spEl.EnumerateObject())
            {
                if (Enum.TryParse<ModLoader>(prop.Name, true, out var loader))
                    project.SupportedLoaders.Add(loader);

                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in prop.Value.EnumerateArray())
                    {
                        if (v.GetString() is { } gv && !project.GameVersions.Contains(gv))
                            project.GameVersions.Add(gv);
                    }
                }
            }
        }

        // iconUrl — Hangar 项目没有直接的 icon 字段，可用 namespace slug 构造头像
        if (item.TryGetProperty("namespace", out var ns) && ns.TryGetProperty("owner", out var owner))
        {
            var ownerStr = owner.GetString() ?? "";
            if (!string.IsNullOrEmpty(ownerStr))
                project.IconUrl = $"https://hangarcdn.papermc.io/avatars/{ownerStr}.png";
        }

        project.ProjectUrl = $"https://hangar.papermc.io/{project.Slug}";

        return project;
    }

    private static MarketVersion ParseVersion(JsonElement item, string projectId)
    {
        var version = new MarketVersion
        {
            ProjectId = projectId,
            Id = item.TryGetProperty("id", out var idEl) ? idEl.GetInt32().ToString() : "",
            VersionNumber = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
            Name = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "",
            Changelog = item.TryGetProperty("description", out var descEl) ? descEl.GetString() : null,
            ReleasedAt = item.TryGetProperty("createdAt", out var caEl) && caEl.TryGetDateTimeOffset(out var ca) ? ca : null,
        };

        // P4 诚实返回链 + 因果链（新版 API 结构已变更）：
        // Hangar API 旧版: downloads.PAPER.downloadName (文件名字符串)
        // Hangar API 新版: downloads.PAPER = { fileInfo, externalUrl, downloadUrl }
        //   - downloadUrl = Hangar 直链下载（最优，若有）
        //   - fileInfo = { size, md5, sha1, downloadUrl } (有些版本用这个结构)
        //   - externalUrl = 外部发布链接（GitHub Release 页，不是直链，不能用于下载）
        // 诚实原则：只有 directUrl 能下到 JAR 才填 DownloadUrl，否则留空。
        if (item.TryGetProperty("downloads", out var dlEl) && dlEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in dlEl.EnumerateObject())
            {
                if (!prop.Name.Equals("PAPER", StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;

                var platformObj = prop.Value;
                string? directUrl = null;

                // 1) 顶层 downloadUrl（新版 API）
                if (platformObj.TryGetProperty("downloadUrl", out var topUrlEl)
                    && topUrlEl.GetString() is { } topUrl && !string.IsNullOrEmpty(topUrl))
                {
                    directUrl = topUrl;
                }
                // 2) fileInfo.downloadUrl / fileInfo.url（嵌套结构 API）
                if (directUrl == null && platformObj.TryGetProperty("fileInfo", out var fiEl)
                    && fiEl.ValueKind == JsonValueKind.Object)
                {
                    if (fiEl.TryGetProperty("downloadUrl", out var fiUrlEl)
                        && fiUrlEl.GetString() is { } fiUrl && !string.IsNullOrEmpty(fiUrl))
                    {
                        directUrl = fiUrl;
                    }
                    else if (fiEl.TryGetProperty("url", out var fiUrl2El)
                        && fiUrl2El.GetString() is { } fiUrl2 && !string.IsNullOrEmpty(fiUrl2))
                    {
                        directUrl = fiUrl2;
                    }
                }
                // 3) 老版本：downloadName（旧版字段，若仍存在则用旧路径拼接）
                if (directUrl == null && platformObj.TryGetProperty("downloadName", out var dnEl)
                    && dnEl.GetString() is { } dn && !string.IsNullOrEmpty(dn))
                {
                    directUrl = $"{BaseUrl}/projects/{Uri.EscapeDataString(projectId)}/versions/{Uri.EscapeDataString(version.VersionNumber)}/{Uri.EscapeDataString(dn)}";
                }

                // 4) externalUrl — 仅记录日志，不作为 directUrl（外部页不是直链 JAR）
                if (directUrl == null && platformObj.TryGetProperty("externalUrl", out var extEl)
                    && extEl.GetString() is { } extUrl && !string.IsNullOrEmpty(extUrl))
                {
                    // P4 诚实：externalUrl 指向 GitHub Release 页，无法直下 JAR，不填 DownloadUrl
                    // _logger 此时不可用（static ParseVersion），保留注释说明行为
                    // 调用方看到 DownloadUrl 为 null 就会走「诚实报错」路径而非空文件
                }

                if (!string.IsNullOrEmpty(directUrl))
                {
                    version.DownloadUrl = directUrl;
                    break;
                }
            }
        }

        // platformDependencies (游戏版本)
        if (item.TryGetProperty("platformDependencies", out var pdEl) && pdEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in pdEl.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in prop.Value.EnumerateArray())
                    {
                        if (v.GetString() is { } gv && !version.GameVersions.Contains(gv))
                            version.GameVersions.Add(gv);
                    }
                }
            }
        }

        return version;
    }
}
