// -----------------------------------------------------------------------------
// 文件名: MarketProject.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Models
// 功能描述: 市场项目模型 —— Mod/Plugin 数据结构
// 设计模式: 三链原则 - 因果链：搜索关键词 → 项目列表
// -----------------------------------------------------------------------------

namespace io.NET.ZTR_OS.Features.ContentMarket.Models;

/// <summary>
/// 市场来源
/// </summary>
public enum MarketSource
{
    Modrinth,
    Hangar,        // PaperMC 官方插件站
    Spiget,        // SpigotMC 资源站
    CurseForge,
    Polymart,
    CustomUrl,
    Local
}

/// <summary>
/// Mod 加载器类型
/// </summary>
public enum ModLoader
{
    Forge,
    Fabric,
    Quilt,
    Bukkit,
    Spigot,
    Paper,
    Purpur,
    Folia,        // Paper 的区域多线程分支
    Velocity,
    BungeeCord,
    Generic
}

/// <summary>
/// 市场项目（搜索结果）
/// </summary>
public class MarketProject
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public long Downloads { get; set; }
    public long Followers { get; set; }
    public MarketSource Source { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<ModLoader> SupportedLoaders { get; set; } = new();
    public List<string> GameVersions { get; set; } = new();
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? ProjectUrl { get; set; }
}

/// <summary>
/// 市场版本（下载对象）
/// </summary>
public class MarketVersion
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string VersionNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DownloadUrl { get; set; }
    public string? Sha1Hash { get; set; }
    public long FileSize { get; set; }
    public List<ModLoader> Loaders { get; set; } = new();
    public List<string> GameVersions { get; set; } = new();
    public string? Changelog { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public bool IsPreRelease { get; set; }
}

/// <summary>
/// 搜索请求
/// </summary>
public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? GameVersion { get; set; }
    public ModLoader? Loader { get; set; }
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;
    public string? Category { get; set; }
}

/// <summary>
/// 搜索响应
/// </summary>
public class SearchResponse
{
    public int TotalHits { get; set; }
    public List<MarketProject> Projects { get; set; } = new();
}

/// <summary>
/// 下载进度
/// </summary>
public class DownloadProgress
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double Percentage => TotalBytes > 0 ? (double)BytesDownloaded / TotalBytes * 100 : 0;
}

/// <summary>
/// 安装结果（与前端 TS 类型 InstallResult 对齐，序列化为 camelCase）
/// </summary>
public class InstallResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;
    public string? BackupPath { get; set; }

    public static InstallResult Succeeded(string projectId, string projectName, string version, string? backupPath = null) => new()
    {
        Success = true,
        ProjectId = projectId,
        ProjectName = projectName,
        Version = version,
        InstalledAt = DateTimeOffset.UtcNow,
        BackupPath = backupPath
    };

    public static InstallResult Failed(string projectId, string error) => new()
    {
        Success = false,
        ProjectId = projectId,
        Error = error,
        Version = string.Empty
    };
}

/// <summary>
/// 已安装插件记录（与前端 TS 类型 InstalledPlugin 对齐，序列化为 camelCase）
/// </summary>
public class InstalledPlugin
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;
    public string? BackupPath { get; set; }
    public string ServerPath { get; set; } = string.Empty;

    /// <summary>
    /// 内部用：文件名（用于卸载时定位文件，不暴露给前端 TS 类型）
    /// </summary>
    public string FileName { get; set; } = string.Empty;
}
