// -----------------------------------------------------------------------------
// 文件名: ServerConstants.cs
// 命名空间: io.NET.ZTR_OS.Features.JavaInstallation.Constants
// 功能描述: 服务器类型与识别常量定义，提供服务端指纹识别与配置文件模式
// 依赖组件: 无
// 设计模式: 常量容器 + 判别式枚举
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.JavaInstallation.Constants;

/// <summary>
/// Minecraft 服务器类型枚举，作为服务端核心品牌的判别式。
/// 用于标识不同发行版的服务端软件，覆盖主流的 Mod 端与插件端。
/// </summary>
public enum ServerType
{
    /// <summary>
    /// 未知类型，尚未判定或无法识别。
    /// </summary>
    Unknown,

    /// <summary>
    /// 原版（Vanilla）服务端，Mojang 官方发布。
    /// </summary>
    Vanilla,

    /// <summary>
    /// Spigot 服务端，高性能插件服务端。
    /// </summary>
    Spigot,

    /// <summary>
    /// Paper 服务端，基于 Spigot 的优化分支。
    /// </summary>
    Paper,

    /// <summary>
    /// Forge 服务端，Mod 加载器服务端。
    /// </summary>
    Forge,

    /// <summary>
    /// Fabric 服务端，轻量级 Mod 加载器服务端。
    /// </summary>
    Fabric,

    /// <summary>
    /// Bukkit 服务端，经典插件 API 服务端。
    /// </summary>
    Bukkit,

    /// <summary>
    /// Folia 服务端，基于 Paper 的多线程区域化服务端。
    /// </summary>
    Folia,

    /// <summary>
    /// Purpur 服务端，基于 Paper 的高性能优化分支。
    /// </summary>
    Purpur,

    /// <summary>
    /// Pufferfish 服务端，基于 Paper 的异步优化分支。
    /// </summary>
    Pufferfish,

    /// <summary>
    /// NeoForge 服务端，Forge 的现代分支。
    /// </summary>
    NeoForge,

    /// <summary>
    /// BungeeCord 代理端，经典 Minecraft 代理。
    /// </summary>
    BungeeCord,

    /// <summary>
    /// Velocity 代理端，现代高性能代理。
    /// </summary>
    Velocity,

    /// <summary>
    /// Mohist 混合端，Forge + Bukkit 混合。
    /// </summary>
    Mohist,

    /// <summary>
    /// Arclight 混合端，可配置 Forge/NeoForge/Fabric + Bukkit 混合。
    /// </summary>
    Arclight,

    /// <summary>
    /// CatServer 混合端，Forge + Bukkit 混合。
    /// </summary>
    CatServer,

    /// <summary>
    /// Sponge 服务端，独立插件 API（SpongeVanilla）。
    /// </summary>
    Sponge,

    /// <summary>
    /// Waterfall 代理端，BungeeCord 的 PaperMC fork（已归档）。
    /// </summary>
    Waterfall,

    /// <summary>
    /// FlameCord 代理端，BungeeCord 的反机器人分支。
    /// </summary>
    FlameCord,

    /// <summary>
    /// HexaCord 代理端，支持基岩版协议的 BungeeCord fork。
    /// </summary>
    HexaCord,

    /// <summary>
    /// Quilt 模组加载器服务端，Fabric 的现代分支。
    /// </summary>
    Quilt,

    /// <summary>
    /// Airplane 服务端，Paper fork（已停止更新）。
    /// </summary>
    Airplane,

    /// <summary>
    /// Tuinity 服务端，Paper fork（已合并到 Paper）。
    /// </summary>
    Tuinity,

    /// <summary>
    /// Yatopia 服务端，Tuinity fork 极限优化。
    /// </summary>
    Yatopia,

    /// <summary>
    /// Akarin 服务端，Paper fork 多线程优化。
    /// </summary>
    Akarin,

    /// <summary>
    /// Kaiiju 服务端，Folia fork 优化版。
    /// </summary>
    Kaiiju,

    /// <summary>
    /// NachoSpigot 服务端，Paper fork。
    /// </summary>
    NachoSpigot,

    /// <summary>
    /// Magma 混合端，Forge + Bukkit（基于 Thermos）。
    /// </summary>
    Magma,

    /// <summary>
    /// Banner 混合端，Fabric + Bukkit（Mohist 团队新作）。
    /// </summary>
    Banner,

