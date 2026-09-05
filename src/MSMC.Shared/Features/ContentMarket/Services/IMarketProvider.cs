// -----------------------------------------------------------------------------
// 文件名: IMarketProvider.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: 市场提供者接口
// -----------------------------------------------------------------------------

using io.NET.ZTR_OS.Features.ContentMarket.Models;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

public interface IMarketProvider
{
    MarketSource Source { get; }
    Task<IReadOnlyList<MarketProject>> SearchAsync(SearchRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(string projectId, CancellationToken ct = default);
    Task<byte[]> DownloadVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default);
}
