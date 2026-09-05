// -----------------------------------------------------------------------------
// 文件名: MarketProviderFactory.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: 多 Provider 聚合工厂 —— 并行搜索、去重、返回合并结果
// -----------------------------------------------------------------------------

using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

/// <summary>
/// 多市场 Provider 聚合工厂
/// 并行搜索多个来源（Modrinth + Hangar + Spiget），合并去重返回
/// </summary>
public class MarketProviderFactory
{
    private readonly IEnumerable<IMarketProvider> _providers;
    private readonly ILogger<MarketProviderFactory> _logger;

    public MarketProviderFactory(
        IEnumerable<IMarketProvider> providers,
        ILogger<MarketProviderFactory> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// 搜索：并行查所有 Provider 或指定 source
    /// </summary>
    public async Task<List<MarketProject>> SearchAsync(SearchRequest request, string? source = null, CancellationToken ct = default)
    {
        var targets = string.IsNullOrEmpty(source)
            ? _providers
            : _providers.Where(p => p.Source.ToString().Equals(source, StringComparison.OrdinalIgnoreCase));

        var targetList = targets.ToList();
        if (targetList.Count == 0)
        {
            _logger.LogWarning("没有匹配的 Provider (source={Source})", source ?? "all");
            return new List<MarketProject>();
        }

        _logger.LogInformation("Market 搜索 [{SourceCount} 源]: {Query}", targetList.Count, request.Query);

        // 并行搜索
        var tasks = targetList.Select(p => SearchWithTimeoutAsync(p, request, ct));
        var results = await Task.WhenAll(tasks);

        // 合并 + 去重（按 Name + Source）
        var merged = new List<MarketProject>();
        var seen = new HashSet<string>();
        foreach (var batch in results)
        {
            foreach (var project in batch)
            {
                var key = $"{project.Source}|{project.Name.ToLowerInvariant()}";
                if (seen.Add(key))
                    merged.Add(project);
            }
        }

        // 按下载量排序，取 Top N
        var topN = merged
            .OrderByDescending(p => p.Downloads)
            .Take(request.Limit)
            .ToList();

        _logger.LogInformation("Market 搜索完成: 返回 {Count} 条", topN.Count);
        return topN;
    }

    /// <summary>
    /// 版本查询：根据 source 路由到对应 Provider
    /// </summary>
    public async Task<List<MarketVersion>> GetVersionsAsync(string projectId, string source, CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p =>
            p.Source.ToString().Equals(source, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            // 没有指定 source，遍历所有 Provider 找第一个能返回版本的
            _logger.LogDebug("未指定 source，遍历所有 Provider 查询版本");
            foreach (var p in _providers)
            {
                try
                {
                    var versions = await GetVersionsWithTimeoutAsync(p, projectId, ct);
                    if (versions.Count > 0)
                        return versions.ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "{Provider} 版本查询失败", p.Source);
                }
            }
            return new List<MarketVersion>();
        }

        var result = await GetVersionsWithTimeoutAsync(provider, projectId, ct);
        return result.ToList();
    }

    private async Task<IReadOnlyList<MarketProject>> SearchWithTimeoutAsync(
        IMarketProvider provider, SearchRequest request, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            return await provider.SearchAsync(request, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Provider} 搜索失败: {Query}", provider.Source, request.Query);
            return Array.Empty<MarketProject>();
        }
    }

    private async Task<IReadOnlyList<MarketVersion>> GetVersionsWithTimeoutAsync(
        IMarketProvider provider, string projectId, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            return await provider.GetVersionsAsync(projectId, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Provider} 版本查询失败: {Id}", provider.Source, projectId);
            return Array.Empty<MarketVersion>();
        }
    }
}