    /// <summary>
    /// SpongeForge 服务端，Sponge on Forge 实现。
    /// </summary>
    SpongeForge,

    /// <summary>
    /// Nukkit 服务端，基岩版 Java 实现。
    /// </summary>
    Nukkit,

    /// <summary>
    /// PowerNukkit 服务端，Nukkit fork。
    /// </summary>
    PowerNukkit,

    /// <summary>
    /// Glowstone 服务端，独立 Bukkit API 实现。
    /// </summary>
    Glowstone,

    /// <summary>
    /// Luminol 服务端，基于 Leaves 的极速优化分支，主打启动性能与异步方块生成。
    /// </summary>
    Luminol,

    /// <summary>
    /// Leaf 服务端，基于 Leaves 的轻量优化分支（LeafMC）。
    /// </summary>
    Leaf,

    /// <summary>
    /// Leaves 服务端，基于 Paper 的下游优化分支，专注于启动速度与性能优化。
    /// </summary>
    Leaves,

    /// <summary>
    /// USpigot 服务端，Spigot 的下游优化分支（国内衍生核心）。
    /// </summary>
    USpigot,
}

/// <summary>
/// 服务器常量容器类，提供服务端识别指纹与配置文件模式。
/// 包含 JAR 文件名模式、进程标识特征、配置文件清单等识别依据。
/// </summary>
public static class ServerConstants
{
    /// <summary>
    /// 各类型服务器核心 JAR 文件的文件名模式。
    /// 用于从进程命令行中识别服务器类型。
    /// 采用通配符匹配规则。
    /// </summary>
    public static readonly string[] VanillaJarPatterns = ["minecraft_server.*.jar", "server.jar"];
    public static readonly string[] SpigotJarPatterns = ["spigot-*.jar", "spigot.jar"];
    public static readonly string[] PaperJarPatterns = ["paper-*.jar", "paper.jar"];
    public static readonly string[] ForgeJarPatterns = ["forge-*.jar", "forge.jar"];
    public static readonly string[] FabricJarPatterns = ["fabric-server-launch.jar", "fabric-server.jar"];
    public static readonly string[] BukkitJarPatterns = ["craftbukkit-*.jar"];
    public static readonly string[] FoliaJarPatterns = ["folia-*.jar", "folia.jar"];
    public static readonly string[] PurpurJarPatterns = ["purpur-*.jar", "purpur.jar"];
    public static readonly string[] PufferfishJarPatterns = ["pufferfish-*.jar", "pufferfish.jar"];
    public static readonly string[] NeoForgeJarPatterns = ["neoforge-*.jar", "neoforge.jar"];
    public static readonly string[] BungeeCordJarPatterns = ["bungeecord-*.jar", "bungeecord.jar"];
    public static readonly string[] VelocityJarPatterns = ["velocity-*.jar", "velocity.jar"];
    public static readonly string[] MohistJarPatterns = ["mohist-*.jar", "mohist.jar"];
    public static readonly string[] ArclightJarPatterns = ["arclight-*.jar", "arclight.jar"];
    public static readonly string[] CatServerJarPatterns = ["catserver-*.jar", "catserver.jar"];
    public static readonly string[] SpongeJarPatterns = ["sponge-*.jar", "spongevanilla-*.jar", "spongeforge-*.jar"];
    public static readonly string[] WaterfallJarPatterns = ["waterfall-*.jar", "waterfall.jar"];
    public static readonly string[] FlameCordJarPatterns = ["flamecord-*.jar", "flamecord.jar"];
    public static readonly string[] HexaCordJarPatterns = ["hexacord-*.jar", "hexacord.jar"];
    public static readonly string[] QuiltJarPatterns = ["quilt-server-launch.jar", "quilt-server.jar"];
    public static readonly string[] AirplaneJarPatterns = ["airplane-*.jar", "airplane.jar"];
    public static readonly string[] TuinityJarPatterns = ["tuinity-*.jar", "tuinity.jar"];
    public static readonly string[] YatopiaJarPatterns = ["yatopia-*.jar", "yatopia.jar"];
    public static readonly string[] AkarinJarPatterns = ["akarin-*.jar", "akarin.jar"];
    public static readonly string[] KaiijuJarPatterns = ["kaiiju-*.jar", "kaiiju.jar"];
    public static readonly string[] NachoSpigotJarPatterns = ["nacho-*.jar", "nachospigot-*.jar"];
    public static readonly string[] MagmaJarPatterns = ["magma-*.jar", "magma.jar"];
    public static readonly string[] BannerJarPatterns = ["banner-*.jar", "banner.jar"];
    public static readonly string[] SpongeForgeJarPatterns = ["spongeforge-*.jar"];
    public static readonly string[] NukkitJarPatterns = ["nukkit-*.jar", "nukkit.jar"];
    public static readonly string[] PowerNukkitJarPatterns = ["powernukkit-*.jar", "powernukkit.jar"];
    public static readonly string[] GlowstoneJarPatterns = ["glowstone-*.jar", "glowstone.jar"];
    public static readonly string[] LuminolJarPatterns = ["luminol-*.jar", "luminol.jar"];
    public static readonly string[] LeafJarPatterns = ["leaf-*.jar", "leafmc-*.jar", "leaf.jar", "leafmc.jar"];
    public static readonly string[] LeavesJarPatterns = ["leaves-*.jar", "leaves.jar"];
    public static readonly string[] USpigotJarPatterns = ["uspigot-*.jar", "uspigot.jar", "u-spigot-*.jar"];

