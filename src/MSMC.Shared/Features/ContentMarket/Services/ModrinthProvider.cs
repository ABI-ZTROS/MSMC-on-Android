// -----------------------------------------------------------------------------
// 文件名: ModrinthProvider.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: Modrinth API 提供器 —— 搜索/版本/下载
// 设计模式: 三链原则 - 因果链：搜索请求 → 项目列表；执行链：HTTP 容错 + 进度回调
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
/// Modrinth API v2 客户端
/// 文档: https://docs.modrinth.com/
/// </summary>
public class ModrinthProvider : IMarketProvider
{
    private const string BaseUrl = "https://api.modrinth.com/v2";
    // P7 资源诚信：禁止每方法 new HttpClient（Socket 泄漏），改为 static 单例
    private static readonly HttpClient _sharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    // P7 — static 构造器里只跑一次 UserAgent 配置，不要反复加
    static ModrinthProvider()
    {
        _sharedHttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MSMC", "1.0"));
    }
    private readonly ILogger<ModrinthProvider> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // P7 / 搜索修复：从 categories 中识别 loader 名称的关键词白名单
    // （实测 Modrinth categories 中会把 "bukkit"/"paper"/"fabric" 这种 loader 作为 category）
    private static readonly HashSet<string> _knownLoaderKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "forge", "fabric", "quilt",
        "bukkit", "spigot", "paper", "purpur", "folia",
        "velocity", "bungeecord", "bungee"
    };

    public MarketSource Source => MarketSource.Modrinth;

    public ModrinthProvider(ILogger<ModrinthProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 搜索 Modrinth 项目
    /// </summary>
    public async Task<IReadOnlyList<MarketProject>> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString["query"] = request.Query;
        queryString["limit"] = request.Limit.ToString();
        queryString["offset"] = request.Offset.ToString();

        // 构造 facets：默认搜 plugin（服务器插件），不是 mod（客户端模组）
        var facets = new List<string> { "[\"project_type:plugin\"]" };

        if (!string.IsNullOrEmpty(request.GameVersion))
            facets.Add($"[\"versions:{request.GameVersion}\"]");

        if (request.Loader.HasValue)
        {
            string loaderValue = request.Loader.Value switch
            {
                ModLoader.Bukkit => "bukkit",
                ModLoader.Spigot => "spigot",
                ModLoader.Paper => "paper",
                ModLoader.Purpur => "purpur",
                ModLoader.Folia => "folia",
                ModLoader.Velocity => "velocity",
                ModLoader.BungeeCord => "bungeecord",
                ModLoader.Forge => "forge",
                ModLoader.Fabric => "fabric",
                ModLoader.Quilt => "quilt",
                _ => ""
            };
            if (!string.IsNullOrEmpty(loaderValue))
                facets.Add($"[\"loaders:{loaderValue}\"]");
        }

        if (!string.IsNullOrEmpty(request.Category))
            facets.Add($"[\"categories:{request.Category}\"]");

        queryString["facets"] = $"[{string.Join(",", facets)}]";

        var url = $"{BaseUrl}/search?{queryString}";
        _logger.LogInformation("[Modrinth] Searching: {Query} (limit={Limit})", request.Query, request.Limit);

        try
        {
            var json = await _sharedHttpClient.GetStringAsync(url, ct);
            var response = JsonSerializer.Deserialize<ModrinthSearchResponse>(json, _jsonOptions);
            if (response == null) return new List<MarketProject>();

            var projects = response.Hits.Select(h =>
            {
                // Bug 修复（因果链 - API 字段漂移）：
                // Modrinth /v2/search 结果已不再提供独立的 loaders 字段，
                // 加载器信息混在 categories 数组里（如 "bukkit", "paper"）。
                // 必须从 categories 中抽取已知的 loader 名称作为 SupportedLoaders。
                // 实测 API 返回: categories=['bukkit','economy','paper','spigot',...]
                var hitLoaders = (h.Loaders != null && h.Loaders.Count > 0)
                    ? h.Loaders
                    : (h.Categories ?? new List<string>())
                        .Where(c => _knownLoaderKeywords.Contains(c.ToLowerInvariant()))
                        .ToList();

                return new MarketProject
                {
                    Id = h.ProjectId ?? h.Project_id ?? string.Empty,
                    Slug = h.Slug ?? string.Empty,
                    Name = h.Title ?? string.Empty,
                    Description = h.Description ?? string.Empty,
                    Author = h.Author ?? string.Empty,
                    IconUrl = h.IconUrl,
                    Downloads = h.Downloads,
                    Followers = h.Follows,
                    Source = MarketSource.Modrinth,
                    Categories = h.Categories ?? new List<string>(),
                    SupportedLoaders = hitLoaders
                        .Select(ParseModLoader).Where(l => l != ModLoader.Generic).ToList(),
                    GameVersions = h.Versions ?? new List<string>(),
                    UpdatedAt = h.DateModified
                };
            }).ToList();

            _logger.LogInformation("[Modrinth] Found {Count} results", projects.Count);

            return projects;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Modrinth] Search failed for query: {Query}", request.Query);
            return new List<MarketProject>();
        }
    }

    /// <summary>
    /// 获取项目的可用版本列表
    /// </summary>
    public async Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(string projectId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/project/{projectId}/version";
        _logger.LogInformation("[Modrinth] Fetching versions for project: {ProjectId}", projectId);

        try
        {
            var json = await _sharedHttpClient.GetStringAsync(url, ct);
            var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(json, _jsonOptions);
            if (versions == null) return new List<MarketVersion>();

            return versions.Select(v => new MarketVersion
            {
                Id = v.Id,
                ProjectId = v.ProjectId,
                VersionNumber = v.VersionNumber,
                Name = v.Name,
                Loaders = v.Loaders?.Select(ParseModLoader).ToList() ?? new List<ModLoader>(),
                GameVersions = v.GameVersions ?? new List<string>(),
                Changelog = v.Changelog,
                ReleasedAt = v.DatePublished,
                IsPreRelease = v.VersionType?.Contains("beta", StringComparison.OrdinalIgnoreCase) == true ||
                               v.VersionType?.Contains("alpha", StringComparison.OrdinalIgnoreCase) == true,
                DownloadUrl = v.Files?.FirstOrDefault(f => f.Primary)?.Url ?? v.Files?.FirstOrDefault()?.Url,
                FileSize = v.Files?.FirstOrDefault()?.Size ?? 0,
                Sha1Hash = v.Files?.FirstOrDefault()?.Hashes?.Sha1
            }).ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Modrinth] Version fetch failed for project: {ProjectId}", projectId);
            return new List<MarketVersion>();
        }
    }

    /// <summary>
    /// 下载指定版本文件（带进度回调 + 取消支持）
    /// </summary>
    public async Task<byte[]> DownloadVersionAsync(
        string versionId,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        // 1. 先获取版本详情以得到下载 URL
        var versionUrl = $"{BaseUrl}/version/{versionId}";
        _logger.LogInformation("[Modrinth] Fetching download URL for version: {VersionId}", versionId);

        ModrinthVersion? version;
        try
        {
            var json = await _sharedHttpClient.GetStringAsync(versionUrl, ct);
            version = JsonSerializer.Deserialize<ModrinthVersion>(json, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Modrinth] Version fetch failed: {VersionId}", versionId);
            throw;
        }

        var fileUrl = version?.Files?.FirstOrDefault(f => f.Primary)?.Url
            ?? version?.Files?.FirstOrDefault()?.Url;
        if (string.IsNullOrEmpty(fileUrl))
            throw new InvalidOperationException($"No download URL found for version {versionId}");

        _logger.LogInformation("[Modrinth] Downloading version {VersionId} from {Url}", versionId, fileUrl);

        // 2. 下载文件（带进度）
        using var response = await _sharedHttpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memoryStream = new MemoryStream();

        var buffer = new byte[65536]; // 64KB buffer
        long totalRead = 0;
        int bytesRead;
        long lastProgressReport = 0;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await memoryStream.WriteAsync(buffer, 0, bytesRead, ct);
            totalRead += bytesRead;

            // 每至少 5% 或 100KB 报告一次进度
            if (progress != null && totalBytes.HasValue)
            {
                long reportThreshold = Math.Max(totalBytes.Value / 20, 102400);
                if (totalRead - lastProgressReport >= reportThreshold || totalRead == totalBytes.Value)
                {
                    progress.Report(new DownloadProgress { BytesDownloaded = totalRead, TotalBytes = totalBytes.Value });
                    lastProgressReport = totalRead;
                }
            }
        }

        progress?.Report(new DownloadProgress { BytesDownloaded = totalRead, TotalBytes = totalBytes ?? 0 });
        _logger.LogInformation("[Modrinth] Download complete: {Bytes} bytes for version {VersionId}",
            totalRead, versionId);

        return memoryStream.ToArray();
    }

    private static ModLoader ParseModLoader(string loaderStr)
    {
        return loaderStr.ToLowerInvariant() switch
        {
            "forge" => ModLoader.Forge,
            "fabric" => ModLoader.Fabric,
            "quilt" => ModLoader.Quilt,
            "bukkit" => ModLoader.Bukkit,
            "spigot" => ModLoader.Spigot,
            "paper" => ModLoader.Paper,
            "purpur" => ModLoader.Purpur,
            "folia" => ModLoader.Folia,
            "velocity" => ModLoader.Velocity,
            "bungeecord" => ModLoader.BungeeCord,
            _ => ModLoader.Generic
        };
    }

    #region 私有 DTO（仅用于 JSON 反序列化）

    private class ModrinthSearchResponse
    {
        public int TotalHits { get; set; }
        public List<ModrinthHit> Hits { get; set; } = new();
    }

    private class ModrinthHit
    {
        public string ProjectId { get; set; } = string.Empty;
        public string Project_id { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public long Downloads { get; set; }
        public long Follows { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<string> Loaders { get; set; } = new();
        public List<string> Versions { get; set; } = new();
        public DateTimeOffset? DateModified { get; set; }
    }

    private class ModrinthVersion
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string Project_id { get; set; } = string.Empty;
        public string VersionNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string VersionType { get; set; } = string.Empty;
        public string Changelog { get; set; } = string.Empty;
        public List<string> GameVersions { get; set; } = new();
        public List<string> Loaders { get; set; } = new();
        public DateTimeOffset? DatePublished { get; set; }
        public List<ModrinthFile> Files { get; set; } = new();
    }

    private class ModrinthFile
    {
        public string Url { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public bool Primary { get; set; }
        public long Size { get; set; }
        public ModrinthFileHashes? Hashes { get; set; }
    }

    private class ModrinthFileHashes
    {
        public string Sha1 { get; set; } = string.Empty;
        public string Sha512 { get; set; } = string.Empty;
    }

    #endregion
}