    /// <summary>
    /// 服务器 JAR 文件关键词列表。
    /// 用于从进程命令行中快速判定 JAR 是否为服务端而非客户端。
    /// 只要 JAR 名称包含其中任一关键词即判定为服务器。
    /// </summary>
    public static readonly string[] ServerJarKeywords = [
        "minecraft_server", "server", "spigot", "paper", "forge", "fabric-server-launch",
        "craftbukkit", "folia", "purpur", "pufferfish", "neoforge", "bungeecord", "velocity",
        "mohist", "arclight", "catserver", "sponge", "spongevanilla",
        "waterfall", "flamecord", "hexacord", "quilt", "airplane", "tuinity", "yatopia",
        "akarin", "kaiiju", "nacho", "nachospigot", "magma", "banner", "spongeforge",
        "nukkit", "powernukkit", "glowstone",
        "luminol", "leafmc", "leaves", "uspigot"
    ];

    /// <summary>
    /// 服务端进程标识特征。
    /// 命令行中包含这些参数时，强化服务端判定。
    /// </summary>
    public static readonly string[] ServerProcessMarkers = ["nogui", "--nogui"];

    /// <summary>
    /// 客户端进程标识特征。
    /// 命令行中包含这些参数时，排除服务端判定。
    /// </summary>
    public static readonly string[] ClientProcessMarkers = ["--version", "--accessToken", "--userType", "--assetsDir"];

    /// <summary>
    /// 各类型服务器的特征配置文件映射。
    /// 通过检测目录下是否存在特定文件组合，辅助判定服务器类型。
    /// 键为服务器类型，值为该类型特有的文件或目录路径数组。
    /// </summary>
    public static readonly Dictionary<ServerType, string[]> TypeIndicatorFiles = new()
    {
        [ServerType.Vanilla] = [],
        [ServerType.Spigot] = ["spigot.yml", "bukkit.yml"],
        [ServerType.Paper] = ["config/paper-global.yml"],
        [ServerType.Forge] = ["mods/", "forge-server.toml"],
        [ServerType.Fabric] = ["fabric-server-launch.properties", ".fabric/"],
        [ServerType.Bukkit] = ["bukkit.yml"],
        [ServerType.Folia] = ["config/paper-global.yml", "config/folia-global.yml"],
        [ServerType.Purpur] = ["purpur.yml", "config/purpur.yml"],
        [ServerType.Pufferfish] = ["pufferfish.yml"],
        [ServerType.NeoForge] = ["neoforge.yml", "config/neoforge/"],
        [ServerType.BungeeCord] = ["config.yml"],
        [ServerType.Velocity] = ["velocity.toml"],
        [ServerType.Mohist] = ["mohist-config.yml"],
        [ServerType.Arclight] = ["arclight.yml"],
        [ServerType.CatServer] = ["catserver.yml"],
        [ServerType.Sponge] = ["config/sponge/", "global.conf"],
        [ServerType.Waterfall] = ["waterfall.yml"],
        [ServerType.FlameCord] = ["flamecord.yml"],
        [ServerType.HexaCord] = ["hexacord.yml"],
        [ServerType.Quilt] = ["quilt-server-launch.properties"],
        [ServerType.Airplane] = ["airplane.yml"],
        [ServerType.Tuinity] = ["tuinity.yml"],
        [ServerType.Yatopia] = ["yatopia.yml"],
        [ServerType.Akarin] = ["akarin.yml"],
        [ServerType.Kaiiju] = ["kaiiju.yml"],
        [ServerType.NachoSpigot] = ["nacho.yml"],
        [ServerType.Magma] = ["magma.conf", "plugins/Magma/"],
        [ServerType.Banner] = ["banner.yml"],
        [ServerType.SpongeForge] = ["config/sponge/"],
        [ServerType.Nukkit] = ["nukkit.yml"],
        [ServerType.PowerNukkit] = ["powernukkit.yml"],
        [ServerType.Glowstone] = ["config/glowstone/"],
        [ServerType.Luminol] = ["luminol.yml", "config/luminol/"],
        [ServerType.Leaf] = ["leaf.yml", "leafmc.yml", "config/leaf/"],
        [ServerType.Leaves] = ["leaves.yml", "config/leaves-global.yml"],
        [ServerType.USpigot] = ["uspigot.yml", "u-spigot.yml"],
    };

    /// <summary>
    /// 所有服务器共有的标准配置文件列表。
    /// 包含 server.properties 及权限、封禁等标准配置文件。
    /// </summary>
    public static readonly string[] CommonConfigFiles = ["server.properties", "eula.txt", "ops.json", "whitelist.json", "banned-players.json", "banned-ips.json", "permissions.yml", "commands.yml"];

    /// <summary>
    /// Spigot 特有的配置文件列表。
    /// </summary>
    public static readonly string[] SpigotConfigFiles = ["spigot.yml", "bukkit.yml"];

    /// <summary>
    /// Paper 特有的配置文件列表。
    /// </summary>
    public static readonly string[] PaperConfigFiles = ["config/paper-global.yml", "config/paper-world-defaults.yml"];

    /// <summary>
    /// Forge 特有的配置文件与目录列表。
    /// </summary>
    public static readonly string[] ForgeConfigFiles = ["server.toml", "forge-server.toml", "mods/"];

    /// <summary>
    /// Fabric 特有的配置文件与目录列表。
    /// </summary>
    public static readonly string[] FabricConfigFiles = ["fabric-server-launch.properties", "mods/", ".fabric/"];

    /// <summary>
    /// 服务器目录验证文件名。
    /// 存在该文件的目录被判定为有效服务器目录。
    /// </summary>
    public const string ServerValidationFile = "server.properties";

    /// <summary>
    /// Minecraft 服务器默认监听端口号。
    /// 标准值为 25565。
    /// </summary>
    public const int DefaultServerPort = 25565;

    /// <summary>
    /// 端口扫描起始端口（含）。覆盖标准 MC 端口及常见多实例偏移。
    /// </summary>
    public const int PortScanStart = 25565;

    /// <summary>
    /// 端口扫描结束端口（含）。主区间扫描 25565-25590 共 26 个端口，
    /// 覆盖原版/Spigot/Paper 默认端口及 BungeeCord/Velocity 代理原生端口段。
    /// </summary>
    public const int PortScanEnd = 25590;

    /// <summary>
    /// 主区间外的补充扫描端口（Geyser/Bedrock TCP 桥接、自定义高位常用端口）。
    /// 由 ServerDetector 合并到主区间后统一扫描，避免改 PortScanner.ScanRangeAsync 签名。
    /// </summary>
    public static readonly int[] AdditionalScanPorts = { 19132, 26000, 30000, 40000 };

    /// <summary>
    /// 单端口 TCP connect 超时（毫秒）。必须远小于 3 秒轮询周期。
    /// </summary>
    public const int PortScanTimeoutMs = 800;

    /// <summary>
    /// 端口扫描最大并发数。11 个端口用 50 并发绰绰有余。
    /// </summary>
    public const int PortScanMaxConcurrency = 50;

    /// <summary>
    /// 端口扫描结果缓存 TTL（秒）。比 3 秒轮询周期长，避免每轮都 TCP connect。
    /// </summary>
    public const int PortScanCacheTtlSeconds = 10;
}
