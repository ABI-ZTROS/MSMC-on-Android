// -----------------------------------------------------------------------------
// 文件名: ConfigDescriptorRegistry.cs
// 命名空间: io.NET.ZTR_OS.Features.ConfigEditor.Services
// 功能描述: 配置描述符注册表，集中管理 Minecraft 服务器各配置文件的配置项元数据
// 依赖组件: System.IO, System.Text.RegularExpressions, Serilog
// 设计模式: 注册表模式、键控查找、多维哈希映射
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ConfigEditor.Services;

using System.IO;
using System.Text.RegularExpressions;
using Serilog;

/// <summary>
/// 服务器配置描述符
/// </summary>
/// <remarks>
/// 封装单个配置项的完整元数据，包括键名、显示名、描述、分类、
/// 默认值、取值范围约束、正则验证模式及重启要求等属性。
/// 作为配置描述符注册表中的基本数据单元。
/// </remarks>
public sealed class ServerConfigDescriptor
{
 /// <summary>配置项的键名标识</summary>
 public required string Key { get; init; }

 /// <summary>所属配置文件的名称</summary>
 public required string ConfigFileName { get; init; }

 /// <summary>配置项的中文显示名称</summary>
 public required string DisplayName { get; init; }

 /// <summary>配置项的中文详细描述</summary>
 public required string Description { get; init; }

 /// <summary>配置项所属的功能分类</summary>
 public required string Category { get; init; }

 /// <summary>配置项的默认值（字符串表示形式）</summary>
 public string? DefaultValue { get; init; }

 /// <summary>数值类型配置项的最小值约束</summary>
 public int? MinValue { get; init; }

 /// <summary>数值类型配置项的最大值约束</summary>
 public int? MaxValue { get; init; }

 /// <summary>枚举类型配置项的允许值集合</summary>
 public string[]? AllowedValues { get; init; }

 /// <summary>字符串类型配置项的正则验证模式</summary>
 public string? RegexPattern { get; init; }

 /// <summary>配置项的值类型标识</summary>
 public string ValueType { get; init; } = "string";

 /// <summary>指示配置项修改后是否需要重启服务器方能生效</summary>
 public bool RequiresRestart { get; init; }

 /// <summary>预编译正则表达式实例，采用延迟初始化策略</summary>
 private Regex? _compiledRegex;

 /// <summary>
 /// 获取预编译的正则表达式实例
 /// </summary>
 /// <returns>预编译的 Regex 实例；若无正则约束则返回 null</returns>
 /// <remarks>采用懒加载模式，首次调用时进行编译并缓存</remarks>
 public Regex? GetCompiledRegex()
 {
 if (RegexPattern is null)
 return null;

 return _compiledRegex ??= new Regex(RegexPattern, RegexOptions.Compiled);
 }
}

/// <summary>
/// 配置描述符注册表
/// </summary>
/// <remarks>
/// 集中管理 Minecraft 服务器各配置文件的配置项元数据，
/// 包括 server.properties、bukkit.yml、spigot.yml、paper-global.yml 等
/// 配置文件的关键配置项描述信息。支持多级键控查找策略：
/// 精确匹配、纯文件名匹配、后缀模糊匹配。
/// </remarks>
public sealed class ConfigDescriptorRegistry
{
 /// <summary>
 /// 内部存储结构：以 (配置文件名, 配置键名) 复合键为索引的多维哈希映射
 /// </summary>
 private readonly Dictionary<(string ConfigFileName, string Key), ServerConfigDescriptor> _descriptors = new();

 /// <summary>
 /// 初始化配置描述符注册表
 /// </summary>
 /// <remarks>构造函数中完成所有预置配置项的注册，构建完成后仅提供只读查询</remarks>
 public ConfigDescriptorRegistry()
 {
 Log.Information("ConfigDescriptorRegistry 初始化，注册配置描述符...");
 // Vanilla / Bukkit 系基础配置
 RegisterServerProperties();
 RegisterServerPropertiesExtras();
 RegisterBukkitYml();
 RegisterSpigotYml();
 RegisterPaperGlobalYml();
 RegisterPaperWorldDefaultsYml();

 // 第一批：Paper 系派生核心专属配置
 RegisterPurpurYml();
 RegisterPufferfishYml();
 RegisterLeavesYml();
 RegisterLeafYml();

 // 第一批：代理端核心专属配置
 RegisterVelocityToml();
 RegisterBungeeCordConfigYml();

 // 第一批：Folia 系派生核心专属配置
 RegisterLuminolToml();

 // 第二批：Paper 系派生核心专属配置
 RegisterFoliaGlobalYml();
 RegisterKaiijuYml();
 RegisterNachoYml();
 RegisterUSpigotYml();

 // 第二批：代理端核心专属配置
 RegisterWaterfallYml();
 RegisterFlameCordYml();
 RegisterHexaCordYml();

 // 第二批：混合端核心专属配置
 RegisterMohistConfigYml();
 RegisterArclightYml();
 RegisterCatServerYml();
 RegisterMagmaConf();
 RegisterBannerYml();

 // 第二批：模组端核心专属配置
 RegisterForgeServerToml();
 RegisterNeoForgeYml();
 RegisterFabricServerProperties();
 RegisterQuiltServerProperties();

 // 第二批：基岩版 / 独立实现 / Sponge
 RegisterNukkitYml();
 RegisterPowerNukkitYml();
 RegisterGlowstoneConfig();
 RegisterSpongeGlobalConf();
 RegisterSpongeForgeConf();

 // 第三批：已停更核心（仍完整提供翻译）
 RegisterYatopiaYml();
 RegisterAirplaneYml();
 RegisterTuinityYml();
 RegisterAkarinYml();

 // 第四批：Bukkit 系通用配置
 RegisterPermissionsYml();
 RegisterCommandsYml();
 RegisterHelpYml();

 Log.Information("注册表构建完成，共 {Count} 个描述符", _descriptors.Count);
 }

 /// <summary>
 /// 向注册表中注册单个配置描述符
 /// </summary>
 /// <param name="descriptor">待注册的配置描述符实例</param>
 /// <exception cref="ArgumentNullException">当 descriptor 为 null 时抛出</exception>
 private void Register(ServerConfigDescriptor descriptor)
 {
 ArgumentNullException.ThrowIfNull(descriptor);

 var key = (descriptor.ConfigFileName, descriptor.Key);
 _descriptors[key] = descriptor;

 Log.Debug("注册配置描述符: {Key} → {Name}", descriptor.Key, descriptor.DisplayName);
 }

 /// <summary>
 /// 根据配置键名和配置文件名获取对应的配置描述符
 /// </summary>
 /// <param name="key">配置项键名</param>
 /// <param name="configFileName">配置文件名称（可包含路径）</param>
 /// <returns>匹配的配置描述符；未找到则返回 null</returns>
 /// <remarks>
 /// 采用四级匹配策略：
 /// 1. 精确匹配：使用 (configFileName, key) 复合键进行精确查找
 /// 2. 纯文件名匹配：去除目录前缀后进行匹配（如 config/paper-global.yml → paper-global.yml）
 /// 3. 后缀匹配：针对 YAML 压平后的层级键，用注册键作为后缀进行模糊匹配
 /// （例：解析键 "world-settings.default.mob-spawn-range" 匹配注册键 "mob-spawn-range"）
 /// 4. 叶键匹配：提取注册键的最后一段与解析键比较，处理 YAML 扁平化丢失父级前缀的场景
 /// （例：解析键 "allow-end" 匹配注册键 "settings.allow-end"）
 /// </remarks>
 public ServerConfigDescriptor? GetDescriptor(string key, string configFileName)
 {
 if (key is null || configFileName is null)
 return null;

 // 第一级：精确匹配
 if (_descriptors.TryGetValue((configFileName, key), out var desc))
 return desc;

 // 第二级：纯文件名匹配（去除目录前缀）
 var pureFileName = Path.GetFileName(configFileName);
 if (!string.IsNullOrEmpty(pureFileName) && pureFileName != configFileName)
 {
 if (_descriptors.TryGetValue((pureFileName, key), out desc))
 return desc;
 }

 // 第三级：后缀匹配 —— 处理 YAML 层级压平场景
 // 例如键 "world-settings.default.mob-spawn-range" 可匹配注册键 "mob-spawn-range"
 foreach (var kvp in _descriptors)
 {
 if (!kvp.Key.ConfigFileName.Equals(configFileName, StringComparison.OrdinalIgnoreCase) &&
 !kvp.Key.ConfigFileName.Equals(pureFileName, StringComparison.OrdinalIgnoreCase))
 continue;

 var registeredKey = kvp.Key.Key;
 if (key.Length > registeredKey.Length &&
 key.EndsWith(registeredKey, StringComparison.OrdinalIgnoreCase) &&
 key[key.Length - registeredKey.Length - 1] == '.')
 {
 return kvp.Value;
 }
 }

 // 第四级：叶键匹配 —— 处理 YAML 扁平化父级前缀差异的场景
 // 解析键 "settings.debug" 可匹配注册键 "debug"（叶键相同即可）
 // 解析键 "debug" 可匹配注册键 "settings.debug"（叶键相同即可）
 foreach (var kvp in _descriptors)
 {
 if (!kvp.Key.ConfigFileName.Equals(configFileName, StringComparison.OrdinalIgnoreCase) &&
 !kvp.Key.ConfigFileName.Equals(pureFileName, StringComparison.OrdinalIgnoreCase))
 continue;

 var registeredKey = kvp.Key.Key;
 var registeredLeaf = registeredKey.Contains('.')
 ? registeredKey[(registeredKey.LastIndexOf('.') + 1)..]
 : registeredKey;

 var keyLeaf = key.Contains('.')
 ? key[(key.LastIndexOf('.') + 1)..]
 : key;

 if (keyLeaf.Equals(registeredLeaf, StringComparison.OrdinalIgnoreCase))
 return kvp.Value;
 }

 return null;
 }

 /// <summary>
 /// 获取指定配置文件的所有已注册配置描述符
 /// </summary>
 /// <param name="configFileName">配置文件名称（可包含路径）</param>
 /// <returns>匹配的配置描述符列表</returns>
 /// <remarks>同时支持完整路径匹配和纯文件名匹配</remarks>
 public List<ServerConfigDescriptor> GetDescriptorsForFile(string configFileName)
 {
 if (configFileName is null)
 return [];

 var pureFileName = Path.GetFileName(configFileName);

 return _descriptors
 .Where(kv =>
 kv.Key.ConfigFileName.Equals(configFileName, StringComparison.OrdinalIgnoreCase) ||
 (!string.IsNullOrEmpty(pureFileName) &&
 kv.Key.ConfigFileName.Equals(pureFileName, StringComparison.OrdinalIgnoreCase)))
 .Select(kv => kv.Value)
 .ToList();
 }

 /// <summary>
 /// 生成配置描述符覆盖率报告
 /// </summary>
 /// <returns>包含总描述符数量和各文件统计的覆盖率报告</returns>
 public CoverageReport GetCoverageReport()
 {
 var fileStats = _descriptors
 .GroupBy(d => d.Key.ConfigFileName)
 .Select(g => new FileCoverageStat(g.Key, g.Count()))
 .OrderBy(f => f.ConfigFileName)
 .ToList();

 return new CoverageReport(
 TotalDescriptors: _descriptors.Count,
 FileStats: fileStats
 );
 }

 /// <summary>
 /// 查找指定键名列表中未匹配描述符的键
 /// </summary>
 /// <param name="keys">待检查的配置键名列表</param>
 /// <param name="configFileName">配置文件名称</param>
 /// <returns>未找到对应描述符的键名列表</returns>
 /// <remarks>用于诊断配置描述符的覆盖范围</remarks>
 public List<string> FindUnmatchedKeys(List<string> keys, string configFileName)
 {
 var pureName = Path.GetFileName(configFileName);
 return keys
 .Where(k => GetDescriptor(k, pureName) is null)
 .ToList();
 }

 /// <summary>
 /// 配置描述符覆盖率报告
 /// </summary>
 /// <param name="TotalDescriptors">已注册描述符总数</param>
 /// <param name="FileStats">各配置文件的覆盖率统计列表</param>
 public sealed record CoverageReport(int TotalDescriptors, List<FileCoverageStat> FileStats);

 /// <summary>
 /// 单文件覆盖率统计信息
 /// </summary>
 /// <param name="ConfigFileName">配置文件名称</param>
 /// <param name="DescriptorCount">该文件已注册的描述符数量</param>
 public sealed record FileCoverageStat(string ConfigFileName, int DescriptorCount);

 // ==================== 核心索引表 ====================

 /// <summary>
 /// 核心索引条目：描述一种服务器核心及其配置文件清单
 /// </summary>
 public sealed record CoreIndexEntry(
 string CoreType, // 核心代号（与 ServerType 枚举名一致）
 string DisplayName, // 中文显示名
 string Category, // 分类（原版/Paper系/代理端/混合端/模组端/基岩版/独立实现）
 string Inheritance, // 继承关系链
 bool IsDeprecated, // 是否已停更/归档
 List<CoreConfigFileInfo> ConfigFiles // 该核心的配置文件列表
 );

 /// <summary>
 /// 核心配置文件信息
 /// </summary>
 public sealed record CoreConfigFileInfo(
 string FileName, // 配置文件名（可含路径）
 string Format, // 格式：YAML/TOML/Properties/HOCON/CONF
 string Source, // 来源：原版继承/Bukkit继承/核心专属
 int DescriptorCount // 已注册的描述符数量
 );

 /// <summary>
 /// 核心索引表：手动维护的核心元数据，用于软件查询和展示
 /// </summary>
 private static readonly List<CoreIndexEntry> _coreIndex =
 [
 // ── 原版与基础插件端 ──
 new("Vanilla", "原版", "原版", "Mojang 官方", false,
 [
 new("server.properties", "Properties", "原版", 0),
 ]),
 new("Bukkit", "Bukkit", "基础插件端", "Vanilla → Bukkit", false,
 [
 new("bukkit.yml", "YAML", "Bukkit 专属", 0),
 new("permissions.yml", "YAML", "Bukkit 专属", 0),
 new("commands.yml", "YAML", "Bukkit 专属", 0),
 new("help.yml", "YAML", "Bukkit 专属", 0),
 new("server.properties", "Properties", "原版继承", 0),
 ]),
 new("Spigot", "Spigot", "基础插件端", "Vanilla → Bukkit → Spigot", false,
 [
 new("spigot.yml", "YAML", "Spigot 专属", 0),
 new("bukkit.yml", "YAML", "Bukkit 继承", 0),
 new("server.properties", "Properties", "原版继承", 0),
 ]),
 new("Paper", "Paper", "基础插件端", "Vanilla → Bukkit → Spigot → Paper", false,
 [
 new("config/paper-global.yml", "YAML", "Paper 专属", 0),
 new("config/paper-world-defaults.yml", "YAML", "Paper 专属", 0),
 new("spigot.yml", "YAML", "Spigot 继承", 0),
 new("bukkit.yml", "YAML", "Bukkit 继承", 0),
 new("server.properties", "Properties", "原版继承", 0),
 ]),

 // ── Paper 系派生核心（活跃） ──
 new("Folia", "Folia", "Paper系", "Paper → Folia", false,
 [
 new("config/paper-global.yml", "YAML", "Folia 追加 ThreadedRegions 节", 0),
 ]),
 new("Purpur", "Purpur", "Paper系", "Paper → Pufferfish → Purpur", false,
 [
 new("purpur.yml", "YAML", "Purpur 专属", 0),
 ]),
 new("Pufferfish", "Pufferfish", "Paper系", "Paper → Pufferfish", false,
 [
 new("pufferfish.yml", "YAML", "Pufferfish 专属", 0),
 ]),
 new("Leaves", "Leaves", "Paper系", "Paper → Leaves", false,
 [
 new("leaves.yml", "YAML", "Leaves 专属", 0),
 ]),
 new("Leaf", "Leaf", "Paper系", "Leaves → Leaf", false,
 [
 new("leaf.yml", "YAML", "Leaf 专属", 0),
 new("config/leaf-global.yml", "YAML", "Leaf 专属", 0),
 ]),
 new("Luminol", "Luminol", "Paper系", "Leaves → Luminol", false,
 [
 new("luminol_global_config.toml", "TOML", "Luminol 专属", 0),
 ]),
 new("Kaiiju", "Kaiiju", "Paper系", "Folia → Kaiiju", false,
 [
 new("kaiiju.yml", "YAML", "Kaiiju 专属", 0),
 ]),
 new("NachoSpigot", "NachoSpigot", "Paper系", "Paper → NachoSpigot", false,
 [
 new("nacho.yml", "YAML", "NachoSpigot 专属", 0),
 ]),
 new("USpigot", "USpigot", "Paper系", "Spigot → USpigot", false,
 [
 new("uspigot.yml", "YAML", "USpigot 专属", 0),
 ]),

 // ── Paper 系派生核心（已停更） ──
 new("Yatopia", "Yatopia", "Paper系", "Tuinity → Yatopia", true,
 [
 new("yatopia.yml", "YAML", "Yatopia 专属", 0),
 ]),
 new("Airplane", "Airplane", "Paper系", "Paper → Airplane", true,
 [
 new("airplane.yml", "YAML", "Airplane 专属", 0),
 ]),
 new("Tuinity", "Tuinity", "Paper系", "Paper → Tuinity", true,
 [
 new("tuinity.yml", "YAML", "Tuinity 专属", 0),
 ]),
 new("Akarin", "Akarin", "Paper系", "Paper → Akarin", true,
 [
 new("akarin.yml", "YAML", "Akarin 专属", 0),
 ]),

 // ── 模组端 ──
 new("Forge", "Forge", "模组端", "Mojang → Forge", false,
 [
 new("forge-server.toml", "TOML", "Forge 专属", 0),
 ]),
 new("NeoForge", "NeoForge", "模组端", "Forge → NeoForge", false,
 [
 new("neoforge-server.toml", "TOML", "NeoForge 专属", 0),
 new("neoforge-common.toml", "TOML", "NeoForge 专属", 0),
 ]),
 new("Fabric", "Fabric", "模组端", "Mojang → Fabric", false,
 [
 new("fabric-server-launcher.properties", "Properties", "Fabric 专属", 0),
 ]),
 new("Quilt", "Quilt", "模组端", "Fabric → Quilt", false,
 [
 new("quilt-server-launcher.properties", "Properties", "Quilt 专属", 0),
 ]),

 // ── 代理端 ──
 new("BungeeCord", "BungeeCord", "代理端", "Spigot 团队", false,
 [
 new("config.yml", "YAML", "BungeeCord 专属", 0),
 ]),
 new("Velocity", "Velocity", "代理端", "PaperMC 独立实现", false,
 [
 new("velocity.toml", "TOML", "Velocity 专属", 0),
 ]),
 new("Waterfall", "Waterfall", "代理端", "BungeeCord → Waterfall", true,
 [
 new("waterfall.yml", "YAML", "Waterfall 专属", 0),
 new("config.yml", "YAML", "BungeeCord 继承", 0),
 ]),
 new("FlameCord", "FlameCord", "代理端", "BungeeCord → FlameCord", false,
 [
 new("flamecord.yml", "YAML", "FlameCord 专属", 0),
 new("config.yml", "YAML", "BungeeCord 继承", 0),
 ]),
 new("HexaCord", "HexaCord", "代理端", "BungeeCord → HexaCord", false,
 [
 new("hexacord.yml", "YAML", "HexaCord 专属", 0),
 new("config.yml", "YAML", "BungeeCord 继承", 0),
 ]),

 // ── 混合端 ──
 new("Mohist", "Mohist", "混合端", "Forge + Bukkit", false,
 [
 new("mohist-config.yml", "YAML", "Mohist 专属", 0),
 ]),
 new("Arclight", "Arclight", "混合端", "Forge/NeoForge/Fabric + Bukkit", false,
 [
 new("arclight.conf", "HOCON", "Arclight 专属", 0),
 ]),
 new("CatServer", "CatServer", "混合端", "Forge + Bukkit", false,
 [
 new("catserver.yml", "YAML", "CatServer 专属", 0),
 ]),
 new("Magma", "Magma", "混合端", "Thermos → Magma (Forge + Bukkit)", false,
 [
 new("magma.yml", "Properties", "Magma 专属", 0),
 ]),
 new("Banner", "Banner", "混合端", "Fabric + Bukkit", false,
 [
 new("banner.yml", "YAML", "Banner 专属", 0),
 ]),

 // ── 基岩版 / 独立实现 / Sponge ──
 new("Sponge", "Sponge", "独立实现", "SpongePowered 独立", false,
 [
 new("config/sponge/global.conf", "HOCON", "Sponge 专属", 0),
 ]),
 new("SpongeForge", "SpongeForge", "独立实现", "Sponge on Forge", false,
 [
 new("config/sponge/spongeforge-global.conf", "HOCON", "SpongeForge 差异", 0),
 ]),
 new("Nukkit", "Nukkit", "基岩版", "CloudburstMC 基岩版 Java 实现", false,
 [
 new("nukkit.yml", "YAML", "Nukkit 专属", 0),
 new("nukkit-server.properties", "Properties", "Nukkit 基岩版专属", 0),
 ]),
 new("PowerNukkit", "PowerNukkit", "基岩版", "Nukkit → PowerNukkit", false,
 [
 new("powernukkit.yml", "YAML", "PowerNukkit 专属", 0),
 new("powernukkit-server.properties", "Properties", "PowerNukkit 基岩版专属", 0),
 ]),
 new("Glowstone", "Glowstone", "独立实现", "独立 Bukkit API 实现", false,
 [
 new("config/glowstone/glowstone.yml", "YAML", "Glowstone 专属", 0),
 ]),
 ];

 /// <summary>
 /// 获取核心索引表，供软件查询和展示所有服务器核心的配置文件翻译索引
 /// </summary>
 /// <returns>核心索引条目列表，每个条目包含核心代号、显示名、分类、继承关系和配置文件清单（含描述符数量）</returns>
 public List<CoreIndexEntry> GetCoreIndex()
 {
 // 为每个配置文件填充实际的描述符数量
 return _coreIndex.Select(entry => entry with
 {
 ConfigFiles = entry.ConfigFiles
 .Select(f => f with { DescriptorCount = _descriptors.Count(d => d.Key.ConfigFileName == f.FileName) })
 .ToList()
 }).ToList();
 }

 /// <summary>
 /// 注册 server.properties 配置文件的所有关键配置项
 /// </summary>
 /// <remarks>
 /// server.properties 是 Minecraft 服务器的核心配置文件，
 /// 包含网络、玩家、世界、游戏机制、性能优化等核心配置项。
 /// 数据来源：Minecraft Wiki + Folia 26.1.2 默认配置
 /// </remarks>
 private void RegisterServerProperties()
 {
 const string file = "server.properties";

 // ==================== 网络设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "server-port",
 ConfigFileName = file,
 DisplayName = "服务器端口",
 Description = "服务器监听的端口号。玩家连接时需要指定这个端口。\n范围 1-65533，默认 25565 ",
 Category = "网络",
 DefaultValue = "25565",
 MinValue = 1,
 MaxValue = 65533,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server-ip",
 ConfigFileName = file,
 DisplayName = "服务器 IP",
 Description = "服务器绑定的 IP 地址。留空则绑定所有可用地址（0.0.0.0）。\n多网卡或有公网/内网区分时才需要设置 ",
 Category = "网络",
 RegexPattern = @"^(\d{1,3}\.){3}\d{1,3}$|^$",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-compression-threshold",
 ConfigFileName = file,
 DisplayName = "网络压缩阈值",
 Description = "数据包压缩的大小阈值（字节）。\n-1=禁用压缩，0=压缩所有数据包，默认 256 ",
 Category = "网络",
 DefaultValue = "256",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "rate-limit",
 ConfigFileName = file,
 DisplayName = "每玩家数据包速率限制",
 Description = "限制单个玩家每秒的数据包速率。0=禁用限制。\n用于防止玩家通过大量数据包攻击服务器 ️",
 Category = "网络",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "use-native-transport",
 ConfigFileName = file,
 DisplayName = "使用原生传输",
 Description = "是否使用 Linux epoll 等原生网络优化。\nLinux 服务器建议开启，可显著提升网络性能 ",
 Category = "网络",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 玩家设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "max-players",
 ConfigFileName = file,
 DisplayName = "最大玩家数",
 Description = "服务器同时允许连接的最大玩家数量。\n设太大也没用，还得看你的服务器性能撑不撑得住 ",
 Category = "玩家",
 DefaultValue = "20",
 MinValue = 0,
 MaxValue = 2147483647,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "online-mode",
 ConfigFileName = file,
 DisplayName = "正版验证",
 Description = "是否启用 Minecraft 正版验证。\ntrue=只允许正版玩家，false=允许离线/盗版玩家。\n️ 关闭正版验证意味着任何人都可以冒充别人登录，注意安全！",
 Category = "玩家",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "white-list",
 ConfigFileName = file,
 DisplayName = "白名单",
 Description = "是否启用白名单。启用后只有白名单里的玩家才能进入服务器。\n私密服必备功能 ",
 Category = "玩家",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enforce-whitelist",
 ConfigFileName = file,
 DisplayName = "强制白名单",
 Description = "启用后，如果白名单被重新加载，不在白名单里的在线玩家会被踢出。\n确保白名单即时生效 ",
 Category = "玩家",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enforce-secure-profile",
 ConfigFileName = file,
 DisplayName = "强制安全配置",
 Description = "是否强制 Mojang 签名验证。\n启用后，使用未签名聊天消息的玩家无法连接 ",
 Category = "玩家",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-flight",
 ConfigFileName = file,
 DisplayName = "允许飞行",
 Description = "是否允许玩家在生存模式下飞行。\nfalse=检测到飞行的玩家会被踢出。\n如果装了飞行模组或处于创造模式，需要设为 true ️",
 Category = "玩家",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-idle-timeout",
 ConfigFileName = file,
 DisplayName = "玩家空闲踢出",
 Description = "玩家空闲多久后会被踢出服务器（分钟）。0=永不踢出。\n防止挂机玩家占用服务器资源 ",
 Category = "玩家",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "hide-online-players",
 ConfigFileName = file,
 DisplayName = "隐藏在线玩家",
 Description = "是否在服务器列表中隐藏在线玩家数量和列表。\n隐私保护选项 ",
 Category = "玩家",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "log-ips",
 ConfigFileName = file,
 DisplayName = "记录 IP 地址",
 Description = "是否在日志中记录玩家的 IP 地址。\n隐私敏感选项，不需要排查问题时可以关掉 ",
 Category = "玩家",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "accepts-transfers",
 ConfigFileName = file,
 DisplayName = "接受玩家转移",
 Description = "是否接收从其他服务器转入的玩家（transfer 数据包）。\n用于跨服传送场景 ",
 Category = "玩家",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "op-permission-level",
 ConfigFileName = file,
 DisplayName = "OP 权限等级",
 Description = "OP 玩家的默认权限等级。\n1=绕过出生保护 2=可以使用所有单玩家命令 3=可以使用所有多人命令 4=可以使用所有命令\n4=最高权限 [STAR]",
 Category = "玩家",
 DefaultValue = "4",
 MinValue = 0,
 MaxValue = 4,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "function-permission-level",
 ConfigFileName = file,
 DisplayName = "函数权限等级",
 Description = "函数（function）和命令方块使用的默认权限等级。\n范围 1-4，默认 2 ",
 Category = "玩家",
 DefaultValue = "2",
 MinValue = 1,
 MaxValue = 4,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 世界设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "level-name",
 ConfigFileName = file,
 DisplayName = "世界名称",
 Description = "主世界的文件夹名称。对应服务器目录下的 level-name 文件夹。\n改名等于换世界，慎操作！",
 Category = "世界",
 DefaultValue = "world",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-seed",
 ConfigFileName = file,
 DisplayName = "世界种子",
 Description = "世界生成的种子。留空则随机生成。\n相同的种子会生成相同的世界 ",
 Category = "世界",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-type",
 ConfigFileName = file,
 DisplayName = "世界类型",
 Description = "世界生成的类型。\nminecraft:normal=默认 minecraft:flat=超平坦 minecraft:large_biomes=大型生物群系 minecraft:amplified=放大化 ️",
 Category = "世界",
 DefaultValue = "minecraft:normal",
 AllowedValues = ["minecraft:normal", "minecraft:flat", "minecraft:large_biomes", "minecraft:amplified", "minecraft:single_biome_surface"],
 ValueType = "enum",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "generate-structures",
 ConfigFileName = file,
 DisplayName = "生成结构",
 Description = "是否生成村庄、地牢、要塞等结构。\n关掉的话世界会很空旷，但生成速度更快 ️",
 Category = "世界",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "max-world-size",
 ConfigFileName = file,
 DisplayName = "世界大小限制",
 Description = "世界的最大半径（方块）。\n玩家不能越过这个边界，范围 1-29999984 ",
 Category = "世界",
 DefaultValue = "29999984",
 MinValue = 1,
 MaxValue = 29999984,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "view-distance",
 ConfigFileName = file,
 DisplayName = "视距",
 Description = "服务器向玩家发送的区块渲染范围（单位：区块）。\n值越大看到越远，但服务器和客户端的负担也越重。建议 8-12 ",
 Category = "世界",
 DefaultValue = "10",
 MinValue = 3,
 MaxValue = 32,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "simulation-distance",
 ConfigFileName = file,
 DisplayName = "模拟距离",
 Description = "服务器对玩家周围区块进行游戏逻辑模拟的范围（单位：区块）。\n控制红石/实体/农作物计算范围，红石相关最关键参数 ",
 Category = "世界",
 DefaultValue = "10",
 MinValue = 3,
 MaxValue = 32,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-protection",
 ConfigFileName = file,
 DisplayName = "出生点保护范围",
 Description = "出生点周围的保护范围。非 OP 玩家不能在保护区域内破坏/放置方块。\n边长 = 2×此值+1，0=禁用 ️",
 Category = "世界",
 DefaultValue = "16",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "generator-settings",
 ConfigFileName = file,
 DisplayName = "世界生成设置",
 Description = "自定义世界生成的 JSON 配置。\n用于超平坦等自定义世界类型 ️",
 Category = "世界",
 DefaultValue = "{}",
 ValueType = "string",
 RequiresRestart = true,
 });

 // ==================== 游戏机制 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "difficulty",
 ConfigFileName = file,
 DisplayName = "游戏难度",
 Description = "服务器默认的游戏难度。也可以通过 /difficulty 命令在运行时修改。\npeaceful=和平 easy=简单 normal=普通 hard=困难 ️",
 Category = "游戏机制",
 DefaultValue = "easy",
 AllowedValues = ["peaceful", "easy", "normal", "hard"],
 ValueType = "enum",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gamemode",
 ConfigFileName = file,
 DisplayName = "游戏模式",
 Description = "新玩家加入时的默认游戏模式。\nsurvival=生存 creative=创造 adventure=冒险 spectator=旁观 ",
 Category = "游戏机制",
 DefaultValue = "survival",
 AllowedValues = ["survival", "creative", "adventure", "spectator"],
 ValueType = "enum",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "force-gamemode",
 ConfigFileName = file,
 DisplayName = "强制游戏模式",
 Description = "是否强制所有玩家使用默认游戏模式。\n启用后，玩家每次加入服务器都会被设置为默认游戏模式 ",
 Category = "游戏机制",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "hardcore",
 ConfigFileName = file,
 DisplayName = "极限模式",
 Description = "是否启用极限模式。死亡后玩家会被切换为旁观模式（banspec）。\n高难度挑战，死亡即永久旁观 ",
 Category = "游戏机制",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "pvp",
 ConfigFileName = file,
 DisplayName = "PVP",
 Description = "是否允许玩家互相攻击。false=和平服，true=可以打架。\n想搞生存竞技就开，想搞建筑服就关 ",
 Category = "游戏机制",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-command-block",
 ConfigFileName = file,
 DisplayName = "命令方块",
 Description = "是否允许命令方块工作。命令方块可以自动执行命令，是地图制作利器。\n如果不是做地图/红石机器，建议关掉以防滥用 ",
 Category = "游戏机制",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "max-tick-time",
 ConfigFileName = file,
 DisplayName = "单 tick 最大时间",
 Description = "单个游戏 tick 的最大执行时间（毫秒）。\n超过此时间服务器会崩溃并生成崩溃报告。-1=禁用看门狗超时 [TIME]",
 Category = "游戏机制",
 DefaultValue = "60000",
 MinValue = -1,
 MaxValue = int.MaxValue,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "max-chained-neighbor-updates",
 ConfigFileName = file,
 DisplayName = "最大连锁邻居更新",
 Description = "单个方块更新最多引发多少次连锁邻居更新。\n负数=禁用，红石大规模更新时相关的关键参数 ",
 Category = "游戏机制",
 DefaultValue = "1000000",
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "entity-broadcast-range-percentage",
 ConfigFileName = file,
 DisplayName = "实体广播范围百分比",
 Description = "实体元数据广播范围占原始范围的百分比。\n实体元数据发送范围 = 原始范围 × 此值%。\n范围 10-1000，默认 100 ",
 Category = "游戏机制",
 DefaultValue = "100",
 MinValue = 10,
 MaxValue = 1000,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 服务器信息 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "motd",
 ConfigFileName = file,
 DisplayName = "服务器标语 (MOTD)",
 Description = "Message Of The Day —— 服务器在玩家列表里显示的描述文字。\n支持 Minecraft 颜色代码（如 §a 绿色）和格式代码 ",
 Category = "服务器信息",
 DefaultValue = "A Minecraft Server",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-status",
 ConfigFileName = file,
 DisplayName = "在线状态显示",
 Description = "服务器是否在服务器列表中显示为在线。\n关闭后服务器不会响应状态查询 ",
 Category = "服务器信息",
 DefaultValue = "true",
 ValueType = "bool",
 });

 // ==================== 性能优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "sync-chunk-writes",
 ConfigFileName = file,
 DisplayName = "同步区块写入",
 Description = "是否同步写入区块数据。\ntrue=防止崩溃导致数据丢失，false=SSD 可设为 false 提升写入速度 ",
 Category = "性能优化",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "region-file-compression",
 ConfigFileName = file,
 DisplayName = "区域文件压缩",
 Description = "区域文件（.mca）的压缩算法。\ndeflate=默认压缩 lz4=读写最快 none=不压缩 ",
 Category = "性能优化",
 DefaultValue = "deflate",
 AllowedValues = ["deflate", "lz4", "none"],
 ValueType = "enum",
 RequiresRestart = true,
 });

 // ==================== 远程控制 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "enable-rcon",
 ConfigFileName = file,
 DisplayName = "启用 RCON",
 Description = "是否启用远程控制台（RCON）。启用后可以通过网络发送服务器命令。\n方便管理面板对接，但要注意设置强密码！",
 Category = "远程控制",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "rcon.port",
 ConfigFileName = file,
 DisplayName = "RCON 端口",
 Description = "RCON 远程控制台监听的端口号。建议设为非标准端口以提高安全性。\n记得在防火墙里放行这个端口 ",
 Category = "远程控制",
 DefaultValue = "25575",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "rcon.password",
 ConfigFileName = file,
 DisplayName = "RCON 密码",
 Description = "RCON 远程控制台的密码。\n务必设置强密码，不要用默认值！",
 Category = "远程控制",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-query",
 ConfigFileName = file,
 DisplayName = "启用 Query",
 Description = "是否启用 GameSpy4 Query 协议。\n用于服务器列表查询服务器信息 ",
 Category = "远程控制",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "query.port",
 ConfigFileName = file,
 DisplayName = "Query 端口",
 Description = "Query 协议监听的端口号。\n默认与 server-port 相同 ",
 Category = "远程控制",
 DefaultValue = "25565",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-jmx-monitoring",
 ConfigFileName = file,
 DisplayName = "启用 JMX 监控",
 Description = "是否启用 JMX 监控。\n用于监控 Java 虚拟机运行状态 ",
 Category = "远程控制",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "management-server-enabled",
 ConfigFileName = file,
 DisplayName = "管理服务器启用",
 Description = "是否启用管理服务器（JMX/飞行记录器等）。\n生产环境建议关闭 ️",
 Category = "远程控制",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "management-server-host",
 ConfigFileName = file,
 DisplayName = "管理服务器主机",
 Description = "管理服务器绑定的主机地址。\n默认 localhost ️",
 Category = "远程控制",
 DefaultValue = "localhost",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "management-server-port",
 ConfigFileName = file,
 DisplayName = "管理服务器端口",
 Description = "管理服务器监听的端口号。0=自动选择 ",
 Category = "远程控制",
 DefaultValue = "0",
 MinValue = 0,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 资源包 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "resource-pack",
 ConfigFileName = file,
 DisplayName = "资源包 URL",
 Description = "服务器资源包的下载地址。\n玩家可以选择是否使用服务器资源包 ",
 Category = "资源包",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "resource-pack-sha1",
 ConfigFileName = file,
 DisplayName = "资源包 SHA1",
 Description = "资源包的 SHA1 哈希值，用于验证文件完整性。\n确保玩家下载的资源包没有被篡改 ",
 Category = "资源包",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "resource-pack-id",
 ConfigFileName = file,
 DisplayName = "资源包 UUID",
 Description = "资源包的唯一标识符（UUID）。\n用于标识特定的资源包版本 ",
 Category = "资源包",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "require-resource-pack",
 ConfigFileName = file,
 DisplayName = "强制资源包",
 Description = "是否强制玩家使用服务器资源包。\ntrue=玩家必须接受资源包才能进入 ",
 Category = "资源包",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "resource-pack-prompt",
 ConfigFileName = file,
 DisplayName = "资源包提示",
 Description = "提示玩家是否使用资源包时显示的文字。\n可以写一些说明文字 ",
 Category = "资源包",
 ValueType = "string",
 });

 // ==================== 安全 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "prevent-proxy-connections",
 ConfigFileName = file,
 DisplayName = "防止代理连接",
 Description = "是否阻止通过代理/VPN 连接的玩家。\n一定程度上防止恶意玩家换 IP 捣乱 ️",
 Category = "安全",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-code-of-conduct",
 ConfigFileName = file,
 DisplayName = "启用行为准则",
 Description = "是否启用社区行为准则。\n用于符合某些平台的要求 ",
 Category = "安全",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 聊天 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "chat-spam-threshold-seconds",
 ConfigFileName = file,
 DisplayName = "聊天刷屏阈值",
 Description = "聊天消息之间的最小间隔（秒）。\n超过此频率发送消息的玩家会被踢出。0=禁用踢出 ",
 Category = "聊天",
 DefaultValue = "10",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "command-spam-threshold-seconds",
 ConfigFileName = file,
 DisplayName = "命令刷屏阈值",
 Description = "命令之间的最小间隔（秒）。\n超过此频率发送命令的玩家会被踢出。0=禁用踢出 ⌨️",
 Category = "聊天",
 DefaultValue = "10",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "text-filtering-config",
 ConfigFileName = file,
 DisplayName = "聊天过滤配置",
 Description = "聊天内容过滤服务的配置。\n用于过滤不当言论 ",
 Category = "聊天",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "text-filtering-version",
 ConfigFileName = file,
 DisplayName = "聊天过滤版本",
 Description = "聊天过滤的版本号。0 或 1 ",
 Category = "聊天",
 DefaultValue = "0",
 MinValue = 0,
 MaxValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "broadcast-console-to-ops",
 ConfigFileName = file,
 DisplayName = "控制台广播到 OP",
 Description = "是否将控制台输出广播给所有在线 OP 玩家。\n方便 OP 实时查看服务器状态 ",
 Category = "聊天",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "broadcast-rcon-to-ops",
 ConfigFileName = file,
 DisplayName = "RCON 广播到 OP",
 Description = "是否将 RCON 命令输出广播给所有在线 OP 玩家。\n方便多人协作管理 ",
 Category = "聊天",
 DefaultValue = "true",
 ValueType = "bool",
 });

 // ==================== 其他 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "pause-when-empty-seconds",
 ConfigFileName = file,
 DisplayName = "空服暂停延迟",
 Description = "服务器无玩家时多久后暂停游戏 tick（秒）。\n0=不暂停，节省空服时的 CPU 占用 ⏸️",
 Category = "其他",
 DefaultValue = "60",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "initial-enabled-packs",
 ConfigFileName = file,
 DisplayName = "初始启用数据包",
 Description = "初始启用的数据包，逗号分隔。\n默认仅启用 vanilla ",
 Category = "其他",
 DefaultValue = "vanilla",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "initial-disabled-packs",
 ConfigFileName = file,
 DisplayName = "初始禁用数据包",
 Description = "初始禁用的数据包，逗号分隔。\n默认为空 ",
 Category = "其他",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "bug-report-link",
 ConfigFileName = file,
 DisplayName = "Bug 报告链接",
 Description = "玩家崩溃时显示的 Bug 报告链接。\n可以指向你自己的问题追踪页面 ",
 Category = "其他",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "status-heartbeat-interval",
 ConfigFileName = file,
 DisplayName = "状态心跳间隔",
 Description = "状态心跳的发送间隔（秒）。0=禁用。\n用于某些服务器列表的在线统计 ",
 Category = "其他",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });
 }

 /// <summary>
 /// 注册 server.properties 配置文件的补充配置项
 /// </summary>
 /// <remarks>补充注册主方法中可能遗漏的常见配置项</remarks>
 private void RegisterServerPropertiesExtras()
 {
 const string file = "server.properties";

 Register(new ServerConfigDescriptor
 {
 Key = "initial-enabled-packet-type",
 ConfigFileName = file,
 DisplayName = "初始启用数据包类型",
 Description = "服务器启动时默认启用的数据包类型列表。用于细粒度网络协议控制。",
 Category = "网络",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "snooper-enabled",
 ConfigFileName = file,
 DisplayName = "Snooper 数据收集",
 Description = "是否启用 Snooper 匿名数据收集（发送到 Mojang 服务器）。建议关闭以保护隐私。",
 Category = "性能优化",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = false,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "pause-when-empty-seconds",
 ConfigFileName = file,
 DisplayName = "空闲暂停秒数",
 Description = "当服务器无玩家时，等待多少秒后自动暂停 tick 以节省 CPU。0=禁用。",
 Category = "性能优化",
 DefaultValue = "0",
 MinValue = 0,
 MaxValue = 3600,
 ValueType = "int",
 RequiresRestart = false,
 });
 }

 /// <summary>
 /// 注册 bukkit.yml 配置文件的关键配置项
 /// </summary>
 /// <remarks>
 /// Bukkit API 层的基础配置，所有 Bukkit 系服务端核心共享此配置文件。
 /// 数据来源：Bukkit 官方文档 + Spigot 默认配置
 /// </remarks>
 private void RegisterBukkitYml()
 {
 const string file = "bukkit.yml";

 // ==================== 世界设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.allow-end",
 ConfigFileName = file,
 DisplayName = "允许末地",
 Description = "是否允许末地世界。\n关闭后玩家无法进入末地 ",
 Category = "世界",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.allow-nether",
 ConfigFileName = file,
 DisplayName = "允许下界",
 Description = "是否允许下界（地狱）世界。\n关闭后玩家无法进入下界 ",
 Category = "世界",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.world-container",
 ConfigFileName = file,
 DisplayName = "世界容器目录",
 Description = "存放世界文件夹的目录。默认为服务器根目录。\n可以把世界放到其他磁盘或目录 ",
 Category = "世界",
 DefaultValue = ".",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.default-world-size",
 ConfigFileName = file,
 DisplayName = "默认世界大小",
 Description = "新创建世界的默认大小限制。0=无限制 ",
 Category = "世界",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 刷怪设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-limits.monsters",
 ConfigFileName = file,
 DisplayName = "怪物刷怪上限",
 Description = "每个玩家周围的怪物生成上限。\n值越小怪物越少，服务器越轻松 ",
 Category = "刷怪",
 DefaultValue = "70",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-limits.animals",
 ConfigFileName = file,
 DisplayName = "动物刷怪上限",
 Description = "每个玩家周围的动物生成上限。",
 Category = "刷怪",
 DefaultValue = "10",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-limits.water-animals",
 ConfigFileName = file,
 DisplayName = "水生动物刷怪上限",
 Description = "每个玩家周围的水生动物生成上限。",
 Category = "刷怪",
 DefaultValue = "15",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-limits.water-ambient",
 ConfigFileName = file,
 DisplayName = "水环境生物刷怪上限",
 Description = "每个玩家周围的水环境生物生成上限（如鱿鱼）。",
 Category = "刷怪",
 DefaultValue = "20",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-limits.ambient",
 ConfigFileName = file,
 DisplayName = "环境生物刷怪上限",
 Description = "每个玩家周围的环境生物生成上限（如蝙蝠）。",
 Category = "刷怪",
 DefaultValue = "15",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "ticks-per.animal-spawns",
 ConfigFileName = file,
 DisplayName = "动物生成间隔",
 Description = "动物生成的间隔（tick）。值越大生成越慢。\n20 tick = 1 秒 [TIME]",
 Category = "刷怪",
 DefaultValue = "400",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "ticks-per.monster-spawns",
 ConfigFileName = file,
 DisplayName = "怪物生成间隔",
 Description = "怪物生成的间隔（tick）。值越大生成越慢。\n20 tick = 1 秒 ",
 Category = "刷怪",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 自动保存 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "autosave",
 ConfigFileName = file,
 DisplayName = "自动保存",
 Description = "是否启用自动保存。\n关闭后世界数据只在服务器关闭时保存，有数据丢失风险！",
 Category = "自动保存",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "autosave-period-ticks",
 ConfigFileName = file,
 DisplayName = "自动保存间隔",
 Description = "自动保存的间隔（tick）。\n默认 5 分钟（6000 tick）[TIME]",
 Category = "自动保存",
 DefaultValue = "6000",
 MinValue = 100,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 性能优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-gc.period-in-ticks",
 ConfigFileName = file,
 DisplayName = "区块 GC 间隔",
 Description = "区块垃圾回收的间隔（tick）。\n定期回收不需要的区块，释放内存 ️",
 Category = "性能优化",
 DefaultValue = "400",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-gc.load-threshold",
 ConfigFileName = file,
 DisplayName = "区块加载阈值",
 Description = "触发区块 GC 的加载区块数阈值。\n当加载的区块数超过此值时触发 GC ",
 Category = "性能优化",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 玩家设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.connection-throttle",
 ConfigFileName = file,
 DisplayName = "连接节流",
 Description = "同一 IP 两次连接之间的最小间隔（毫秒）。\n防止玩家快速重连攻击 ",
 Category = "玩家",
 DefaultValue = "4000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.legacy-structure-conversion",
 ConfigFileName = file,
 DisplayName = "旧结构转换",
 Description = "是否转换旧版结构数据。\n从旧版本升级服务器时需要 ️",
 Category = "玩家",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.shutdown-timeout",
 ConfigFileName = file,
 DisplayName = "关闭超时",
 Description = "服务器关闭时等待玩家数据保存的超时时间（秒）。\n超时后强制关闭 [TIME]",
 Category = "玩家",
 DefaultValue = "30",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 杂项设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "warn-on-overload",
 ConfigFileName = file,
 DisplayName = "过载警告",
 Description = "当服务器 TPS 低于阈值时是否在控制台输出警告。\n帮助管理员及时发现性能问题 ️",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "permissions-file",
 ConfigFileName = file,
 DisplayName = "权限配置文件",
 Description = "权限配置文件的路径。\n指定权限配置文件的位置 ",
 Category = "杂项",
 DefaultValue = "permissions.yml",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "plugin-profiling",
 ConfigFileName = file,
 DisplayName = "插件性能分析",
 Description = "是否启用插件性能分析。\n开启后可查看各插件的耗时统计 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "query-plugins",
 ConfigFileName = file,
 DisplayName = "查询插件信息",
 Description = "是否在服务器查询（Query）中显示插件列表。\n允许外部工具查询服务器插件信息 ",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "deprecated-verbose",
 ConfigFileName = file,
 DisplayName = "弃用警告详细度",
 Description = "废弃 API 警告的详细程度。\n控制弃用 API 警告的输出等级 ",
 Category = "杂项",
 DefaultValue = "default",
 ValueType = "enum",
 AllowedValues = ["default", "quiet"],
 });

 Register(new ServerConfigDescriptor
 {
 Key = "shutdown-message",
 ConfigFileName = file,
 DisplayName = "关闭消息",
 Description = "服务器关闭时显示给玩家的消息。\n玩家会在被踢出时看到这条消息 ",
 Category = "杂项",
 DefaultValue = "Server closed",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "minimum-api",
 ConfigFileName = file,
 DisplayName = "最低 API 版本",
 Description = "插件所需的最低 Bukkit API 版本。\n低于此版本的插件将被拒绝加载 ",
 Category = "杂项",
 DefaultValue = "none",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "use-map-color-cache",
 ConfigFileName = file,
 DisplayName = "使用地图颜色缓存",
 Description = "是否启用地图颜色缓存。\n开启后可提升地图渲染性能 ️",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 });
 }

 /// <summary>
 /// 注册 spigot.yml 配置文件的关键配置项
 /// </summary>
 /// <remarks>
 /// Spigot 是 CraftBukkit 的增强版，提供性能优化和功能扩展配置。
 /// 包含实体激活范围、物品合并、世界设置等性能关键参数。
 /// </remarks>
 private void RegisterSpigotYml()
 {
 const string file = "spigot.yml";

 // ==================== 性能优化 - 刷怪 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mob-spawn-range",
 ConfigFileName = file,
 DisplayName = "怪物生成范围",
 Description = "怪物在玩家周围生成的最大距离（单位：区块）。\n值越小生成的怪物越少，服务器越轻松，但世界会显得空荡荡的 ",
 Category = "性能优化",
 DefaultValue = "8",
 MinValue = 2,
 MaxValue = 128,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.animals",
 ConfigFileName = file,
 DisplayName = "动物激活范围",
 Description = "动物（牛、羊、猪等）在玩家周围多远内会被激活（开始运行 AI 逻辑）。\n降低此值可以显著减少服务器 CPU 占用 ",
 Category = "性能优化",
 DefaultValue = "32",
 MinValue = 1,
 MaxValue = 512,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.monsters",
 ConfigFileName = file,
 DisplayName = "怪物激活范围",
 Description = "怪物（僵尸、骷髅、爬行者等）的 AI 激活范围。\n这是影响服务器性能的关键参数之一！降低它能让服务器喘口气 ",
 Category = "性能优化",
 DefaultValue = "32",
 MinValue = 1,
 MaxValue = 512,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.misc",
 ConfigFileName = file,
 DisplayName = "杂项实体激活范围",
 Description = "其他实体（掉落物、矿车、经验球等）的激活范围。\n如果你的服务器地上的掉落物特别多，降低这个值有奇效 ",
 Category = "性能优化",
 DefaultValue = "16",
 MinValue = 1,
 MaxValue = 512,
 });

 // ==================== 性能优化 - 合并 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "merge-radius.item",
 ConfigFileName = file,
 DisplayName = "物品合并半径",
 Description = "地上的掉落物品在多大范围内会自动合并为一堆。\n值越大，地面越干净，同时也减少实体数量 ",
 Category = "性能优化",
 DefaultValue = "2.5",
 MinValue = 0,
 MaxValue = 64,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "merge-radius.exp",
 ConfigFileName = file,
 DisplayName = "经验球合并半径",
 Description = "经验球在多大范围内会自动合并。\n打怪之后满地经验球的罪魁祸首就是这个值太小了 ",
 Category = "性能优化",
 DefaultValue = "3.0",
 MinValue = 0,
 MaxValue = 64,
 });

 // ==================== 世界设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.view-distance",
 ConfigFileName = file,
 DisplayName = "视距",
 Description = "服务器向玩家发送的区块渲染范围（单位：区块）。\n值越大看到越远，但服务器和客户端负担也越重。建议 8-12 ",
 Category = "世界设置",
 DefaultValue = "default",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.merge-radius.item",
 ConfigFileName = file,
 DisplayName = "物品合并半径",
 Description = "地上的掉落物品在多大范围内会自动合并为一堆。\n值越大，地面越干净，同时也减少实体数量 ",
 Category = "世界设置",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.mob-spawn-range",
 ConfigFileName = file,
 DisplayName = "怪物生成范围",
 Description = "怪物在玩家周围生成的最大距离（单位：区块）。\n值越小生成的怪物越少，服务器越轻松 ",
 Category = "世界设置",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entity-per-chunk-save-limit",
 ConfigFileName = file,
 DisplayName = "每区块实体保存限制",
 Description = "每个区块保存的实体数量上限。\n超过此数量的实体会被删除，防止区块实体过多导致卡顿 ",
 Category = "世界设置",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.growth",
 ConfigFileName = file,
 DisplayName = "作物生长调整",
 Description = "调整各种作物的生长速度倍率。\n值越大生长越快，1.0=默认速度 ",
 Category = "世界设置",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.tick-per",
 ConfigFileName = file,
 DisplayName = "Tick 间隔调整",
 Description = "调整各种系统的 tick 执行间隔。\n值越大执行频率越低 [TIME]",
 Category = "世界设置",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.random-light-updates",
 ConfigFileName = file,
 DisplayName = "随机光照更新",
 Description = "是否启用随机光照更新。\n关闭可减少光照计算开销 ",
 Category = "世界设置",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.save-structure-info",
 ConfigFileName = file,
 DisplayName = "保存结构信息",
 Description = "是否保存结构信息（如村庄、神殿等）。\n关闭可节省少量磁盘空间 ️",
 Category = "世界设置",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.max-bulk-chunks",
 ConfigFileName = file,
 DisplayName = "最大批量区块数",
 Description = "批量处理的最大区块数量。\n影响区块发送和处理的效率 ",
 Category = "世界设置",
 DefaultValue = "5",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.max-entity-collisions",
 ConfigFileName = file,
 DisplayName = "最大实体碰撞数",
 Description = "单个实体每 tick 最多处理的碰撞次数。\n降低可减少实体密集时的性能消耗 ",
 Category = "世界设置",
 DefaultValue = "8",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.dragon-death-sound-radius",
 ConfigFileName = file,
 DisplayName = "末影龙死亡音效范围",
 Description = "末影龙死亡时播放音效的范围（方块）。\n0=只有附近玩家能听到 ",
 Category = "世界设置",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.seed-village",
 ConfigFileName = file,
 DisplayName = "村庄种子",
 Description = "村庄生成的种子。\n用于控制村庄的生成位置 ️",
 Category = "世界设置",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.seed-feature",
 ConfigFileName = file,
 DisplayName = "地物种子",
 Description = "地物（洞穴、矿脉等）生成的种子。\n用于控制地物的生成位置 ️",
 Category = "世界设置",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.seed-monument",
 ConfigFileName = file,
 DisplayName = "海底神殿种子",
 Description = "海底神殿生成的种子。\n用于控制海底神殿的生成位置 ️",
 Category = "世界设置",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.seed-slime",
 ConfigFileName = file,
 DisplayName = "史莱姆区块种子",
 Description = "史莱姆区块生成的种子。\n用于控制史莱姆生成的区块位置 ",
 Category = "世界设置",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.hunger",
 ConfigFileName = file,
 DisplayName = "饥饿机制",
 Description = "饥饿相关的机制调整。\n影响玩家饥饿值消耗速度 ",
 Category = "世界设置",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.movement-speed-atk",
 ConfigFileName = file,
 DisplayName = "移动速度攻击修正",
 Description = "移动速度对攻击的修正系数。\n影响移动攻击的伤害计算 ️",
 Category = "世界设置",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.item-dirty-ticks",
 ConfigFileName = file,
 DisplayName = "物品脏 Tick 数",
 Description = "掉落物实体多久标记为脏（需要保存）。\n值越大保存频率越低 ",
 Category = "世界设置",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.arrow-despawn-rate",
 ConfigFileName = file,
 DisplayName = "箭矢消失速率",
 Description = "射出的箭矢多久后消失（tick）。\n值越小箭矢消失越快 ",
 Category = "世界设置",
 DefaultValue = "1200",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.trident-despawn-rate",
 ConfigFileName = file,
 DisplayName = "三叉戟消失速率",
 Description = "投掷的三叉戟多久后消失（tick）。\n值越小消失越快 ",
 Category = "世界设置",
 DefaultValue = "1200",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.nerf-spawner-mobs",
 ConfigFileName = file,
 DisplayName = "削弱刷怪笼怪物",
 Description = "是否削弱刷怪笼生成的怪物。\n削弱后的怪物 AI 会减弱，性能更好 ",
 Category = "世界设置",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.enable-zombie-pigmen-portal-spawns",
 ConfigFileName = file,
 DisplayName = "猪灵下界传送门生成",
 Description = "是否允许猪灵（僵尸猪人）从下界传送门生成。\n关闭可减少猪灵数量 ",
 Category = "世界设置",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.wither-spawn-sound-radius",
 ConfigFileName = file,
 DisplayName = "凋灵生成音效范围",
 Description = "凋灵生成时播放音效的范围（方块）。\n0=只有附近玩家能听到 ",
 Category = "世界设置",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.hanging-tick-frequency",
 ConfigFileName = file,
 DisplayName = "悬挂实体 Tick 频率",
 Description = "画、物品展示框等悬挂实体的 tick 频率。\n值越大处理频率越低 ️",
 Category = "世界设置",
 DefaultValue = "100",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.zombie-aggressive-towards-villager",
 ConfigFileName = file,
 DisplayName = "僵尸攻击村民",
 Description = "僵尸是否主动攻击村民。\n关闭可保护村民不被僵尸攻击 ‍️",
 Category = "世界设置",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.log-villager-deaths",
 ConfigFileName = file,
 DisplayName = "记录村民死亡",
 Description = "是否在日志中记录村民死亡。\n方便排查村民死亡原因 ",
 Category = "世界设置",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.log-named-deaths",
 ConfigFileName = file,
 DisplayName = "记录命名实体死亡",
 Description = "是否在日志中记录命名实体的死亡。\n命名实体包括被命名的宠物、村民等 ",
 Category = "世界设置",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.log-deaths",
 ConfigFileName = file,
 DisplayName = "记录死亡信息",
 Description = "是否在日志中记录实体死亡信息。\n关闭可减少日志输出 ",
 Category = "世界设置",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.save-usercache-on-player-join",
 ConfigFileName = file,
 DisplayName = "玩家加入时保存用户缓存",
 Description = "玩家加入时是否保存用户缓存（usercache.json）。\n开启可确保缓存及时更新 ",
 Category = "世界设置",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.player-filter",
 ConfigFileName = file,
 DisplayName = "玩家过滤器",
 Description = "玩家过滤相关设置。\n用于过滤不符合条件的玩家 ",
 Category = "世界设置",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.filter-creative-items",
 ConfigFileName = file,
 DisplayName = "过滤创造模式物品",
 Description = "是否过滤创造模式物品栏中的某些物品。\n用于限制创造模式可用物品 ",
 Category = "世界设置",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.world-border",
 ConfigFileName = file,
 DisplayName = "世界边界",
 Description = "世界边界相关设置。\n控制世界边界的大小和行为 ",
 Category = "世界设置",
 ValueType = "string",
 });

 // ==================== 玩家设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "players.ping-sample",
 ConfigFileName = file,
 DisplayName = "延迟采样数",
 Description = "服务器列表中显示的玩家延迟采样数量。\n影响服务器列表显示的延迟信息 ",
 Category = "玩家设置",
 DefaultValue = "12",
 MinValue = 1,
 MaxValue = 1000,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "tab-replace",
 ConfigFileName = file,
 DisplayName = "Tab 列表替换",
 Description = "是否替换 Tab 玩家列表显示。\n用于自定义 Tab 列表显示 ",
 Category = "玩家设置",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "commands.tab-complete",
 ConfigFileName = file,
 DisplayName = "Tab 命令补全",
 Description = "是否启用命令 Tab 补全。\n关闭可提高安全性 ⌨️",
 Category = "玩家设置",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 网络设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.bungeecord",
 ConfigFileName = file,
 DisplayName = "BungeeCord 模式",
 Description = "是否启用 BungeeCord 模式。\n启用后服务器会信任 BungeeCord 转发的玩家信息 ",
 Category = "网络设置",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.timeout-time",
 ConfigFileName = file,
 DisplayName = "超时时间",
 Description = "玩家连接超时时间（秒）。\n超过此时间无响应则断开连接 [TIME]",
 Category = "网络设置",
 DefaultValue = "30",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.restart-on-crash",
 ConfigFileName = file,
 DisplayName = "崩溃自动重启",
 Description = "服务器崩溃后是否自动重启。\n需要配合重启脚本使用 ",
 Category = "网络设置",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.restart-script",
 ConfigFileName = file,
 DisplayName = "重启脚本",
 Description = "服务器崩溃时执行的重启脚本路径。\n需配合 restart-on-crash 使用 ",
 Category = "网络设置",
 DefaultValue = "./start.sh",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.player-shuffle",
 ConfigFileName = file,
 DisplayName = "玩家混洗",
 Description = "是否打乱玩家连接处理顺序。\n可防止某些针对连接顺序的攻击 ",
 Category = "网络设置",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.advanced-ipc",
 ConfigFileName = file,
 DisplayName = "高级 IPC",
 Description = "是否启用高级进程间通信。\n用于某些插件的跨进程通信 ",
 Category = "网络设置",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.watchdog-thread",
 ConfigFileName = file,
 DisplayName = "看门狗线程",
 Description = "是否启用看门狗线程。\n用于检测服务器卡顿并生成报告 ",
 Category = "网络设置",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.netty-threads",
 ConfigFileName = file,
 DisplayName = "Netty 线程数",
 Description = "Netty 网络 IO 线程数。\n-1=自动（CPU 核心数的一半）",
 Category = "网络设置",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "late-bind",
 ConfigFileName = file,
 DisplayName = "延迟绑定",
 Description = "是否启用延迟绑定。\n启用后直到玩家完成握手才分配连接资源 ",
 Category = "网络设置",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 基础设置（settings.*） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.debug",
 ConfigFileName = file,
 DisplayName = "调试模式",
 Description = "是否启用调试模式。\n开启后服务器会输出更详细的调试日志，可能影响性能 ",
 Category = "基础设置",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.sample-count",
 ConfigFileName = file,
 DisplayName = "采样计数",
 Description = "性能采样的计数。\n用于统计服务器性能数据 ",
 Category = "基础设置",
 DefaultValue = "12",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.user-cache-size",
 ConfigFileName = file,
 DisplayName = "用户缓存大小",
 Description = "每个玩家的缓存条目数量。\n影响玩家数据存取效率 ",
 Category = "基础设置",
 DefaultValue = "1000",
 MinValue = 100,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.update-folder",
 ConfigFileName = file,
 DisplayName = "更新文件夹",
 Description = "服务器检查更新时使用的文件夹路径。\n指定服务器更新文件存放位置 ",
 Category = "基础设置",
 ValueType = "string",
 });

 // ==================== 属性设置（attribute.*） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "attribute.maxabsorption",
 ConfigFileName = file,
 DisplayName = "最大吸取等级",
 Description = "物品可吸取的最大等级。\n影响附魔吸取效果 ",
 Category = "属性设置",
 DefaultValue = "2048.0",
 MinValue = 0,
 ValueType = "double",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "attribute.max",
 ConfigFileName = file,
 DisplayName = "属性上限",
 Description = "玩家属性的最大值。\n限制玩家可达到的属性上限 ",
 Category = "属性设置",
 DefaultValue = "2048.0",
 MinValue = 0,
 ValueType = "double",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "attribute.maxhealth",
 ConfigFileName = file,
 DisplayName = "最大生命",
 Description = "玩家可达到的最大生命值。\n限制玩家血量上限 ️",
 Category = "属性设置",
 DefaultValue = "2048.0",
 MinValue = 0,
 ValueType = "double",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "attribute.movementspeed",
 ConfigFileName = file,
 DisplayName = "移动速度上限",
 Description = "玩家可达到的最大移动速度。\n限制玩家移动速度上限 ",
 Category = "属性设置",
 DefaultValue = "2048.0",
 MinValue = 0,
 ValueType = "double",
 });
 

    // ===== AUTO-INJECTED by core-fetcher pipeline =====
    Register(new ServerConfigDescriptor
    {
        Key = "advancements.disable-saving",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "禁用进度保存",
        Description = "关闭后玩家进度数据不再自动写入磁盘",
        Category = "Spigot 通用",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advancements.disabled",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "禁用的进度",
        Description = "这些进度 ID 会被禁用，玩家无法获取",
        Category = "Spigot 通用",
        DefaultValue = "[\"minecraft:story/disabled\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "commands.log",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "记录命令日志",
        Description = "是否在控制台记录所有玩家执行的命令",
        Category = "Spigot 通用",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "commands.replace-commands",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "替换命令",
        Description = "这些命令会被优化版实现替换",
        Category = "Spigot 通用",
        DefaultValue = "[\"setblock\", \"summon\", \"testforblock\", \"tellraw\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "commands.silent-commandblock-console",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "命令方块静默",
        Description = "命令方块执行命令时不在控制台输出",
        Category = "Spigot 通用",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "commands.spam-exclusions",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "刷屏排除命令",
        Description = "这些命令不受反刷屏限制（如 /skill）",
        Category = "Spigot 通用",
        DefaultValue = "[\"/skill\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "配置版本号",
        Description = "内部配置版本标识，请勿手动修改",
        Category = "Spigot 通用",
        DefaultValue = "11",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.outdated-client",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "客户端过旧消息",
        Description = "{0} 为服务器版本；客户端版本太旧时的提示",
        Category = "Spigot 通用",
        DefaultValue = "Outdated client! Please use {0}",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.outdated-server",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "服务端过旧消息",
        Description = "{0} 为客户端版本；服务器版本落后时的提示",
        Category = "Spigot 通用",
        DefaultValue = "Outdated server! I'm still on {0}",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.restart",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "重启消息",
        Description = "服务器即将重启时显示给在线玩家的消息",
        Category = "Spigot 通用",
        DefaultValue = "Server is restarting",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.server-full",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "服务器已满消息",
        Description = "达到玩家上限后新玩家连接时看到的提示",
        Category = "Spigot 通用",
        DefaultValue = "The server is full!",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.unknown-command",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "未知命令消息",
        Description = "玩家输入不存在的命令时显示的提示",
        Category = "Spigot 通用",
        DefaultValue = "Unknown command. Type \"/help\" for help.",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.whitelist",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "白名单消息",
        Description = "未在白名单的玩家尝试加入时看到的提示",
        Category = "Spigot 通用",
        DefaultValue = "You are not whitelisted on this server!",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.attribute.attackDamage.max",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "攻击伤害属性上限",
        Description = "攻击伤害 attribute 允许的最大值",
        Category = "Spigot 通用",
        DefaultValue = "2048.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.attribute.maxHealth.max",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "最大生命属性上限",
        Description = "最大生命值 attribute 允许的最大值",
        Category = "Spigot 通用",
        DefaultValue = "2048.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.attribute.movementSpeed.max",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "移动速度属性上限",
        Description = "移动速度 attribute 允许的最大值",
        Category = "Spigot 通用",
        DefaultValue = "2048.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.filter-creative-items",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "过滤创造物品",
        Description = "过滤创造模式物品栏中无效/不存在的物品",
        Category = "Spigot 通用",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.int-cache-limit",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "整数缓存上限",
        Description = "优化用的 Integer 对象缓存池上限，影响 GC 频率",
        Category = "Spigot 通用",
        DefaultValue = "1024",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.item-dirty-ticks",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "物品脏数据 Tick",
        Description = "物品实体被标记为脏后等待多久才自动清理",
        Category = "Spigot 通用",
        DefaultValue = "20",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.late-bind",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "延迟绑定",
        Description = "插件监听器采用延迟绑定以提升加载速度",
        Category = "Spigot 通用",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.moved-too-quickly-multiplier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "过速检测倍数",
        Description = "玩家被检测到移动过快时的容忍倍数",
        Category = "Spigot 通用",
        DefaultValue = "10.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.moved-wrongly-threshold",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "位置异常阈值",
        Description = "玩家位置被判定异常/作弊的最小位移阈值",
        Category = "Spigot 通用",
        DefaultValue = "0.0625",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.save-user-cache-on-stop-only",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "仅停机保存用户缓存",
        Description = "usercache.json 只在服务器关闭时才写入磁盘",
        Category = "Spigot 通用",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "stats.disable-saving",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "禁用统计保存",
        Description = "关闭后玩家统计数据不再自动写入磁盘",
        Category = "Spigot 通用",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-activation-range.animals",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "动物激活距离",
        Description = "动物在玩家周围多远范围内才会执行 AI Tick",
        Category = "Spigot 通用",
        DefaultValue = "32",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-activation-range.misc",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "杂项实体激活距离",
        Description = "矿车、箭等杂项实体的激活距离",
        Category = "Spigot 通用",
        DefaultValue = "16",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-activation-range.monsters",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "怪物激活距离",
        Description = "僵尸、骷髅等怪物的激活距离",
        Category = "Spigot 通用",
        DefaultValue = "32",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-activation-range.tick-inactive-villagers",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "非活跃村民 Tick",
        Description = "是否持续 Tick 附近没有玩家的村民实体",
        Category = "Spigot 通用",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-activation-range.water",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "水生实体激活距离",
        Description = "鱿鱼、海豚等水生实体的激活距离",
        Category = "Spigot 通用",
        DefaultValue = "16",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-tracking-range.animals",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "动物追踪距离",
        Description = "客户端接收动物实体移动包的距离",
        Category = "Spigot 通用",
        DefaultValue = "48",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-tracking-range.misc",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "杂项实体追踪距离",
        Description = "客户端接收杂项实体移动包的距离",
        Category = "Spigot 通用",
        DefaultValue = "32",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-tracking-range.monsters",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "怪物追踪距离",
        Description = "客户端接收怪物实体移动包的距离",
        Category = "Spigot 通用",
        DefaultValue = "48",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-tracking-range.other",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "其他实体追踪距离",
        Description = "盔甲架、物品展示框等的追踪距离",
        Category = "Spigot 通用",
        DefaultValue = "64",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.entity-tracking-range.players",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "玩家追踪距离",
        Description = "客户端接收其他玩家实体的距离",
        Category = "Spigot 通用",
        DefaultValue = "48",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.cactus-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "仙人掌生长速度",
        Description = "仙人掌生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.cane-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "甘蔗生长速度",
        Description = "甘蔗生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.cocoa-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "可可豆生长速度",
        Description = "可可豆生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.melon-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "西瓜生长速度",
        Description = "西瓜生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.mushroom-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "蘑菇生长速度",
        Description = "蘑菇生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.netherwart-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "地狱疣生长速度",
        Description = "地狱疣生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.pumpkin-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "南瓜生长速度",
        Description = "南瓜生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.sapling-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "树苗生长速度",
        Description = "树苗生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.vine-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "藤蔓生长速度",
        Description = "藤蔓生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.growth.wheat-modifier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "小麦生长速度",
        Description = "小麦生长速度百分比，100=原版",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.hopper-amount",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "漏斗每次传输数量",
        Description = "每个漏斗检查周期内可移动的物品堆数上限",
        Category = "Spigot 通用",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.hunger.combat-exhaustion",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "战斗饥饿消耗",
        Description = "战斗时的饥饿度消耗系数",
        Category = "Spigot 通用",
        DefaultValue = "0.1",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.hunger.jump-sprint-exhaustion",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "疾跑跳跃饥饿",
        Description = "疾跑中跳跃的饥饿度消耗",
        Category = "Spigot 通用",
        DefaultValue = "0.2",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.hunger.jump-walk-exhaustion",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "走路跳跃饥饿",
        Description = "普通走路跳跃的饥饿度消耗",
        Category = "Spigot 通用",
        DefaultValue = "0.05",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.hunger.other-multiplier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "其他饥饿倍率",
        Description = "其他未分类行为的饥饿消耗倍率",
        Category = "Spigot 通用",
        DefaultValue = "0.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.hunger.regen-exhaustion",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "回血饥饿消耗",
        Description = "自然回血时消耗的饥饿度值",
        Category = "Spigot 通用",
        DefaultValue = "6.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.hunger.sprint-multiplier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "疾跑饥饿倍率",
        Description = "疾跑时的饥饿消耗倍率",
        Category = "Spigot 通用",
        DefaultValue = "0.1",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.hunger.swim-multiplier",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "游泳饥饿倍率",
        Description = "游泳时的饥饿消耗倍率",
        Category = "Spigot 通用",
        DefaultValue = "0.01",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.item-despawn-rate",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "物品消失时间",
        Description = "掉落物在地上保留的游戏刻数（6000=5 分钟）",
        Category = "Spigot 通用",
        DefaultValue = "6000",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.max-tick-time.entity",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "实体最大 Tick 时间",
        Description = "单个实体单次 Tick 允许的最大耗时（毫秒）",
        Category = "Spigot 通用",
        DefaultValue = "50",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.max-tick-time.tile",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "Tile 实体最大 Tick 时间",
        Description = "命令方块、漏斗等 Tile 单次 Tick 最大耗时",
        Category = "Spigot 通用",
        DefaultValue = "50",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.max-tnt-per-tick",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "每 Tick TNT 爆炸上限",
        Description = "单个 tick 内允许同时爆炸的 TNT 数量上限",
        Category = "Spigot 通用",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.merge-radius.exp",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "经验球合并半径",
        Description = "经验球实体互相靠近时自动合并的距离（格）",
        Category = "Spigot 通用",
        DefaultValue = "3.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.squid-spawn-range.min",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "鱿鱼最小生成距离",
        Description = "鱿鱼生成区域距离玩家的最小半径（格）",
        Category = "Spigot 通用",
        DefaultValue = "45.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.ticks-per.hopper-check",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "漏斗检查间隔",
        Description = "漏斗检查上方物品的 Tick 间隔",
        Category = "Spigot 通用",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.ticks-per.hopper-transfer",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "漏斗传输间隔",
        Description = "漏斗尝试吸/推物品每 N tick 一次",
        Category = "Spigot 通用",
        DefaultValue = "8",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world-settings.default.verbose",
        ConfigFileName = "config/spigot.yml",
        DisplayName = "详细日志",
        Description = "是否在控制台输出更详细的调试/告警信息",
        Category = "Spigot 通用",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    // ===== END AUTO-INJECTED =====
}

 /// <summary>
 /// 注册 config/paper-global.yml 配置文件的关键配置项
 /// </summary>
 /// <remarks>
 /// Paper/Folia 的全局配置文件，包含区域化多线程、方块更新控制、
 /// 区块系统、命令、控制台、物品验证等高级优化配置。
 /// 数据来源：PaperMC 官方文档 + Folia 26.1.2 默认配置
 /// </remarks>
 private void RegisterPaperGlobalYml()
 {
 const string file = "config/paper-global.yml";

 // ==================== Folia 专属：区域化多线程 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "threaded-regions.threads",
 ConfigFileName = file,
 DisplayName = "区域 tick 线程数",
 Description = "Folia 区域化多线程的 tick 线程数量。\n-1=根据 CPU 自动分配。分配完 Netty IO、Chunk IO、Chunk Worker、GC 并发线程后，剩余核心的 80% 以内分配给此项 ",
 Category = "区域化多线程",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "threaded-regions.gridExponent",
 ConfigFileName = file,
 DisplayName = "区域大小指数",
 Description = "每个区域 = 2^n × 2^n 区块。\n4=16×16区块(256×256格)；5=32×32(512×512)；6=64×64(1024×1024)。红石机器多时应调大到 6 ",
 Category = "区域化多线程",
 DefaultValue = "4",
 MinValue = 2,
 MaxValue = 7,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "threaded-regions.scheduler",
 ConfigFileName = file,
 DisplayName = "区域调度算法",
 Description = "区域线程的调度算法。\nEDF=最早截止时间优先（最稳定）；WORK_STEALING=工作窃取（性能更好但已知有问题）️",
 Category = "区域化多线程",
 DefaultValue = "EDF",
 AllowedValues = ["EDF", "WORK_STEALING"],
 ValueType = "enum",
 RequiresRestart = true,
 });

 // ==================== 方块更新控制 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "block-updates.disable-chorus-plant-updates",
 ConfigFileName = file,
 DisplayName = "禁用紫颂植物更新",
 Description = "是否禁用紫颂植物的方块更新。\n可以减少紫颂花/紫颂果生长导致的服务器卡顿 ",
 Category = "方块更新",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "block-updates.disable-mushroom-block-updates",
 ConfigFileName = file,
 DisplayName = "禁用蘑菇方块更新",
 Description = "是否禁用蘑菇方块的方块更新。\n减少蘑菇传播导致的更新开销 ",
 Category = "方块更新",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "block-updates.disable-noteblock-updates",
 ConfigFileName = file,
 DisplayName = "禁用音符盒更新",
 Description = "是否禁用音符盒的方块更新。\n大型红石音乐机器可能需要 ",
 Category = "方块更新",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "block-updates.disable-tripwire-updates",
 ConfigFileName = file,
 DisplayName = "禁用绊线更新",
 Description = "是否禁用绊线的方块更新。\n减少绊线更新开销 ",
 Category = "方块更新",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 区块系统 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-system.gen-parallelism",
 ConfigFileName = file,
 DisplayName = "区块生成并行度",
 Description = "区块生成的并行度。\ndefault=自动，true=启用，false=禁用 ️",
 Category = "区块系统",
 DefaultValue = "default",
 AllowedValues = ["default", "true", "false"],
 ValueType = "enum",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-system.io-threads",
 ConfigFileName = file,
 DisplayName = "区块 IO 线程数",
 Description = "区块 IO 操作的线程数。-1=自动 ",
 Category = "区块系统",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-system.worker-threads",
 ConfigFileName = file,
 DisplayName = "区块工作线程数",
 Description = "区块处理工作线程数。-1=自动（物理核心数一半）",
 Category = "区块系统",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 区块加载（高级）====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-loading-advanced.auto-config-send-distance",
 ConfigFileName = file,
 DisplayName = "自动配置发送距离",
 Description = "是否基于视距自动匹配发送距离。\n推荐开启，自动优化 ",
 Category = "区块加载",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-loading-advanced.player-max-concurrent-chunk-generates",
 ConfigFileName = file,
 DisplayName = "每玩家最大并发区块生成",
 Description = "每个玩家最多同时生成多少个区块。0=无限 ",
 Category = "区块加载",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-loading-advanced.player-max-concurrent-chunk-loads",
 ConfigFileName = file,
 DisplayName = "每玩家最大并发区块加载",
 Description = "每个玩家最多同时加载多少个区块。0=无限 ",
 Category = "区块加载",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 区块加载（基础）====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-loading-basic.player-max-chunk-generate-rate",
 ConfigFileName = file,
 DisplayName = "每玩家每秒区块生成速率",
 Description = "每个玩家每秒最多生成多少个区块。-1.0=无限 ",
 Category = "区块加载",
 DefaultValue = "-1.0",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-loading-basic.player-max-chunk-load-rate",
 ConfigFileName = file,
 DisplayName = "每玩家每秒区块加载速率",
 Description = "每个玩家每秒最多加载多少个区块。-1.0=无限 ",
 Category = "区块加载",
 DefaultValue = "100.0",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-loading-basic.player-max-chunk-send-rate",
 ConfigFileName = file,
 DisplayName = "每玩家每秒区块发送速率",
 Description = "每个玩家每秒最多发送多少个区块数据包 ",
 Category = "区块加载",
 DefaultValue = "75.0",
 ValueType = "string",
 RequiresRestart = true,
 });

 // ==================== 碰撞（全局）====================

 Register(new ServerConfigDescriptor
 {
 Key = "collisions.enable-player-collisions",
 ConfigFileName = file,
 DisplayName = "启用玩家碰撞",
 Description = "是否启用玩家之间的碰撞。\n关闭后玩家可以互相穿过 ",
 Category = "碰撞",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "collisions.send-full-pos-for-hard-colliding-entities",
 ConfigFileName = file,
 DisplayName = "硬碰撞实体完整坐标",
 Description = "是否为硬碰撞的实体发送完整位置信息。\n用于减少位置不同步问题 ",
 Category = "碰撞",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 命令 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "commands.ride-command-allow-player-as-vehicle",
 ConfigFileName = file,
 DisplayName = "/ride 允许玩家作载具",
 Description = "是否允许 /ride 命令让玩家作为其他实体的载具。\n可能被滥用，谨慎开启 ",
 Category = "命令",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "commands.suggest-player-names-when-null-tab-completions",
 ConfigFileName = file,
 DisplayName = "Tab 补全建议玩家名",
 Description = "当 Tab 补全结果为空时，是否建议玩家名。\n方便输入玩家名 ",
 Category = "命令",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "commands.time-command-affects-all-worlds",
 ConfigFileName = file,
 DisplayName = "/time 影响所有世界",
 Description = "/time 命令是否同时影响所有世界。\n默认只影响当前世界 [TIME]",
 Category = "命令",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 控制台 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "console.enable-brigadier-completions",
 ConfigFileName = file,
 DisplayName = "Brigadier 补全",
 Description = "是否启用控制台命令的 Brigadier Tab 补全。\n让控制台命令输入更智能 ",
 Category = "控制台",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "console.enable-brigadier-highlighting",
 ConfigFileName = file,
 DisplayName = "Brigadier 高亮",
 Description = "是否启用控制台命令的 Brigadier 语法高亮。\n让命令更易读 ",
 Category = "控制台",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "console.has-all-permissions",
 ConfigFileName = file,
 DisplayName = "控制台拥有所有权限",
 Description = "控制台是否默认拥有所有权限。\n关闭后控制台也需要权限插件管理 ",
 Category = "控制台",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 物品验证 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "item-validation.display-name",
 ConfigFileName = file,
 DisplayName = "显示名最大长度",
 Description = "物品显示名（DisplayName）的最大字符长度。\n防止过长名称导致客户端崩溃 ",
 Category = "物品验证",
 DefaultValue = "8192",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "item-validation.lore-line",
 ConfigFileName = file,
 DisplayName = "Lore 每行最大长度",
 Description = "物品 Lore（描述）每行的最大字符长度 ",
 Category = "物品验证",
 DefaultValue = "8192",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "item-validation.book.author",
 ConfigFileName = file,
 DisplayName = "书作者名最大长度",
 Description = "书本作者名的最大字符长度 ️",
 Category = "物品验证",
 DefaultValue = "8192",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "item-validation.book.title",
 ConfigFileName = file,
 DisplayName = "书标题最大长度",
 Description = "书本标题的最大字符长度 ",
 Category = "物品验证",
 DefaultValue = "8192",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "item-validation.book.page",
 ConfigFileName = file,
 DisplayName = "书每页最大长度",
 Description = "书每页内容的最大字符长度 ",
 Category = "物品验证",
 DefaultValue = "16384",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "item-validation.book-size.page-max",
 ConfigFileName = file,
 DisplayName = "书最大页数",
 Description = "一本书最多有多少页 ",
 Category = "物品验证",
 DefaultValue = "2560",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "item-validation.book-size.total-multiplier",
 ConfigFileName = file,
 DisplayName = "书总大小乘数",
 Description = "书本总大小限制的乘数。\n0.0~1.0，值越小限制越严格 ️",
 Category = "物品验证",
 DefaultValue = "0.98",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "item-validation.resolve-selectors-in-books",
 ConfigFileName = file,
 DisplayName = "书中解析选择器",
 Description = "是否在书本中解析目标选择器（如 @a）。\n可能导致性能问题，建议关闭 ",
 Category = "物品验证",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 // ==================== 杂项 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.fix-entity-position-desync",
 ConfigFileName = file,
 DisplayName = "修复实体位置不同步",
 Description = "是否修复实体位置不同步的问题。\n推荐开启 ",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.load-permissions-yml-before-plugins",
 ConfigFileName = file,
 DisplayName = "插件前加载权限",
 Description = "是否在插件加载前加载 permissions.yml。\n确保权限配置及时生效 ",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.max-joins-per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 最大加入玩家数",
 Description = "单个游戏 tick 内最多允许多少玩家加入服务器。\n防止大量玩家同时加入导致卡顿 ",
 Category = "杂项",
 DefaultValue = "5",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.prevent-negative-villager-demand",
 ConfigFileName = file,
 DisplayName = "防止村民负需求",
 Description = "是否防止村民交易需求变为负数。\n修复村民交易价格异常的问题 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.region-file-cache-size",
 ConfigFileName = file,
 DisplayName = "区域文件缓存大小",
 Description = "区域文件（.mca）的缓存大小。\n更大的缓存可以减少磁盘 IO，但占用更多内存 ",
 Category = "杂项",
 DefaultValue = "256",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.send-full-pos-for-item-entities",
 ConfigFileName = file,
 DisplayName = "掉落物完整坐标",
 Description = "是否为掉落物实体发送完整位置信息。\n减少掉落物位置抖动，但增加网络开销 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.strict-advancement-dimension-check",
 ConfigFileName = file,
 DisplayName = "严格进度维度检查",
 Description = "是否严格检查进度的维度。\n防止玩家在错误的维度解锁进度 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.use-alternative-luck-formula",
 ConfigFileName = file,
 DisplayName = "替代幸运公式",
 Description = "是否使用替代的幸运值计算公式。\n可能影响附魔、钓鱼等的幸运效果 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.use-dimension-type-for-custom-spawners",
 ConfigFileName = file,
 DisplayName = "自定义刷怪笼用维度类型",
 Description = "自定义刷怪笼是否使用维度类型来决定刷怪。\n影响自定义刷怪笼的行为 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.xp-orb-groups-per-area",
 ConfigFileName = file,
 DisplayName = "每区域经验球分组数",
 Description = "每个区域的经验球分组数。default=自动。\n更多分组可以减少经验球卡顿 ",
 Category = "杂项",
 DefaultValue = "default",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.client-interaction-leniency-distance",
 ConfigFileName = file,
 DisplayName = "客户端交互宽容距离",
 Description = "客户端交互的宽容距离。default=自动。\n值越大，玩家可以从越远的距离与方块/实体交互 ",
 Category = "杂项",
 DefaultValue = "default",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.compression-level",
 ConfigFileName = file,
 DisplayName = "网络压缩级别",
 Description = "网络数据包的压缩级别。default=自动，-1~9。\n值越高压缩率越高但 CPU 占用也越高 ",
 Category = "杂项",
 DefaultValue = "default",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.chat-threads.chat-executor-core-size",
 ConfigFileName = file,
 DisplayName = "聊天执行器核心线程数",
 Description = "聊天处理线程池的核心线程数。-1=自动 ",
 Category = "杂项",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.chat-threads.chat-executor-max-size",
 ConfigFileName = file,
 DisplayName = "聊天执行器最大线程数",
 Description = "聊天处理线程池的最大线程数。-1=自动 ",
 Category = "杂项",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 数据包限制器 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "packet-limiter.all-packets.action",
 ConfigFileName = file,
 DisplayName = "超限操作",
 Description = "数据包超过限制时采取的操作。\nKICK=踢出玩家 DROP=丢弃数据包 ",
 Category = "数据包限制器",
 DefaultValue = "KICK",
 AllowedValues = ["KICK", "DROP"],
 ValueType = "enum",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "packet-limiter.all-packets.interval",
 ConfigFileName = file,
 DisplayName = "检测间隔",
 Description = "数据包速率检测的间隔（秒）[TIME]",
 Category = "数据包限制器",
 DefaultValue = "7.0",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "packet-limiter.all-packets.max-packet-rate",
 ConfigFileName = file,
 DisplayName = "最大数据包速率",
 Description = "每秒最大数据包数量 ",
 Category = "数据包限制器",
 DefaultValue = "500.0",
 ValueType = "string",
 RequiresRestart = true,
 });

 // ==================== 玩家自动保存 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "player-auto-save.max-per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 最大保存玩家数",
 Description = "单个游戏 tick 内最多保存多少个玩家数据。-1=无限 ",
 Category = "玩家自动保存",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-auto-save.rate",
 ConfigFileName = file,
 DisplayName = "自动保存间隔",
 Description = "玩家数据自动保存的间隔（tick）。-1=禁用。\n默认 5 分钟保存一次 [TIME]",
 Category = "玩家自动保存",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 垃圾信息限制 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "spam-limiter.incoming-packet-threshold",
 ConfigFileName = file,
 DisplayName = "入站包阈值",
 Description = "入站数据包的阈值，超过则视为垃圾信息 ",
 Category = "垃圾信息限制",
 DefaultValue = "300",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spam-limiter.recipe-spam-increment",
 ConfigFileName = file,
 DisplayName = "合成配方递增量",
 Description = "每次合成配方操作增加的垃圾值 ",
 Category = "垃圾信息限制",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spam-limiter.recipe-spam-limit",
 ConfigFileName = file,
 DisplayName = "合成配方限制",
 Description = "合成配方操作的垃圾信息限制值 ",
 Category = "垃圾信息限制",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spam-limiter.tab-spam-increment",
 ConfigFileName = file,
 DisplayName = "Tab 补全递增量",
 Description = "每次 Tab 补全操作增加的垃圾值 ",
 Category = "垃圾信息限制",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spam-limiter.tab-spam-limit",
 ConfigFileName = file,
 DisplayName = "Tab 补全限制",
 Description = "Tab 补全操作的垃圾信息限制值 ⌨️",
 Category = "垃圾信息限制",
 DefaultValue = "500",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true,
 });

 // ==================== 不支持设置（风险自担）====================

 Register(new ServerConfigDescriptor
 {
 Key = "unsupported-settings.allow-headless-pistons",
 ConfigFileName = file,
 DisplayName = "允许无头活塞",
 Description = "是否允许无头活塞（headless pistons）。\n可能导致漏洞，慎用！️",
 Category = "不支持设置",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "unsupported-settings.allow-permanent-block-break-exploits",
 ConfigFileName = file,
 DisplayName = "允许永久破坏方块",
 Description = "是否允许永久破坏方块的漏洞。\n严重影响游戏平衡，非常不建议开启！",
 Category = "不支持设置",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });
 

    // ===== AUTO-INJECTED by core-fetcher pipeline =====
    Register(new ServerConfigDescriptor
    {
        Key = "_version",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "配置文件版本号",
        Description = "内部版本标识，请勿手动修改。Paper 升级时会自动更新。",
        Category = "Paper 全局配置",
        DefaultValue = "31",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "anticheat.obfuscation.items.all-models.also-obfuscate",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "额外混淆模型",
        Description = "这些模型会被额外混淆（叠加到基础混淆规则上）",
        Category = "Paper 全局配置",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "anticheat.obfuscation.items.all-models.dont-obfuscate",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "跳过混淆模型",
        Description = "这些模型永远不参与混淆（例如绑定追踪器）",
        Category = "Paper 全局配置",
        DefaultValue = "[\"minecraft:lodestone_tracker\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "anticheat.obfuscation.items.all-models.sanitize-count",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "清理计数",
        Description = "是否清理物品的 stack count 信息",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "anticheat.obfuscation.items.enable-item-obfuscation",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "启用物品混淆",
        Description = "对 ItemStack 进行反作弊混淆，防止客户端识别特定物品",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "anticheat.obfuscation.items.model-overrides.minecraft:elytra.also-obfuscate",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "鞘翅额外混淆模型",
        Description = "鞘翅上的模型额外混淆规则",
        Category = "Paper 全局配置",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "anticheat.obfuscation.items.model-overrides.minecraft:elytra.dont-obfuscate",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "鞘翅跳过混淆模型",
        Description = "鞘翅上不参与混淆的模型",
        Category = "Paper 全局配置",
        DefaultValue = "[\"minecraft:damage\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "anticheat.obfuscation.items.model-overrides.minecraft:elytra.sanitize-count",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "鞘翅清理计数",
        Description = "鞘翅是否清理 stack count",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "logging.deobfuscate-stacktraces",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "反混淆堆栈",
        Description = "日志中的异常堆栈是否反混淆，便于定位问题。线上服可保持 true 以保留可读性",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.kick.authentication-servers-down",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "认证服离线踢出消息",
        Description = "Mojang 认证服务器离线时，正版玩家被踢出看到的消息。可用 <lang:> 引用原版语言键",
        Category = "Paper 全局配置",
        DefaultValue = "<lang:multiplayer.disconnect.authservers_down>",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.kick.connection-throttle",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "连接限流踢出消息",
        Description = "玩家连接过快被限流踢出时显示的消息",
        Category = "Paper 全局配置",
        DefaultValue = "Connection throttled! Please wait before reconnecting.",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.kick.flying-player",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "飞行玩家踢出消息",
        Description = "生存模式检测到玩家飞行（无飞行权限）时踢出的提示",
        Category = "Paper 全局配置",
        DefaultValue = "<lang:multiplayer.disconnect.flying>",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.kick.flying-vehicle",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "飞行载具踢出消息",
        Description = "检测到玩家坐在飞行载具上（无权限）时踢出的提示",
        Category = "Paper 全局配置",
        DefaultValue = "<lang:multiplayer.disconnect.flying>",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.no-permission",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "无权限提示",
        Description = "玩家执行无权限命令时显示的提示消息。可用 § 颜色码。建议引导 OP 需求",
        Category = "Paper 全局配置",
        DefaultValue = "<red>I'm sorry, but you do not have permission to perform this command. Please contact the server administrators if you believe that this is in error.",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.use-display-name-in-quit-message",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "退出消息用显示名",
        Description = "玩家退出服务器时，是否用 display name 代替用户名显示在退出消息中",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "misc.enable-nether",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "启用下界",
        Description = "是否允许玩家进入下界维度",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "misc.fix-far-end-terrain-generation",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "修复远处地形生成",
        Description = "修复离玩家较远的 chunk 地形生成缺失问题",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "packet-limiter.kick-message",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "限流踢出消息",
        Description = "因超限被踢出时显示的消息",
        Category = "Paper 全局配置",
        DefaultValue = "<red><lang:disconnect.exceeded_packet_rate>",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "packet-limiter.overrides.minecraft:place_recipe.action",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "配方放置限流动作",
        Description = "place_recipe 包单独限流阈值触发后的动作",
        Category = "Paper 全局配置",
        DefaultValue = "DROP",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "packet-limiter.overrides.minecraft:place_recipe.interval",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "配方放置检测间隔",
        Description = "place_recipe 限流时间窗口",
        Category = "Paper 全局配置",
        DefaultValue = "4.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "packet-limiter.overrides.minecraft:place_recipe.max-packet-rate",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "配方放置最大包速率",
        Description = "place_recipe 每秒最大包速率",
        Category = "Paper 全局配置",
        DefaultValue = "5.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "proxies.bungee-cord.online-mode",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "BC 代理正版验证",
        Description = "BungeeCord 反代下是否保持正版验证（需配置 forwarding secret）",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "proxies.proxy-protocol",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "启用代理协议",
        Description = "是否支持 HAProxy PROXY protocol v1/v2",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "scoreboards.save-empty-scoreboard-teams",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "保存空队伍计分板",
        Description = "是否保存没有成员的计分板队伍",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "scoreboards.track-plugin-scoreboards",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "追踪插件计分板",
        Description = "是否追踪插件创建的计分板以优化",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "spark.enable-immediately",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "立即启用 spark",
        Description = "服务器启动后是否立即初始化 spark profiler",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "spark.enabled",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "启用 spark",
        Description = "是否启用 spark profiler（性能分析）",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "unsupported-settings.allow-piston-duplication",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "允许活塞复制",
        Description = "是否允许活塞复制物品的旧 exploit",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "unsupported-settings.allow-unsafe-end-portal-teleportation",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "允许不安全末地传送",
        Description = "是否允许玩家用安全协议不支持的方式进入末地",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "unsupported-settings.compression-format",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "压缩格式",
        Description = "网络数据包压缩算法。ZLIB = 原版",
        Category = "Paper 全局配置",
        DefaultValue = "ZLIB",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "unsupported-settings.oversized-item-component-sanitizer.dont-sanitize",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "跳过清理的组件",
        Description = "这些 item component 即使超过大小限制也不清理",
        Category = "Paper 全局配置",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "unsupported-settings.perform-username-validation",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "用户名验证",
        Description = "是否对玩家名做严格字符验证",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "unsupported-settings.skip-tripwire-hook-placement-validation",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "跳过绊线放置验证",
        Description = "是否跳过绊线钩放置的合法性检查",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "unsupported-settings.skip-vanilla-damage-tick-when-shield-blocked",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "跳过盾牌格挡伤害",
        Description = "原版在盾牌格挡时跳过的 damage tick 是否也跳过",
        Category = "Paper 全局配置",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "unsupported-settings.update-equipment-on-player-actions",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "玩家行为时更新装备",
        Description = "玩家进行某些操作（潜行、吃东西）时是否强制同步装备到客户端",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "update-checker.enabled",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "启用更新检查",
        Description = "Paper 启动时是否检查新版本",
        Category = "Paper 全局配置",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "watchdog.early-warning-delay",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "Watchdog 预警延迟",
        Description = "主线程超过此毫秒触发 watchdog 预警日志",
        Category = "Paper 全局配置",
        DefaultValue = "10000",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "watchdog.early-warning-every",
        ConfigFileName = "config/paper-global.yml",
        DisplayName = "Watchdog 预警间隔",
        Description = "预警日志的最小输出间隔（毫秒），避免刷屏",
        Category = "Paper 全局配置",
        DefaultValue = "5000",
        ValueType = "int",
        RequiresRestart = false,
    });
    // ===== END AUTO-INJECTED =====
}

 /// <summary>
 /// 注册 config/paper-world-defaults.yml 配置文件的关键配置项
 /// </summary>
 /// <remarks>
 /// Paper/Folia 的世界默认配置文件，作为各世界个性化配置的模板。
 /// 包含实体、世界生成、杂项等世界级配置项。
 /// 数据来源：PaperMC 官方文档 + Folia 26.1.2 默认配置
 /// </remarks>
 private void RegisterPaperWorldDefaultsYml()
 {
 const string file = "config/paper-world-defaults.yml";

 // ==================== 方块与物理 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "anti-xray",
 ConfigFileName = file,
 DisplayName = "反矿透",
 Description = "反 X 射线（矿透）设置。\n通过隐藏或伪装矿石防止玩家使用透视作弊 ️",
 Category = "方块与物理",
 ValueType = "string",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "water-over-lava",
 ConfigFileName = file,
 DisplayName = "水浇岩浆",
 Description = "水流到岩浆上时的行为设置。\n控制水与岩浆交互生成石头/黑曜石的行为 ",
 Category = "方块与物理",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-ice-and-snow",
 ConfigFileName = file,
 DisplayName = "禁用冰和雪",
 Description = "是否禁用冰和雪的形成与融化。\n开启后冰雪不会自然形成或融化 ️",
 Category = "方块与物理",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-thunder",
 ConfigFileName = file,
 DisplayName = "禁用雷暴",
 Description = "是否禁用雷暴天气。\n开启后不会打雷闪电 ",
 Category = "方块与物理",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-raining",
 ConfigFileName = file,
 DisplayName = "禁用下雨",
 Description = "是否禁用下雨天气。\n开启后永远是晴天 ️",
 Category = "方块与物理",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "snow-accumulation-height",
 ConfigFileName = file,
 DisplayName = "积雪堆积高度",
 Description = "雪自然堆积的最大高度（层）。\n0=无限制 ️",
 Category = "方块与物理",
 DefaultValue = "8",
 MinValue = 0,
 MaxValue = 256,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "grass-spread",
 ConfigFileName = file,
 DisplayName = "草方块蔓延",
 Description = "草方块的蔓延速度调整。\n控制草方块向泥土蔓延的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mycelium-spread",
 ConfigFileName = file,
 DisplayName = "菌丝蔓延",
 Description = "菌丝方块的蔓延速度调整。\n控制菌丝向泥土蔓延的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "vine-growth",
 ConfigFileName = file,
 DisplayName = "藤蔓生长",
 Description = "藤蔓的生长速度调整。\n控制藤蔓蔓延和生长的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "cocoa-growth",
 ConfigFileName = file,
 DisplayName = "可可豆生长",
 Description = "可可豆的生长速度调整。\n控制可可豆成熟的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "bamboo-growth",
 ConfigFileName = file,
 DisplayName = "竹子生长",
 Description = "竹子的生长速度调整。\n控制竹子长高的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "kelp-growth",
 ConfigFileName = file,
 DisplayName = "海带生长",
 Description = "海带的生长速度调整。\n控制海带生长的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "sugar-cane-growth",
 ConfigFileName = file,
 DisplayName = "甘蔗生长",
 Description = "甘蔗的生长速度调整。\n控制甘蔗长高的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "cactus-growth",
 ConfigFileName = file,
 DisplayName = "仙人掌生长",
 Description = "仙人掌的生长速度调整。\n控制仙人掌长高的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "pumpkin-and-melon-growth",
 ConfigFileName = file,
 DisplayName = "南瓜和西瓜生长",
 Description = "南瓜和西瓜的生长速度调整。\n控制南瓜和西瓜结果的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mushroom-growth",
 ConfigFileName = file,
 DisplayName = "蘑菇生长",
 Description = "蘑菇的生长速度调整。\n控制蘑菇蔓延和巨型蘑菇生成的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "leaf-decay",
 ConfigFileName = file,
 DisplayName = "树叶腐烂",
 Description = "树叶的腐烂速度调整。\n控制树木被砍伐后树叶消失的速率 ",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "ice-and-snow",
 ConfigFileName = file,
 DisplayName = "冰和雪",
 Description = "冰和雪的形成/融化速度调整。\n控制冰雪的自然变化速率 ️",
 Category = "方块与物理",
 DefaultValue = "default",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "thunder",
 ConfigFileName = file,
 DisplayName = "雷暴",
 Description = "雷暴天气相关设置。\n控制打雷闪电的频率和行为 ",
 Category = "方块与物理",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "rain",
 ConfigFileName = file,
 DisplayName = "降雨",
 Description = "降雨天气相关设置。\n控制下雨的频率和行为 ️",
 Category = "方块与物理",
 ValueType = "string",
 });

 // ==================== 实体 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "armor-stands",
 ConfigFileName = file,
 DisplayName = "盔甲架",
 Description = "盔甲架相关设置。\n控制盔甲架的行为和优化 ️",
 Category = "实体",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "painting",
 ConfigFileName = file,
 DisplayName = "画",
 Description = "画相关设置。\n控制画的放置和行为 ️",
 Category = "实体",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "sitting",
 ConfigFileName = file,
 DisplayName = "坐",
 Description = "玩家坐的相关设置。\n控制玩家坐下的行为 ",
 Category = "实体",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "zombie-pigmen-portal-spawn",
 ConfigFileName = file,
 DisplayName = "猪灵传送门生成",
 Description = "猪灵（僵尸猪人）从下界传送门生成的设置。\n控制猪灵生成的概率和数量 ",
 Category = "实体",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "wandering-trader",
 ConfigFileName = file,
 DisplayName = "流浪商人",
 Description = "流浪商人的生成设置。\n控制流浪商人出现的频率和条件 ",
 Category = "实体",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawner-nerfed-mobs-should-jump",
 ConfigFileName = file,
 DisplayName = "刷怪笼削弱怪物跳跃",
 Description = "被削弱的刷怪笼怪物是否还能跳跃。\n开启后削弱的怪物仍可跳跃 ",
 Category = "实体",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "per-player-mob-spawns",
 ConfigFileName = file,
 DisplayName = "每玩家怪物生成",
 Description = "是否按玩家单独计算怪物生成上限。\n开启后多玩家不会共享怪物上限 ",
 Category = "实体",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fix-items-merging-through-walls",
 ConfigFileName = file,
 DisplayName = "修复穿墙物品合并",
 Description = "是否修复掉落物穿墙合并的问题。\n开启后物品不会穿过墙壁合并 ",
 Category = "实体",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-chest-cat-detection",
 ConfigFileName = file,
 DisplayName = "禁用箱子猫检测",
 Description = "是否禁用箱子上的猫检测。\n开启后猫不会阻止打开箱子，性能更好 ",
 Category = "实体",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-end-credits",
 ConfigFileName = file,
 DisplayName = "禁用终末之诗",
 Description = "是否禁用击败末影龙后的终末之诗和字幕。\n开启后击败末影龙直接重生 ",
 Category = "实体",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-relative-projectile-velocity",
 ConfigFileName = file,
 DisplayName = "禁用相对弹射物速度",
 Description = "是否禁用弹射物的相对速度计算。\n修复某些弹射物速度异常的问题 ",
 Category = "实体",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-sprint-interruption-on-attack",
 ConfigFileName = file,
 DisplayName = "禁用攻击打断冲刺",
 Description = "是否禁用攻击时打断玩家冲刺。\n开启后攻击不会打断冲刺 ",
 Category = "实体",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "shield-blocking-delay",
 ConfigFileName = file,
 DisplayName = "盾牌格挡延迟",
 Description = "举盾后到能格挡的延迟时间（tick）。\n值越大举盾后需要等越久才能格挡 ️",
 Category = "实体",
 DefaultValue = "5",
 MinValue = 0,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "only-players-collide",
 ConfigFileName = file,
 DisplayName = "仅玩家碰撞",
 Description = "是否只有玩家之间会发生碰撞。\n开启后玩家不会与其他实体碰撞 ",
 Category = "实体",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "max-leash-distance",
 ConfigFileName = file,
 DisplayName = "最大牵引距离",
 Description = "拴绳的最大距离（方块）。\n超过此距离拴绳会断裂 ",
 Category = "实体",
 DefaultValue = "10.0",
 MinValue = 1,
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "projectile-burden",
 ConfigFileName = file,
 DisplayName = "弹射物负担",
 Description = "弹射物的性能负担设置。\n控制弹射物数量上限以优化性能 ",
 Category = "实体",
 ValueType = "string",
 });

 // ==================== 世界生成 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "disable-vanilla-api-ticking",
 ConfigFileName = file,
 DisplayName = "禁用原版 API Tick",
 Description = "是否禁用原版 API 的 tick 事件。\n可能提升性能但影响某些插件 ️",
 Category = "世界生成",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "generate-random-seeds-for-all",
 ConfigFileName = file,
 DisplayName = "为所有结构生成随机种子",
 Description = "是否为所有结构生成随机种子。\n开启后每个世界的结构位置更随机 ",
 Category = "世界生成",
 DefaultValue = "false",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "seed-based-feature-search",
 ConfigFileName = file,
 DisplayName = "基于种子的地物搜索",
 Description = "是否启用基于种子的地物搜索优化。\n加速 locate 等命令的搜索速度 ",
 Category = "世界生成",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "seed-based-feature-search-loads-chunks",
 ConfigFileName = file,
 DisplayName = "地物搜索加载区块",
 Description = "基于种子的地物搜索是否加载区块。\n关闭可减少搜索时的区块加载 ",
 Category = "世界生成",
 DefaultValue = "true",
 ValueType = "bool",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimize-explosions",
 ConfigFileName = file,
 DisplayName = "优化爆炸",
 Description = "是否优化爆炸的计算。\n开启后爆炸性能更好，行为略有不同 ",
 Category = "世界生成",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimize-hoppers",
 ConfigFileName = file,
 DisplayName = "优化漏斗",
 Description = "是否优化漏斗的行为。\n开启后漏斗性能更好 ",
 Category = "世界生成",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "hopper-can-load-chunks",
 ConfigFileName = file,
 DisplayName = "漏斗可加载区块",
 Description = "漏斗是否能够加载区块。\n关闭可防止漏斗跨区块加载导致的性能问题 ",
 Category = "世界生成",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-non-player-entities-on-scoreboards",
 ConfigFileName = file,
 DisplayName = "允许非玩家实体在计分板",
 Description = "是否允许非玩家实体出现在计分板上。\n关闭可提升计分板性能 ",
 Category = "世界生成",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "display-connection-messages-on-first-join",
 ConfigFileName = file,
 DisplayName = "首次加入显示连接消息",
 Description = "是否仅在玩家首次加入时显示连接消息。\n减少刷屏 ",
 Category = "世界生成",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "do-not-tick-entities-in-unloaded-chunks",
 ConfigFileName = file,
 DisplayName = "不处理未加载区块实体",
 Description = "是否不对未加载区块中的实体进行 tick 处理。\n防止实体在未加载区块中异常 ",
 Category = "世界生成",
 DefaultValue = "false",
 ValueType = "bool",
 });

 // ==================== 杂项 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "fire-tick-delay",
 ConfigFileName = file,
 DisplayName = "火焰 Tick 延迟",
 Description = "火焰传播的 tick 延迟。\n值越大火焰蔓延越慢 ",
 Category = "杂项",
 DefaultValue = "30",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "light-queue-size",
 ConfigFileName = file,
 DisplayName = "光照队列大小",
 Description = "光照更新队列的最大大小。\n过大可能导致卡顿，过小可能导致光照不同步 ",
 Category = "杂项",
 DefaultValue = "10000",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "auto-save-interval",
 ConfigFileName = file,
 DisplayName = "自动保存间隔",
 Description = "世界自动保存的间隔（tick）。\n默认 5 分钟（6000 tick）[TIME]",
 Category = "杂项",
 DefaultValue = "6000",
 MinValue = 100,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fix-curing-zombie-villager-discount-exploit",
 ConfigFileName = file,
 DisplayName = "修复村民交易折扣漏洞",
 Description = "是否修复多次治愈僵尸村民导致交易折扣叠加的漏洞。\n防止玩家刷低价交易 ",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs-can-always-use-spawn-egg",
 ConfigFileName = file,
 DisplayName = "生物总能用刷怪蛋",
 Description = "怪物是否总能使用刷怪蛋生成。\n默认受刷怪限制影响 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-leashing-villagers",
 ConfigFileName = file,
 DisplayName = "允许拴住村民",
 Description = "是否允许用拴绳拴住村民。\n方便搬运村民 ‍",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-chunks-size",
 ConfigFileName = file,
 DisplayName = "出生点区块大小",
 Description = "出生点区块的大小（区块）。\n出生点区块会常驻加载 ",
 Category = "杂项",
 DefaultValue = "3",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true,
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-chunks-tick",
 ConfigFileName = file,
 DisplayName = "出生点区块 Tick",
 Description = "出生点区块是否进行 tick 处理。\n关闭可节省出生点区块的性能消耗 [TIME]",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "max-auto-save-chunks-per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 最大自动保存区块数",
 Description = "单个 tick 内最多自动保存多少个区块。\n限制保存速度防止卡顿 ",
 Category = "杂项",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "falling-block-height-nerf",
 ConfigFileName = file,
 DisplayName = "下落方块高度削弱",
 Description = "超过此高度的下落方块会被直接删除。\n0=禁用，防止大量下落方块导致卡顿 ",
 Category = "杂项",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "tnt-entity-height-nerf",
 ConfigFileName = file,
 DisplayName = "TNT 实体高度削弱",
 Description = "超过此高度的 TNT 实体会被直接删除。\n0=禁用，防止高空 TNT 导致卡顿 ",
 Category = "杂项",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "water-over-lava-flow-speed",
 ConfigFileName = file,
 DisplayName = "水在岩浆上流速",
 Description = "水在岩浆上方流动的速度倍率。\n影响水浇岩浆生成石头的速度 ",
 Category = "杂项",
 DefaultValue = "2",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "grass-spread-tick-rate",
 ConfigFileName = file,
 DisplayName = "草蔓延 Tick 速率",
 Description = "草方块蔓延的 tick 速率。\n值越大蔓延越慢 ",
 Category = "杂项",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "bed-search-radius",
 ConfigFileName = file,
 DisplayName = "床位搜索半径",
 Description = "玩家重生时搜索床位的半径（方块）。\n值越大找床范围越大 ️",
 Category = "杂项",
 DefaultValue = "1",
 MinValue = 1,
 MaxValue = 10,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-explosion-knockback",
 ConfigFileName = file,
 DisplayName = "禁用爆炸击退",
 Description = "是否禁用爆炸的击退效果。\n开启后爆炸不会击退实体 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "water-lava-flow-speed",
 ConfigFileName = file,
 DisplayName = "水岩浆流速",
 Description = "水和岩浆的流动速度设置。\n控制液体流动的快慢 ",
 Category = "杂项",
 ValueType = "string",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fixed-chunk-inhabited-time",
 ConfigFileName = file,
 DisplayName = "固定区块居住时间",
 Description = "是否使用固定的区块居住时间。\n影响区块的游戏机制难度 [TIME]",
 Category = "杂项",
 DefaultValue = "-1",
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "use-vanilla-world-scoreboard-name-coloring",
 ConfigFileName = file,
 DisplayName = "使用原版计分板名称着色",
 Description = "是否使用原版计分板的名称着色方式。\n关闭可支持更多颜色格式 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "remove-corrupt-tile-entities",
 ConfigFileName = file,
 DisplayName = "移除损坏的方块实体",
 Description = "是否自动移除损坏的方块实体（如箱子、刷怪笼等）。\n防止损坏数据导致崩溃 ️",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "experience-merge-max-value",
 ConfigFileName = file,
 DisplayName = "经验合并最大值",
 Description = "经验球合并后的最大经验值。\n防止单个经验球经验过高导致不平衡 ",
 Category = "杂项",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "prevent-moving-into-unloaded-chunks",
 ConfigFileName = file,
 DisplayName = "防止移入未加载区块",
 Description = "是否阻止玩家移动到未加载的区块中。\n防止玩家卡入未加载区域 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "ender-dragons-death-always-places-dragon-egg",
 ConfigFileName = file,
 DisplayName = "末影龙死亡总是生成龙蛋",
 Description = "每次击败末影龙是否都生成龙蛋。\n默认只有第一次会生成 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "use-faster-eigencraft-redstone",
 ConfigFileName = file,
 DisplayName = "使用快速红石算法",
 Description = "是否使用更快的 Eigencraft 红石算法。\n大幅提升红石性能，可能有细微行为差异 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "map-item-frame-cursor-limit",
 ConfigFileName = file,
 DisplayName = "地图物品展示框光标限制",
 Description = "每个地图上物品展示框光标的最大数量。\n过多光标可能导致性能问题 ️",
 Category = "杂项",
 DefaultValue = "128",
 MinValue = 0,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-permanent-block-break-exploits",
 ConfigFileName = file,
 DisplayName = "允许永久破坏方块漏洞",
 Description = "是否允许永久破坏方块的漏洞。\n严重影响游戏平衡，非常不建议开启！",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-headless-pistons",
 ConfigFileName = file,
 DisplayName = "允许无头活塞",
 Description = "是否允许无头活塞（headless pistons）。\n可能导致漏洞，慎用！️",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-piston-duplication",
 ConfigFileName = file,
 DisplayName = "允许活塞复制",
 Description = "是否允许活塞复制物品的漏洞。\n严重破坏游戏平衡，非常不建议开启！",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "perform-username-validation",
 ConfigFileName = file,
 DisplayName = "执行用户名验证",
 Description = "是否验证用户名的合法性。\n防止使用非法字符的用户名 ",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "validate-function-tags-before-applying",
 ConfigFileName = file,
 DisplayName = "应用函数标签前验证",
 Description = "是否在应用函数标签前进行验证。\n防止无效函数标签导致错误 ",
 Category = "杂项",
 DefaultValue = "true",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "entities-target-with-follow-range",
 ConfigFileName = file,
 DisplayName = "实体使用跟随范围寻敌",
 Description = "实体是否使用跟随范围来寻找目标。\n可能减少实体寻敌的性能消耗 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mob-spawner-tick-rate",
 ConfigFileName = file,
 DisplayName = "刷怪笼 Tick 速率",
 Description = "刷怪笼的 tick 处理速率。\n值越大刷怪笼工作越慢 ",
 Category = "杂项",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-tasks-per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 区块任务数",
 Description = "单个 tick 内最多执行多少个区块任务。\n限制区块处理速度防止卡顿 ",
 Category = "杂项",
 DefaultValue = "1000",
 MinValue = 1,
 ValueType = "int",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-end-portal-creation",
 ConfigFileName = file,
 DisplayName = "禁用末地传送门创建",
 Description = "是否禁用末地传送门的创建。\n开启后无法激活末地传送门 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disable-wither-spawning",
 ConfigFileName = file,
 DisplayName = "禁用凋灵生成",
 Description = "是否禁用凋灵的生成。\n开启后无法召唤凋灵 ",
 Category = "杂项",
 DefaultValue = "false",
 ValueType = "bool",
 });
 }

 // ============================================================
 // 第一批：Paper 系派生核心专属配置
 // ============================================================

 /// <summary>
 /// 注册 Purpur 专属配置文件 purpur.yml 的配置描述符
 /// </summary>
 private void RegisterPurpurYml()
 {
 const string file = "purpur.yml";

 // ==================== settings 全局设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.use-alternate-keepalive",
 ConfigFileName = file,
 DisplayName = "备用心跳检测",
 Description = "启用 Purpur 的备用保持连接系统\n网络较差的玩家不会经常超时\n️ 已知与 TCPShield 不兼容",
 Category = "网络",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.timeout-time",
 ConfigFileName = file,
 DisplayName = "超时时间",
 Description = "服务器无响应多久后判定玩家断线（秒）\n0 = 关闭超时检测\n调大可改善网络差玩家的体验",
 Category = "网络",
 DefaultValue = "60",
 MinValue = 0,
 ValueType = "int"
 });

 // ==================== gameplay-mechanics 游戏机制 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-mechanics.player.idle-timeout",
 ConfigFileName = file,
 DisplayName = "玩家挂机超时",
 Description = "玩家多久不动自动踢出服务器（分钟）\n0 = 永不踢出挂机玩家",
 Category = "游戏机制",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-mechanics.player.allow-teleportation-during-levitation",
 ConfigFileName = file,
 DisplayName = "漂浮时允许传送",
 Description = "玩家处于漂浮状态时是否允许传送",
 Category = "游戏机制",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-mechanics.player.disable-sneak-sprint",
 ConfigFileName = file,
 DisplayName = "禁用潜行冲刺",
 Description = "禁用潜行时冲刺功能",
 Category = "游戏机制",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-mechanics.entity.villager.spawn-egg-breeding",
 ConfigFileName = file,
 DisplayName = "村民刷怪蛋繁殖",
 Description = "用村民刷怪蛋右键两只村民时是否让它们繁殖",
 Category = "实体",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-mechanics.entity.villager.toboldlygolembaby",
 ConfigFileName = file,
 DisplayName = "幼年铁傀儡",
 Description = "幼年村民是否可能变成幼年铁傀儡",
 Category = "实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== blocks 方块配置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "blocks.anvil.allow-unsafe-enchants",
 ConfigFileName = file,
 DisplayName = "允许不安全附魔",
 Description = "铁砧是否允许超越游戏附魔上限\n️ 可能破坏游戏平衡",
 Category = "方块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "blocks.barrel.allow-void-void-barrel",
 ConfigFileName = file,
 DisplayName = "木桶虚空存储",
 Description = "木桶是否可作为虚空存储（永远装不满）\n实验性功能，谨慎使用",
 Category = "方块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "blocks.beacon.allow-effects-outside-world-border",
 ConfigFileName = file,
 DisplayName = "信标越界生效",
 Description = "信标效果是否在世界边界外生效",
 Category = "方块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "blocks.dispenser.disable-shulker-box-fill",
 ConfigFileName = file,
 DisplayName = "禁用发射器填装潜影盒",
 Description = "禁用发射器自动填装潜影盒的功能",
 Category = "方块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== entities 实体通用配置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "entities.armorstand.tick-when-no-tick",
 ConfigFileName = file,
 DisplayName = "盔甲架无 tick 时仍运行",
 Description = "盔甲架在无 tick 区域是否仍处理逻辑",
 Category = "实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "entities.armorstand.place-with-name",
 ConfigFileName = file,
 DisplayName = "放置带名称的盔甲架",
 Description = "玩家放置盔甲架时是否将手持盔甲架的自定义名称应用上去",
 Category = "实体",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "entities.mobs.disable-zombie-reinforcement",
 ConfigFileName = file,
 DisplayName = "禁用僵尸增援",
 Description = "禁用僵尸受攻击时召唤同伴增援的机制\n可提升性能，但改变游戏难度",
 Category = "实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== mobs 重要生物特有配置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.creeper.charged-chance",
 ConfigFileName = file,
 DisplayName = "闪电苦力怕生成概率",
 Description = "苦力怕被闪电击中后变成闪电苦力怕的概率\n0.0 = 永不 / 1.0 = 必定",
 Category = "生物",
 DefaultValue = "1.0",
 ValueType = "double"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.ender_dragon.can-die",
 ConfigFileName = file,
 DisplayName = "末影龙可死亡",
 Description = "末影龙是否可被正常击杀\nfalse 时末影龙无敌（仅生电服使用）",
 Category = "生物",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.enderman.allow-griefing",
 ConfigFileName = file,
 DisplayName = "末影人允许破坏方块",
 Description = "末影人是否可拾取 / 放置方块\n关闭可减少区块修改，但改变游戏玩法",
 Category = "生物",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.iron_golem.can-build",
 ConfigFileName = file,
 DisplayName = "可建造铁傀儡",
 Description = "玩家是否可用南瓜 + 铁块摆放建造铁傀儡",
 Category = "生物",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.snow_golem.can-build",
 ConfigFileName = file,
 DisplayName = "可建造雪傀儡",
 Description = "玩家是否可用南瓜 + 雪块摆放建造雪傀儡",
 Category = "生物",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.villager.breeding.enabled",
 ConfigFileName = file,
 DisplayName = "村民繁殖开关",
 Description = "村民是否可繁殖\n关闭后门禁机制失效",
 Category = "生物",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.villager.brain.tick-when-no-tick",
 ConfigFileName = file,
 DisplayName = "村民大脑无 tick 时仍运行",
 Description = "村民在无 tick 区域是否仍处理大脑逻辑",
 Category = "生物",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.wither.can-die",
 ConfigFileName = file,
 DisplayName = "凋灵可死亡",
 Description = "凋灵是否可被正常击杀\nfalse 时凋灵无敌",
 Category = "生物",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mobs.zombie.aggressive-towards-villager",
 ConfigFileName = file,
 DisplayName = "僵尸主动攻击村民",
 Description = "僵尸是否主动寻找并攻击村民\n关闭可保护村民但改变游戏难度",
 Category = "生物",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== world 世界设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world.orb-spawn-radius",
 ConfigFileName = file,
 DisplayName = "经验球生成半径",
 Description = "击杀实体时经验球在实体周围的生成半径（方块）",
 Category = "世界",
 DefaultValue = "8",
 MinValue = 0,
 ValueType = "int"
 });
 }

 /// <summary>
 /// 注册 Pufferfish 专属配置文件 pufferfish.yml 的配置描述符
 /// </summary>
 private void RegisterPufferfishYml()
 {
 const string file = "pufferfish.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "info.version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "Pufferfish 配置文件内部版本号\n由程序自动维护，请勿手动修改",
 Category = "信息",
 DefaultValue = "1.0",
 ValueType = "string"
 });

 // ==================== 书籍设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "enable-books",
 ConfigFileName = file,
 DisplayName = "允许写入书本",
 Description = "是否允许玩家在成书上继续写入内容\n关闭可防止复制漏洞（duping）",
 Category = "书籍",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== 性能优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "tps-catchup",
 ConfigFileName = file,
 DisplayName = "卡顿后补帧追赶",
 Description = "服务器卡顿后是否加速运行以维持 20 TPS\n副作用：卡顿后生物可能短暂瞬移",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-suffocation-optimization",
 ConfigFileName = file,
 DisplayName = "窒息检测优化",
 Description = "通过有选择地跳过窒息检测来优化性能\n跳过方式在玩家视角下几乎察觉不到",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-async-mob-spawning",
 ConfigFileName = file,
 DisplayName = "异步生物生成",
 Description = "将生物生成计算转移到异步线程（非真正生成）\n实体较多的服务器可提升约 15% 性能\n前置条件：必须开启 per-player-mob-spawns\n️ 仅启动时读取，必须重启",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "inactive-goal-selector-throttle",
 ConfigFileName = file,
 DisplayName = "节流非激活实体 AI",
 Description = "实体处于非激活 tick 时节流其 AI 目标选择器\n带来百分之几的性能提升",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== 弹射物优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "projectile.max-loads-per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 弹射物加载区块上限",
 Description = "每个游戏 tick 内所有弹射物合计允许同步加载多少个区块\n降低此值可缓解弹射物密集时的卡顿",
 Category = "弹射物",
 DefaultValue = "10",
 MinValue = 0,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "projectile.max-loads-per-projectile",
 ConfigFileName = file,
 DisplayName = "单个弹射物加载区块上限",
 Description = "单个弹射物生命周期内最多能加载多少个区块，超过即被移除\n防止恶意玩家用投射物拖垮服务器",
 Category = "弹射物",
 DefaultValue = "10",
 MinValue = 0,
 ValueType = "int"
 });

 // ==================== DEAR 实体 AI 优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "dab.enabled",
 ConfigFileName = file,
 DisplayName = "启用 DEAR 实体大脑优化",
 Description = "动态大脑激活：远离玩家的实体降低 AI tick 频率\n大幅降低 CPU 占用",
 Category = "DEAR 实体 AI",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "dab.start-distance",
 ConfigFileName = file,
 DisplayName = "DEAR 生效起始距离",
 Description = "实体距玩家多远时开始受 DEAR 影响\n距离小于此值的实体保持原版全速 tick",
 Category = "DEAR 实体 AI",
 DefaultValue = "12",
 MinValue = 0,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "dab.max-tick-freq",
 ConfigFileName = file,
 DisplayName = "最远实体最大 tick 间隔",
 Description = "距离最远的实体多久 tick 一次 AI\n值越大越省 CPU，但远处实体行为越迟钝\n20 = 1 秒",
 Category = "DEAR 实体 AI",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "dab.activation-dist-mod",
 ConfigFileName = file,
 DisplayName = "距离对频率的影响系数",
 Description = "距离对 tick 频率的影响强度\n公式：频率 = (到玩家距离^2) / (2^本值)\n7 = 更省 CPU / 9 = 更接近原版",
 Category = "DEAR 实体 AI",
 DefaultValue = "8",
 ValueType = "int"
 });

 // ==================== 末地设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "allow-end-crystal-respawn",
 ConfigFileName = file,
 DisplayName = "允许末影水晶复活末影龙",
 Description = "是否允许末影水晶复活末影龙\nPvP 末地服务器关闭可避免昂贵复活搜索",
 Category = "末地",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== Flare 性能分析器 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "flare.enabled",
 ConfigFileName = file,
 DisplayName = "启用 Flare",
 Description = "启用 Pufferfish 内置的零开销性能分析器\n配合在线服务生成可视化火焰图",
 Category = "Flare",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "flare.resource-token",
 ConfigFileName = file,
 DisplayName = "Flare 资源令牌",
 Description = "Flare 在线服务的访问令牌\n可在 Pufferfish 官网获取",
 Category = "Flare",
 DefaultValue = "",
 ValueType = "string"
 });

 // ==================== Sentry 错误监控 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "sentry.dsn",
 ConfigFileName = file,
 DisplayName = "Sentry DSN",
 Description = "Sentry 错误追踪平台的 Data Source Name\n留空则禁用 Sentry 上报",
 Category = "Sentry",
 DefaultValue = "",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "sentry.only-log-thrown",
 ConfigFileName = file,
 DisplayName = "仅上报抛出异常",
 Description = "是否仅上报实际抛出的异常\n过滤掉纯日志记录",
 Category = "Sentry",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });
 }

 /// <summary>
 /// 注册 Leaves 专属配置文件 leaves.yml 的配置描述符
 /// </summary>
 private void RegisterLeavesYml()
 {
 const string file = "leaves.yml";

 // ==================== settings 设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.bstats-usage",
 ConfigFileName = file,
 DisplayName = "bStats 统计上报",
 Description = "是否向 bStats 上报服务器匿名统计信息\n帮助 Leaves 团队了解使用情况",
 Category = "设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== protocol 协议支持 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "protocol.jade-protocol",
 ConfigFileName = file,
 DisplayName = "Jade 协议支持",
 Description = "Jade 客户端 Mod 的服务器端协议\n玩家无需 Jade 服务端插件即可看到方块/实体信息",
 Category = "协议",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "protocol.appleskin-protocol",
 ConfigFileName = file,
 DisplayName = "AppleSkin 协议支持",
 Description = "AppleSkin 客户端 Mod 的服务器端协议\n显示饥饿值、饱和度、消耗度",
 Category = "协议",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "protocol.xaero-map-protocol",
 ConfigFileName = file,
 DisplayName = "Xaero 地图协议支持",
 Description = "Xaero's Minimap / World Map 客户端 Mod 的服务器端协议\n向其发送世界边界等数据",
 Category = "协议",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "protocol.syncmatica-protocol",
 ConfigFileName = file,
 DisplayName = "Syncmatica 协议支持",
 Description = "Syncmatica 客户端 Mod 的服务器端协议\n允许客户端在服务器世界共享 schematica 模式",
 Category = "协议",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== gameplay 玩法 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay.allowed-redstone-not-get-flushed",
 ConfigFileName = file,
 DisplayName = "红石不被刷新",
 Description = "保护红石信号在区块卸载时不被刷新\n生电玩家重要功能",
 Category = "玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay.allow-redstone-with-explosion",
 ConfigFileName = file,
 DisplayName = "爆炸不影响红石",
 Description = "爆炸是否破坏红石元件\n关闭可保护红石装置",
 Category = "玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay.allow-player-get-damage-if-invincible",
 ConfigFileName = file,
 DisplayName = "无敌玩家受伤",
 Description = "处于无敌状态的玩家是否仍显示受伤动画\n不影响实际伤害",
 Category = "玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay.sand-duplication",
 ConfigFileName = file,
 DisplayName = "允许刷沙",
 Description = "是否恢复原版沙子 / 沙砾 duplication bug\n生电玩家常用\n️ 会破坏经济平衡",
 Category = "玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay.tnt-duplication",
 ConfigFileName = file,
 DisplayName = "允许 TNT 复制",
 Description = "是否恢复原版 TNT duplication bug\n生电玩家常用\n️ 易被滥用",
 Category = "玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay.allow-end-portal-destroy",
 ConfigFileName = file,
 DisplayName = "允许破坏末地传送门",
 Description = "是否允许玩家破坏末地传送门方块\n原版无法破坏",
 Category = "玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== performance 性能 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "performance.remove-null-armorstand-tick",
 ConfigFileName = file,
 DisplayName = "移除空盔甲架 tick",
 Description = "移除没有装备的盔甲架的 tick\n减少 CPU 占用",
 Category = "性能",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance.optimized-entity-teleport",
 ConfigFileName = file,
 DisplayName = "优化实体传送",
 Description = "优化实体传送时的处理逻辑\n减少传送卡顿",
 Category = "性能",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== misc 杂项 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.disable-packet-limit-check",
 ConfigFileName = file,
 DisplayName = "禁用数据包限制检查",
 Description = "禁用对玩家发送数据包频率的限制\n️ 可能让反作弊失效，谨慎关闭",
 Category = "杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.spigot-bramding",
 ConfigFileName = file,
 DisplayName = "Spigot 品牌替换",
 Description = "将服务器品牌从 Spigot 替换为 Leaves\n仅影响 F3 显示",
 Category = "杂项",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.including-5s-in-get-tps",
 ConfigFileName = file,
 DisplayName = "TPS 包含 5 秒数据",
 Description = "计算 TPS 时是否包含最近 5 秒数据\n提供更平滑的性能视图",
 Category = "杂项",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });
 

    // ===== AUTO-INJECTED by core-fetcher pipeline =====
    Register(new ServerConfigDescriptor
    {
        Key = "config-version",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "配置版本号",
        Description = "内部配置版本标识，请勿手动修改",
        Category = "Leaves 专属",
        DefaultValue = "6",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.fix.collision-behavior",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "碰撞行为",
        Description = "原版/Paper/Leaves 不同碰撞判定逻辑，默认 PAPER",
        Category = "Leaves 专属",
        DefaultValue = "PAPER",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.fix.stacked-container-destroyed-drop",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "堆叠容器破坏掉落",
        Description = "堆叠容器（如木桶）被破坏时是否正确掉落物品",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.fix.vanilla-display-name",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "原版显示名修复",
        Description = "修复物品/方块显示名回归原版行为",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.fix.vanilla-hopper",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "原版漏斗修复",
        Description = "还原漏斗为原版工作逻辑，关闭 Paper 优化",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.fix.vanilla-portal-handle",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "原版传送门处理",
        Description = "修复 Nether/End 传送门处理流程回归原版",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.async-keepalive.enable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "异步保活",
        Description = "是否异步处理玩家 Keepalive 包以降低主线程开销",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.async-keepalive.timeout-seconds",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "保活超时秒",
        Description = "异步保活模式下判定玩家掉线的超时时间（秒）",
        Category = "Leaves 专属",
        DefaultValue = "20",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.auto-update.allow-experimental",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "允许实验版更新",
        Description = "自动更新时是否允许拉取实验/预览版本",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.auto-update.download-source",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "更新下载源",
        Description = "自动更新时从何处拉取：application = 构建渠道内置源",
        Category = "Leaves 专属",
        DefaultValue = "application",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.auto-update.enable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "自动更新",
        Description = "是否在后台定时检查并下载 Leaves 核心新版本",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.auto-update.time",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "自动更新时间",
        Description = "每天执行自动更新检查的时间点（可多个）",
        Category = "Leaves 专属",
        DefaultValue = "[\"14:00\", \"2:00\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.bstats-privacy-mode",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "BStats 隐私模式",
        Description = "开启后 bStats 匿名统计将不上报敏感信息",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.disable-method-profiler",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "关闭方法分析器",
        Description = "禁用 Java 方法级 Profiler，降低生产环境性能开销",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.dont-respond-ping-before-start-fully",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "完全启动前不响应 Ping",
        Description = "服务器未完整就绪时直接拒绝 Ping 包，防止过早连接",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.extra-yggdrasil-service.enable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "外置 Yggdrasil 服务",
        Description = "启用自定义的外置正版验证服务（authlib-injector 用法）",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.extra-yggdrasil-service.login-protect",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Yggdrasil 登录保护",
        Description = "开启后 Yggdrasil 登录请求经过额外风控拦截",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.extra-yggdrasil-service.urls",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Yggdrasil 服务地址",
        Description = "外置 Yggdrasil 验证服务的 URL 列表，支持多个节点",
        Category = "Leaves 专属",
        DefaultValue = "[\"https://url.with.authlib-injector-yggdrasil\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.force-minecraft-command",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "强制 Minecraft 命令",
        Description = "将 /minecraft:xxx 形式的命令强制走原版命令解析器",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.leaves-packet-event",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Leaves 包事件",
        Description = "向插件派发 Leaves 特有的网络包事件，方便监听自定义协议",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.no-chat-sign",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "禁用聊天签名",
        Description = "禁用 Minecraft 1.19+ 的聊天签名验证，允许多签名聊天",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.server-lang",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "服务器语言",
        Description = "服务器侧 UI/日志的语言代码，如 zh_cn",
        Category = "Leaves 专属",
        DefaultValue = "en_us",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.misc.server-mod-name",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "服务端模组名",
        Description = "客户端查看服务端信息时显示的模组/核心名称（默认 Leaves）",
        Category = "Leaves 专属",
        DefaultValue = "Leaves",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.avoid-anvil-too-expensive",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "规避砧太昂贵",
        Description = "当砧合并费用过高时自动丢弃费用最高的附魔，强制保留",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.bedrock-break-list",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "可破坏基岩列表",
        Description = "允许玩家破坏的基岩方块清单（配命令使用）",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.bow-infinity-fix",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "无限弓修复",
        Description = "修复 1.21+ 无限附魔弓仍然需要箭的 bug",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.container-passthrough",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "容器穿透",
        Description = "让玩家在潜行时能穿过容器方块而不打开界面",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.creative-no-clip",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "创造模式无碰撞",
        Description = "创造模式玩家对所有方块开启穿透",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.despawn-enderman-with-block",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "带方块末影人消失",
        Description = "让正抱着方块的末影人也能因距离过远而消失",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.disable-check-out-of-order-command",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "禁用命令乱序检查",
        Description = "跳过命令包顺序的安全校验，减少指令顺序错误的拒包",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.disable-distance-check-for-use-item",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "禁用物品使用距离",
        Description = "服务器不再校验客户端使用物品时与目标的距离",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.disable-packet-limit",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "禁用包速率限制",
        Description = "关闭所有 packet-limiter，不对客户端发包频率做限速",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.disable-vault-blacklist",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "禁用 Vault 黑名单",
        Description = "跳过 Vault 权限插件对特定名称的黑名单拦截",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.elytra-aeronautics.message",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "巡航模式提示",
        Description = "进入/退出鞘翅巡航模式时是否在玩家屏幕提示文字",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.elytra-aeronautics.message-end",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "退出巡航提示",
        Description = "退出鞘翅巡航模式时显示的提示文字",
        Category = "Leaves 专属",
        DefaultValue = "Flight exit cruise mode",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.elytra-aeronautics.message-start",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "进入巡航提示",
        Description = "进入鞘翅巡航模式时显示的提示文字",
        Category = "Leaves 专属",
        DefaultValue = "Flight enter cruise mode",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.elytra-aeronautics.no-chunk-height",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "无区块加载高度",
        Description = "启用无区块加载滑翔的最小飞行高度（y 值），低于则取消",
        Category = "Leaves 专属",
        DefaultValue = "500.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.elytra-aeronautics.no-chunk-load",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "无区块加载滑翔",
        Description = "鞘翅巡航时不主动加载前方区块，适合高延时网络",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.elytra-aeronautics.no-chunk-speed",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "无区块加载速度",
        Description = "无区块加载模式下滑翔的目标速度上限，-1 表示不限制",
        Category = "Leaves 专属",
        DefaultValue = "-1.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.exp-orb-absorb-mode",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "经验球吸收模式",
        Description = "经验球被玩家吸入的策略：VANILLA=原版、PLAYER=集中到玩家身上等",
        Category = "Leaves 专属",
        DefaultValue = "VANILLA",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.cache-skin",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "缓存假人皮肤",
        Description = "在本地缓存假人皮肤数据，避免每次登录都重新请求",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.enable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "开启假人",
        Description = "允许通过指令/插件在服务器内生成模拟玩家（FakePlayer）",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.in-game.always-send-data",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "始终同步游戏数据",
        Description = "假人在线时始终向客户端同步游戏状态（即便附近没有真实玩家）",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.in-game.enable-locator-bar",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "启用定位信标",
        Description = "允许其他玩家通过定位信标（指南针）追踪假人位置",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.in-game.simulation-distance",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人模拟距离",
        Description = "假人周围多少格内保持实体活动，-1 表示跟随服务器模拟距离",
        Category = "Leaves 专属",
        DefaultValue = "-1",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.in-game.skip-sleep-check",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "跳过睡觉检查",
        Description = "假人进入世界时不触发关于睡觉的村民/玩家判定",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.in-game.spawn-phantom",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "生成幻翼",
        Description = "假人长时间不睡觉后是否在其周围生成幻翼",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.in-game.tick-type",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人 Tick 类型",
        Description = "假人使用的 Tick 策略：NETWORK=网络驱动、TICK=随世界 Tick",
        Category = "Leaves 专属",
        DefaultValue = "NETWORK",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.limit",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人上限",
        Description = "单个服务器实例同时存在的假人数量上限",
        Category = "Leaves 专属",
        DefaultValue = "10",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.manual-save-and-load",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "手动存档载入",
        Description = "关闭假人自动存档流程，改为按指令手动保存/载入",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.modify-config",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人改配置",
        Description = "允许假人登录后修改自身的部分运行配置",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.open-fakeplayer-inventory",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人库存打开",
        Description = "允许在游戏内 GUI 中直接打开假人的背包/末影箱",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.prefix",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人前缀",
        Description = "自动生成假人名字时追加的前缀字符串",
        Category = "Leaves 专属",
        DefaultValue = "",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.regen-amount",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人自然回血量",
        Description = "假人静置时每秒自动恢复的生命值点数，0 表示关闭",
        Category = "Leaves 专属",
        DefaultValue = "0.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.resident-fakeplayer",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "常驻假人",
        Description = "开启后服务器重启时会自动恢复上次的假人名单",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.suffix",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人后缀",
        Description = "自动生成假人名字时追加的后缀字符串",
        Category = "Leaves 专属",
        DefaultValue = "",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.unable-fakeplayer-names",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "禁用的假人名字",
        Description = "不允许被用作假人的玩家名列表，避免与真实账号冲突",
        Category = "Leaves 专属",
        DefaultValue = "[\"player-name\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fakeplayer.use-action",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "假人动作同步",
        Description = "让假人参与原版动作/动画（挥剑、进食等）状态同步",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fix-stuck-zombified-piglin-anger-target",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "修复僵猪灵仇恨卡壳",
        Description = "僵猪灵追击时目标消失后不再卡死仇恨",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.fix-update-suppression-crash",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "修复更新抑制崩溃",
        Description = "修正由方块更新抑制链引起的主线程崩溃",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.flatten-triangular-distribution",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "三角分布平坦化",
        Description = "把某些随机三角分布拉平成均匀分布，减少极端值",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.follow-tick-sequence-merge",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "遵循 Tick 合并顺序",
        Description = "让实体合并流程严格跟随原版 Tick 序列执行",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.force-void-trade",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "强制虚空交易",
        Description = "允许村民交易因浮点数溢出而出现负数价格",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.hopper-counter.enable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "漏斗计数器",
        Description = "开启漏斗物品计数/速率统计面板（给管理员看的）",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.hopper-counter.unlimited-speed",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "漏斗无限速",
        Description = "跳过漏斗吸取物品的速率限制，让漏斗瞬间吸走物品",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.lava-riptide",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "岩浆激流",
        Description = "激流三叉戟在岩浆中也能触发冲刺效果",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.mc-technical-survival-mode",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "MC 技术生存模式",
        Description = "启用技术向生存玩法的一组组合调整（如零刻、红石刻板等）",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.allow-anvil-destroy-item-entities",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "砧压落物",
        Description = "让下落中的砧方块能砸扁地上的掉落物实体",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.allow-entity-portal-with-passenger",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "带乘客实体过传送门",
        Description = "允许骑乘中的实体（马、船等）一起穿过 Nether/End 传送门",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.allow-inf-nan-motion-values",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "允许 Inf/NaN 运动",
        Description = "跳过运动向量的边界检查，允许非法值通过（可能引入 bug）",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.block-updater.cce-update-suppression",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "抑制 CCE 更新",
        Description = "抑制会导致 ConcurrentModificationException 的方块更新链",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.block-updater.instant-block-updater-reintroduced",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "即时方块更新器",
        Description = "重新引入旧版即时方块更新逻辑（1.12 时代行为）",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.block-updater.old-block-remove-behaviour",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版方块移除",
        Description = "还原旧版方块被破坏/清除时的更新传播方式",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.block-updater.redstone-ignore-upwards-update",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "红石忽略向上更新",
        Description = "让红石方块在上方方块变动时不触发更新",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.block-updater.sound-update-suppression",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "抑制音效更新",
        Description = "取消由方块更新触发的冗余音效播放",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.copper-bulb-1gt-delay",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "铜灯泡 1gt 延迟",
        Description = "让铜灯泡的点亮/熄灭延迟 1 game tick，还原早期行为",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.crafter-1gt-delay",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "合成器 1gt 延迟",
        Description = "让合成器（Crafter）的合成产出延迟 1gt",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.disable-LivingEntity-ai-step-alive-check",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "跳过存活判定",
        Description = "关闭 LivingEntity AI 每 tick 的 isAlive 检查，减少分支",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.disable-gateway-portal-entity-ticking",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "关闭 Gateway Tick",
        Description = "关闭末地折跃门（Gateway）的实体 Tick 过程，节省性能",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.disable-item-damage-check",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "关闭物品伤害校验",
        Description = "跳过物品使用/装备时的伤害值边界检查，允许负值等",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.ender-dragon-part-can-use-end-portal",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "龙穿过末地传送门",
        Description = "让末影龙的身体部位也能穿过末地传送门",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.keep-leash-connect-when-use-firework",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "烟花时保留缰绳",
        Description = "玩家被烟花弹射时仍保留与马/豹猫的缰绳连接",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.old-hopper-suck-in-behavior",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版漏斗吸入",
        Description = "恢复旧版漏斗吸入物品的速率/范围逻辑",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.old-minecart-motion-behavior",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版矿车运动",
        Description = "还原 1.10 前矿车加速度、摩擦的计算公式",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.old-projectile-explosion-behavior",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版爆炸弹射物",
        Description = "还原早期版本爆炸产生的弹射物（如 TNT 二次弹射）行为",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.old-raid-behavior",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版袭击",
        Description = "恢复 1.19 前的村庄袭击（Raid）行为",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.old-throwable-projectile-tick-order",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版投掷物 Tick 顺序",
        Description = "还原雪球/鸡蛋等投掷物的 Tick 执行先后次序",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.old-zombie-piglin-drop",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版僵猪灵掉落",
        Description = "让 zombified piglin 按 1.15 前版本掉落金粒/金锭",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.old-zombie-reinforcement",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版僵尸增援",
        Description = "让僵尸在阳光下也可能召唤增援，还原早期行为",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.rng-fishing",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版钓鱼 RNG",
        Description = "还原 1.16 时代钓鱼奖励的随机数序列",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.shears-in-dispenser-can-zero-amount",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "发射器剪刀零耐久",
        Description = "让发射器内剪刀即使耐久为 0 也能继续剪方块",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.spawn-invulnerable-time",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旧版出生无敌",
        Description = "恢复 1.16 时代玩家出生时的短暂无敌时间",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.tnt-wet-explosion-no-item-damage",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "湿 TNT 无物品伤害",
        Description = "让潮湿环境中的 TNT 爆炸不破坏掉落物",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.tripwire-and-hook-behavior.string-tripwire-hook-duplicate",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "绊线钩复制物品",
        Description = "还原旧版绊线钩被破坏时可能复制物品的行为",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.tripwire-and-hook-behavior.tripwire-behavior",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "绊线钩行为模式",
        Description = "绊线判定逻辑：VANILLA_21=1.21 行为等",
        Category = "Leaves 专属",
        DefaultValue = "VANILLA_21",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.villager-infinite-discounts",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "村民无限折扣",
        Description = "允许村民反复打折而不触发打折上限机制",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.void-trade",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "虚空交易",
        Description = "允许村民交易的价格计算出现负数（原版废弃特性）",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.minecraft-old.zero-tick-plants",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "零刻植物",
        Description = "允许在同一 tick 内完成农作物生长/破坏（作物可被跳过）",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.movable-budding-amethyst",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "可移动紫水晶母岩",
        Description = "允许用活塞/黏性活塞推动紫水晶母岩方块",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.no-block-update-command",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "无方块更新命令",
        Description = "让某些世界编辑类操作不触发方块更新广播",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.no-feather-falling-trample",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "无摔落踩踏",
        Description = "关闭有摔落保护时对农作物/耕地的踩踏判定",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.no-tnt-place-update",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "放置 TNT 不触发更新",
        Description = "玩家放置 TNT 方块时不触发周围方块更新",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.player-operation-limiter",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "玩家操作限流",
        Description = "对高频操作（连点、长按）进行节流，防止恶意刷包",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.redstone-shears-wrench",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "红石刻板扳手",
        Description = "开启后用剪刀/扳手右键红石比较器可切换模式",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.renewable-coral",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "可再生珊瑚",
        Description = "让珊瑚块/珊瑚在特定条件下能够重新生长繁殖",
        Category = "Leaves 专属",
        DefaultValue = "FALSE",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.renewable-deepslate",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "可再生深板岩",
        Description = "允许通过刷怪/转换等方式让深板岩变成可再生资源",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.renewable-elytra",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "可再生鞘翅",
        Description = "击杀末影龙时额外掉落鞘翅的概率，-1 表示关闭",
        Category = "Leaves 专属",
        DefaultValue = "-1.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.renewable-sponges",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "可再生海绵",
        Description = "允许从海底神殿守卫者等途径获得可再生海绵",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.return-nether-portal-fix",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "返程传送门修复",
        Description = "修复 Nether 返程传送门总是把玩家送回世界出生点的 bug",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.shared-villager-discounts",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "共享村民折扣",
        Description = "一名玩家压低的村民价格会被所有玩家共享，而不是各自独立",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.shave-snow-layers",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "刮除雪层",
        Description = "用铲/剑扫雪时会顺带刮掉一层雪层方块",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.shulker-box.same-nbt-stackable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "同 NBT 堆叠",
        Description = "不仅空盒，完全相同 NBT 的潜影盒也允许堆叠",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.shulker-box.stackable-shulker-boxes",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "潜影盒可堆叠",
        Description = "让物品栏中相同的空潜影盒能够堆叠到一组 64",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.snowball-and-egg-can-knockback-player",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "雪球鸡蛋击退玩家",
        Description = "让雪球/鸡蛋也能把玩家击退（类似其他弹射物）",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.spectator-dont-get-advancement",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "旁观者不获取进度",
        Description = "处于旁观模式的玩家完成行为时不触发任何进度",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.spider-jockeys-drop-gapples",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "蜘蛛骑士掉落金苹果",
        Description = "蜘蛛骑士/骷髅骑手被击杀时额外掉落金苹果的概率，-1 表示关闭",
        Category = "Leaves 专属",
        DefaultValue = "-1.0",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.stick-change-armorstand-arm-status",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "木棍切换盔甲架手臂",
        Description = "用木棍右键盔甲架可切换其手臂姿态",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.modify.use-vanilla-random",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "使用原版随机源",
        Description = "让所有随机决策都走原版 Random 实现，而不是 Leaves 的优化 RNG",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.cache-climb-check",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "缓存攀爬检查",
        Description = "把玩家/实体是否可攀爬（梯子、藤蔓）的结果缓存几 tick",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.cache-ignite-odds",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "缓存点燃概率",
        Description = "缓存苦力怕点燃概率等随机值，避免重复计算",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.check-frozen-ticks-before-landing-block",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "落地前检查冰 Tick",
        Description = "玩家/实体落地前先检查下方是否有冰冻方块，避免空翻事件",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.dont-send-useless-entity-packets",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "不发无用实体包",
        Description = "客户端无需接收的实体移动/位置包直接丢弃，减少带宽",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.enable-suffocation-optimization",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "窒息优化",
        Description = "重写方块内窒息伤害计算，减少每 tick 的位置碰撞次数",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.equipment-tracking",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "装备追踪",
        Description = "开启后实时追踪所有玩家装备，方便插件查询但会增加开销",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.faster-chunk-serialization",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "更快区块序列化",
        Description = "对区块保存到磁盘的 NBT 序列化进行加速",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.inactive-goal-selector-disable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "停用目标选择器",
        Description = "当实体附近无目标时，禁用其 AI goal selector 循环",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.optimize-noise-generation",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "优化噪声生成",
        Description = "优化地形噪声（Perlin）生成速度，主要影响新世界加载",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.optimize-sun-burn-tick",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "优化晒 AI Tick",
        Description = "把僵尸/骷髅等的晒阳光判定合并批量计算",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.optimized-CubePointRange",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "优化 CubePointRange",
        Description = "对 AABB 范围查询用更紧凑的数据结构，加速玩家/目标碰撞",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.optimized-dragon-respawn",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "龙复活优化",
        Description = "压缩末影龙复活的区块加载范围与 Tick 开销",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.reduce-chuck-load-and-lookup",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "减少区块加载查找",
        Description = "批量合并同类区块加载/查找请求，降低磁盘 IO",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.reduce-entity-allocations",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "减少实体对象分配",
        Description = "让部分短暂实体（箭、经验球）复用对象而不是 new",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.remove.damage-lambda",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "移除伤害 Lambda",
        Description = "移除实体伤害流程中频繁创建的 Lambda",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.remove.tick-guard-lambda",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "移除 Tick 守护 Lambda",
        Description = "移除每个 Tick 都创建的匿名 Lambda 对象，降低 GC 压力",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.skip-cloning-advancement-criteria",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "跳过进度条件克隆",
        Description = "跳过进度判定中不必要的 Criteria 对象克隆",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.skip-entity-move-if-movement-is-zero",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "零运动跳过移动",
        Description = "实体 movement 向量为 0 时跳过整个 move 流程",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.skip-negligible-planar-movement-multiplication",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "跳过极小水平运动乘法",
        Description = "当实体水平运动极小时跳过与摩擦/阻力的乘法计算",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.skip-secondary-POI-sensor-if-absent",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "跳过次级 POI 传感器",
        Description = "当附近没有村民工作站等 POI 时直接跳过 AI 次级传感器扫描",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.sleeping-block-entity",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "休眠方块实体",
        Description = "对长期未被访问的方块实体（容器、红石）进行 Tick 休眠",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.performance.store-mob-counts-in-array",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "数组存储怪物计数",
        Description = "把各怪物类型的全局计数用数组代替散列表，提升查询速度",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.alternative-block-placement",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "替代方块放置",
        Description = "与模组客户端的特殊方块放置协议对接方式，默认 NONE（关闭）",
        Category = "Leaves 专属",
        DefaultValue = "NONE",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.appleskin.protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "AppleSkin 协议",
        Description = "与 AppleSkin 模组对接，同步营养值/饱和度 HUD 数据",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.appleskin.sync-tick-interval",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "AppleSkin 同步间隔",
        Description = "AppleSkin 数据同步到客户端的 Tick 间隔",
        Category = "Leaves 专属",
        DefaultValue = "20",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.bbor-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "BBOR 模组协议",
        Description = "与 BBOR（更好的 Boss 血条）模组协议对接，服务端向客户端发送血条数据",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.bladeren.mspt-sync-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Bladeren MSPT 同步",
        Description = "将服务器 MSPT 实时推送到所有连接的 Bladeren 客户端",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.bladeren.mspt-sync-tick-interval",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Bladeren MSPT 间隔",
        Description = "MSPT 数据同步的 Tick 间隔，越小越实时但开销越高",
        Category = "Leaves 专属",
        DefaultValue = "20",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.bladeren.protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Bladeren 协议",
        Description = "启用 Leaves 自研的 Bladeren 客户端协议，用于更好的实体/网络同步",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.chat-image-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "聊天图片协议",
        Description = "对接聊天图片模组，允许玩家在聊天里发图片",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.jade-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Jade 模组协议",
        Description = "与 Jade（WAILA 后继）模组协议对接，同步方块/实体信息给客户端 HUD",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.leaves-carpet-support",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Leaves Carpet 支持",
        Description = "给 Carpet Mod（假人）提供 Leaves 专属的底层兼容",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.pca.pca-sync-player-entity",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "PCA 同步玩家实体范围",
        Description = "向哪些客户端同步玩家实体：OPS=仅 OP、ALL=全员",
        Category = "Leaves 专属",
        DefaultValue = "OPS",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.pca.pca-sync-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "PCA 同步协议",
        Description = "启用 PCA（玩家聊天动画类）协议，同步客户端动作动画",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.rei-server-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "REI 服务端协议",
        Description = "为 REI（Roughly Enough Items）提供服务端物品/配方查询数据",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.entity-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux 实体协议",
        Description = "与 Servux 模组对接，同步实体详情给客户端 HUD",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.hud-enabled-loggers",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux HUD 启用日志源",
        Description = "Servux HUD 中显示哪些数据源：TPS/MOB_CAPS/...",
        Category = "Leaves 专属",
        DefaultValue = "[\"TPS\", \"MOB_CAPS\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.hud-logger-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux HUD 日志协议",
        Description = "让 Servux HUD 从服务器拉取关键运行日志进行显示",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.hud-metadata-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux HUD 元数据协议",
        Description = "向 Servux 客户端 HUD 发送服务器元数据（TPS、版本等）",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.hud-metadata-protocol-share-seed",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux 共享种子",
        Description = "Servux 是否把世界种子发送给客户端 HUD（影响地图/结构显示）",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.hud-update-interval",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux HUD 更新间隔",
        Description = "Servux HUD 每隔几个 Tick 向服务器请求一次状态",
        Category = "Leaves 专属",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.litematics.enable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux Litematica 支持",
        Description = "允许 Servux 在客户端侧运行 Litematica 模组并同步保存到服务器",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.litematics.max-nbt-size",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux Litematica NBT 上限",
        Description = "Servux 向服务器提交 Litematica 结构的最大 NBT 字节数",
        Category = "Leaves 专属",
        DefaultValue = "2097152",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.servux.structure-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Servux 结构协议",
        Description = "与 Servux 模组对接，同步结构（Structure）NBT 数据给客户端",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.strict-mode",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "严格协议模式",
        Description = "开启后完全拒绝偏离原版协议的客户端包，兼容差但安全",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.syncmatica.enable",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Syncmatica 启用",
        Description = "允许 Syncmatica 模组将客户端的结构/选区数据上传服务器共享",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.syncmatica.quota",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Syncmatica 配额",
        Description = "开启后对 Syncmatica 上传做字节量配额控制",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.syncmatica.quota-limit",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Syncmatica 配额上限",
        Description = "单个玩家/会话允许的 Syncmatica 上传总字节数上限",
        Category = "Leaves 专属",
        DefaultValue = "40000000",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.xaero-map-protocol",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Xaero 地图协议",
        Description = "向 Xaero's Minimap 模组发送世界地图、路点等数据",
        Category = "Leaves 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.protocol.xaero-map-server-id",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Xaero 地图服务器 ID",
        Description = "在 Xaero 客户端中用于区分不同服务器存档的 ID 数字",
        Category = "Leaves 专属",
        DefaultValue = "69732304",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.format",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "区域文件格式",
        Description = "世界区域文件的存储格式：ANVIL、LINEAR 等",
        Category = "Leaves 专属",
        DefaultValue = "ANVIL",
        ValueType = "enum",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.linear.compression-level",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Linear 压缩等级",
        Description = "Linear 区域文件的 Zstd/Deflate 压缩等级（越高压得越小越慢）",
        Category = "Leaves 专属",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.linear.flush-delay-ms",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Linear 刷新延迟",
        Description = "区域数据改动到真正写盘之间的延迟毫秒数，越大批量越大",
        Category = "Leaves 专属",
        DefaultValue = "100",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.linear.flush-max-threads",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Linear 刷新最大线程数",
        Description = "Linear 格式并行写盘时最多同时使用的工作线程数",
        Category = "Leaves 专属",
        DefaultValue = "6",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.linear.max-flush-per-run",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Linear 单次最大刷新量",
        Description = "每轮刷新最多同时写入多少个区域文件，防止 IO 风暴",
        Category = "Leaves 专属",
        DefaultValue = "256",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.linear.region-unload-check-interval-ms",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Linear 卸载检查间隔",
        Description = "每隔多少毫秒执行一次可卸载区域文件的检查",
        Category = "Leaves 专属",
        DefaultValue = "30000",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.linear.region-unload-idle-ms",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Linear 区域卸载闲置",
        Description = "区域文件连续闲置多少毫秒后可被从内存中卸载",
        Category = "Leaves 专属",
        DefaultValue = "600000",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.linear.use-virtual-thread",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Linear 使用虚拟线程",
        Description = "Java 21+ 环境下用虚拟线程执行区域写盘，减少平台线程占用",
        Category = "Leaves 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "settings.region.linear.version",
        ConfigFileName = "config/leaves.yml",
        DisplayName = "Linear 存储版本",
        Description = "Linear（平展式区域文件）的版本号，目前为 V2",
        Category = "Leaves 专属",
        DefaultValue = "V2",
        ValueType = "int",
        RequiresRestart = false,
    });
    // ===== END AUTO-INJECTED =====
}

 /// <summary>
 /// 注册 Leaf 专属配置文件 leaf.yml 的配置描述符
 /// </summary>
 private void RegisterLeafYml()
 {
 const string file = "leaf.yml";

 // ==================== async 异步处理 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-pathfinding.enabled",
 ConfigFileName = file,
 DisplayName = "启用异步路径查找",
 Description = "将实体寻路计算转移到异步线程池\n实体寻路不再阻塞主线程\n️ 仅启动时读取，必须重启",
 Category = "异步",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-pathfinding.max-threads",
 ConfigFileName = file,
 DisplayName = "路径查找最大线程数",
 Description = "异步路径查找线程池最大线程数\n0 = 自动 = CPU 核心数/4\n<0 = CPU 核心数 + 此值\n8 核 CPU 推荐 4",
 Category = "异步",
 DefaultValue = "0",
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-pathfinding.queue-size",
 ConfigFileName = file,
 DisplayName = "路径查找任务队列大小",
 Description = "等待执行的任务队列容量\n0 = 自动 = 线程数 × 256\n队列满将触发拒绝策略",
 Category = "异步",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-pathfinding.reject-policy",
 ConfigFileName = file,
 DisplayName = "队列满拒绝策略",
 Description = "FLUSH_ALL：清空队列并在主线程执行所有任务（适合 CPU ≥ 12 核）\nCALLER_RUNS：仅在新任务提交时在主线程执行（适合低配）",
 Category = "异步",
 DefaultValue = "FLUSH_ALL",
 AllowedValues = ["FLUSH_ALL", "CALLER_RUNS"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-entity-tracker.enabled",
 ConfigFileName = file,
 DisplayName = "启用多线程实体追踪",
 Description = "将实体追踪转移到异步线程池\n可提升 40-60% 实体处理性能\n️ 仅启动时读取，必须重启",
 Category = "异步",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-entity-tracker.max-threads",
 ConfigFileName = file,
 DisplayName = "实体追踪最大线程数",
 Description = "实体追踪线程池最大线程数\n0 = 自动 = CPU 核心数 / 6\n8 核 CPU 推荐 3",
 Category = "异步",
 DefaultValue = "0",
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-entity-tracker.compat-mode",
 ConfigFileName = file,
 DisplayName = "NPC 兼容模式",
 Description = "启用 Citizens 等 NPC 插件兼容模式\n基于实体 NPC 插件建议开启\n基于数据包 NPC 插件可关闭以获得更好性能",
 Category = "异步",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-mob-spawning.enabled",
 ConfigFileName = file,
 DisplayName = "启用异步生物生成",
 Description = "将生物生成计算转移到异步线程\n前置条件：必须开启 per-player-mob-spawns\n️ 仅启动时读取，必须重启",
 Category = "异步",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.async-player-data-save.enabled",
 ConfigFileName = file,
 DisplayName = "异步玩家数据保存",
 Description = "将玩家数据 .dat 文件保存转移到异步线程\n避免主线程 I/O 阻塞",
 Category = "异步",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.faster-random-generator.enabled",
 ConfigFileName = file,
 DisplayName = "启用快速随机数生成器",
 Description = "用更快的随机数算法替代原版 java.util.Random\n可提升 15-25% 涉及随机的性能",
 Category = "异步",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.faster-random-generator.random-generator",
 ConfigFileName = file,
 DisplayName = "随机数算法",
 Description = "使用的随机数算法\n推荐 XOROSHIRO128_PLUS_PLUS（速度与质量平衡佳）",
 Category = "异步",
 DefaultValue = "XOROSHIRO128_PLUS_PLUS",
 AllowedValues = ["XOROSHIRO128_PLUS_PLUS", "XOSHIRO256_PLUS_PLUS", "JAVA_UTIL_RANDOM", "SPLITABLE_RANDOM"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.faster-random-generator.enable-for-worldgen",
 ConfigFileName = file,
 DisplayName = "用于世界生成",
 Description = "是否将快速随机数生成器用于世界生成\n️ 强烈建议保持 false，否则影响地形生成一致性",
 Category = "异步",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "async.faster-random-generator.use-legacy-for-slime-chunk",
 ConfigFileName = file,
 DisplayName = "史莱姆区块使用旧算法",
 Description = "对史莱姆区块判定继续使用原版随机算法\n保证区块分布与原版一致\n建议开启",
 Category = "异步",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== performance 性能优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "performance.dab.enabled",
 ConfigFileName = file,
 DisplayName = "启用 DEAR 实体大脑优化",
 Description = "动态大脑激活：远离玩家的实体降低 AI tick 频率\n大幅降低 CPU 占用",
 Category = "性能",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance.dab.start-distance",
 ConfigFileName = file,
 DisplayName = "DEAR 生效起始距离",
 Description = "实体距玩家多远时开始受 DEAR 影响\n距离小于此值的实体保持原版全速 tick",
 Category = "性能",
 DefaultValue = "12",
 MinValue = 0,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance.dab.max-tick-freq",
 ConfigFileName = file,
 DisplayName = "最远实体最大 tick 间隔",
 Description = "距离最远的实体多久 tick 一次 AI\n值越大越省 CPU，但远处实体行为越迟钝\n20 = 1 秒",
 Category = "性能",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance.dab.activation-dist-mod",
 ConfigFileName = file,
 DisplayName = "距离对频率的影响系数",
 Description = "距离对 tick 频率的影响强度\n公式：频率 = (到玩家距离^2) / (2^本值)\n7 = 更省 CPU / 9 = 更接近原版",
 Category = "性能",
 DefaultValue = "8",
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance.dab.blacklisted-entities",
 ConfigFileName = file,
 DisplayName = "DEAR 忽略的实体列表",
 Description = "不受 DEAR 影响、始终保持全速 AI 的实体列表\n填实体类型 ID，如 minecraft:villager",
 Category = "性能",
 DefaultValue = "[]",
 ValueType = "string[]"
 });

 // ==================== network 网络 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "network.compression-threshold",
 ConfigFileName = file,
 DisplayName = "网络压缩阈值",
 Description = "数据包大小超过此值时才进行压缩\n0 = 全部压缩 / -1 = 禁用压缩",
 Category = "网络",
 DefaultValue = "256",
 MinValue = -1,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network.compression-level",
 ConfigFileName = file,
 DisplayName = "压缩级别",
 Description = "网络压缩的级别\n0 = 不压缩（最快）/ 9 = 最大压缩（最慢）",
 Category = "网络",
 DefaultValue = "6",
 MinValue = 0,
 MaxValue = 9,
 ValueType = "int"
 });

 // ==================== misc 杂项 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.rebrand.server-mod-name",
 ConfigFileName = file,
 DisplayName = "服务器 Mod 名称",
 Description = "玩家按 F3 看到的服务器 Mod 名称\n原版显示 vanilla\n可改成你的服务器品牌",
 Category = "杂项",
 DefaultValue = "Leaf",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.rebrand.server-gui-name",
 ConfigFileName = file,
 DisplayName = "服务器 GUI 标题",
 Description = "服务器控制台窗口标题\n仅在不使用 nogui 启动时生效",
 Category = "杂项",
 DefaultValue = "Leaf Console",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.secure-seed.enabled",
 ConfigFileName = file,
 DisplayName = "启用安全种子",
 Description = "启用 1024 位安全种子\n所有矿物与结构生成使用加密种子，无法被分析\n️ 启用后无法关闭",
 Category = "杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.sentry.dsn",
 ConfigFileName = file,
 DisplayName = "Sentry DSN 地址",
 Description = "Sentry 项目的 Data Source Name\n留空则禁用 Sentry 上报\n可在 sentry.io 免费注册获取",
 Category = "杂项",
 DefaultValue = "",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.cache.cache-player-profile-result",
 ConfigFileName = file,
 DisplayName = "缓存玩家档案",
 Description = "是否缓存玩家档案（皮肤、UUID）查询结果\n减少 Mojang API 调用",
 Category = "杂项",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== opt 极简优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "opt.skip-map-item-data-updates",
 ConfigFileName = file,
 DisplayName = "跳过地图物品数据更新",
 Description = "跳过不必要的地图物品数据更新\n减少网络包发送",
 Category = "优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "opt.reduce-useless-packets",
 ConfigFileName = file,
 DisplayName = "减少无用数据包",
 Description = "合并或跳过部分无用数据包\n降低网络开销",
 Category = "优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "opt.throttle-hopper-when-full",
 ConfigFileName = file,
 DisplayName = "满漏斗节流",
 Description = "当漏斗容器已满时限制其检查频率\n减少 CPU 占用",
 Category = "优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });
 }

 // ============================================================
 // 第一批：代理端核心专属配置
 // ============================================================

 /// <summary>
 /// 注册 Velocity 专属配置文件 velocity.toml 的配置描述符
 /// </summary>
 private void RegisterVelocityToml()
 {
 const string file = "velocity.toml";

 // ==================== 基础设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "bind",
 ConfigFileName = file,
 DisplayName = "绑定地址",
 Description = "代理监听玩家连接的地址和端口\n格式：IP:端口\n例如：0.0.0.0:25577 表示监听所有网卡的 25577 端口\n玩家就连接这个端口进入服务器",
 Category = "基础设置",
 DefaultValue = "0.0.0.0:25577",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "motd",
 ConfigFileName = file,
 DisplayName = "服务器 MOTD",
 Description = "玩家在服务器列表中看到的服务器描述\n支持 § 颜色代码\n两行用 \\n 分隔",
 Category = "基础设置",
 DefaultValue = "&3A Velocity Server",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "show-max-players",
 ConfigFileName = file,
 DisplayName = "显示最大玩家数",
 Description = "在服务器列表中显示的最大玩家数\n实际人数可超过此值，仅作显示",
 Category = "基础设置",
 DefaultValue = "500",
 MinValue = 0,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "online-mode",
 ConfigFileName = file,
 DisplayName = "正版验证",
 Description = "是否要求玩家使用 Minecraft 正版账号登录\ntrue = 仅正版 / false = 支持离线模式\n️ 关闭后必须配置防火墙防止 IP 伪造",
 Category = "基础设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "prevent-client-proxy-connections",
 ConfigFileName = file,
 DisplayName = "禁止客户端代理连接",
 Description = "禁止玩家通过 VPN/代理连接服务器\n依赖正版验证的 IP 检查\n仅 online-mode=true 时生效",
 Category = "基础设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-info-forwarding-mode",
 ConfigFileName = file,
 DisplayName = "玩家信息转发模式",
 Description = "如何将玩家信息（IP、UUID）转发给后端子服\nnone = 不转发（子服看到代理 IP）\nlegacy = BungeeCord 兼容模式\nmodern = Velocity 推荐模式（需后端支持）\n️ modern 模式需配合 secret 值使用",
 Category = "基础设置",
 DefaultValue = "NONE",
 AllowedValues = ["NONE", "LEGACY", "MODERN"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "forwarding-secret",
 ConfigFileName = file,
 DisplayName = "转发密钥",
 Description = "modern 模式下用于验证的密钥\n需与后端 Velocity 插件配置一致\n建议使用随机字符串",
 Category = "基础设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 // ==================== 后端服务器列表 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "try",
 ConfigFileName = file,
 DisplayName = "尝试连接顺序",
 Description = "玩家进入代理时尝试连接的后端服务器顺序\n第一个可用的将被使用\n逗号分隔，如：lobby-1,lobby-2,main",
 Category = "后端服务器",
 DefaultValue = "lobby",
 ValueType = "string[]"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "default-server",
 ConfigFileName = file,
 DisplayName = "默认服务器",
 Description = "玩家首次进入或服务器选择失败时进入的默认后端\n️ 必须在 [servers] 节中已定义",
 Category = "后端服务器",
 DefaultValue = "lobby",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "forced-hosts",
 ConfigFileName = file,
 DisplayName = "强制主机映射",
 Description = "根据玩家连接的域名直接进入指定后端\n格式：{ 域名 = \"后端服务器\" }\n如：{ \"pvp.example.com\" = \"pvp-server\" }",
 Category = "后端服务器",
 DefaultValue = "{}",
 ValueType = "string"
 });

 // ==================== 高级设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "connection-timeout",
 ConfigFileName = file,
 DisplayName = "连接超时",
 Description = "与后端服务器建立连接的超时时间（毫秒）\n超时后尝试下一个后端或断开玩家",
 Category = "高级设置",
 DefaultValue = "5000",
 MinValue = 100,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "read-timeout",
 ConfigFileName = file,
 DisplayName = "读取超时",
 Description = "与后端服务器通信的读取超时时间（毫秒）\n超时后断开与后端的连接",
 Category = "高级设置",
 DefaultValue = "30000",
 MinValue = 1000,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "haproxy-protocol",
 ConfigFileName = file,
 DisplayName = "HAProxy 协议",
 Description = "是否启用 HAProxy PROXY 协议\n使用 HAProxy / Cloudflare Spectrum 等负载均衡时开启\n获取玩家真实 IP",
 Category = "高级设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "tcp-fast-open",
 ConfigFileName = file,
 DisplayName = "TCP Fast Open",
 Description = "启用 TCP Fast Open 减少连接建立延迟\n需操作系统支持\nLinux 推荐开启",
 Category = "高级设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "bungee-plugin-message-channel",
 ConfigFileName = file,
 DisplayName = "BungeeCord 插件消息通道",
 Description = "兼容 BungeeCord 的插件消息通道\n使部分 BungeeCord 插件可在 Velocity 上运行",
 Category = "高级设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "show-ping-requests",
 ConfigFileName = file,
 DisplayName = "显示 Ping 请求",
 Description = "是否在控制台显示服务器列表 ping 请求日志\n调试时开启，正常使用关闭",
 Category = "高级设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "failover-on-unexpected-server-disconnect",
 ConfigFileName = file,
 DisplayName = "意外断连时故障转移",
 Description = "后端服务器意外断开时是否自动转移到下一个后端\n而非踢出玩家",
 Category = "高级设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "announce-proxy-commands",
 ConfigFileName = file,
 DisplayName = "宣告代理命令",
 Description = "是否向客户端发送代理命令列表\n关闭可隐藏 /server 等命令的自动补全",
 Category = "高级设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "log-command-executions",
 ConfigFileName = file,
 DisplayName = "记录命令执行",
 Description = "是否在控制台记录玩家执行的代理命令\n审计时开启",
 Category = "高级设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "log-player-connections",
 ConfigFileName = file,
 DisplayName = "记录玩家连接",
 Description = "是否在控制台记录玩家进入 / 离开代理的日志",
 Category = "高级设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== 查询协议 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "query-enabled",
 ConfigFileName = file,
 DisplayName = "启用查询协议",
 Description = "是否启用 MC Server Query（查询协议）\n允许外部工具查询服务器状态（玩家数、版本等）",
 Category = "查询协议",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "query-port",
 ConfigFileName = file,
 DisplayName = "查询端口",
 Description = "查询协议监听的端口\n通常与 bind 端口相同",
 Category = "查询协议",
 DefaultValue = "25577",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "query-map",
 ConfigFileName = file,
 DisplayName = "查询显示地图名",
 Description = "查询协议返回的世界名\n默认为 world",
 Category = "查询协议",
 DefaultValue = "world",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "query-show-plugins",
 ConfigFileName = file,
 DisplayName = "查询显示插件",
 Description = "查询协议是否返回插件列表\n️ 关闭可隐藏服务器插件信息",
 Category = "查询协议",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });
 }

 /// <summary>
 /// 注册 BungeeCord 专属配置文件 config.yml 的配置描述符
 /// </summary>
 private void RegisterBungeeCordConfigYml()
 {
 const string file = "config.yml";

 // ==================== 全局设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "ip_forward",
 ConfigFileName = file,
 DisplayName = "IP 转发",
 Description = "是否把玩家真实 IP 与 UUID 转发给后端子服\ntrue = 子服能看到玩家真实 IP（需配合子服 spigot.yml 的 bungeecord: true）\nfalse = 子服看到的 IP 是 127.0.0.1\n️ 开启后需要在后端服务器配置防火墙",
 Category = "全局设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "online_mode",
 ConfigFileName = file,
 DisplayName = "正版验证",
 Description = "是否要求玩家使用 Minecraft 正版账号登录\ntrue = 仅正版 / false = 支持离线模式\n️ 关闭后无法获取玩家真实 UUID",
 Category = "全局设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player_limit",
 ConfigFileName = file,
 DisplayName = "玩家上限",
 Description = "代理允许的最大在线玩家数\n-1 = 无限制\n实际人数仍受各后端限制",
 Category = "全局设置",
 DefaultValue = "-1",
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "log_pings",
 ConfigFileName = file,
 DisplayName = "记录 Ping 请求",
 Description = "是否在控制台记录服务器列表 ping 请求\n调试时开启",
 Category = "全局设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "log_commands",
 ConfigFileName = file,
 DisplayName = "记录命令",
 Description = "是否在控制台记录玩家执行的命令\n审计时开启",
 Category = "全局设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server_connect_timeout",
 ConfigFileName = file,
 DisplayName = "服务器连接超时",
 Description = "与后端服务器建立连接的超时时间（毫秒）",
 Category = "全局设置",
 DefaultValue = "5000",
 MinValue = 100,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "remote_ping_timeout",
 ConfigFileName = file,
 DisplayName = "远程 Ping 超时",
 Description = "Ping 后端服务器时的超时时间（毫秒）",
 Category = "全局设置",
 DefaultValue = "5000",
 MinValue = 100,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "remote_ping_cache",
 ConfigFileName = file,
 DisplayName = "远程 Ping 缓存",
 Description = "后端 Ping 结果的缓存时长（毫秒）\n减少对后端的 Ping 频率",
 Category = "全局设置",
 DefaultValue = "5000",
 MinValue = 0,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "timeout",
 ConfigFileName = file,
 DisplayName = "玩家超时",
 Description = "玩家无响应多久后断开连接（毫秒）\n调大可改善网络差玩家的体验",
 Category = "全局设置",
 DefaultValue = "30000",
 MinValue = 1000,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "preload_servers",
 ConfigFileName = file,
 DisplayName = "预加载服务器",
 Description = "代理启动时是否预先 ping 所有后端服务器\n减少玩家首次连接时的延迟",
 Category = "全局设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "max_players",
 ConfigFileName = file,
 DisplayName = "显示最大玩家数",
 Description = "在服务器列表中显示的最大玩家数\n与 player_limit 不同，仅作显示",
 Category = "全局设置",
 DefaultValue = "1",
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disabled_commands",
 ConfigFileName = file,
 DisplayName = "禁用命令列表",
 Description = "被禁用的代理命令列表\n如：[\"alert\", \"send\"]",
 Category = "全局设置",
 DefaultValue = "[]",
 ValueType = "string[]"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "servers.default",
 ConfigFileName = file,
 DisplayName = "默认后端服务器",
 Description = "玩家进入代理时默认连接的后端服务器\n必须在 servers 列表中已定义",
 Category = "后端服务器",
 DefaultValue = "lobby",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "servers.timeout",
 ConfigFileName = file,
 DisplayName = "后端服务器超时",
 Description = "连接后端服务器超时时间（毫秒）",
 Category = "后端服务器",
 DefaultValue = "1000",
 ValueType = "int"
 });

 // ==================== listeners 监听器 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "listeners.host",
 ConfigFileName = file,
 DisplayName = "监听地址",
 Description = "代理监听玩家连接的地址和端口\n格式：IP:端口\n如：0.0.0.0:25577",
 Category = "监听器",
 DefaultValue = "0.0.0.0:25577",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "listeners.max_players",
 ConfigFileName = file,
 DisplayName = "监听器最大玩家数",
 Description = "在服务器列表中显示的最大玩家数\n（监听器级别）",
 Category = "监听器",
 DefaultValue = "1",
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "listeners motd",
 ConfigFileName = file,
 DisplayName = "监听器 MOTD",
 Description = "玩家在服务器列表中看到的服务器描述\n支持 § 颜色代码\n两行用 \\n 分隔",
 Category = "监听器",
 DefaultValue = "&1Just another BungeeCord - Forced Host",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "listeners.force_default",
 ConfigFileName = file,
 DisplayName = "强制默认服务器",
 Description = "玩家每次进入代理是否强制送到默认后端\ntrue = 强制（忽略之前所在的后端）",
 Category = "监听器",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "listeners.tab_list",
 ConfigFileName = file,
 DisplayName = "Tab 列表模式",
 Description = "Tab 玩家列表的显示模式\nGLOBAL = 显示所有子服玩家\nSERVER = 仅显示当前子服玩家\nGLOBAL_PING = 全局并显示延迟",
 Category = "监听器",
 DefaultValue = "GLOBAL_PING",
 AllowedValues = ["GLOBAL", "SERVER", "GLOBAL_PING"],
 ValueType = "enum"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "listeners.query_enabled",
 ConfigFileName = file,
 DisplayName = "启用查询协议",
 Description = "是否启用 MC Server Query\n允许外部工具查询服务器状态",
 Category = "监听器",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "listeners.query_port",
 ConfigFileName = file,
 DisplayName = "查询端口",
 Description = "查询协议监听的端口",
 Category = "监听器",
 DefaultValue = "25577",
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "listeners.proxy_protocol",
 ConfigFileName = file,
 DisplayName = "PROXY 协议",
 Description = "是否启用 HAProxy PROXY 协议\n使用 HAProxy 等负载均衡时开启",
 Category = "监听器",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== permissions 权限 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "permissions.default",
 ConfigFileName = file,
 DisplayName = "默认玩家权限",
 Description = "默认玩家拥有的代理命令权限\n如：[\"bungeecord.command.server\", \"bungeecord.command.list\"]",
 Category = "权限",
 DefaultValue = "[\"bungeecord.command.server\", \"bungeecord.command.list\"]",
 ValueType = "string[]"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "permissions.admin",
 ConfigFileName = file,
 DisplayName = "管理员权限",
 Description = "管理员拥有的代理命令权限\n如：[\"bungeecord.command.alert\", \"bungeecord.command.end\", \"bungeecord.command.ip\", \"bungeecord.command.reload\"]",
 Category = "权限",
 DefaultValue = "[\"bungeecord.command.alert\", \"bungeecord.command.end\", \"bungeecord.command.ip\", \"bungeecord.command.reload\"]",
 ValueType = "string[]"
 });
 }

 // ============================================================
 // 第一批：Folia 系派生核心专属配置
 // ============================================================

 /// <summary>
 /// 注册 Luminol 专属配置文件 luminol_config/luminol_global_config.toml 的配置描述符
 /// </summary>
 private void RegisterLuminolToml()
 {
 const string file = "luminol_global_config.toml";

 // ==================== 服务器品牌重写 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.server_mod_name.name",
 ConfigFileName = file,
 DisplayName = "服务器 Mod 名称",
 Description = "玩家按 F3 看到的服务器 Mod 名称\n设为 vanilla 可伪装成原版服务器\n用于绕过部分客户端 Mod 的服务端检测",
 Category = "服务器品牌",
 DefaultValue = "Luminol",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.server_mod_name.vanilla_spoof",
 ConfigFileName = file,
 DisplayName = "原版伪装",
 Description = "是否将服务器在网络协议中伪装成原版 vanilla\n开启后部分客户端反作弊 Mod 会将服务器视为原版\n️ 可能与依赖服务端品牌识别的插件冲突",
 Category = "服务器品牌",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== 聊天校验 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.chat.chat_check",
 ConfigFileName = file,
 DisplayName = "聊天签名校验",
 Description = "是否校验玩家聊天消息的签名\n关闭后服务器不再验证消息签名真伪\n可改善离线模式或第三方客户端的聊天兼容性\n️ 关闭后无法检测伪造消息",
 Category = "聊天",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.chat.only_aura_real_player",
 ConfigFileName = file,
 DisplayName = "仅光环真实玩家",
 Description = "是否只对真实玩家应用光环效果\n可用于过滤假人产生的光环",
 Category = "聊天",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== TPS 状态条 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.tpsbar.enabled",
 ConfigFileName = file,
 DisplayName = "启用 Tpsbar",
 Description = "是否默认为所有玩家启用 Tpsbar\n玩家可用 /tpsbar 命令切换个人状态",
 Category = "Tpsbar",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.tpsbar.color",
 ConfigFileName = file,
 DisplayName = "Boss 条颜色",
 Description = "Tpsbar Boss 条的基础颜色\n部分实现会根据 TPS 高低自动切换颜色\n此值作为默认 / 最佳状态颜色",
 Category = "Tpsbar",
 DefaultValue = "GREEN",
 AllowedValues = ["PINK", "BLUE", "RED", "GREEN", "YELLOW", "PURPLE", "WHITE"],
 ValueType = "enum"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.tpsbar.style",
 ConfigFileName = file,
 DisplayName = "Boss 条样式",
 Description = "Boss 条的进度条样式\nNOTCHED_20 表示分成 20 段（对应 20 TPS）",
 Category = "Tpsbar",
 DefaultValue = "NOTCHED_20",
 AllowedValues = ["PROGRESS", "NOTCHED_6", "NOTCHED_10", "NOTCHED_12", "NOTCHED_20"],
 ValueType = "enum"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.tpsbar.progress",
 ConfigFileName = file,
 DisplayName = "进度来源",
 Description = "Boss 条进度依据的指标\nTPS = 按每秒 tick 数（0-20 映射到 0-100%）\nMSPT = 按每 tick 毫秒数（0-50ms 映射到 100%-0%）",
 Category = "Tpsbar",
 DefaultValue = "MSPT",
 AllowedValues = ["TPS", "MSPT"],
 ValueType = "enum"
 });

 // ==================== 原版特性修复 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "fixes.allow_void_trade.enabled",
 ConfigFileName = file,
 DisplayName = "允许虚空交易",
 Description = "是否允许玩家在虚空（y < 0 或维度外）与村民交易\n原版默认禁止\n开启后可恢复早期版本的虚空交易行为",
 Category = "原版修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fixes.sand_duplication.enabled",
 ConfigFileName = file,
 DisplayName = "允许刷沙",
 Description = "是否恢复原版沙子 / 沙砾 duplication bug\n（沙子落入末地传送门时复制）\n生电玩家常用\n️ 会破坏服务器经济平衡，谨慎开启",
 Category = "原版修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fixes.gravel_duplication.enabled",
 ConfigFileName = file,
 DisplayName = "允许刷沙砾",
 Description = "恢复沙砾的 duplication bug\n生电玩家常用\n️ 易被滥用",
 Category = "原版修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fixes.tnt_duplication.enabled",
 ConfigFileName = file,
 DisplayName = "允许 TNT 复制",
 Description = "恢复原版 TNT duplication bug\n（TNT 在传送门 / 活塞推动时复制）\n生电玩家常用\n️ 易被滥用",
 Category = "原版修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fixes.ender_dragon_escape_fix.enabled",
 ConfigFileName = file,
 DisplayName = "末影龙逃逸修复",
 Description = "是否修复末影龙飞出末地主岛边界的 bug\n开启后末影龙将被限制在末地中心区域活动",
 Category = "原版修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fixes.entity_collision.enabled",
 ConfigFileName = file,
 DisplayName = "实体挤压修复",
 Description = "是否修复多个实体挤压进入同一方块导致的崩溃 / 推动异常\n开启后实体的挤压行为更接近原版",
 Category = "原版修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 // ==================== 性能优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "performance.region_tick.optimize_tick_occupancy",
 ConfigFileName = file,
 DisplayName = "优化 Tick 占用率",
 Description = "优化区域 tick 任务的线程分配\n让空闲线程接管更多区域任务\n开启后可提升多核 CPU 利用率\n️ 仅启动时读取",
 Category = "性能",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance.region_tick.max_tick_time",
 ConfigFileName = file,
 DisplayName = "单区域最大 tick 时长",
 Description = "单个区域单次 tick 允许的最大耗时（毫秒）\n超出此值的区域将被记录警告\n50ms 对应 20 TPS 上限",
 Category = "性能",
 DefaultValue = "50",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance.chunk_load.async_chunk_load",
 ConfigFileName = file,
 DisplayName = "异步区块加载",
 Description = "启用异步区块加载（Folia 默认已异步）\n此项为额外的优化开关",
 Category = "性能",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance.chunk_load.max_chunk_load_per_tick",
 ConfigFileName = file,
 DisplayName = "每 tick 最大加载区块数",
 Description = "每个游戏 tick 内最多加载多少个区块\n避免瞬间加载大量区块导致卡顿",
 Category = "性能",
 DefaultValue = "100",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== 区域文件格式 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "regions.use_linear_region",
 ConfigFileName = file,
 DisplayName = "使用线性区域文件",
 Description = "启用线性区域文件格式（.linear）\n相比 MCA 格式可减少 50-70% 文件大小\n️ 已有 MCA 文件需通过工具转换",
 Category = "区域文件",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "regions.linear_region_compression",
 ConfigFileName = file,
 DisplayName = "线性区域压缩算法",
 Description = "线性区域文件使用的压缩算法\nZSTD（推荐）：压缩率高、解压快\nGZIP：兼容性好\nNONE：不压缩（最大文件，最快读写）",
 Category = "区域文件",
 DefaultValue = "ZSTD",
 AllowedValues = ["ZSTD", "GZIP", "NONE"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "regions.linear_region_buffer_size",
 ConfigFileName = file,
 DisplayName = "线性区域缓冲区大小",
 Description = "线性区域文件读写缓冲区大小（字节）\n较大的缓冲区可减少 IO 次数但占用更多内存\n1MB = 1048576",
 Category = "区域文件",
 DefaultValue = "1048576",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "regions.mca_region_auto_convert",
 ConfigFileName = file,
 DisplayName = "自动转换 MCA 区域",
 Description = "是否在加载时自动将旧的 MCA 区域文件转换为线性格式\n开启后服务器启动可能较慢",
 Category = "区域文件",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 命令权限 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "commands.luminol_reload_permission",
 ConfigFileName = file,
 DisplayName = "/luminol reload 权限",
 Description = "执行 /luminol reload 命令所需的权限节点",
 Category = "命令权限",
 DefaultValue = "luminol.reload",
 ValueType = "string"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "commands.tpsbar_permission",
 ConfigFileName = file,
 DisplayName = "/tpsbar 权限",
 Description = "执行 /tpsbar 命令所需的权限节点",
 Category = "命令权限",
 DefaultValue = "luminol.tpsbar",
 ValueType = "string"
 });

 // ==================== 安全设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.security.disable_book_exploit",
 ConfigFileName = file,
 DisplayName = "禁用书本漏洞",
 Description = "是否禁用书本复制 / 注入漏洞\n建议保持开启",
 Category = "安全",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.security.max_book_pages",
 ConfigFileName = file,
 DisplayName = "书本最大页数",
 Description = "单本书允许的最大页数\n防止恶意玩家发送超大书本导致卡顿",
 Category = "安全",
 DefaultValue = "100",
 MinValue = 1,
 ValueType = "int"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.security.max_book_chars",
 ConfigFileName = file,
 DisplayName = "书本最大字符数",
 Description = "单本书允许的最大字符总数",
 Category = "安全",
 DefaultValue = "50000",
 MinValue = 1,
 ValueType = "int"
 });

 // ==================== 调试 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.debug.enable_region_debug",
 ConfigFileName = file,
 DisplayName = "启用区域调试",
 Description = "是否输出区域 tick / 调度的详细调试日志\n仅排查问题时开启，正常使用请关闭",
 Category = "调试",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc.debug.enable_thread_debug",
 ConfigFileName = file,
 DisplayName = "启用线程调试",
 Description = "是否输出线程池调度的详细调试日志",
 Category = "调试",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool"
 });
 }
// -----------------------------------------------------------------------------
// 文件名: RegisterFoliaGlobalYml.cs
// 功能描述: 注册 Folia 配置文件的描述符
// ️ Folia 不存在独立的 config/folia-global.yml，所有 Folia 新增多线程
// 区域配置（ThreadedRegions）直接追加到 Paper 的 config/paper-global.yml
// 本文件仅注册 Folia 新增的 threaded-regions 节 + Folia 部署高频调优项
// 数据来源: PaperMC/Folia folia-server/paper-patches/features/0001-Region-Threading-Base.patch
// （commit e48800d，Folia 26.x）+ 官方 FAQ 线程分配建议
// 适用版本: Folia 1.20.4+ / 26.x
// -----------------------------------------------------------------------------

private void RegisterFoliaGlobalYml()
{
 // ️ Folia 配置追加到 paper-global.yml，不存在独立的 folia-global.yml
 const string file = "config/paper-global.yml";

 // ==================== threaded-regions（Folia 新增：多线程核心） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "threaded-regions.threads",
 ConfigFileName = file,
 DisplayName = "区域 tick 线程数",
 Description = "区域 tick 循环所使用的线程池大小\n-1 = 自动（根据可用 CPU 计算）\n手动设置：建议设为「物理核心数 − Netty IO − 区块 IO − 区块工作 − GC 并发」后的剩余值\n️ 所有可配置线程总和不应超过物理核心数的 80%\n例：32 核 / 500 人服可设约 10\nFolia 多线程核心配置，性能调优第一项",
 Category = "线程化区域",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "threaded-regions.grid-exponent",
 ConfigFileName = file,
 DisplayName = "区域网格指数",
 Description = "控制区域划分的网格粒度\n每个网格单元边长 = 2^gridExponent 个区块\n默认 4 = 16 区块边长（256 区块为一网格单元）\n值越大区域越大、并行度越低；值越小区域越碎、并行度越高但跨区域开销越大\n️ 非高级用户请勿修改，错误值会显著降低性能",
 Category = "线程化区域",
 DefaultValue = "4",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "threaded-regions.scheduler",
 ConfigFileName = file,
 DisplayName = "区域调度算法",
 Description = "区域 tick 任务的调度策略\nEDF = Earliest Deadline First（最早截止期优先），按 tick 截止时间排序优先调度最紧迫的区域\n目前仅 EDF 一种已实现值",
 Category = "线程化区域",
 DefaultValue = "EDF",
 AllowedValues = ["EDF"],
 ValueType = "enum",
 RequiresRestart = true
 });

 // ==================== chunk-system（Paper 继承：Folia 需重新分配预算） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-system.io-threads",
 ConfigFileName = file,
 DisplayName = "区块 IO 线程数",
 Description = "负责从磁盘读写区块文件的线程数\nFolia 官方建议：每 200-300 名玩家约 3 个\n预生成世界后可适当下调\n需计入 80% 总线程预算",
 Category = "区块系统",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-system.worker-threads",
 ConfigFileName = file,
 DisplayName = "区块工作线程数",
 Description = "负责区块生成 / 装饰计算的线程数\nFolia 官方建议：预生成后每 200-300 名玩家约 2 个\n未预生成时需大幅增加（曾测试 16 线程仍偏慢）\n需计入 80% 总线程预算\n强烈建议上线前用 Chunky 预生成世界",
 Category = "区块系统",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== misc（Paper 继承：杂项） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc.region-file-cache-size",
 ConfigFileName = file,
 DisplayName = "区域文件缓存大小",
 Description = "缓存的 Region 文件（.mca）句柄数\n大型世界 / 玩家分散时调大（如 512）可减少磁盘 IO\n但占用更多内存",
 Category = "杂项",
 DefaultValue = "256",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== proxies.velocity（Paper 继承：Velocity 代理） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "proxies.velocity.enabled",
 ConfigFileName = file,
 DisplayName = "启用 Velocity 转发",
 Description = "是否启用 Velocity 现代转发（modern forwarding）\n启用后玩家信息由 Velocity 转发，Folia 侧 server.properties 的 online-mode 应设为 false\n前置 Velocity 代理时开启",
 Category = "代理",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "proxies.velocity.secret",
 ConfigFileName = file,
 DisplayName = "Velocity 共享密钥",
 Description = "与 Velocity forwarding.secret 一致的密钥，用于验证代理身份\n️ 生产环境必须设置强密钥，留空则任何人都可伪造玩家身份\n留空 = 禁用",
 Category = "代理",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "proxies.velocity.online-mode",
 ConfigFileName = file,
 DisplayName = "在线模式（Velocity 侧）",
 Description = "表示 Velocity 是否已做 Mojang 正版验证\n设为 true 时 Folia 信任 Velocity 转发的正版身份",
 Category = "代理",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== spigot.yml（Netty 线程，Folia 调优相关） ====================
 // 注意：此项在 spigot.yml，但与 Folia 线程分配强相关，故在此注册

 Register(new ServerConfigDescriptor
 {
 Key = "settings.netty-threads",
 ConfigFileName = "spigot.yml",
 DisplayName = "Netty IO 线程数",
 Description = "处理玩家网络数据包的 Netty 线程数\nFolia 官方建议：每 200-300 名玩家约 4 个\n500 人服可设 8\n需计入 80% 总线程预算\n️ 注意此项在 spigot.yml 而非 paper-global.yml",
 Category = "网络",
 DefaultValue = "4",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterKaiijuYml.cs
// 功能描述: 注册 Kaiiju（基于 Folia 的原版/无政府服分支）配置文件的描述符
// 包含 kaiiju.yml 全局节 + 每世界节
// 数据来源: KaiijuMC/Kaiiju README.md (ver/1.20.1, build #240) + Configuration Wiki
// 适用版本: Kaiiju 1.20.1（项目已 Public archive，停更）
// -----------------------------------------------------------------------------

private void RegisterKaiijuYml()
{
 const string file = "kaiiju.yml";

 // ==================== region-format.linear（全局：线性格式刷新） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "region-format.linear.flush-frequency",
 ConfigFileName = file,
 DisplayName = "线性文件刷新频率",
 Description = "多久将内存中的线性 Region 数据刷新到磁盘一次（秒）\n值越小越频繁、崩服丢数据越少但 IO 越多\n值越大越省 IO 但丢数据风险越高",
 Category = "线性格式",
 DefaultValue = "10",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "region-format.linear.flush-max-threads",
 ConfigFileName = file,
 DisplayName = "刷新最大线程数",
 Description = "刷新线性 Region 文件时使用的最大线程数\n1 = 单线程刷新（安全）\n增大可加快刷新但增加磁盘 IO 争用",
 Category = "线性格式",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== network（全局：网络） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "network.send-null-entity-packets",
 ConfigFileName = file,
 DisplayName = "发送空实体移动包",
 Description = "是否发送空移动实体数据包\n设为 false 可减少网络流量\n除非有插件依赖此行为，否则建议 false",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network.alternate-keepalive",
 ConfigFileName = file,
 DisplayName = "备用心跳机制",
 Description = "沿用 Purpur 的备用心跳：每秒发送一个 keepalive 包\n仅当 30 秒内无任何响应才踢出玩家\n可避免因偶发丢包导致的误踢\n玩家不会因为丢一个心跳包就被踢",
 Category = "网络",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network.kick-player-on-bad-packet",
 ConfigFileName = file,
 DisplayName = "收到坏包踢出玩家",
 Description = "收到损坏 / 非法数据包时是否踢出玩家\n设为 false 不踢（实验性，可能被恶意客户端利用）\n无政府服可考虑 false，正常服保持 true",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== optimization（全局：优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.disable-vanish-api",
 ConfigFileName = file,
 DisplayName = "禁用隐身 API",
 Description = "禁用 Bukkit 的 Player#hidePlayer / showPlayer 隐身 API\n无隐身需求的服务器可设 true 以节省性能",
 Category = "优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.disable-player-stats",
 ConfigFileName = file,
 DisplayName = "禁用玩家统计",
 Description = "禁用玩家统计信息（如走了多少格、挖了多少方块）的记录与持久化\n无政府 / 战斗服通常不需要统计，可设 true 提速",
 Category = "优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.disable-arm-swing-event",
 ConfigFileName = file,
 DisplayName = "禁用手臂挥动事件",
 Description = "不调用 PlayerArmSwingEvent\n若没有插件监听此事件（绝大多数服都没有），可设 true 减少事件开销",
 Category = "优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.async-path-processing.enable",
 ConfigFileName = file,
 DisplayName = "启用异步寻路",
 Description = "是否启用异步寻路处理\n️ 修改必须重启，热重载无效\n开启后实体寻路移至异步线程池，可显著降低主线程负载\nKaiiju 修复并重构了 Petal 的异步寻路",
 Category = "优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.async-path-processing.max-threads",
 ConfigFileName = file,
 DisplayName = "异步寻路最大线程数",
 Description = "寻路线程池最大线程数\n0 = 自动 (max(核心数/4, 1))\n负数 -n = max(核心数 − n, 1)\n正数 = 固定值\n允许线程池在突发负载时临时扩张到该上限",
 Category = "优化",
 DefaultValue = "0",
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.async-path-processing.keepalive",
 ConfigFileName = file,
 DisplayName = "空闲线程存活时间",
 Description = "当线程数超过核心池大小时，多余空闲线程的存活秒数\n短存活时间可快速回收多余线程，长存活时间可应对频繁突发",
 Category = "优化",
 DefaultValue = "60",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.async-path-processing.queue-capacity",
 ConfigFileName = file,
 DisplayName = "任务队列容量",
 Description = "寻路任务等待队列的最大长度\n队列满后才会创建新线程（直到 max-threads）\n大队列可吸收突发任务而不创建过多线程，但会增加延迟",
 Category = "优化",
 DefaultValue = "4096",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== gameplay（全局：玩法） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay.server-mod-name",
 ConfigFileName = file,
 DisplayName = "服务端名称",
 Description = "发送给客户端的服务端品牌名（F3 界面显示的 Mod 字段）\n可用于品牌定制或隐藏真实核心类型",
 Category = "玩法",
 DefaultValue = "Kaiiju",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay.shared-random-for-players",
 ConfigFileName = file,
 DisplayName = "玩家共享随机源",
 Description = "玩家共用同一个随机数生成器，而非每个玩家独立 RNG\n这是原版 RNG 操纵（RNG manipulation）的关键\n开启时所有玩家共享 RNG，可被用于预测 / 操纵随机事件（如掉落、生物生成）\n无政府服保持 true 以允许 RNG 控制",
 Category = "玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== unsupported（全局：不安全实验） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "unsupported.disable-ensure-tick-thread-checks",
 ConfigFileName = file,
 DisplayName = "禁用线程检查",
 Description = "禁用 Folia 的「确保在正确 tick 线程」安全检查\n️ 绝对不要开启，会导致数据竞争与崩溃\n仅用于调试",
 Category = "不安全实验",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "unsupported.global-event-synchronization",
 ConfigFileName = file,
 DisplayName = "全局事件同步",
 Description = "启用全局事件同步锁\n会显著降低多线程性能，仅用于排查事件竞态问题",
 Category = "不安全实验",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== world-settings.default.region-format（每世界：区域文件格式） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.region-format.format",
 ConfigFileName = file,
 DisplayName = "区域文件格式",
 Description = "世界在磁盘上使用的 Region 文件格式\nANVIL = Minecraft 原生 .mca 格式（兼容性最好）\nLINEAR = Xymb 线性格式（主世界/下界省 ~50% 磁盘，末地省 ~95%）\n️ Linear 与 ANVIL 不兼容，切换前必须用 LinearRegionFileFormatTools 转换数据，否则世界会丢失",
 Category = "世界-区域格式",
 DefaultValue = "ANVIL",
 AllowedValues = ["ANVIL", "LINEAR"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.region-format.linear.compression-level",
 ConfigFileName = file,
 DisplayName = "Linear 压缩级别",
 Description = "Linear 格式使用的 ZSTD 压缩级别\n推荐 1 / 3 / 6\n级别越高磁盘越省但 CPU 越高\n实测：级别 1 总占用 7.88GB，级别 6 仅 6.59GB（省约 16%）",
 Category = "世界-区域格式",
 DefaultValue = "1",
 MinValue = 1,
 MaxValue = 22,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.region-format.linear.crash-on-broken-symlink",
 ConfigFileName = file,
 DisplayName = "符号链接损坏时崩溃",
 Description = "当 Region 文件的符号链接损坏时是否让服务器崩溃\ntrue（推荐）= 崩溃以暴露问题\nfalse = 静默跳过\n通过 NFS 访问 Region 文件时建议 true",
 Category = "世界-区域格式",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== world-settings.default.optimization（每世界：优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimization.shulker-box-drop-contents-when-destroyed",
 ConfigFileName = file,
 DisplayName = "潜影盒被毁掉落内容",
 Description = "潜影盒被熔岩 / 仙人掌等摧毁时，是否掉落其内部物品\ntrue = 原版行为\nfalse = 内容物一并销毁",
 Category = "世界-优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimization.optimize-hoppers",
 ConfigFileName = file,
 DisplayName = "漏斗优化",
 Description = "启用 Paper 的漏斗优化\nfalse 可还原 100% 原版漏斗行为，但会破坏大量生电红石机器\n生电服可考虑 false",
 Category = "世界-优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimization.tick-when-empty",
 ConfigFileName = file,
 DisplayName = "空世界仍 tick",
 Description = "世界无玩家时是否仍进行 tick（实体、红石等）\nfalse = 无玩家时世界冻结，省 CPU 但红石机器会停",
 Category = "世界-优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimization.enable-entity-throttling",
 ConfigFileName = file,
 DisplayName = "实体节流",
 Description = "启用实体数量节流\n开启后超限的实体会被限制 / 移除\n具体限制在 kaiiju-entity-limits.yml 中配置",
 Category = "世界-优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimization.disable-achievements",
 ConfigFileName = file,
 DisplayName = "禁用成就",
 Description = "禁用成就 / 进度系统的触发与记录\n无政府服可设 true 提速",
 Category = "世界-优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimization.disable-creatures-spawn-events",
 ConfigFileName = file,
 DisplayName = "禁用生物生成事件",
 Description = "不触发 CreatureSpawnEvent\n无插件监听时可设 true 减少事件开销\n但反作弊 / 限制类插件会失效",
 Category = "世界-优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimization.disable-dolphin-swim-to-treasure",
 ConfigFileName = file,
 DisplayName = "禁用海豚寻宝",
 Description = "禁用海豚引导玩家寻找沉船 / 海底废墟的行为\n可减少海豚寻路计算开销",
 Category = "世界-优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.gameplay（每世界：玩法 / 漏洞开关） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.gameplay.fix-void-trading",
 ConfigFileName = file,
 DisplayName = "修复虚空交易",
 Description = "是否修复虚空交易漏洞\ntrue（默认）= 修复\nfalse = 允许虚空交易\n若关闭，建议安装 Kaiivoid 插件替代",
 Category = "世界-玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.gameplay.break-redstone-on-top-of-trap-doors-early",
 ConfigFileName = file,
 DisplayName = "提前破坏活板门上红石",
 Description = "始终提前破坏活板门上的红石\nfalse 会允许「门切片（portal slicing）」与活板门卡服机器\n生电服可设 false 还原漏洞",
 Category = "世界-玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.gameplay.fix-tripwire-state-inconsistency",
 ConfigFileName = file,
 DisplayName = "修复绊线状态不一致",
 Description = "修复绊线状态不一致\nfalse 会启用线复制漏洞，并允许末地黑曜石平台抑制",
 Category = "世界-玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.gameplay.safe-teleportation",
 ConfigFileName = file,
 DisplayName = "安全传送",
 Description = "true = 末地传送门只传送活着的实体（修复刷沙）\nfalse = 允许末地传送门传送已移除的实体（刷沙前置）\n要开启刷沙必须设为 false",
 Category = "世界-玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.gameplay.sand-duplication",
 ConfigFileName = file,
 DisplayName = "沙子复制",
 Description = "允许沙子复制漏洞\n️ 前置条件：必须同时将 safe-teleportation 设为 false 才能生效\n无政府刷沙服开启",
 Category = "世界-玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.gameplay.teleport-async-on-high-velocity",
 ConfigFileName = file,
 DisplayName = "高速时异步传送",
 Description = "玩家高速移动（高速度）时使用异步传送\n实验性，可能改善高速场景下的传送稳定性",
 Category = "世界-玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterNachoYml.cs
// 功能描述: 注册 NachoSpigot（基于 TacoSpigot 1.8.9）配置文件的描述符
// 包含 nacho.yml 全局 settings 节 + 每世界 world-settings 节（共 56 项）
// 数据来源: CobbleSword/NachoSpigot README.md (master, commit 5655b72) + 社区默认 nacho.yml
// 适用版本: NachoSpigot 1.8.9（项目已停更，2022 年最后构建）
// -----------------------------------------------------------------------------

private void RegisterNachoYml()
{
 const string file = "nacho.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "config-version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "内部使用，不要手动修改\nNachoSpigot 用它做配置自动升级与兼容性判断",
 Category = "信息",
 DefaultValue = "6",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== settings.chunk（区块线程） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.chunk.threads",
 ConfigFileName = file,
 DisplayName = "区块线程数",
 Description = "用于区块加载 / 生成的线程数\n0 = 禁用多线程区块\n建议 2-4\n值越大区块加载越快但 CPU 越高",
 Category = "区块",
 DefaultValue = "2",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.chunk.players-per-thread",
 ConfigFileName = file,
 DisplayName = "每线程玩家数",
 Description = "每多少名玩家分配 1 个区块线程（与 threads 配合的负载估算参数）",
 Category = "区块",
 DefaultValue = "50",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== settings（全局杂项） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.player-time-statistics-interval",
 ConfigFileName = file,
 DisplayName = "玩家统计间隔",
 Description = "多久统计一次玩家在线时间等数据（tick）\n20 tick = 1 秒，90 = 4.5 秒\n值越大越省 CPU 但统计精度越低",
 Category = "全局",
 DefaultValue = "90",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.panda-wire",
 ConfigFileName = file,
 DisplayName = "Panda 红石线优化",
 Description = "启用 PandaSpigot 的红石线优化\n可显著降低红石密集场景的 CPU 占用\n生电服可能需要 false 还原原版时序",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.brand-name",
 ConfigFileName = file,
 DisplayName = "服务端品牌名",
 Description = "发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码\n可隐藏真实核心类型\n建议改为通用名以防信息泄露",
 Category = "全局",
 DefaultValue = "NachoSpigot",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.anti-malware",
 ConfigFileName = file,
 DisplayName = "反恶意软件扫描",
 Description = "启动时扫描插件 jar 是否包含已知恶意代码特征\n开发 / 测试服可开启\n生产服按需",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.disabled-block-fall-animation",
 ConfigFileName = file,
 DisplayName = "禁用方块下落动画",
 Description = "禁用方块（如沙子、砂砾）下落时的客户端动画\ntrue 可减少网络包但视觉体验下降",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.patch-protocollib",
 ConfigFileName = file,
 DisplayName = "修补 ProtocolLib",
 Description = "应用 ProtocolLib 兼容性补丁\n使用 ProtocolLib 的服建议保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.stop-notify-bungee",
 ConfigFileName = file,
 DisplayName = "停止 Bungee 通知",
 Description = "不向 BungeeCord 发送服务器状态通知\n可减少跨服通信开销",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.anti-crash",
 ConfigFileName = file,
 DisplayName = "反崩溃保护",
 Description = "启用反崩溃机制，捕获并阻止可能导致服务器崩溃的异常操作\n生产服保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.fast-operators",
 ConfigFileName = file,
 DisplayName = "快速 OP 操作",
 Description = "优化 OP 权限检查的性能\nOP 较多的服可开启以加速权限判定",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.save-empty-scoreboard-teams",
 ConfigFileName = file,
 DisplayName = "保存空记分板队伍",
 Description = "是否保存空的记分板队伍到磁盘\nfalse 可减少无意义的队伍数据写入",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.kick-on-illegal-behavior",
 ConfigFileName = file,
 DisplayName = "非法行为踢出",
 Description = "玩家执行非法操作（如发包作弊）时是否踢出\n反作弊相关，生产服保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.stop-decoding-itemstack-on-place",
 ConfigFileName = file,
 DisplayName = "放置时不解码物品",
 Description = "放置方块时跳过 ItemStack 的重复解码\n可减少 CPU 开销，正常服保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.use-tcp-nodelay",
 ConfigFileName = file,
 DisplayName = "启用 TCP_NODELAY",
 Description = "启用 TCP_NODELAY 禁用 Nagle 算法，降低网络延迟\nPvP 服强烈建议 true\n修改需重启",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.faster-cannon-tracker",
 ConfigFileName = file,
 DisplayName = "快速炮弹追踪",
 Description = "优化 TNT / 炮弹实体的追踪性能\nTNT 大炮服保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.fix-eat-while-running",
 ConfigFileName = file,
 DisplayName = "修复跑动进食",
 Description = "修复玩家跑动时进食的漏洞\nPvP 服保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.hide-projectiles-from-hidden-players",
 ConfigFileName = file,
 DisplayName = "隐藏玩家对隐藏玩家发射弹射物",
 Description = "被隐藏的玩家发射的弹射物对其他玩家也不可见\n隐身插件相关",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.lag-compensated-potions",
 ConfigFileName = file,
 DisplayName = "卡顿补偿药水",
 Description = "启用卡顿补偿的药水效果计算\n实验性，可能影响 PvP 平衡",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.smooth-potting",
 ConfigFileName = file,
 DisplayName = "平滑投掷药水",
 Description = "平滑投掷药水的动画 / 时机\nPvP 服保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.anti-enderpearl-glitch",
 ConfigFileName = file,
 DisplayName = "防末影珍珠漏洞",
 Description = "防止末影珍珠传送漏洞\nPvP 服保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.disable-infinisleeper-thread-usage",
 ConfigFileName = file,
 DisplayName = "禁用 Infinisleeper 线程",
 Description = "禁用 Infinisleeper 后台线程\n一般保持 false",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.enable-fastmath",
 ConfigFileName = file,
 DisplayName = "启用 FastMath",
 Description = "使用更快的数学运算库替代原版\n实验性，可能影响某些计算精度",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.tile-entity-ticking-time",
 ConfigFileName = file,
 DisplayName = "方块实体 tick 时间",
 Description = "方块实体（如熔炉、漏斗）的 tick 间隔（tick）\n20 = 每 20 tick（1 秒）处理一次\n值越大越省 CPU 但方块实体变慢",
 Category = "全局",
 DefaultValue = "20",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.item-dirty-ticks",
 ConfigFileName = file,
 DisplayName = "物品脏标记 tick",
 Description = "多久标记一次物品栏为「脏」以同步给客户端\n值越大网络包越少但物品栏更新越慢",
 Category = "全局",
 DefaultValue = "20",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.use-tcp-fastopen",
 ConfigFileName = file,
 DisplayName = "启用 TCP Fast Open",
 Description = "启用 TCP Fast Open（TFO）减少握手延迟\n需操作系统与内核支持\n修改需重启",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.tcp-fastopen-mode",
 ConfigFileName = file,
 DisplayName = "TCP Fast Open 模式",
 Description = "TFO 模式\n0 = 禁用\n1 = 仅客户端模式\n2 = 仅服务端模式\n3 = 双向启用\n修改需重启",
 Category = "全局",
 DefaultValue = "1",
 MinValue = 0,
 MaxValue = 3,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.enable-protocollib-shim",
 ConfigFileName = file,
 DisplayName = "启用 ProtocolLib 垫片",
 Description = "启用 ProtocolLib 兼容垫片\n使用 ProtocolLib 的服保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.instant-interaction",
 ConfigFileName = file,
 DisplayName = "瞬时交互",
 Description = "跳过交互延迟检查\ntrue 可能影响反作弊\n一般保持 false",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.instant-use-entity",
 ConfigFileName = file,
 DisplayName = "瞬时实体使用",
 Description = "跳过实体使用延迟检查\ntrue 可能影响反作弊\n一般保持 false",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== settings.commands（命令开关） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.commands.enable-version-command",
 ConfigFileName = file,
 DisplayName = "启用 /version 命令",
 Description = "是否允许玩家使用 /version（/ver）查看服务端版本信息\n关闭可隐藏核心类型，防信息泄露",
 Category = "命令",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.commands.enable-plugins-command",
 ConfigFileName = file,
 DisplayName = "启用 /plugins 命令",
 Description = "是否允许玩家使用 /plugins（/pl）查看已加载插件列表\n公网服建议关闭以防泄露插件信息",
 Category = "命令",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.commands.enable-reload-command",
 ConfigFileName = file,
 DisplayName = "启用 /reload 命令",
 Description = "是否允许使用 /reload 命令\n/reload 易导致插件状态异常，强烈建议关闭",
 Category = "命令",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== settings.event（事件开关） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.event.fire-entity-explode-event",
 ConfigFileName = file,
 DisplayName = "触发实体爆炸事件",
 Description = "是否触发 EntityExplodeEvent\n无插件监听时可设 false 减少开销，但爆炸保护插件会失效",
 Category = "事件",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.event.fire-player-move-event",
 ConfigFileName = file,
 DisplayName = "触发玩家移动事件",
 Description = "是否触发 PlayerMoveEvent\n️ 设为 false 会破坏大量插件（区域保护、反作弊等）\n仅极度追求性能且无移动相关插件时才可关",
 Category = "事件",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.event.fire-leaf-decay-event",
 ConfigFileName = file,
 DisplayName = "触发树叶凋落事件",
 Description = "是否触发 LeavesDecayEvent\n无插件监听时可设 false 减少开销",
 Category = "事件",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== settings.fixed-pools（固定对象池） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.fixed-pools.use-fixed-pools-for-explosions",
 ConfigFileName = file,
 DisplayName = "爆炸用固定池",
 Description = "爆炸计算使用固定大小的对象池，避免频繁 GC\nTNT 密集服（如 TNT 大炮）可设 true 减少卡顿",
 Category = "对象池",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.fixed-pools.size",
 ConfigFileName = file,
 DisplayName = "固定池大小",
 Description = "固定对象池的容量\n需大于同时进行的爆炸计算数，过小会回退到普通分配",
 Category = "对象池",
 DefaultValue = "500",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 // ==================== world-settings.default（每世界杂项） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.verbose",
 ConfigFileName = file,
 DisplayName = "详细日志",
 Description = "是否在世界启动时输出该世界配置的详细信息\n排查问题可临时开启",
 Category = "世界-杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.enable-lava-to-cobblestone",
 ConfigFileName = file,
 DisplayName = "岩浆变圆石",
 Description = "允许水流接触岩浆生成圆石（原版行为）\nfalse 可禁用以减少圆石农场卡服",
 Category = "世界-杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.infinite-water-sources",
 ConfigFileName = file,
 DisplayName = "无限水源",
 Description = "允许 2x2 水池形成无限水源（原版行为）\nfalse 可禁用以限制水农场",
 Category = "世界-杂项",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.disable-sponge-absorption",
 ConfigFileName = file,
 DisplayName = "禁用海绵吸水",
 Description = "禁用海绵吸水行为\ntrue 可减少大量吸水计算的开销",
 Category = "世界-杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.tick-enchantment-tables",
 ConfigFileName = file,
 DisplayName = "附魔台 tick",
 Description = "是否 tick 附魔台（周围书架的浮动书页动画）\nfalse 跳过此 tick 以省 CPU\n对应补丁 Nacho-0049",
 Category = "世界-杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.block-operations",
 ConfigFileName = file,
 DisplayName = "方块操作",
 Description = "启用方块操作批处理优化\n一般保持 true",
 Category = "世界-杂项",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.unload-chunks",
 ConfigFileName = file,
 DisplayName = "卸载区块",
 Description = "允许自动卸载无玩家附近的区块以释放内存\n内存紧张服保持 true",
 Category = "世界-杂项",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.physics（每世界物理） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.physics.disable-place",
 ConfigFileName = file,
 DisplayName = "禁用放置物理",
 Description = "放置方块时不触发物理更新（如沙子下落、红石更新）\n️ 会影响大量生电机制，仅极限性能服使用\n生电服请改为 false",
 Category = "世界-物理",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.physics.disable-update",
 ConfigFileName = file,
 DisplayName = "禁用更新物理",
 Description = "方块变化时不触发周边物理更新\n️ 与 disable-place 类似，会破坏红石与生电\n生电服请改为 false",
 Category = "世界-物理",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.explosions（每世界爆炸） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.explosions.constant-radius",
 ConfigFileName = file,
 DisplayName = "恒定爆炸半径",
 Description = "爆炸使用恒定半径而非随机半径\ntrue 使爆炸范围可预测，便于 PvP 平衡",
 Category = "世界-爆炸",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.explosions.explode-protected-regions",
 ConfigFileName = file,
 DisplayName = "受保护区域爆炸",
 Description = "是否在受保护区域（如 spawn 保护区）仍计算爆炸\nfalse 可跳过保护区爆炸以省 CPU",
 Category = "世界-爆炸",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.explosions.reduced-density-rays",
 ConfigFileName = file,
 DisplayName = "减少密度射线",
 Description = "减少爆炸密度射线计算量\ntrue 可显著降低 TNT 大量爆炸时的 CPU 占用，但爆炸破坏精度略降",
 Category = "世界-爆炸",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.entity（每世界实体） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entity.mob-ai",
 ConfigFileName = file,
 DisplayName = "生物 AI",
 Description = "️ 字段名易误解：\nfalse = 启用原版生物 AI\ntrue = 禁用生物 AI（生物静止不动）\n极限性能服才设 true",
 Category = "世界-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entity.mob-sound",
 ConfigFileName = file,
 DisplayName = "生物声音",
 Description = "️ 同上语义反转：\nfalse = 启用生物声音\ntrue = 禁用生物声音以省 CPU",
 Category = "世界-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entity.entity-activation",
 ConfigFileName = file,
 DisplayName = "实体激活",
 Description = "️ false = 启用原版实体激活范围\ntrue = 禁用激活范围（所有实体全 tick）\n一般保持 false",
 Category = "世界-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entity.endermite-spawning",
 ConfigFileName = file,
 DisplayName = "末影螨生成",
 Description = "是否允许末影螨生成\nfalse 禁用以减少末影珍珠农场产生的实体",
 Category = "世界-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterUSpigotYml.cs
// 功能描述: 注册 USpigot 配置文件的描述符
// ️ USpigot 无可访问的官方 GitHub 仓库与公开源码，仅在 MineBBS 等国内
// 社区以二进制 jar 分发。本文件所有配置项均为基于 Spigot/Paper 分支惯例
// 的推断项，未经官方源码核实。请勿作为权威依据。
// 数据来源: ️ 无官方源码；基于 NachoSpigot/Pufferfish 等同类分支命名惯例推断
// 适用版本: 未知（社区分发版本不一，无统一版本号）
// -----------------------------------------------------------------------------

private void RegisterUSpigotYml()
{
 // ️ USpigot 实际配置文件名未知，此处按 Spigot/Paper 分支惯例推断为 uspigot.yml
 // 实际可能为 u-spigot.yml、core.yml，或根本无独立配置文件（混入 spigot.yml）
 // 请以核心启动后生成的实际文件为准
 const string file = "uspigot.yml";

 // ==================== settings（基础设置 / 推断） ====================
 // ️ 以下 3 项均为基于同类分支惯例的推断项，可能与实际不符

 Register(new ServerConfigDescriptor
 {
 Key = "settings.brand-name",
 ConfigFileName = file,
 DisplayName = "服务端品牌名",
 Description = "️ 推断项，未经官方源码核实\n发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码定制\n建议改为通用名（如 Paper）以隐藏核心类型\n实际默认值以核心启动后生成的配置为准",
 Category = "基础设置",
 DefaultValue = "USpigot",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.commands.enable-version-command",
 ConfigFileName = file,
 DisplayName = "启用 /version 命令",
 Description = "️ 推断项，未经官方源码核实\n是否允许玩家使用 /version（/ver）查看服务端版本信息\n公网服建议关闭以防信息泄露\n实际默认值以核心启动后生成的配置为准",
 Category = "基础设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.commands.enable-plugins-command",
 ConfigFileName = file,
 DisplayName = "启用 /plugins 命令",
 Description = "️ 推断项，未经官方源码核实\n是否允许玩家使用 /plugins（/pl）查看已加载插件列表\n公网服建议关闭以防泄露插件信息\n实际默认值以核心启动后生成的配置为准",
 Category = "基础设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterWaterfallYml.cs
// 功能描述: 注册 Waterfall（PaperMC 维护的 BungeeCord 分支，已归档）配置文件的描述符
// 包含 waterfall.yml 日志 + MOTD + 网络 + 限流四大部分
// 数据来源: PaperMC/Waterfall README + 默认 waterfall.yml 模板（最终归档版本）
// 适用版本: Waterfall 1.20.x（项目已归档，停更）
// -----------------------------------------------------------------------------

private void RegisterWaterfallYml()
{
 const string file = "waterfall.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "config-version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "内部使用，不要手动修改\nWaterfall 用它做配置自动升级与兼容性判断",
 Category = "信息",
 DefaultValue = "1",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== log（日志设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "log_initial_handler_logs",
 ConfigFileName = file,
 DisplayName = "初始连接日志",
 Description = "是否记录玩家建立连接时的初始 Netty Handler 日志\ntrue=记录（便于排查握手问题）\nfalse=关闭以减少日志噪音",
 Category = "日志",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "log_pings",
 ConfigFileName = file,
 DisplayName = "Ping 请求日志",
 Description = "是否记录客户端对代理的 ping 请求（即服务器列表刷新触发的 ping）\n关闭可大幅减少日志量",
 Category = "日志",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== motd-sample（MOTD 与玩家样本） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "force_empty_motd",
 ConfigFileName = file,
 DisplayName = "强制空 MOTD",
 Description = "true=忽略 config.yml 中 listeners.motd，服务器列表始终显示空 MOTD\n适合子服列表不希望被外部探测的场景",
 Category = "MOTD与样本",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "force_empty_player_sample",
 ConfigFileName = file,
 DisplayName = "强制空玩家样本",
 Description = "true=服务器列表不再显示在线玩家头像与名字\n可隐藏玩家身份，避免被外挂工具批量探测",
 Category = "MOTD与样本",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "sample_count",
 ConfigFileName = file,
 DisplayName = "玩家样本数量",
 Description = "服务器列表显示的在线玩家头像 / 名字数量\n调小可减少数据包大小\n0=不显示任何玩家",
 Category = "MOTD与样本",
 DefaultValue = "12",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 // ==================== network（网络设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "disable_tab_list_rewrite",
 ConfigFileName = file,
 DisplayName = "禁用 Tab 重写",
 Description = "是否禁用代理对 Tab 列表的强制重写\ntrue=把 Tab 列表交还给后端子服控制（适合 GLOBAL 模式异常的服）\nfalse=由代理统一管理",
 Category = "网络",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "use_netty_dns_resolver",
 ConfigFileName = file,
 DisplayName = "使用 Netty DNS 解析器",
 Description = "是否使用 Netty 自带的异步 DNS 解析器（而非 JDK 同步解析）\ntrue=解析更快、不阻塞主线程\nfalse=退回 JDK 解析，便于排查 DNS 问题",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== throttling（限流） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "throttling.tabcomplete",
 ConfigFileName = file,
 DisplayName = "Tab 补全限流",
 Description = "同一玩家两次 Tab 补全请求之间的最小间隔（毫秒）\n防止恶意客户端通过疯狂 Tab 补全窃取命令列表或刷 CPU",
 Category = "限流",
 DefaultValue = "1000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterFlameCordYml.cs
// 功能描述: 注册 FlameCord（基于 BungeeCord 的反机器人分支）配置文件的描述符
// 包含 flamecord.yml 反机器人 + 防火墙 + 防重连三大部分
// 数据来源: 4drian3d/FlameCord README + 默认 flamecord.yml 模板
// 适用版本: FlameCord（基于 BungeeCord 1.19+ 分支）
// -----------------------------------------------------------------------------

private void RegisterFlameCordYml()
{
 const string file = "flamecord.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "config-version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "内部使用，不要手动修改\nFlameCord 用它做配置自动升级与兼容性判断",
 Category = "信息",
 DefaultValue = "1",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== antibot（反机器人模块） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "antibot.enabled",
 ConfigFileName = file,
 DisplayName = "启用反机器人",
 Description = "FlameCord AntiBot 总开关\ntrue=启用内置反机器人\nfalse=完全关闭，退化为普通 BungeeCord\n被攻击时务必 true",
 Category = "反机器人",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "antibot.check-accounts",
 ConfigFileName = file,
 DisplayName = "检查账户爆破",
 Description = "是否启用账户频率检测\ntrue=限制单 IP 在窗口内尝试登录不同账号的次数，可防撞库\nfalse=不检测",
 Category = "反机器人",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "antibot.max-accounts-per-ip",
 ConfigFileName = file,
 DisplayName = "单 IP 最大账号数",
 Description = "同一 IP 在窗口时间内最多尝试登录多少个不同账号\n超过此值会被视为机器人并踢出 / 封禁",
 Category = "反机器人",
 DefaultValue = "3",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "antibot.accounts-per-second",
 ConfigFileName = file,
 DisplayName = "账号请求频率",
 Description = "单 IP 每秒最多尝试登录的账号次数\n值越小越严格，但可能误杀家庭网络共享 IP 的玩家",
 Category = "反机器人",
 DefaultValue = "2",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "antibot.max-connections-per-ip",
 ConfigFileName = file,
 DisplayName = "单 IP 最大连接数",
 Description = "同一 IP 同时允许的未完成握手连接数\n超过此值的连接会被直接丢弃，防止 TCP 连接洪水",
 Category = "反机器人",
 DefaultValue = "5",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "antibot.connections-per-second",
 ConfigFileName = file,
 DisplayName = "连接请求频率",
 Description = "单 IP 每秒最多发起新连接的次数\n建议与正常玩家进入频率匹配，过低会误杀玩家",
 Category = "反机器人",
 DefaultValue = "4",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== firewall（防火墙模块） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "firewall.enabled",
 ConfigFileName = file,
 DisplayName = "启用防火墙",
 Description = "Netty 层流量限速总开关\ntrue=启用 L4 层防护\nfalse=关闭，所有连接直通代理主线程",
 Category = "防火墙",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "firewall.max-rate",
 ConfigFileName = file,
 DisplayName = "最大速率",
 Description = "单 IP 每秒允许通过的最大数据包数\n超过此速率的包会被丢弃，可有效缓解坏包攻击（BadPacket）",
 Category = "防火墙",
 DefaultValue = "10",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "firewall.timeout",
 ConfigFileName = file,
 DisplayName = "超时时间",
 Description = "单连接无数据传输的超时时间（毫秒）\n超过此值无响应的连接会被关闭，可释放僵尸连接占用",
 Category = "防火墙",
 DefaultValue = "5000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== reconnect-handler（防快速重连模块） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "reconnect-handler.enabled",
 ConfigFileName = file,
 DisplayName = "启用防重连",
 Description = "总开关\ntrue=被踢出后短时间内禁止重连\nfalse=允许立即重连，会被机器人利用绕过 AntiBot",
 Category = "防重连",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "reconnect-handler.time",
 ConfigFileName = file,
 DisplayName = "重连冷却时间",
 Description = "被踢出 / 封禁后再次允许连接的间隔（秒）\n值越大越安全，但正常玩家被误杀后等待越久",
 Category = "防重连",
 DefaultValue = "600",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterHexaCordYml.cs
// 功能描述: 注册 HexaCord（基于 BungeeCord 的基岩版兼容分支）配置文件的描述符
// 包含 hexacord.yml 基岩协议 + 跨版本 + 网络层三大部分
// 数据来源: Hexacord/HexaCord README + 默认 hexacord.yml 模板
// 适用版本: HexaCord（基于 BungeeCord 1.19+ 分支，含基岩协议适配层）
// -----------------------------------------------------------------------------

private void RegisterHexaCordYml()
{
 const string file = "hexacord.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "config-version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "内部使用，不要手动修改\nHexaCord 用它做配置自动升级与兼容性判断",
 Category = "信息",
 DefaultValue = "1",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== bedrock（基岩版协议适配） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "bedrock.enabled",
 ConfigFileName = file,
 DisplayName = "启用基岩版",
 Description = "总开关\ntrue=在 listen-port 上额外监听 UDP 基岩版流量\nfalse=只接受 Java 版连接\n开启后必须重启",
 Category = "基岩协议",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "bedrock.listen-port",
 ConfigFileName = file,
 DisplayName = "基岩版监听端口",
 Description = "基岩版客户端连接的 UDP 端口\n️ 必须与 config.yml 中 Java 版 host 端口不同\n且防火墙需放行 UDP",
 Category = "基岩协议",
 DefaultValue = "19132",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "bedrock.max-players",
 ConfigFileName = file,
 DisplayName = "基岩版玩家上限",
 Description = "同时允许的基岩版连接数上限\n0=不限制\n正数=达上限后拒绝新连接\n建议略小于后端实际承载",
 Category = "基岩协议",
 DefaultValue = "100",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "bedrock.broadcast-port",
 ConfigFileName = file,
 DisplayName = "广播端口",
 Description = "基岩版 LAN 广播与 MOTD 查询使用的端口\n通常与 listen-port 一致\n仅在内网穿透 / 多代理时需调整",
 Category = "基岩协议",
 DefaultValue = "19132",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "bedrock.motd",
 ConfigFileName = file,
 DisplayName = "基岩版 MOTD",
 Description = "基岩版客户端在服务器列表中看到的 MOTD 文本\n支持 § 颜色码与两行显示（用 \\n 分隔）",
 Category = "基岩协议",
 DefaultValue = "HexaCord Proxy",
 ValueType = "string",
 RequiresRestart = false
 });

 // ==================== protocol（跨版本协议） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "protocol.allow-old-clients",
 ConfigFileName = file,
 DisplayName = "允许旧版客户端",
 Description = "是否允许低于后端子服版本的 Java 客户端通过协议转换进入\ntrue=开启跨版本\nfalse=严格匹配版本",
 Category = "跨版本",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "protocol.min-version",
 ConfigFileName = file,
 DisplayName = "最低客户端版本",
 Description = "允许进入代理的最低 Java 客户端版本\n低于此版本会被直接踢出\n调高可减少协议转换开销",
 Category = "跨版本",
 DefaultValue = "1.7.2",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "protocol.max-version",
 ConfigFileName = file,
 DisplayName = "最高客户端版本",
 Description = "允许进入代理的最高 Java 客户端版本\n高于此版本的客户端会被踢出\n用于在 MC 新版本发布后等待适配",
 Category = "跨版本",
 DefaultValue = "1.21.x",
 ValueType = "string",
 RequiresRestart = true
 });

 // ==================== network（网络层） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "network.packet-compression-level",
 ConfigFileName = file,
 DisplayName = "数据包压缩级别",
 Description = "Netty Zlib 压缩级别\n0=不压缩（最快、最费带宽）\n9=最高压缩（最省带宽、最费 CPU）\n推荐 6 平衡",
 Category = "网络层",
 DefaultValue = "6",
 MinValue = 0,
 MaxValue = 9,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network.use-direct-memory",
 ConfigFileName = file,
 DisplayName = "使用堆外内存",
 Description = "是否使用 Netty 堆外内存（Direct Buffer）\ntrue=减少 GC 压力，提升吞吐\nfalse=堆内存，便于调试内存泄漏",
 Category = "网络层",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterMohistConfigYml.cs
// 功能描述: 注册 Mohist（混合端）配置文件的描述符
// 对应 mohist-config/mohist.yml（1.20.1+ 路径，早期版本在根目录）
// 数据来源: MohistMC/Mohist src/main/java/com/mohistmc/config/MohistConfig.java
// 适用版本: Mohist 1.20.1（develop 分支）
// -----------------------------------------------------------------------------

private void RegisterMohistConfigYml()
{
 const string file = "mohist-config.yml";

 // ==================== 通用设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.lang",
 ConfigFileName = file,
 DisplayName = "控制台语言",
 Description = "Mohist 启动日志与控制台提示所使用的语言\n仅影响 Mohist 自身日志，不影响 Minecraft 原版日志\n修改后需重启",
 Category = "通用设置",
 DefaultValue = "en_US",
 AllowedValues = ["en_US", "zh_CN", "fr_FR", "es_ES", "de_DE", "ja_JP", "ko_KR", "ru_RU", "pt_BR", "zh_TW"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.check_update",
 ConfigFileName = file,
 DisplayName = "检查 Mohist 更新",
 Description = "启动时是否联网检查 Mohist 新版本\n公网服务器可开启；离线服可关闭以避免启动卡顿",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.check_update_bukkit",
 ConfigFileName = file,
 DisplayName = "检查 Bukkit 兼容性",
 Description = "启动时是否联网检查当前 Mohist 与最新 Bukkit/Spigot API 的兼容性",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.check_libraries_update",
 ConfigFileName = file,
 DisplayName = "检查依赖库更新",
 Description = "启动时是否检查并自动下载缺失的依赖库文件\n首次启动务必开启",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.metrics",
 ConfigFileName = file,
 DisplayName = "bStats 统计上报",
 Description = "是否启用 bStats 匿名数据上报\n无隐私敏感信息，建议保持开启帮助开发者了解使用情况",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.show_logo",
 ConfigFileName = file,
 DisplayName = "启动显示 Logo",
 Description = "控制台启动时是否打印 Mohist ASCII Logo",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.console_name",
 ConfigFileName = file,
 DisplayName = "控制台名称",
 Description = "控制台作为虚拟发送者执行命令时的显示名称",
 Category = "通用设置",
 DefaultValue = "Server",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.only_english",
 ConfigFileName = file,
 DisplayName = "强制仅英文日志",
 Description = "是否强制所有日志输出为英文（即使 lang 设置为其他语言）\n便于向 GitHub 提交 Issue",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 兼容性设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.bukkit_version",
 ConfigFileName = file,
 DisplayName = "Bukkit API 版本",
 Description = "Mohist 内部使用的 Bukkit API 版本号\n通常由 Mohist 自动写入，请勿手动修改",
 Category = "兼容性",
 DefaultValue = "1.20.1-R0.1-SNAPSHOT",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.support_non_paper_plugins",
 ConfigFileName = file,
 DisplayName = "允许非 Paper 系插件",
 Description = "是否允许加载仅声明支持 Spigot/CraftBukkit 的插件\n关闭后只允许加载声明支持 Paper 的插件",
 Category = "兼容性",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.disable_plugins_blacklist",
 ConfigFileName = file,
 DisplayName = "禁用插件黑名单",
 Description = "Mohist 维护了一份已知与混合端不兼容的插件黑名单\n设为 true 跳过该检查（不推荐，可能导致崩溃）",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.disable_mods_blacklist",
 ConfigFileName = file,
 DisplayName = "禁用模组黑名单",
 Description = "跳过 Mohist 维护的已知不兼容 Forge 模组黑名单\n不推荐，可能导致崩溃",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.use_blacklist_extensions",
 ConfigFileName = file,
 DisplayName = "启用扩展黑名单",
 Description = "是否启用更严格的扩展黑名单（包含更多边缘案例）\n开启可能阻止更多模组/插件加载",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.plugins_hot_reload",
 ConfigFileName = file,
 DisplayName = "插件热重载",
 Description = "是否启用插件热重载功能（如 /plugin reload）\n实验性功能，部分插件热重载可能引发内存泄漏",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.disable_warn",
 ConfigFileName = file,
 DisplayName = "禁用兼容性警告",
 Description = "是否在启动日志中禁用 Mohist 对某些不兼容插件/模组的警告信息\n生产环境为减少日志噪音可考虑开启",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== 性能优化（实体/异步） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.max_entities",
 ConfigFileName = file,
 DisplayName = "实体数量上限",
 Description = "单一世界内允许的最大实体数量\n超出则阻止新实体生成；-1 表示不限制\n注意：与 Forge 模组的实体（如机器内的物品）可能冲突",
 Category = "性能-实体",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.entity_tick",
 ConfigFileName = file,
 DisplayName = "实体 tick 优化级别",
 Description = "实体 tick 优化级别\n值越大越省 CPU 但实体 AI 越迟钝；1 = 原版\n️ 影响模组怪物 AI，建议保持默认",
 Category = "性能-实体",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.entity_tick_skip",
 ConfigFileName = file,
 DisplayName = "跳过远实体 tick",
 Description = "是否跳过远离玩家实体的 tick 计算\n开启可提升性能，但可能破坏部分模组刷怪塔/农场",
 Category = "性能-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.async_pathfinding",
 ConfigFileName = file,
 DisplayName = "异步寻路",
 Description = "将生物寻路计算转移到异步线程\n️ 部分模组（如自定义 AI 模组）可能与异步寻路冲突，开启前请测试",
 Category = "性能-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.async_mob_spawning",
 ConfigFileName = file,
 DisplayName = "异步生物生成",
 Description = "将生物生成计算转移到异步线程\n️ 与 Forge 模组的事件监听可能冲突，模组较多的服务器请谨慎开启",
 Category = "性能-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.enable_real_ticking",
 ConfigFileName = file,
 DisplayName = "真实 tick 远实体",
 Description = "是否对远离玩家的实体也保持真实 tick（原版行为）\n关闭可省性能，但部分模组的机器/农场可能失效",
 Category = "性能-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.runtime_optimizations",
 ConfigFileName = file,
 DisplayName = "运行时优化",
 Description = "是否启用 Mohist 运行时性能优化补丁\n包含若干异步处理与缓存优化\n️ 与高性能需求模组可能冲突",
 Category = "性能-综合",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.tps_real_time",
 ConfigFileName = file,
 DisplayName = "真实 TPS 显示",
 Description = "/tps 命令显示真实 TPS（包含所有线程负载）还是仅主线程 TPS",
 Category = "性能-综合",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.use_Spark_and_Sync_Timer",
 ConfigFileName = file,
 DisplayName = "Spark 计时器",
 Description = "是否启用 Mohist 内置的同步计时器（用于性能分析）\nSpark 插件依赖此功能",
 Category = "性能-综合",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 区块与世界 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.unload_worlds",
 ConfigFileName = file,
 DisplayName = "允许卸载世界",
 Description = "是否允许在无玩家时卸载非主世界（如下界、末地）以节省内存\n多世界服建议开启",
 Category = "区块与世界",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.disable_chunk_unload",
 ConfigFileName = file,
 DisplayName = "禁用区块卸载",
 Description = "是否禁用区块自动卸载（所有加载过的区块常驻内存）\n开启可减少卡顿但极大增加内存占用",
 Category = "区块与世界",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.chunk_unload_delay",
 ConfigFileName = file,
 DisplayName = "区块卸载延迟",
 Description = "玩家离开后多久才卸载对应区块（毫秒）\n值越大越省 CPU 但内存占用越高",
 Category = "区块与世界",
 DefaultValue = "15000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.max-tick-time",
 ConfigFileName = file,
 DisplayName = "单 tick 最大耗时",
 Description = "单个 tick 超过此时间则触发 watchdog 崩服报告（毫秒）\n-1 禁用 watchdog（不推荐，模组卡死将无报警）",
 Category = "区块与世界",
 DefaultValue = "60000",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = false
 });

 // ==================== 事件桥接 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.fire_MC_ExplosionEvent",
 ConfigFileName = file,
 DisplayName = "转发爆炸事件",
 Description = "是否将 Forge 的爆炸事件转发到 Bukkit 的 EntityExplodeEvent/BlockExplodeEvent\n关闭可省 CPU，但 WorldGuard 等保护插件将无法拦截模组爆炸",
 Category = "事件桥接",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.fire_MC_BlockBreakEvent",
 ConfigFileName = file,
 DisplayName = "转发破坏方块事件",
 Description = "是否将 Forge 的方块破坏事件转发到 Bukkit 的 BlockBreakEvent\n关闭后保护插件将无法拦截模组方块破坏",
 Category = "事件桥接",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.fire_MC_BlockPlaceEvent",
 ConfigFileName = file,
 DisplayName = "转发放置方块事件",
 Description = "是否将 Forge 的方块放置事件转发到 Bukkit 的 BlockPlaceEvent",
 Category = "事件桥接",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.implement_entity_collision_event",
 ConfigFileName = file,
 DisplayName = "实体碰撞事件",
 Description = "是否实现 Bukkit 的实体碰撞事件（EntityInteractEvent 等）\n关闭可提升性能，但部分反作弊/物理插件会失效",
 Category = "事件桥接",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.implement_entity_damage_event",
 ConfigFileName = file,
 DisplayName = "实体伤害事件",
 Description = "是否为 Forge 模组的实体伤害触发 Bukkit 的 EntityDamageEvent\n关闭后 RPG/伤害修改类插件将无法作用于模组伤害",
 Category = "事件桥接",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== 玩家与权限 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.hide_online_players",
 ConfigFileName = file,
 DisplayName = "隐藏在线玩家列表",
 Description = "是否对其他服务器隐藏本服在线玩家列表（用于跨服防止 Tab 自动补全）",
 Category = "玩家与权限",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.disable_op_permissions",
 ConfigFileName = file,
 DisplayName = "禁用 OP 权限",
 Description = "是否禁用原版 OP 权限系统，强制所有权限通过 LuckPerms 等插件管理",
 Category = "玩家与权限",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== 日志与调试 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.log_mods_deaths",
 ConfigFileName = file,
 DisplayName = "记录模组实体死亡",
 Description = "是否在日志中记录所有 Forge 模组实体的死亡事件（用于排查刷怪问题）\n开启会产生大量日志",
 Category = "日志与调试",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.watchdog",
 ConfigFileName = file,
 DisplayName = "启用看门狗",
 Description = "是否启用 watchdog 线程监控主线程卡顿\n生产环境强烈建议开启",
 Category = "日志与调试",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "mohist.use_java_Hoe",
 ConfigFileName = file,
 DisplayName = "Java 优化（实验性）",
 Description = "实验性：启用 Java 内部优化（如向量化运算）\n需要 JDK 17+ 支持\n️ 实验功能，可能不稳定",
 Category = "日志与调试",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterArclightYml.cs
// 功能描述: 注册 Arclight（混合端）配置文件的描述符
// ️ 注意：实际配置文件名为 arclight.conf（HOCON 格式），不是 .yml
// 方法名沿用 RegisterArclightYml 以保持命名一致性
// 数据来源: IzzelAliz/Arclight arclight-common/src/main/java/io/izzel/arclight/config/ArclightConfig.java
// 适用版本: Arclight 1.20.1（master 分支）
// -----------------------------------------------------------------------------

private void RegisterArclightYml()
{
 // ️ 真实文件名是 arclight.conf（HOCON 格式），不是 yml
 const string file = "arclight.conf";

 // ==================== 通用设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.setdefaultlocale",
 ConfigFileName = file,
 DisplayName = "设置默认区域语言",
 Description = "是否强制将服务器的默认区域设置为系统区域（而非 en_US）\n影响部分插件的本地化文本",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.bukkit-version",
 ConfigFileName = file,
 DisplayName = "Bukkit API 版本",
 Description = "Arclight 内部使用的 Bukkit API 版本号\n由 Arclight 自动写入，请勿手动修改",
 Category = "通用设置",
 DefaultValue = "1.20.1-R0.1-SNAPSHOT",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.bukkit-version-override",
 ConfigFileName = file,
 DisplayName = "强制覆盖 Bukkit 版本",
 Description = "强制覆盖对插件声明的 Bukkit 版本号\n仅在插件因版本检查拒绝加载时使用",
 Category = "通用设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.api-version-check",
 ConfigFileName = file,
 DisplayName = "API 版本检查",
 Description = "是否对插件进行 Bukkit API 版本兼容性检查\n关闭后所有插件无视版本声明强制加载（可能导致崩溃）",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.verbose",
 ConfigFileName = file,
 DisplayName = "详细日志输出",
 Description = "是否启用 Arclight 详细日志（包含 Mixin 注入、事件桥接等调试信息）\n排查兼容性问题时开启",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 性能与并发 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.async-tick.enabled",
 ConfigFileName = file,
 DisplayName = "异步 tick 模式",
 Description = "实验性：是否启用异步 tick 模式（部分世界逻辑异步执行）\n️ 极不稳定，与绝大多数 Forge 模组冲突\n强烈不建议开启",
 Category = "性能与并发",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.disable-flush",
 ConfigFileName = file,
 DisplayName = "禁用批量刷新",
 Description = "是否禁用网络数据包批量刷新\n开启可能减少延迟但增加带宽\n一般保持 false",
 Category = "性能与并发",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.disable-watchdog",
 ConfigFileName = file,
 DisplayName = "禁用看门狗",
 Description = "是否禁用 watchdog 主线程监控\n️ 不推荐，模组卡死将无报警",
 Category = "性能与并发",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.optimize-entity-portal",
 ConfigFileName = file,
 DisplayName = "优化实体传送门",
 Description = "是否优化实体穿越传送门（下界/末地）的处理逻辑\n开启可减少传送门附近的卡顿",
 Category = "性能与并发",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 兼容性与事件桥接 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.capture-compound",
 ConfigFileName = file,
 DisplayName = "捕获 NBT 复合事件",
 Description = "是否捕获模组方块的 NBT 复合数据用于 Bukkit 事件\n开启可让 ChestShop 等插件识别模组方块，但增加少量开销",
 Category = "兼容性",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.event-transformation",
 ConfigFileName = file,
 DisplayName = "事件类型转换",
 Description = "是否启用 Forge ↔ Bukkit 事件类型自动转换\n关闭后大量 Bukkit 插件将无法响应模组事件\n务必保持 true",
 Category = "兼容性",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.entity-spawn.unique-id",
 ConfigFileName = file,
 DisplayName = "实体生成唯一 ID",
 Description = "是否为模组生成的实体分配 Bukkit 兼容的唯一 UUID\n开启可让 RPG/统计类插件识别模组实体",
 Category = "兼容性",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 命令与权限 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "arclight.command.no-permission-message",
 ConfigFileName = file,
 DisplayName = "无权限提示消息",
 Description = "玩家无权限执行 Arclight 内置命令时显示的提示文本\n支持 & 颜色代码",
 Category = "命令与权限",
 DefaultValue = "You do not have permission to use this command.",
 ValueType = "string",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterCatServerYml.cs
// 功能描述: 注册 CatServer（混合端）配置文件的描述符
// 对应 catserver.yml
// 数据来源: Luohuayu/CatServer src/main/java/catserver/server/CatServerConfig.java
// 适用版本: CatServer 1.16.5（长期支持版本）
// -----------------------------------------------------------------------------

private void RegisterCatServerYml()
{
 const string file = "catserver.yml";

 // ==================== 世界设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world.keepSpawnInMemory",
 ConfigFileName = file,
 DisplayName = "出生点常驻内存",
 Description = "是否始终将出生点区域区块加载到内存中\n开启可避免新玩家进入时卡顿，但占用内存\n小型服建议 true，内存紧张的大型服可考虑 false",
 Category = "世界设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world.forceSaveOnWatchdog",
 ConfigFileName = file,
 DisplayName = "看门狗触发时强制保存",
 Description = "当服务器因 watchdog 超时崩溃时是否强制保存世界数据\n强烈建议 true 防止数据丢失\n注意：可能延长崩溃恢复时间",
 Category = "世界设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world.worldGenMaxTickTime",
 ConfigFileName = file,
 DisplayName = "世界生成最大 tick 时间",
 Description = "单次 tick 内世界生成的最大耗时（毫秒）\n降低此值可减少世界生成卡顿，但会延长生成完成时间\n玩家频繁飞行（鞘翅）时建议调高",
 Category = "世界设置",
 DefaultValue = "15",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 // ==================== 假人设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "fakePlayer.permissions",
 ConfigFileName = file,
 DisplayName = "假人默认权限列表",
 Description = "为服务器假人（如模组机器触发的虚拟玩家）添加的默认权限节点列表\n配合 Essentials 等插件实现假人自动建造、交互等功能\n每行一个权限节点",
 Category = "假人设置",
 DefaultValue = "essentials.build",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fakePlayer.eventPass",
 ConfigFileName = file,
 DisplayName = "假人事件传递",
 Description = "是否让假人触发玩家事件（如方块破坏、实体交互）\n设为 false 减少服务器负载（推荐）\n设为 true 可实现更真实的假人行为（部分插件可能误判为真人玩家）",
 Category = "假人设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== 插件兼容性补丁 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "plugin.patcher.enableDynmapCompatible",
 ConfigFileName = file,
 DisplayName = "Dynmap 兼容补丁",
 Description = "修复 Dynmap 地图插件与 Forge 模组的兼容性问题\n使用 Dynmap 生成 3D 地图时必须开启",
 Category = "插件兼容补丁",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "plugin.patcher.enableWorldEditCompatible",
 ConfigFileName = file,
 DisplayName = "WorldEdit 兼容补丁",
 Description = "解决 WorldEdit 与部分 Forge 模组的方块操作冲突（如模组自定义方块无法被编辑）\n建议始终开启，除非确认不使用 WorldEdit",
 Category = "插件兼容补丁",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "plugin.patcher.enableEssentialsNewVersionCompatible",
 ConfigFileName = file,
 DisplayName = "Essentials 新版兼容补丁",
 Description = "支持 EssentialsX 等新版本 Essentials 插件\n修复指令冲突、权限管理等兼容性问题\n使用 EssentialsX 时必须开启",
 Category = "插件兼容补丁",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== 性能优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.async-chunk-loading",
 ConfigFileName = file,
 DisplayName = "异步区块加载",
 Description = "是否启用异步区块加载\n开启可减少主线程阻塞，提升玩家飞行/传送时的流畅度\n️ 与部分老式模组可能冲突",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.reduce-lag",
 ConfigFileName = file,
 DisplayName = "启用防卡顿优化",
 Description = "启用 CatServer 的综合防卡顿优化（实体激活范围、AI 节流等）\n建议保持 true",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "optimization.fast-operations",
 ConfigFileName = file,
 DisplayName = "快速操作优化",
 Description = "启用快速方块/实体操作优化\n可提升约 10% TPS\n️ 与依赖精确事件触发的红石插件可能冲突",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 村民与红石 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "villager.atFix",
 ConfigFileName = file,
 DisplayName = "村民 AI 修复",
 Description = "修复部分 Forge 模组导致的村民 AI 异常（村民不工作/卡住）\n建议保持 true",
 Category = "村民与红石",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== 通用设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "versionCheck",
 ConfigFileName = file,
 DisplayName = "版本检查",
 Description = "启动时自动检查 CatServer 更新\n建议 true 以及时获取安全更新",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "disableAsyncCatchWarn",
 ConfigFileName = file,
 DisplayName = "禁用异步捕获警告",
 Description = "是否禁用插件异步操作警告\n插件调试时可设 true，生产环境建议 false 以便发现插件异步调用主线程 API 的问题",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterMagmaConf.cs
// 功能描述: 注册 Magma（混合端）配置文件的描述符
// ️ 注意：配置文件实际名为 magma.yml，但内部是 Properties 格式（key=value）
// 不是真正的 YAML！请使用 Properties 语法编辑
// 数据来源: magmamaintainers/Magma MagmaConfig.java
// 适用版本: Magma 1.18.2
// -----------------------------------------------------------------------------

private void RegisterMagmaConf()
{
 // ️ 实际是 Properties 格式（key=value），不是 YAML
 const string file = "magma.yml";

 // ==================== 通用设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "magma.check-update",
 ConfigFileName = file,
 DisplayName = "检查 Magma 更新",
 Description = "启动时是否联网检查 Magma 新版本",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.bukkit-version",
 ConfigFileName = file,
 DisplayName = "Bukkit API 版本",
 Description = "Magma 内部使用的 Bukkit API 版本号\n由 Magma 自动写入，请勿手动修改",
 Category = "通用设置",
 DefaultValue = "1.18.2-R0.1-SNAPSHOT",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.disable-logger",
 ConfigFileName = file,
 DisplayName = "禁用部分日志",
 Description = "是否禁用 Magma 自身的部分调试日志（如启动日志）\n减少日志噪音",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.disable-sentry",
 ConfigFileName = file,
 DisplayName = "禁用 Sentry 错误上报",
 Description = "是否禁用 Sentry 错误自动上报\nMagma 默认会上报崩溃信息到 Sentry 帮助开发",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.remove-blank-line",
 ConfigFileName = file,
 DisplayName = "移除日志空行",
 Description = "是否移除日志中的多余空行，让日志更紧凑",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.remove-errormods",
 ConfigFileName = file,
 DisplayName = "移除报错模组日志",
 Description = "是否在启动失败时移除报错模组的详细日志（仅显示摘要）",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 性能优化（实体） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "magma.use-multi-thread-entity-tick",
 ConfigFileName = file,
 DisplayName = "多线程实体 tick",
 Description = "实验性：是否使用多线程处理实体 tick\n️ 与绝大多数 Forge 模组冲突，强烈不建议开启",
 Category = "性能-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.max-entity-ticks-per-tick",
 ConfigFileName = file,
 DisplayName = "单 tick 实体上限",
 Description = "单次 tick 最多处理的实体数量\n-1 不限制\n模组较多的服务器可设上限防止实体爆炸卡服",
 Category = "性能-实体",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.entity-tick-limit",
 ConfigFileName = file,
 DisplayName = "实体 tick 限制",
 Description = "类似 max-entity-ticks-per-tick，限制实体 tick 总数\n-1 不限制",
 Category = "性能-实体",
 DefaultValue = "-1",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.enable-real-ticking-entities",
 ConfigFileName = file,
 DisplayName = "真实 tick 实体",
 Description = "是否对所有实体保持真实 tick（原版行为）\n关闭可省性能，但部分模组机器/农场可能失效",
 Category = "性能-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.tick-skip",
 ConfigFileName = file,
 DisplayName = "跳过远实体 tick",
 Description = "是否跳过远离玩家实体的 tick\n开启可省 CPU 但破坏部分模组刷怪塔",
 Category = "性能-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.entity-activation-range",
 ConfigFileName = file,
 DisplayName = "实体激活范围总开关",
 Description = "是否启用实体激活范围机制（远离玩家的实体降低 tick 频率）",
 Category = "性能-实体",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 性能优化（区块与异步） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "magma.enable-asynchronous-chunk",
 ConfigFileName = file,
 DisplayName = "异步区块加载",
 Description = "是否启用异步区块加载/生成\n开启可显著减少主线程卡顿，提升玩家飞行/传送流畅度",
 Category = "性能-区块",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.async-pathfinding",
 ConfigFileName = file,
 DisplayName = "异步寻路",
 Description = "将生物寻路计算转移到异步线程\n️ 部分模组可能与异步寻路冲突",
 Category = "性能-区块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.async-mob-spawning",
 ConfigFileName = file,
 DisplayName = "异步生物生成",
 Description = "将生物生成计算转移到异步线程\n️ 与 Forge 模组的事件监听可能冲突",
 Category = "性能-区块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.use-async-thread",
 ConfigFileName = file,
 DisplayName = "启用异步线程",
 Description = "是否启用 Magma 的异步工作线程（用于区块、寻路等）\n建议保持 true",
 Category = "性能-区块",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.print-chunk",
 ConfigFileName = file,
 DisplayName = "打印区块加载信息",
 Description = "是否在日志中打印区块加载/卸载的详细信息\n排查区块问题时开启",
 Category = "性能-区块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== 性能优化（综合） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "magma.target-tps",
 ConfigFileName = file,
 DisplayName = "目标 TPS",
 Description = "服务器目标 TPS\n一般保持 20（原版）\n降低可省 CPU 但游戏变卡",
 Category = "性能-综合",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.max-tick-time",
 ConfigFileName = file,
 DisplayName = "单 tick 最大耗时",
 Description = "单个 tick 超过此时间触发 watchdog（毫秒）\n-1 禁用看门狗（不推荐）",
 Category = "性能-综合",
 DefaultValue = "60000",
 MinValue = -1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.disable-watchdog",
 ConfigFileName = file,
 DisplayName = "禁用看门狗",
 Description = "是否禁用 watchdog 主线程监控\n️ 不推荐，模组卡死将无报警",
 Category = "性能-综合",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.disable-watcher",
 ConfigFileName = file,
 DisplayName = "禁用文件监视器",
 Description = "是否禁用文件监视器（监视 mods/、plugins/ 等目录变化）\n关闭后无法热检测文件变更",
 Category = "性能-综合",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.optimized-crafting",
 ConfigFileName = file,
 DisplayName = "优化合成",
 Description = "是否启用合成台合成优化（缓存合成结果）\n可提升合成性能",
 Category = "性能-综合",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.fast-rain",
 ConfigFileName = file,
 DisplayName = "快速降雨",
 Description = "是否优化天气变化（降雨/降雪）的处理逻辑\n减少天气切换时的卡顿",
 Category = "性能-综合",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.use-spark",
 ConfigFileName = file,
 DisplayName = "启用 Spark 集成",
 Description = "是否启用与 Spark 性能分析插件的集成\n建议保持 true",
 Category = "性能-综合",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 兼容性与事件 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "magma.allow-fluid-flow",
 ConfigFileName = file,
 DisplayName = "允许流体流动事件",
 Description = "是否允许 Forge 模组的流体流动触发 Bukkit 事件\n关闭可省 CPU，但部分物理/红石插件会失效",
 Category = "兼容性",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.disable-super-vanilla-fallable-block",
 ConfigFileName = file,
 DisplayName = "禁用原版下落方块优化",
 Description = "是否禁用 Magma 对原版下落方块（沙子、砂砾）的优化\n模组下落方块异常时可尝试开启",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.fix-tile-entity",
 ConfigFileName = file,
 DisplayName = "修复方块实体",
 Description = "修复部分 Forge 模组方块实体（TileEntity）与 Bukkit 事件的兼容性\n建议保持 true",
 Category = "兼容性",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.disable-flush",
 ConfigFileName = file,
 DisplayName = "禁用批量刷新",
 Description = "是否禁用网络数据包批量刷新\n开启可能减少延迟但增加带宽",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.disable-book-ban",
 ConfigFileName = file,
 DisplayName = "禁用书本封禁",
 Description = "是否启用书本封禁保护（防止玩家通过恶意 NBT 书本导致客户端/服务器崩溃）\n建议保持 true",
 Category = "兼容性",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "magma.enable-bungee",
 ConfigFileName = file,
 DisplayName = "启用 BungeeCord 支持",
 Description = "是否启用 BungeeCord/Velocity 跨服代理支持\n使用代理服时必须开启，并设置 bungeecord 相关项",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterBannerYml.cs
// 功能描述: 注册 Banner（Fabric 混合端）配置文件的描述符
// 对应 banner.yml
// 数据来源: MohistMC/Banner BannerConfig.java
// 适用版本: Banner 1.20.1（master 分支）
// 注意: Banner 是 Fabric+Bukkit 混合（区别于其他 Forge 系混合端）
// 2025年7月后项目部分分支更名为 Taiyitist，本描述符仍以原始 Banner 为准
// -----------------------------------------------------------------------------

private void RegisterBannerYml()
{
 const string file = "banner.yml";

 // ==================== 通用设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "banner.lang",
 ConfigFileName = file,
 DisplayName = "控制台语言",
 Description = "Banner 启动日志与控制台提示所使用的语言\n仅影响 Banner 自身日志，不影响 Minecraft 原版日志",
 Category = "通用设置",
 DefaultValue = "en_US",
 AllowedValues = ["en_US", "zh_CN", "fr_FR", "es_ES", "de_DE", "ja_JP", "ko_KR", "ru_RU", "pt_BR", "zh_TW"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.check_update",
 ConfigFileName = file,
 DisplayName = "检查 Banner 更新",
 Description = "启动时是否联网检查 Banner 新版本",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.metrics",
 ConfigFileName = file,
 DisplayName = "bStats 统计上报",
 Description = "是否启用 bStats 匿名数据上报\n建议保持开启帮助开发者了解使用情况",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.show_logo",
 ConfigFileName = file,
 DisplayName = "启动显示 Logo",
 Description = "控制台启动时是否打印 Banner ASCII Logo",
 Category = "通用设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.bukkit-version",
 ConfigFileName = file,
 DisplayName = "Bukkit API 版本",
 Description = "Banner 内部使用的 Bukkit API 版本号\n由 Banner 自动写入，请勿手动修改",
 Category = "通用设置",
 DefaultValue = "1.20.1-R0.1-SNAPSHOT",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.bukkit-version-override",
 ConfigFileName = file,
 DisplayName = "强制覆盖 Bukkit 版本",
 Description = "强制覆盖对插件声明的 Bukkit 版本号\n仅在插件因版本检查拒绝加载时使用",
 Category = "通用设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 // ==================== 兼容性设置 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "banner.disable_plugins_blacklist",
 ConfigFileName = file,
 DisplayName = "禁用插件黑名单",
 Description = "Banner 维护了一份已知与混合端不兼容的插件黑名单\n设为 true 跳过该检查（不推荐，可能导致崩溃）",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.disable_mods_blacklist",
 ConfigFileName = file,
 DisplayName = "禁用模组黑名单",
 Description = "跳过 Banner 维护的已知不兼容 Fabric 模组黑名单\n不推荐，可能导致崩溃",
 Category = "兼容性",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.support_non_paper_plugins",
 ConfigFileName = file,
 DisplayName = "允许非 Paper 系插件",
 Description = "是否允许加载仅声明支持 Spigot/CraftBukkit 的插件",
 Category = "兼容性",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 性能优化 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "banner.async-tick",
 ConfigFileName = file,
 DisplayName = "异步 tick 模式",
 Description = "实验性：是否启用异步 tick 模式\n️ 与部分 Fabric 模组（如 Lithium）可能冲突\n强烈不建议开启",
 Category = "性能优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.disable-watchdog",
 ConfigFileName = file,
 DisplayName = "禁用看门狗",
 Description = "是否禁用 watchdog 主线程监控\n️ 不推荐，模组卡死将无报警",
 Category = "性能优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.entity-activation-range",
 ConfigFileName = file,
 DisplayName = "实体激活范围优化",
 Description = "是否启用实体激活范围优化（远离玩家的实体降低 tick 频率）\n与 Lithium 类似模组可能重复优化，建议二选一",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.use-Spark-and-Sync-Timer",
 ConfigFileName = file,
 DisplayName = "Spark 计时器",
 Description = "是否启用 Banner 内置的同步计时器（用于性能分析）\nSpark 插件依赖此功能",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== 事件桥接 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "banner.event-transformation",
 ConfigFileName = file,
 DisplayName = "事件类型转换",
 Description = "是否启用 Fabric ↔ Bukkit 事件类型自动转换\n关闭后大量 Bukkit 插件将无法响应模组事件\n务必保持 true",
 Category = "事件桥接",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "banner.capture-compound",
 ConfigFileName = file,
 DisplayName = "捕获 NBT 复合事件",
 Description = "是否捕获模组方块的 NBT 复合数据用于 Bukkit 事件\n开启可让 ChestShop 等插件识别模组方块",
 Category = "事件桥接",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });
}
// =============================================================================
// 文件名: RegisterForgeServerToml.cs
// 功能描述: Forge 服务端配置文件 forge-server.toml 的描述符注册方法
// 配置文件: <世界名>/serverconfig/forge-server.toml (TOML 格式)
// 来源核心: Minecraft Forge (https://github.com/MinecraftForge/MinecraftForge)
// 适用版本: Forge 1.18 ~ 1.21.x (自 1.14 起配置体系基本一致)
// 数据来源: Forge 1.21.x 源码 ForgeConfig.java
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
// 并在构造函数中调用 RegisterForgeServerToml();
// =============================================================================

private void RegisterForgeServerToml()
{
 const string file = "forge-server.toml";

 // ==================== [server] 节 —— 服务端配置 ====================
 // 注意：Forge 的 forge-server.toml 位于 <世界名>/serverconfig/ 下，不在根目录 config/ 下。
 // 所有配置项位于 [server] 节，TOML 路径形式为 server.<键名>。

 Register(new ServerConfigDescriptor
 {
 Key = "server.removeErroringBlockEntities",
 ConfigFileName = file,
 DisplayName = "删除报错方块实体",
 Description = "设为 true 时，当某个方块实体（BlockEntity，即 TileEntity，如箱子/熔炉/模组机器）在其更新方法中抛出异常，Forge 会直接删除该方块实体，而不是关闭服务器并打印崩溃日志。\n️ 危险选项：可能导致机器内物品丢失、方块状态错乱。\n仅作为排查「Ticking Block Entity」崩溃的应急手段临时开启，处理完务必改回 false！\nForge 官方明确声明对此造成的损失不负责。",
 Category = "服务端 / 故障修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server.removeErroringEntities",
 ConfigFileName = file,
 DisplayName = "删除报错实体",
 Description = "设为 true 时，当某个实体（Entity，如僵尸、掉落物、矿车等，不包括方块实体）在其 tick 方法中抛出异常，Forge 会直接删除该实体，而不是关闭服务器并打印崩溃日志。\n️ 危险选项：可能导致玩家丢失骑乘的坐骑、农场中的关键生物等。\n仅作为排查「Ticking Entity」崩溃的应急手段临时开启，处理完务必改回 false！",
 Category = "服务端 / 故障修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server.fullBoundingBoxLadders",
 ConfigFileName = file,
 DisplayName = "完整碰撞盒爬梯检测",
 Description = "设为 true 时，检测实体是否在爬梯子会检查整个实体碰撞盒所覆盖的方块，而不仅限于实体当前所在的方块。\n会带来明显的机制差异（更高的爬梯判定范围），默认保持原版行为。\n仅在你确知某些模组需要此特性时才开启。",
 Category = "服务端 / 游戏机制",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server.permissionHandler",
 ConfigFileName = file,
 DisplayName = "权限处理器",
 Description = "服务器使用的权限处理器 ID。默认为 forge:default_handler（Forge 内置的默认权限处理器）。\n仅当服务器中安装了提供自定义权限系统的模组时才需要修改。\n普通开服玩家保持默认即可。错误的值会导致服务器启动失败。",
 Category = "服务端 / 权限",
 DefaultValue = "forge:default_handler",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server.advertiseDedicatedServerToLan",
 ConfigFileName = file,
 DisplayName = "向局域网广播服务器",
 Description = "设为 true 时，专用服务端会向本地局域网广播自身存在，使同局域网下的客户端能在「多人游戏」界面自动看到这台服务器。\n公网/VPS 部署时无实际意义；本地测试时不希望他人自动看到可关闭。",
 Category = "服务端 / 网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });
}
// =============================================================================
// 文件名: RegisterNeoForgeYml.cs
// 功能描述: NeoForge 服务端配置文件 neoforge-server.toml 的描述符注册方法
// 配置文件: config/neoforge-server.toml (TOML 格式；任务原名 neoforge.yml，实际为 TOML)
// 来源核心: NeoForge (https://github.com/neoforged/NeoForge)
// 适用版本: NeoForge 1.20.2 ~ 1.21.x
// 数据来源: NeoForge 1.21.x 源码 NeoForgeConfig.java
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
// 并在构造函数中调用 RegisterNeoForgeYml();
// =============================================================================

private void RegisterNeoForgeYml()
{
 const string file = "neoforge-server.toml";

 // ==================== [server] 节 —— 服务端配置 ====================
 // 注意：NeoForge 1.20.4+ 的 neoforge-server.toml 位于服务器根目录 config/ 下
 // （不同于 Forge 1.20.x 把 forge-server.toml 放在 <世界>/serverconfig/ 下）。
 // NeoForge 文件中配置项直接位于文件顶级（无 [server] 表头），但语义上仍属于服务端配置。

 Register(new ServerConfigDescriptor
 {
 Key = "removeErroringBlockEntities",
 ConfigFileName = file,
 DisplayName = "删除报错方块实体",
 Description = "设为 true 时，当某个方块实体（BlockEntity，即 TileEntity，如箱子/熔炉/模组机器）在其更新方法中抛出异常，NeoForge 会直接删除该方块实体，而不是关闭服务器并打印崩溃日志。\n️ 危险选项：可能导致机器内物品丢失、方块状态错乱。\n仅作为排查「Ticking Block Entity」崩溃的应急手段临时开启，处理完务必改回 false！\nNeoForge 官方明确声明对此造成的损失不负责。",
 Category = "服务端 / 故障修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "removeErroringEntities",
 ConfigFileName = file,
 DisplayName = "删除报错实体",
 Description = "设为 true 时，当某个实体（Entity，如僵尸、掉落物、矿车等，不包括方块实体）在其 tick 方法中抛出异常，NeoForge 会直接删除该实体，而不是关闭服务器并打印崩溃日志。\n️ 危险选项：可能导致玩家丢失骑乘的坐骑、农场中的关键生物等。\n仅作为排查「Ticking Entity」崩溃的应急手段临时开启，处理完务必改回 false！",
 Category = "服务端 / 故障修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "fullBoundingBoxLadders",
 ConfigFileName = file,
 DisplayName = "完整碰撞盒爬梯检测",
 Description = "设为 true 时，检测实体是否在爬梯子会检查整个实体碰撞盒所覆盖的方块，而不仅限于实体当前所在的方块。\n会带来明显的机制差异（更高的爬梯判定范围），默认保持原版行为。\n仅在你确知某些模组需要此特性时才开启。",
 Category = "服务端 / 游戏机制",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "permissionHandler",
 ConfigFileName = file,
 DisplayName = "权限处理器",
 Description = "服务器使用的权限处理器 ID。默认为 neoforge:default_handler（NeoForge 内置的默认权限处理器）。\n仅当服务器中安装了提供自定义权限系统的模组时才需要修改。\n普通开服玩家保持默认即可。错误的值会导致服务器启动失败。",
 Category = "服务端 / 权限",
 DefaultValue = "neoforge:default_handler",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "advertiseDedicatedServerToLan",
 ConfigFileName = file,
 DisplayName = "向局域网广播服务器",
 Description = "设为 true 时，专用服务端会向本地局域网广播自身存在，使同局域网下的客户端能在「多人游戏」界面自动看到这台服务器。\n公网/VPS 部署时无实际意义；本地测试时不希望他人自动看到可关闭。",
 Category = "服务端 / 网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== neoforge-common.toml —— 通用配置（同时影响客户端与服务端） ====================
 // 此处将通用配置也注册到 neoforge-server.toml 的文件名下，便于服务端管理员查阅。
 // 若需精确区分文件，可改用 ConfigFileName = "neoforge-common.toml"。

 const string commonFile = "neoforge-common.toml";

 Register(new ServerConfigDescriptor
 {
 Key = "logUntranslatedItemTagWarnings",
 ConfigFileName = commonFile,
 DisplayName = "未翻译物品标签警告模式",
 Description = "主要面向开发者：在内置服务器运行时，记录缺少翻译键（tag.item.<命名空间>.<路径>）的模组物品标签。\nSILENCED（静默，默认）= 不记录\nDEV_SHORT / DEV_LONG = 仅在开发环境中以短/长格式记录\nENABLED = 任何环境都记录\n普通开服者保持 SILENCED。",
 Category = "通用 / 开发者调试",
 DefaultValue = "SILENCED",
 AllowedValues = ["SILENCED", "DEV_SHORT", "DEV_LONG", "ENABLED"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "logLegacyTagWarnings",
 ConfigFileName = commonFile,
 DisplayName = "旧命名空间标签警告模式",
 Description = "主要面向开发者：在内置服务器运行时，记录仍在使用旧的 forge: 命名空间的模组标签。\nDEV_SHORT（默认）= 仅在开发环境中以短格式记录\nSILENCED = 不记录\nDEV_LONG = 长格式\nENABLED = 任何环境都记录\n普通开服者可改为 SILENCED 减少日志噪音。",
 Category = "通用 / 开发者调试",
 DefaultValue = "DEV_SHORT",
 AllowedValues = ["SILENCED", "DEV_SHORT", "DEV_LONG", "ENABLED"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "attributeAdvancedTooltipDebugInfo",
 ConfigFileName = commonFile,
 DisplayName = "属性高级工具提示调试",
 Description = "设为 true 时，开启「高级工具提示」（按 F3+H）后会在物品上额外显示其属性的调试信息。\n开服端一般不显示 tooltip，此项对服务端运行无影响，保持默认即可。",
 Category = "通用 / 开发者调试",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });
}
// =============================================================================
// 文件名: RegisterFabricServerProperties.cs
// 功能描述: Fabric 启动器配置文件 fabric-server-launcher.properties 的描述符注册方法
// 配置文件: fabric-server-launcher.properties (Properties 格式，极简，仅 1 个键)
// 来源核心: Fabric Loader (https://github.com/FabricMC/fabric)
// 适用版本: Fabric Loader 0.4+ / MC 1.14 ~ 1.21.x
// 数据来源: Fabric 官方 Wiki / Fabric 安装器源码
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
// 并在构造函数中调用 RegisterFabricServerProperties();
// =============================================================================

private void RegisterFabricServerProperties()
{
 const string file = "fabric-server-launcher.properties";

 // ==================== 启动器配置 ====================
 // Fabric 是模组加载器，不是完整的服务端实现。其唯一的配置文件 fabric-server-launcher.properties
 // 由 Fabric 安装器自动生成，与 fabric-server-launch.jar 同目录，仅含 1 个键 serverJar。
 // 其他所有服务器行为（端口、视距、白名单等）沿用原版 server.properties，请参阅 Vanilla 手册。
 //
 // 启动入口是 fabric-server-launch.jar，不是 server.jar！
 // 启动命令示例：java -Xmx4G -Xms2G -jar fabric-server-launch.jar nogui

 Register(new ServerConfigDescriptor
 {
 Key = "serverJar",
 ConfigFileName = file,
 DisplayName = "原版服务端 JAR 路径",
 Description = "指向原版 Minecraft 服务端 JAR 文件的路径。Fabric 启动器会加载这个 JAR，并在其启动前注入 Fabric Loader 模组加载逻辑。\n默认值 server.jar 表示与启动器同目录下的 server.jar。\n\n何时需要修改：\n1) 若把原版 JAR 重命名为 vanilla.jar（如某些主机面板要求启动入口必须叫 server.jar），则改为 vanilla.jar，并把 fabric-server-launch.jar 重命名为 server.jar。\n2) 若原版 JAR 在其他目录，可填写相对路径（相对启动器 JAR 所在目录）或绝对路径。\n\n️ 路径错误会导致启动失败，提示找不到主类 net.fabricmc.loader.impl.launch.server.FabricServerLauncher 或找不到 JAR。",
 Category = "启动器配置",
 DefaultValue = "server.jar",
 ValueType = "string",
 RequiresRestart = true
 });
}
// =============================================================================
// 文件名: RegisterQuiltServerProperties.cs
// 功能描述: Quilt 启动器配置文件 quilt-server-launcher.properties 的描述符注册方法
// 配置文件: quilt-server-launcher.properties (Properties 格式，极简，仅 1 个键)
// 来源核心: Quilt Loader (https://github.com/QuiltMC/quilt)
// 适用版本: Quilt Loader 0.20+ / MC 1.14 ~ 1.21.x
// 数据来源: Quilt 官方文档 / Quilt 安装器源码
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
// 并在构造函数中调用 RegisterQuiltServerProperties();
// =============================================================================

private void RegisterQuiltServerProperties()
{
 const string file = "quilt-server-launcher.properties";

 // ==================== 启动器配置 ====================
 // Quilt 是 Fabric 的社区驱动分支，配置模式与 Fabric 完全一致。
 // 唯一的配置文件 quilt-server-launcher.properties 由 Quilt 安装器自动生成，
 // 与 quilt-server-launch.jar 同目录，仅含 1 个键 serverJar。
 // 其他所有服务器行为沿用原版 server.properties，请参阅 Vanilla 手册。
 //
 // ️ 命名陷阱（历史遗留，请照抄）：
 // - JAR 文件名为 quilt-server-launch.jar（无 er）
 // - Properties 文件名为 quilt-server-launcher.properties（有 er）
 //
 // 启动入口是 quilt-server-launch.jar，不是 server.jar！
 // 启动命令示例：java -Xmx4G -Xms2G -jar quilt-server-launch.jar nogui

 Register(new ServerConfigDescriptor
 {
 Key = "serverJar",
 ConfigFileName = file,
 DisplayName = "原版服务端 JAR 路径",
 Description = "指向原版 Minecraft 服务端 JAR 文件的路径。Quilt 启动器会加载这个 JAR，并在其启动前注入 Quilt Loader（含 QSL）模组加载逻辑。\n默认值 server.jar 表示与启动器同目录下的 server.jar。\n\n何时需要修改：\n1) 若把原版 JAR 重命名为 vanilla.jar（如某些主机面板要求启动入口必须叫 server.jar），则改为 vanilla.jar，并把 quilt-server-launch.jar 重命名为 server.jar。\n2) 若原版 JAR 在其他目录，可填写相对路径（相对启动器 JAR 所在目录）或绝对路径。\n\n️ 路径错误会导致启动失败，提示找不到主类 org.quiltmc.loader.impl.launch.server.QuiltServerLauncher 或找不到 JAR。",
 Category = "启动器配置",
 DefaultValue = "server.jar",
 ValueType = "string",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterNukkitYml.cs
// 功能描述: 注册 Nukkit（基岩版）配置文件的描述符
// 包含 nukkit.yml 与基岩版 server.properties（用 nukkit-server.properties 区分）
// 数据来源: CloudburstMC/Nukkit src/main/resources/lang/eng/nukkit.yml + 基岩版 BDS 文档
// 适用版本: Nukkit 1.0（master 分支，commit dbbb7ca）
// -----------------------------------------------------------------------------

private void RegisterNukkitYml()
{
 const string file = "nukkit.yml";

 // ==================== settings（基础设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.language",
 ConfigFileName = file,
 DisplayName = "服务器语言",
 Description = "服务器控制台与提示消息使用的语言\n可选: eng 英语 / chs 简中 / cht 繁中 / jpn 日语 / rus 俄语 / spa 西语 / pol 波兰语 / bra 葡语 / kor 韩语 / ukr 乌克语 / deu 德语 / ltu 立陶宛语 / idn 印尼语 / cze 捷克语 / tur 土耳其语 / fin 芬兰语",
 Category = "基础设置",
 DefaultValue = "eng",
 AllowedValues = ["eng", "chs", "cht", "jpn", "rus", "spa", "pol", "bra", "kor", "ukr", "deu", "ltu", "idn", "cze", "tur", "fin"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.force-language",
 ConfigFileName = file,
 DisplayName = "强制使用服务器语言",
 Description = "true 时所有字符串按服务器语言翻译后发送给客户端\nfalse 时让客户端设备自行处理本地化（推荐）",
 Category = "基础设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.shutdown-message",
 ConfigFileName = file,
 DisplayName = "关服提示消息",
 Description = "服务器关闭时踢出玩家显示的提示文本",
 Category = "基础设置",
 DefaultValue = "Server closed",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.query-plugins",
 ConfigFileName = file,
 DisplayName = "Query 暴露插件列表",
 Description = "true 时允许通过 GameSpy Query 协议列出已加载插件\n公网服务器建议关闭以避免泄露插件信息",
 Category = "基础设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.deprecated-verbose",
 ConfigFileName = file,
 DisplayName = "弃用 API 警告",
 Description = "插件使用已弃用的 API 方法时是否在控制台打印警告\n开发环境建议开启，生产环境可关闭以减少日志噪音",
 Category = "基础设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.async-workers",
 ConfigFileName = file,
 DisplayName = "异步工作线程数",
 Description = "AsyncTask 的工作线程数\nauto 自动检测 CPU 核心数（至少 4）\n手动设置时建议不超过 CPU 核心数",
 Category = "基础设置",
 DefaultValue = "auto",
 ValueType = "string",
 RequiresRestart = true
 });

 // ==================== network（网络设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "network.batch-threshold",
 ConfigFileName = file,
 DisplayName = "批处理字节阈值",
 Description = "数据包累积到此字节数才进行批处理压缩\n0 = 压缩所有包；-1 = 完全禁用压缩\n降低此值减少延迟但增加 CPU 负担",
 Category = "网络",
 DefaultValue = "256",
 MinValue = -1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network.compression-level",
 ConfigFileName = file,
 DisplayName = "Zlib 压缩级别",
 Description = "批处理包的 Zlib 压缩级别\n值越大 CPU 占用越高、带宽越省\n基岩版推荐 5-7",
 Category = "网络",
 DefaultValue = "5",
 MinValue = 1,
 MaxValue = 9,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network.compression-use-snappy",
 ConfigFileName = file,
 DisplayName = "启用 Snappy 压缩",
 Description = "实验性：使用 Google Snappy 算法替代 Zlib\n压缩比低但速度极快，CPU 紧张的服务器可尝试\n️ 实验功能，可能不兼容旧客户端",
 Category = "网络",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network.encryption",
 ConfigFileName = file,
 DisplayName = "启用网络加密",
 Description = "是否启用基岩版网络加密（基于 ECDH 握手）\n强烈建议保持 true，关闭后所有数据明文传输，存在严重安全风险",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== debug（调试设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "debug.level",
 ConfigFileName = file,
 DisplayName = "调试日志级别",
 Description = "控制台调试信息详细程度\n1 = 仅正常日志；2 = 显示调试信息；3 = 显示所有数据包详情（极大量日志）",
 Category = "调试",
 DefaultValue = "1",
 MinValue = 1,
 MaxValue = 3,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== level-settings（世界设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.default-format",
 ConfigFileName = file,
 DisplayName = "默认世界存储格式",
 Description = "新建世界使用的存储格式\nleveldb = 基岩版原生（推荐）；mcbeta = 旧版兼容；anvil = Java 版格式（实验性，不推荐）",
 Category = "世界",
 DefaultValue = "leveldb",
 AllowedValues = ["leveldb", "mcbeta", "anvil"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.auto-tick-rate",
 ConfigFileName = file,
 DisplayName = "自动调节 tick 频率",
 Description = "服务器卡顿时自动降低 tick 频率以维持稳定\n开启后服务器会动态调整以维持 20 TPS",
 Category = "世界",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.auto-tick-rate-limit",
 ConfigFileName = file,
 DisplayName = "自动降频上限",
 Description = "自动降频的最大倍率，避免服务器 tick 速率被降到不可接受的程度",
 Category = "世界",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.base-tick-rate",
 ConfigFileName = file,
 DisplayName = "基础 tick 频率",
 Description = "基础 tick 倍率\n1 = 20 TPS（原版）；2 = 10 TPS（半速）；3 = 约 6.7 TPS\n调大可省 CPU 但游戏变卡",
 Category = "世界",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.always-tick-players",
 ConfigFileName = file,
 DisplayName = "每 tick 都处理玩家",
 Description = "true 时无论其他设置如何，每个 tick 都处理玩家逻辑\n一般保持 false",
 Category = "世界",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== chunk-sending（区块发送） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-sending.per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 发送区块数",
 Description = "每个 tick（1/20 秒）向单个玩家发送多少个区块\n值越大玩家加载地形越快，但带宽和 CPU 占用越高\n低配服建议 4，高配可调到 8-16",
 Category = "区块发送",
 DefaultValue = "4",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-sending.spawn-threshold",
 ConfigFileName = file,
 DisplayName = "出生前发送区块数",
 Description = "玩家进服前至少需要发送多少个区块才能让其出生\n过低会导致玩家悬空或掉入未加载地形；过高会增加登录等待时间",
 Category = "区块发送",
 DefaultValue = "56",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-sending.cache-chunks",
 ConfigFileName = file,
 DisplayName = "缓存区块序列化数据",
 Description = "true 时在内存中保存区块的序列化副本，加快向多个玩家发送同一区块的速度\n适合玩家密集的静态世界（如大厅服）\n动态生存服建议关闭以省内存",
 Category = "区块发送",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== chunk-ticking（区块 tick 处理） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-ticking.per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 处理区块上限",
 Description = "每 tick 最多处理多少个区块（实体的 AI、红石、作物生长等）\n降低此值可缓解实体密集时的卡顿，但作物生长和红石会变慢",
 Category = "区块 tick",
 DefaultValue = "40",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-ticking.tick-radius",
 ConfigFileName = file,
 DisplayName = "区块 tick 半径",
 Description = "玩家周围多少区块半径内会被 tick\n3 = 3 个区块半径（7x7 范围）\n值越大玩家附近活动越流畅，但 CPU 占用越高",
 Category = "区块 tick",
 DefaultValue = "3",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-ticking.clear-tick-list",
 ConfigFileName = file,
 DisplayName = "清空 tick 列表",
 Description = "是否在每次 tick 后清空待处理列表\n开启可防止列表累积但可能影响连续的红石/作物逻辑\n一般保持 false",
 Category = "区块 tick",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== chunk-generation（区块生成） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-generation.queue-size",
 ConfigFileName = file,
 DisplayName = "生成队列上限",
 Description = "等待生成的区块队列最大长度\n队列满时新请求会被丢弃\n玩家快速移动（如鞘翅飞行）时可适当调大",
 Category = "区块生成",
 DefaultValue = "8",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-generation.population-queue-size",
 ConfigFileName = file,
 DisplayName = "装饰队列上限",
 Description = "等待装饰（放置花草、矿物、结构等）的区块队列最大长度\n值过小会导致地形装饰滞后",
 Category = "区块生成",
 DefaultValue = "8",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== leveldb（LevelDB 存储） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "leveldb.use-native",
 ConfigFileName = file,
 DisplayName = "使用原生 LevelDB",
 Description = "true 时使用 C++ 原生 LevelDB 实现以获得更高性能\n需服务器安装对应 native 库，否则回退到 Java 实现",
 Category = "LevelDB",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "leveldb.cache-size-mb",
 ConfigFileName = file,
 DisplayName = "LevelDB 缓存大小",
 Description = "LevelDB 内存缓存大小（MB）\n值越大读取越快但占用内存越多\n大型世界建议 128-256 MB",
 Category = "LevelDB",
 DefaultValue = "80",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== ticks-per ====================

 Register(new ServerConfigDescriptor
 {
 Key = "ticks-per.autosave",
 ConfigFileName = file,
 DisplayName = "自动保存间隔",
 Description = "服务器自动保存世界与玩家数据的间隔（tick）\n6000 = 每 5 分钟保存一次（20 tick = 1 秒）\n0 = 禁用自动保存（不推荐，崩服会丢失进度）",
 Category = "Tick 间隔",
 DefaultValue = "6000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== player（玩家设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "player.save-player-data",
 ConfigFileName = file,
 DisplayName = "保存玩家数据",
 Description = "true 时玩家数据保存为 players/<玩家名>.dat\nfalse 时不保存，便于插件完全接管玩家数据\n一般保持 true",
 Category = "玩家",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player.skin-change-cooldown",
 ConfigFileName = file,
 DisplayName = "皮肤更换冷却",
 Description = "玩家两次更换皮肤之间的冷却时间（秒）\n0 = 无冷却\n防止玩家通过频繁换皮肤刷屏或攻击服务器",
 Category = "玩家",
 DefaultValue = "15",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player.attack-stop-sprint",
 ConfigFileName = file,
 DisplayName = "攻击停止冲刺",
 Description = "true 时玩家攻击实体后会停止冲刺（原版行为）\nfalse 时攻击不会打断冲刺（类似 1.8 PVP 手感）",
 Category = "玩家",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ============================================================
 // 基岩版 server.properties（使用 nukkit-server.properties 区分）
 // ============================================================

 RegisterNukkitServerProperties();
}

/// <summary>
/// 注册 Nukkit 基岩版 server.properties 的描述符
/// ️ 基岩版字段与 Java 版不同（端口 UDP、无 spectator、online-mode 指 Xbox Live）
/// 使用文件名 nukkit-server.properties 与 Java 版描述符区分
/// </summary>
private void RegisterNukkitServerProperties()
{
 const string file = "nukkit-server.properties";

 // ---------- 网络与端口 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "server-name",
 ConfigFileName = file,
 DisplayName = "服务器名称（MOTD）",
 Description = "服务器在客户端服务器列表中显示的名称\n基岩版对 § 颜色码支持有限，建议使用纯文本或简单颜色\n不能包含分号",
 Category = "网络",
 DefaultValue = "Dedicated Server",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server-port",
 ConfigFileName = file,
 DisplayName = "IPv4 端口（UDP）",
 Description = "服务器监听的 IPv4 UDP 端口\n️ 必须开放 UDP 协议，不是 TCP！\n路由器端口转发也需选 UDP\n基岩版默认 19132（Java 版是 25565/TCP）",
 Category = "网络",
 DefaultValue = "19132",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server-portv6",
 ConfigFileName = file,
 DisplayName = "IPv6 端口（UDP）",
 Description = "服务器监听的 IPv6 UDP 端口\n不需要 IPv6 时可设为 0 禁用",
 Category = "网络",
 DefaultValue = "19133",
 MinValue = 0,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-lan-visibility",
 ConfigFileName = file,
 DisplayName = "局域网可见性",
 Description = "true 时监听并响应局域网服务器发现请求\n同一台机器跑多个 Nukkit 时建议关闭以避免端口冲突",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ---------- 玩家与权限 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "max-players",
 ConfigFileName = file,
 DisplayName = "最大玩家数",
 Description = "服务器同时允许的最大玩家数\n值越高对性能影响越大",
 Category = "玩家",
 DefaultValue = "10",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "online-mode",
 ConfigFileName = file,
 DisplayName = "Xbox Live 验证",
 Description = "基岩版关键差异：true 时所有玩家必须通过 Xbox Live 认证\n公网服务器强烈建议开启，关闭会导致玩家可伪装身份\n远程（非 LAN）连接无论此设置如何，始终需要 Xbox Live 认证",
 Category = "玩家",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "white-list",
 ConfigFileName = file,
 DisplayName = "启用白名单",
 Description = "true 时仅 allowlist.json 中的玩家可加入",
 Category = "玩家",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "default-player-permission-level",
 ConfigFileName = file,
 DisplayName = "新玩家权限等级",
 Description = "首次加入的玩家默认权限等级\nvisitor = 访客（仅参观，不能交互）\nmember = 成员（正常游玩，推荐）\noperator = 管理员（OP 权限，️ 生产环境绝不使用！）",
 Category = "玩家",
 DefaultValue = "member",
 AllowedValues = ["visitor", "member", "operator"],
 ValueType = "enum",
 RequiresRestart = true
 });

 // ---------- 游戏模式与难度 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "gamemode",
 ConfigFileName = file,
 DisplayName = "默认游戏模式",
 Description = "新玩家加入时的默认游戏模式\n️ 基岩版无 spectator 选项！\nsurvival = 生存；creative = 创造；adventure = 冒险",
 Category = "游戏",
 DefaultValue = "survival",
 AllowedValues = ["survival", "creative", "adventure"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "force-gamemode",
 ConfigFileName = file,
 DisplayName = "强制游戏模式",
 Description = "true 时玩家进服始终被强制设置为 gamemode 指定的模式\n忽略其上次退出时的模式",
 Category = "游戏",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "difficulty",
 ConfigFileName = file,
 DisplayName = "难度",
 Description = "世界难度\npeaceful = 和平（不刷怪）；easy = 简单；normal = 普通；hard = 困难（僵尸破门等）",
 Category = "游戏",
 DefaultValue = "easy",
 AllowedValues = ["peaceful", "easy", "normal", "hard"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-cheats",
 ConfigFileName = file,
 DisplayName = "允许作弊",
 Description = "true 时允许使用 /gamemode、/give 等作弊命令\n生存服建议 false，创造/测试服可设 true",
 Category = "游戏",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "texturepack-required",
 ConfigFileName = file,
 DisplayName = "强制资源包",
 Description = "true 时玩家必须接受服务器资源包才能进服\n拒绝资源包的玩家会被踢出",
 Category = "游戏",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ---------- 世界生成 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "level-name",
 ConfigFileName = file,
 DisplayName = "世界名称",
 Description = "世界文件夹的名称\n每个世界在 worlds/ 下有独立文件夹\n改名为新世界，原世界保留但不再加载",
 Category = "世界",
 DefaultValue = "Bedrock level",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-seed",
 ConfigFileName = file,
 DisplayName = "世界种子",
 Description = "世界生成种子\n留空则随机生成\n相同种子生成相同地形",
 Category = "世界",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-type",
 ConfigFileName = file,
 DisplayName = "世界类型",
 Description = "地形类型\nDEFAULT = 标准地形；FLAT = 超平坦；LEGACY = 旧版地形\n️ 与 Java 版选项不同（无 amplified、largeBiomes）",
 Category = "世界",
 DefaultValue = "DEFAULT",
 AllowedValues = ["DEFAULT", "FLAT", "LEGACY"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "view-distance",
 ConfigFileName = file,
 DisplayName = "视野距离",
 Description = "玩家可见的区块半径\n️ 基岩版默认 32，比 Java 版的 10 大很多！\n值越大带宽和内存占用越高，公网服建议 10-16",
 Category = "世界",
 DefaultValue = "32",
 MinValue = 5,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "tick-distance",
 ConfigFileName = file,
 DisplayName = "tick 距离",
 Description = "玩家周围多少区块半径内会被服务器 tick（处理实体、红石等）\n基岩版独有字段，Java 版无此项\n值越大 CPU 占用越高",
 Category = "世界",
 DefaultValue = "4",
 MinValue = 4,
 MaxValue = 12,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "generate-structures",
 ConfigFileName = file,
 DisplayName = "生成结构",
 Description = "是否生成村庄、神殿、废弃矿井等结构",
 Category = "世界",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ---------- 安全与反作弊（基岩版独有） ----------

 Register(new ServerConfigDescriptor
 {
 Key = "server-authoritative-movement",
 ConfigFileName = file,
 DisplayName = "服务器权威移动",
 Description = "基岩版反作弊关键字段！\nserver-auth = 服务器校验玩家移动，发现异常回滚\nserver-auth-with-rewind = 同上但允许客户端预测\nclient-auth = 客户端权威（不推荐，易被作弊）",
 Category = "反作弊",
 DefaultValue = "server-auth",
 AllowedValues = ["client-auth", "server-auth", "server-auth-with-rewind"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server-authoritative-block-breaking",
 ConfigFileName = file,
 DisplayName = "服务器权威破坏方块",
 Description = "true 时服务器校验玩家破坏方块的合法性\n防加速挖矿作弊",
 Category = "反作弊",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-movement-action-direction-threshold",
 ConfigFileName = file,
 DisplayName = "移动方向阈值",
 Description = "玩家移动方向与视线方向的偏差阈值\n超过此值视为可疑移动",
 Category = "反作弊",
 DefaultValue = "0.65",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-movement-distance-threshold",
 ConfigFileName = file,
 DisplayName = "移动距离阈值",
 Description = "单 tick 内玩家移动距离超过此值视为可疑\n可能在使用加速/飞行作弊",
 Category = "反作弊",
 DefaultValue = "0.5",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-movement-duration-threshold-in-ms",
 ConfigFileName = file,
 DisplayName = "异常持续时间阈值",
 Description = "玩家移动异常持续多久才视为作弊并触发回滚（毫秒）",
 Category = "反作弊",
 DefaultValue = "500",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "correct-player-movement",
 ConfigFileName = file,
 DisplayName = "纠正玩家移动",
 Description = "true 时服务器主动纠正玩家可疑的移动（强制回滚到合法位置）",
 Category = "反作弊",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ---------- 性能与维护 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "max-threads",
 ConfigFileName = file,
 DisplayName = "最大线程数",
 Description = "服务器最大使用的线程数\n0 = 自动检测使用尽可能多的线程",
 Category = "性能",
 DefaultValue = "8",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-idle-timeout",
 ConfigFileName = file,
 DisplayName = "玩家挂机踢出",
 Description = "玩家挂机多少分钟后被踢出\n0 = 永不踢出",
 Category = "性能",
 DefaultValue = "30",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "content-log-file-enabled",
 ConfigFileName = file,
 DisplayName = "内容日志写文件",
 Description = "true 时将内容错误（如资源包解析失败）写入日志文件\n便于排查问题",
 Category = "性能",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "compression-threshold",
 ConfigFileName = file,
 DisplayName = "压缩阈值",
 Description = "网络数据包压缩的最小原始载荷大小（字节）\n值越大 CPU 越省但带宽越费\n基岩版默认 1（几乎全压缩）",
 Category = "性能",
 DefaultValue = "1",
 MinValue = 0,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "compression-algorithm",
 ConfigFileName = file,
 DisplayName = "压缩算法",
 Description = "网络压缩算法\nzlib = 标准压缩（兼容性好）\nsnappy = Google Snappy（速度更快但压缩比低）",
 Category = "性能",
 DefaultValue = "zlib",
 AllowedValues = ["zlib", "snappy"],
 ValueType = "enum",
 RequiresRestart = true
 });

 // ---------- 远程管理 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "enable-rcon",
 ConfigFileName = file,
 DisplayName = "启用 RCON",
 Description = "是否启用远程控制台协议（RCON）\n允许通过 TCP 发送命令到服务器\n启用务必设置强密码！",
 Category = "远程管理",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "rcon.password",
 ConfigFileName = file,
 DisplayName = "RCON 密码",
 Description = "RCON 远程管理密码\n启用 RCON 时必须设置，否则任何人都能远程控制服务器",
 Category = "远程管理",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "rcon.port",
 ConfigFileName = file,
 DisplayName = "RCON 端口",
 Description = "RCON 监听的 TCP 端口\n️ 注意不要与 server-port（UDP）冲突",
 Category = "远程管理",
 DefaultValue = "19132",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterPowerNukkitYml.cs
// 功能描述: 注册 PowerNukkitX（基岩版）配置文件的描述符
// 包含 powernukkit.yml 与基岩版 server.properties（用 powernukkit-server.properties 区分）
// 数据来源: PowerNukkitX/PowerNukkitX src/main/java/org/powernukkitx/config/* (master 分支)
// 适用版本: PowerNukkitX 3.0.0（master 分支）
// -----------------------------------------------------------------------------

private void RegisterPowerNukkitYml()
{
 const string file = "powernukkit.yml";

 // ==================== settings（基础设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.ip",
 ConfigFileName = file,
 DisplayName = "服务器监听 IP",
 Description = "服务器绑定的 IPv4 地址\n0.0.0.0 表示监听所有网卡；多网卡环境下可指定具体 IP",
 Category = "基础设置",
 DefaultValue = "0.0.0.0",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.port",
 ConfigFileName = file,
 DisplayName = "服务器端口（UDP）",
 Description = "服务器监听的 UDP 端口\n️ 基岩版使用 UDP，路由器端口转发必须选 UDP 协议\n基岩版默认 19132（Java 版是 25565/TCP）",
 Category = "基础设置",
 DefaultValue = "19132",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.maxplayers",
 ConfigFileName = file,
 DisplayName = "最大玩家数",
 Description = "服务器同时允许的最大玩家数",
 Category = "基础设置",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.defaultlevel",
 ConfigFileName = file,
 DisplayName = "默认世界名",
 Description = "玩家首次进服默认进入的世界名称",
 Category = "基础设置",
 DefaultValue = "world",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.allowlist",
 ConfigFileName = file,
 DisplayName = "启用白名单",
 Description = "是否启用白名单\n启用后仅 allowlist.json 中的玩家可加入",
 Category = "基础设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.allowlist.message",
 ConfigFileName = file,
 DisplayName = "白名单拒绝消息",
 Description = "玩家被白名单拒绝时显示的提示文本",
 Category = "基础设置",
 DefaultValue = "Server is white-listed",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.motd",
 ConfigFileName = file,
 DisplayName = "服务器 MOTD",
 Description = "服务器在客户端服务器列表中显示的名称\n可使用 § 颜色码",
 Category = "基础设置",
 DefaultValue = "PowerNukkitX Server",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.sub-motd",
 ConfigFileName = file,
 DisplayName = "子 MOTD",
 Description = "服务器副标题，部分客户端在 MOTD 下方显示",
 Category = "基础设置",
 DefaultValue = "powernukkitx.org",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.language",
 ConfigFileName = file,
 DisplayName = "服务器语言",
 Description = "控制台与提示消息使用的语言代码\neng 英语 / chs 简中 / cht 繁中 / jpn 日语 / rus 俄语 / spa 西语 / pol 波兰语 / bra 葡语 / kor 韩语 / ukr 乌克语 / deu 德语 / ltu 立陶宛语 / idn 印尼语 / cze 捷克语 / tur 土耳其语 / fin 芬兰语",
 Category = "基础设置",
 DefaultValue = "eng",
 AllowedValues = ["eng", "chs", "cht", "jpn", "rus", "spa", "pol", "bra", "kor", "ukr", "deu", "ltu", "idn", "cze", "tur", "fin"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.forcetranslate",
 ConfigFileName = file,
 DisplayName = "强制使用服务器语言",
 Description = "true 时所有字符串按服务器语言翻译后发送给客户端\nfalse 时让客户端自行处理本地化（推荐）",
 Category = "基础设置",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.safespawn",
 ConfigFileName = file,
 DisplayName = "安全出生",
 Description = "是否在玩家首次进服时寻找安全位置出生\n防止卡在方块中",
 Category = "基础设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.autosave",
 ConfigFileName = file,
 DisplayName = "自动保存",
 Description = "是否启用自动保存（间隔由 autosaveDelay 控制）",
 Category = "基础设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.autosaveDelay",
 ConfigFileName = file,
 DisplayName = "自动保存间隔",
 Description = "自动保存的间隔（tick）\n6000 = 每 5 分钟保存一次（20 tick = 1 秒）\n0 = 禁用自动保存（不推荐）",
 Category = "基础设置",
 DefaultValue = "6000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.saveunknownblock",
 ConfigFileName = file,
 DisplayName = "保存未知方块",
 Description = "是否在 NBT 中保存 PNX 无法识别的方块\n用于行为包扩展兼容",
 Category = "基础设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.xboxauth",
 ConfigFileName = file,
 DisplayName = "Xbox Live 验证",
 Description = "是否要求所有玩家通过 Xbox Live 认证\n公网服务器强烈建议开启，关闭会导致玩家可伪装身份\n远程（非 LAN）连接无论此设置如何，始终需要 Xbox Live 认证",
 Category = "基础设置",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== player-settings（玩家设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "player-settings.saveplayerdata",
 ConfigFileName = file,
 DisplayName = "保存玩家数据",
 Description = "true 时玩家数据保存为 players/<UUID>.dat",
 Category = "玩家",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-settings.skinchangecooldown",
 ConfigFileName = file,
 DisplayName = "皮肤更换冷却",
 Description = "玩家两次更换皮肤之间的冷却时间（秒）\n0 = 无冷却\n防止玩家通过频繁换皮肤刷屏或攻击服务器",
 Category = "玩家",
 DefaultValue = "30",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-settings.forceskintrusted",
 ConfigFileName = file,
 DisplayName = "强制可信皮肤",
 Description = "true 时仅使用可信（Xbox Live）的皮肤",
 Category = "玩家",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-settings.checkmovement",
 ConfigFileName = file,
 DisplayName = "校验玩家移动",
 Description = "是否启用服务器端玩家移动校验（反作弊）",
 Category = "玩家",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-settings.rotationupdatethreshold",
 ConfigFileName = file,
 DisplayName = "旋转更新阈值",
 Description = "玩家旋转角度变化超过此值才发送更新\n降低网络包频率",
 Category = "玩家",
 DefaultValue = "1",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-settings.movementdistancethreshold",
 ConfigFileName = file,
 DisplayName = "移动距离阈值",
 Description = "玩家位移超过此值才发送位置更新",
 Category = "玩家",
 DefaultValue = "0.1",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "player-settings.spawnRadius",
 ConfigFileName = file,
 DisplayName = "出生保护半径",
 Description = "出生点周围此半径内的方块受到保护\n非 OP 玩家无法破坏",
 Category = "玩家",
 DefaultValue = "16",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== gameplay-settings（游戏玩法设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enablecommandblocks",
 ConfigFileName = file,
 DisplayName = "启用命令方块",
 Description = "是否允许使用命令方块",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.allowbeta",
 ConfigFileName = file,
 DisplayName = "允许 Beta 客户端",
 Description = "是否允许 Beta 版本客户端连接",
 Category = "游戏玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableredstone",
 ConfigFileName = file,
 DisplayName = "启用红石",
 Description = "是否启用红石系统",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.tickRedstone",
 ConfigFileName = file,
 DisplayName = "红石每 tick 处理",
 Description = "是否每 tick 都处理红石信号\n关闭后红石仍工作但更新频率降低",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.viewDistance",
 ConfigFileName = file,
 DisplayName = "视野距离",
 Description = "玩家可见的区块半径\n值越大带宽和内存占用越高\n公网服建议 8-12",
 Category = "游戏玩法",
 DefaultValue = "8",
 MinValue = 5,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.achivements",
 ConfigFileName = file,
 DisplayName = "启用成就",
 Description = "是否启用成就/进度系统",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.announceAchievements",
 ConfigFileName = file,
 DisplayName = "广播成就",
 Description = "玩家解锁成就时是否在聊天栏广播",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.spawnProtection",
 ConfigFileName = file,
 DisplayName = "出生保护半径",
 Description = "出生点保护半径（方块）\n非 OP 玩家无法在此范围内破坏",
 Category = "游戏玩法",
 DefaultValue = "16",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.gamemode",
 ConfigFileName = file,
 DisplayName = "默认游戏模式",
 Description = "新玩家默认游戏模式\n0 = 生存 / 1 = 创造 / 2 = 冒险\n️ 基岩版无 spectator 选项！",
 Category = "游戏玩法",
 DefaultValue = "0",
 AllowedValues = ["0", "1", "2"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.forceGamemode",
 ConfigFileName = file,
 DisplayName = "强制游戏模式",
 Description = "true 时玩家进服始终被强制设置为 gamemode 指定的模式",
 Category = "游戏玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.hardcore",
 ConfigFileName = file,
 DisplayName = "极限模式",
 Description = "是否启用极限模式（玩家死亡后封禁）",
 Category = "游戏玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.pvp",
 ConfigFileName = file,
 DisplayName = "启用 PvP",
 Description = "是否允许玩家间伤害",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.difficulty",
 ConfigFileName = file,
 DisplayName = "难度",
 Description = "世界难度\n0 = 和平（不刷怪）/ 1 = 简单 / 2 = 普通 / 3 = 困难（僵尸破门等）",
 Category = "游戏玩法",
 DefaultValue = "1",
 AllowedValues = ["0", "1", "2", "3"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.allowNether",
 ConfigFileName = file,
 DisplayName = "启用下界",
 Description = "是否加载下界维度",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.allowEnd",
 ConfigFileName = file,
 DisplayName = "启用末地",
 Description = "是否加载末地维度",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.forceResources",
 ConfigFileName = file,
 DisplayName = "强制资源包",
 Description = "true 时玩家必须接受服务器资源包才能进服\n拒绝资源包的玩家会被踢出",
 Category = "游戏玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.allowClientPacks",
 ConfigFileName = file,
 DisplayName = "允许客户端资源包",
 Description = "是否允许玩家使用客户端自带资源包",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.allowVibrantVisuals",
 ConfigFileName = file,
 DisplayName = "允许 Vibrant Visuals",
 Description = "是否允许客户端使用「鲜明视觉」图形选项",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.experiments",
 ConfigFileName = file,
 DisplayName = "实验特性",
 Description = "启用的实验性特性 ID 列表\n如 data_driven_vanilla_blocks_and_items、experimental_molang_features 等",
 Category = "游戏玩法",
 DefaultValue = "data_driven_vanilla_blocks_and_items",
 ValueType = "list",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.cacheStructures",
 ConfigFileName = file,
 DisplayName = "缓存结构",
 Description = "是否缓存世界生成结构以加速加载（占用内存）",
 Category = "游戏玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableEdu",
 ConfigFileName = file,
 DisplayName = "教育版特性",
 Description = "是否启用 Minecraft 教育版特性（化学、NPC 等）",
 Category = "游戏玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.muteEmoteAnnouncements",
 ConfigFileName = file,
 DisplayName = "静默表情广播",
 Description = "是否屏蔽玩家使用表情时的聊天栏广播",
 Category = "游戏玩法",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enablemobai",
 ConfigFileName = file,
 DisplayName = "启用生物 AI",
 Description = "是否启用实体 AI（寻路、行为）",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableRecipes",
 ConfigFileName = file,
 DisplayName = "启用配方",
 Description = "是否启用合成配方解锁",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableCreativeInventory",
 ConfigFileName = file,
 DisplayName = "启用创造物品栏",
 Description = "是否启用创造模式物品栏",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableDaylightCycle",
 ConfigFileName = file,
 DisplayName = "启用日夜循环",
 Description = "是否启用日夜循环",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableWeather",
 ConfigFileName = file,
 DisplayName = "启用天气",
 Description = "是否启用天气变化（雨、雷暴）",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableEntitySpawning",
 ConfigFileName = file,
 DisplayName = "启用实体生成",
 Description = "是否允许自然生成实体（怪物、动物）",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableBlockRandomTicking",
 ConfigFileName = file,
 DisplayName = "启用方块随机 tick",
 Description = "是否启用方块随机 tick（作物生长、草地蔓延等）",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableLiquidFlow",
 ConfigFileName = file,
 DisplayName = "启用液体流动",
 Description = "是否启用液体（水、熔岩）流动",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableItemDrops",
 ConfigFileName = file,
 DisplayName = "启用物品掉落",
 Description = "是否启用方块破坏后的物品掉落",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableXpOrbs",
 ConfigFileName = file,
 DisplayName = "启用经验球",
 Description = "是否启用经验球实体",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableExplosionBlockDamage",
 ConfigFileName = file,
 DisplayName = "启用爆炸破坏",
 Description = "爆炸是否对方块造成破坏",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableBlockGravity",
 ConfigFileName = file,
 DisplayName = "启用方块重力",
 Description = "是否启用受重力影响的方块（沙子、砂砾）",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "gameplay-settings.enableHunger",
 ConfigFileName = file,
 DisplayName = "启用饥饿值",
 Description = "是否启用玩家饥饿值系统",
 Category = "游戏玩法",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== misc-settings（杂项设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "misc-settings.shutdownmessage",
 ConfigFileName = file,
 DisplayName = "关服提示消息",
 Description = "服务器关闭时踢出玩家显示的提示文本",
 Category = "杂项",
 DefaultValue = "Server closed",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc-settings.installspark",
 ConfigFileName = file,
 DisplayName = "安装 Spark",
 Description = "是否自动下载并加载 Spark 性能分析插件",
 Category = "杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc-settings.bypassapicheck",
 ConfigFileName = file,
 DisplayName = "跳过 API 版本检查",
 Description = "true 时跳过插件对 PNX API 版本的兼容性检查\n️ 不推荐生产环境使用",
 Category = "杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc-settings.overrideserverauthblockbreaking",
 ConfigFileName = file,
 DisplayName = "覆盖服务器权威破坏",
 Description = "true 时覆盖基岩版 server-authoritative-block-breaking 字段\n强制启用服务器权威校验",
 Category = "杂项",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "misc-settings.enablemetrics",
 ConfigFileName = file,
 DisplayName = "启用统计上报",
 Description = "是否向 PNX bStats 上报匿名统计数据",
 Category = "杂项",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== level-settings（世界设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.levelthread",
 ConfigFileName = file,
 DisplayName = "每世界独立线程",
 Description = "true 时每个世界使用独立线程运行（PNX 多线程模型）\n开启可提升多世界性能但可能引发同步问题",
 Category = "世界",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.autotickrate",
 ConfigFileName = file,
 DisplayName = "自动调节 tick 频率",
 Description = "服务器卡顿时自动降低 tick 频率以维持稳定",
 Category = "世界",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.autotickratelimit",
 ConfigFileName = file,
 DisplayName = "自动降频上限",
 Description = "自动降频的最大倍率\n避免服务器 tick 速率被降到不可接受的程度",
 Category = "世界",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.basetickrate",
 ConfigFileName = file,
 DisplayName = "基础 tick 频率",
 Description = "基础 tick 倍率\n1 = 20 TPS（原版）/ 2 = 10 TPS（半速）\n调大可省 CPU 但游戏变卡",
 Category = "世界",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.alwaystickplayers",
 ConfigFileName = file,
 DisplayName = "每 tick 都处理玩家",
 Description = "true 时无论其他设置如何，每个 tick 都处理玩家逻辑\n一般保持 false",
 Category = "世界",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.loadalllevels",
 ConfigFileName = file,
 DisplayName = "加载所有世界",
 Description = "启动时是否加载所有已注册的世界",
 Category = "世界",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.chunkunloaddelay",
 ConfigFileName = file,
 DisplayName = "区块卸载延迟",
 Description = "区块无人引用后多久才卸载（毫秒）",
 Category = "世界",
 DefaultValue = "15000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.entityspawncap",
 ConfigFileName = file,
 DisplayName = "实体生成上限",
 Description = "单个世界实体数量上限",
 Category = "世界",
 DefaultValue = "512",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.fieldofview",
 ConfigFileName = file,
 DisplayName = "视场角",
 Description = "服务器发送给客户端的视场角（FOV）值",
 Category = "世界",
 DefaultValue = "100",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-settings.levelworkerthreads",
 ConfigFileName = file,
 DisplayName = "世界工作线程数",
 Description = "每个世界的工作线程数\n-1 表示自动根据 CPU 核心数决定",
 Category = "世界",
 DefaultValue = "-1",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== chunk-settings（区块设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.spawnlimit",
 ConfigFileName = file,
 DisplayName = "区块生成上限",
 Description = "每 tick 最多生成多少个区块",
 Category = "区块",
 DefaultValue = "3",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.perticksend",
 ConfigFileName = file,
 DisplayName = "每 tick 发送区块数",
 Description = "每个 tick 向单个玩家发送多少个区块\n值越大玩家加载地形越快但带宽占用越高",
 Category = "区块",
 DefaultValue = "32",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.spawnthreshold",
 ConfigFileName = file,
 DisplayName = "出生前发送区块数",
 Description = "玩家进服前至少需要发送多少个区块才能让其出生",
 Category = "区块",
 DefaultValue = "56",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.chunksperticks",
 ConfigFileName = file,
 DisplayName = "每 tick 处理区块数",
 Description = "每 tick 处理多少个区块的 tick（实体、红石、作物）\n-1 表示自动",
 Category = "区块",
 DefaultValue = "-1",
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.tickRadius",
 ConfigFileName = file,
 DisplayName = "区块 tick 半径",
 Description = "玩家周围多少区块半径内会被 tick\n值越大 CPU 占用越高",
 Category = "区块",
 DefaultValue = "4",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.lightupdates",
 ConfigFileName = file,
 DisplayName = "启用光照更新",
 Description = "是否启用光照计算与更新",
 Category = "区块",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.clearticklist",
 ConfigFileName = file,
 DisplayName = "清空 tick 列表",
 Description = "是否在每次 tick 后清空待处理列表",
 Category = "区块",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.generationqueuesize",
 ConfigFileName = file,
 DisplayName = "生成队列上限",
 Description = "等待生成的区块队列最大长度\n玩家快速移动（如鞘翅飞行）时可适当调大",
 Category = "区块",
 DefaultValue = "8",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.saveGenerated",
 ConfigFileName = file,
 DisplayName = "保存生成的区块",
 Description = "是否将新生成的区块立即保存到磁盘",
 Category = "区块",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.convertBDSChunks",
 ConfigFileName = file,
 DisplayName = "转换 BDS 区块",
 Description = "是否将官方 BDS 服务器生成的区块格式转换为 PNX 格式",
 Category = "区块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "chunk-settings.disableblockticking",
 ConfigFileName = file,
 DisplayName = "禁用方块 tick 列表",
 Description = "不进行随机 tick 的方块 ID 列表（如 minecraft:grass）",
 Category = "区块",
 DefaultValue = "",
 ValueType = "list",
 RequiresRestart = true
 });

 // ==================== network-settings（网络设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.queryplugins",
 ConfigFileName = file,
 DisplayName = "Query 暴露插件列表",
 Description = "true 时允许通过 GameSpy Query 协议列出已加载插件\n公网服务器建议关闭以避免泄露插件信息",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.compressionlevel",
 ConfigFileName = file,
 DisplayName = "Zlib 压缩级别",
 Description = "数据包 Zlib 压缩级别\n值越大 CPU 占用越高、带宽越省\n基岩版推荐 4-6",
 Category = "网络",
 DefaultValue = "4",
 MinValue = 1,
 MaxValue = 9,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.zlibprovider",
 ConfigFileName = file,
 DisplayName = "Zlib 实现提供者",
 Description = "Zlib 压缩库的提供者\n0 = Java / 1 = Native / 2 = JNI / 3 = Netty（默认）/ 4 = System",
 Category = "网络",
 DefaultValue = "3",
 MinValue = 0,
 MaxValue = 4,
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.snappy",
 ConfigFileName = file,
 DisplayName = "启用 Snappy 压缩",
 Description = "实验性：使用 Google Snappy 算法替代 Zlib\n压缩比低但速度极快\n️ 实验功能，可能不兼容旧客户端",
 Category = "网络",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.compressionbuffersize",
 ConfigFileName = file,
 DisplayName = "压缩缓冲区大小",
 Description = "Zlib 压缩缓冲区大小（字节）\n默认 1 MB",
 Category = "网络",
 DefaultValue = "1048576",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.maxdecompresssize",
 ConfigFileName = file,
 DisplayName = "最大解压大小",
 Description = "单个数据包最大解压大小（字节）\n默认 256 MB\n防止恶意超大包攻击",
 Category = "网络",
 DefaultValue = "268435456",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.packetlimit",
 ConfigFileName = file,
 DisplayName = "数据包大小上限",
 Description = "单个数据包最大字节数\n超过此值的包会被拒绝",
 Category = "网络",
 DefaultValue = "8000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.query",
 ConfigFileName = file,
 DisplayName = "启用 Query",
 Description = "是否启用 GameSpy Query 协议（用于服务器列表服务）",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.encryption",
 ConfigFileName = file,
 DisplayName = "启用网络加密",
 Description = "是否启用基岩版网络加密（基于 ECDH 握手）\n强烈建议保持 true，关闭后所有数据明文传输，存在严重安全风险",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.logintime",
 ConfigFileName = file,
 DisplayName = "检查登录时间",
 Description = "是否校验玩家登录用时\n防止登录洪水攻击",
 Category = "网络",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.autoflush",
 ConfigFileName = file,
 DisplayName = "自动刷新发送缓冲",
 Description = "是否自动刷新网络发送缓冲\n关闭可省 CPU 但增加延迟",
 Category = "网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.flushinterval",
 ConfigFileName = file,
 DisplayName = "刷新间隔",
 Description = "自动刷新发送缓冲的间隔（tick）",
 Category = "网络",
 DefaultValue = "10",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.maxqueuedbytes",
 ConfigFileName = file,
 DisplayName = "最大排队字节数",
 Description = "单个玩家发送队列最大字节数\n默认 64 MB\n防止慢速客户端拖垮服务器",
 Category = "网络",
 DefaultValue = "67108864",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.cookiemode",
 ConfigFileName = file,
 DisplayName = "Cookie 模式",
 Description = "处理基岩版 1.21+ Cookie 的模式\nACTIVE = 接受并响应 / IGNORE = 忽略",
 Category = "网络",
 DefaultValue = "ACTIVE",
 AllowedValues = ["ACTIVE", "IGNORE"],
 ValueType = "enum",
 RequiresRestart = true
 });

 // ---------- 速率限制（network-settings.rate-limit） ----------

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.rate-limit.enabled",
 ConfigFileName = file,
 DisplayName = "启用速率限制",
 Description = "是否启用网络包速率限制（防洪水攻击）",
 Category = "网络-速率限制",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.rate-limit.maxinboundpersecond",
 ConfigFileName = file,
 DisplayName = "每秒入站包上限",
 Description = "单个玩家每秒可发送的最大数据包数",
 Category = "网络-速率限制",
 DefaultValue = "1500",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.rate-limit.maxpacketspertick",
 ConfigFileName = file,
 DisplayName = "每 tick 包上限",
 Description = "单个玩家每 tick 可发送的最大数据包数",
 Category = "网络-速率限制",
 DefaultValue = "500",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.rate-limit.maxcommandsperplayer",
 ConfigFileName = file,
 DisplayName = "每秒命令上限",
 Description = "单个玩家每秒可执行的命令数",
 Category = "网络-速率限制",
 DefaultValue = "10",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.rate-limit.maxchatperplayer",
 ConfigFileName = file,
 DisplayName = "每秒聊天上限",
 Description = "单个玩家每秒可发送的聊天消息数",
 Category = "网络-速率限制",
 DefaultValue = "2",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.rate-limit.maxformresponsesperplayer",
 ConfigFileName = file,
 DisplayName = "每秒表单响应上限",
 Description = "单个玩家每秒可发送的表单（UI）响应数",
 Category = "网络-速率限制",
 DefaultValue = "20",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.rate-limit.maxmovementperplayer",
 ConfigFileName = file,
 DisplayName = "每秒移动包上限",
 Description = "单个玩家每秒可发送的移动数据包数",
 Category = "网络-速率限制",
 DefaultValue = "40",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ---------- 僵尸网络检测（network-settings.botnet） ----------

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.botnet.enabled",
 ConfigFileName = file,
 DisplayName = "启用僵尸网络检测",
 Description = "是否启用基于行为分析的僵尸网络检测",
 Category = "网络-僵尸网络",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.botnet.suspiciousthreshold",
 ConfigFileName = file,
 DisplayName = "可疑阈值",
 Description = "IP 行为评分超过此值视为可疑",
 Category = "网络-僵尸网络",
 DefaultValue = "300",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.botnet.minsuspiciousips",
 ConfigFileName = file,
 DisplayName = "最小可疑 IP 数",
 Description = "触发自动封禁所需的最小可疑 IP 数",
 Category = "网络-僵尸网络",
 DefaultValue = "3",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.botnet.autoblock",
 ConfigFileName = file,
 DisplayName = "自动封禁",
 Description = "是否在检测到僵尸网络时自动封禁可疑 IP",
 Category = "网络-僵尸网络",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.botnet.autoblockdurationseconds",
 ConfigFileName = file,
 DisplayName = "自动封禁时长",
 Description = "自动封禁的持续时长（秒）",
 Category = "网络-僵尸网络",
 DefaultValue = "60",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-settings.botnet.minscore",
 ConfigFileName = file,
 DisplayName = "最小评分",
 Description = "单个 IP 触发评分的最小行为次数",
 Category = "网络-僵尸网络",
 DefaultValue = "2",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== debug-settings（调试设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "debug-settings.deprecatedverbose",
 ConfigFileName = file,
 DisplayName = "弃用 API 警告",
 Description = "插件使用已弃用的 API 方法时是否在控制台打印警告\n开发环境建议开启，生产环境可关闭以减少日志噪音",
 Category = "调试",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "debug-settings.level",
 ConfigFileName = file,
 DisplayName = "调试日志级别",
 Description = "控制台日志详细程度\nINFO = 正常日志 / DEBUG = 调试信息 / TRACE = 追踪（极大量日志）",
 Category = "调试",
 DefaultValue = "INFO",
 AllowedValues = ["INFO", "DEBUG", "TRACE"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "debug-settings.command",
 ConfigFileName = file,
 DisplayName = "启用调试命令",
 Description = "是否启用 /debug 调试命令",
 Category = "调试",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "debug-settings.packet.mode",
 ConfigFileName = file,
 DisplayName = "数据包调试模式",
 Description = "false = 忽略数据包日志 / true = 记录 packetList 中指定的数据包",
 Category = "调试",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "debug-settings.packetList",
 ConfigFileName = file,
 DisplayName = "数据包白名单",
 Description = "启用 packet.mode 时要记录的数据包 ID 列表",
 Category = "调试",
 DefaultValue = "",
 ValueType = "list",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "debug-settings.disableencodinglimits",
 ConfigFileName = file,
 DisplayName = "禁用编码限制",
 Description = "是否禁用 NBT 编码长度限制\n️ 仅调试用，会带来安全风险",
 Category = "调试",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== performance-settings（性能设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "performance-settings.asyncworkers",
 ConfigFileName = file,
 DisplayName = "异步工作线程数",
 Description = "AsyncTask 的工作线程数\nauto 自动检测 CPU 核心数（至少 4）\n手动设置时建议不超过 CPU 核心数",
 Category = "性能",
 DefaultValue = "auto",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance-settings.basetps",
 ConfigFileName = file,
 DisplayName = "基础 TPS",
 Description = "服务器目标 TPS（每秒 tick 数）\n原版为 20",
 Category = "性能",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance-settings.registrycache.enable",
 ConfigFileName = file,
 DisplayName = "启用注册表缓存",
 Description = "是否在启动时将方块/物品注册表缓存到磁盘以加速下次启动",
 Category = "性能",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance-settings.registrycache.path",
 ConfigFileName = file,
 DisplayName = "缓存文件路径",
 Description = "注册表缓存文件路径",
 Category = "性能",
 DefaultValue = "path/to/your/registry_cache.bin",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance-settings.forcegcpercentage",
 ConfigFileName = file,
 DisplayName = "强制 GC 阈值",
 Description = "内存使用率达到此比例时强制触发 GC\n1.0 = 100%（禁用强制 GC）\n0.85 = 85% 触发 GC",
 Category = "性能",
 DefaultValue = "1.0",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "performance-settings.freeze-array.enable",
 ConfigFileName = file,
 DisplayName = "启用冻结数组",
 Description = "是否启用冻结数组优化\n将常量数组包装为不可变版本，便于 JVM 内联优化",
 Category = "性能-冻结数组",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ============================================================
 // 基岩版 server.properties（使用 powernukkit-server.properties 区分）
 // ============================================================

 RegisterPowerNukkitServerProperties();
}

/// <summary>
/// 注册 PowerNukkitX 基岩版 server.properties 的描述符
/// ️ 基岩版字段与 Java 版不同（端口 UDP、无 spectator、online-mode 指 Xbox Live）
/// 使用文件名 powernukkit-server.properties 与 Java 版描述符区分
/// 数据来源：LegacyServerPropertiesKeys.java 枚举
/// </summary>
private void RegisterPowerNukkitServerProperties()
{
 const string file = "powernukkit-server.properties";

 // ---------- 服务器基础信息 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "motd",
 ConfigFileName = file,
 DisplayName = "服务器 MOTD",
 Description = "服务器在客户端列表中显示的名称\n与 powernukkit.yml 中的 motd 同步",
 Category = "基础信息",
 DefaultValue = "PowerNukkitX Server",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "sub-motd",
 ConfigFileName = file,
 DisplayName = "子 MOTD",
 Description = "服务器副标题",
 Category = "基础信息",
 DefaultValue = "powernukkitx.org",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server-port",
 ConfigFileName = file,
 DisplayName = "IPv4 端口（UDP）",
 Description = "服务器监听的 IPv4 UDP 端口\n️ 必须开放 UDP 协议！路由器端口转发也需选 UDP\n基岩版默认 19132（Java 版是 25565/TCP）",
 Category = "网络",
 DefaultValue = "19132",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "server-ip",
 ConfigFileName = file,
 DisplayName = "服务器 IP",
 Description = "服务器绑定的 IPv4 地址\n0.0.0.0 表示监听所有网卡",
 Category = "网络",
 DefaultValue = "0.0.0.0",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "view-distance",
 ConfigFileName = file,
 DisplayName = "视野距离",
 Description = "玩家可见的区块半径\n️ 基岩版默认 8，比 Java 版的 10 小\n值越大带宽和内存占用越高",
 Category = "世界",
 DefaultValue = "8",
 MinValue = 5,
 ValueType = "int",
 RequiresRestart = true
 });

 // ---------- 玩家与权限 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "white-list",
 ConfigFileName = file,
 DisplayName = "启用白名单",
 Description = "true 时仅 allowlist.json 中的玩家可加入",
 Category = "玩家",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "max-players",
 ConfigFileName = file,
 DisplayName = "最大玩家数",
 Description = "服务器同时允许的最大玩家数\n值越高对性能影响越大",
 Category = "玩家",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "xbox-auth",
 ConfigFileName = file,
 DisplayName = "Xbox Live 验证",
 Description = "基岩版关键差异：true 时所有玩家必须通过 Xbox Live 认证\n️ 与 Java 版 online-mode 含义不同！\n公网服务器强烈建议开启，关闭会导致玩家可伪装身份\n远程（非 LAN）连接无论此设置如何，始终需要 Xbox Live 认证",
 Category = "玩家",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ---------- 游戏模式与难度 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "gamemode",
 ConfigFileName = file,
 DisplayName = "默认游戏模式",
 Description = "新玩家加入时的默认游戏模式\n️ 基岩版无 spectator 选项！\n0 = 生存 / 1 = 创造 / 2 = 冒险",
 Category = "游戏",
 DefaultValue = "0",
 AllowedValues = ["0", "1", "2"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "force-gamemode",
 ConfigFileName = file,
 DisplayName = "强制游戏模式",
 Description = "true 时玩家进服始终被强制设置为 gamemode 指定的模式\n忽略其上次退出时的模式",
 Category = "游戏",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "difficulty",
 ConfigFileName = file,
 DisplayName = "难度",
 Description = "世界难度\n0 = 和平（不刷怪）/ 1 = 简单 / 2 = 普通 / 3 = 困难（僵尸破门等）",
 Category = "游戏",
 DefaultValue = "1",
 AllowedValues = ["0", "1", "2", "3"],
 ValueType = "enum",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "hardcore",
 ConfigFileName = file,
 DisplayName = "极限模式",
 Description = "是否启用极限模式（玩家死亡后封禁）",
 Category = "游戏",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "pvp",
 ConfigFileName = file,
 DisplayName = "启用 PvP",
 Description = "是否允许玩家间伤害",
 Category = "游戏",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-flight",
 ConfigFileName = file,
 DisplayName = "允许飞行",
 Description = "是否允许玩家在生存模式飞行\n️ 这是反作弊豁免，而非启用飞行能力\n建议关闭以防止飞行作弊",
 Category = "游戏",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "achievements",
 ConfigFileName = file,
 DisplayName = "启用成就",
 Description = "是否启用成就/进度系统",
 Category = "游戏",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "announce-player-achievements",
 ConfigFileName = file,
 DisplayName = "广播成就",
 Description = "玩家解锁成就时是否在聊天栏广播",
 Category = "游戏",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-protection",
 ConfigFileName = file,
 DisplayName = "出生保护半径",
 Description = "出生点保护半径（方块）\n非 OP 玩家无法在此范围内破坏\n0 = 禁用保护",
 Category = "游戏",
 DefaultValue = "16",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-animals",
 ConfigFileName = file,
 DisplayName = "生成动物",
 Description = "是否自然生成动物",
 Category = "游戏",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "spawn-mobs",
 ConfigFileName = file,
 DisplayName = "生成怪物",
 Description = "是否自然生成怪物",
 Category = "游戏",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ---------- 世界生成 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "level-name",
 ConfigFileName = file,
 DisplayName = "世界名称",
 Description = "世界文件夹的名称\n每个世界在 worlds/ 下有独立文件夹",
 Category = "世界",
 DefaultValue = "world",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "level-seed",
 ConfigFileName = file,
 DisplayName = "世界种子",
 Description = "世界生成种子\n留空则随机生成\n相同种子生成相同地形",
 Category = "世界",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-nether",
 ConfigFileName = file,
 DisplayName = "启用下界",
 Description = "是否加载下界维度",
 Category = "世界",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "allow-the_end",
 ConfigFileName = file,
 DisplayName = "启用末地",
 Description = "是否加载末地维度",
 Category = "世界",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "auto-save",
 ConfigFileName = file,
 DisplayName = "自动保存",
 Description = "是否启用自动保存（间隔由 powernukkit.yml 的 autosaveDelay 控制）",
 Category = "维护",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ---------- 资源包 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "force-resources",
 ConfigFileName = file,
 DisplayName = "强制资源包",
 Description = "true 时玩家必须接受服务器资源包才能进服\n拒绝资源包的玩家会被踢出",
 Category = "资源包",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "force-resources-allow-client-packs",
 ConfigFileName = file,
 DisplayName = "允许客户端资源包",
 Description = "是否允许玩家使用客户端自带资源包",
 Category = "资源包",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ---------- 网络与远程管理 ----------

 Register(new ServerConfigDescriptor
 {
 Key = "enable-query",
 ConfigFileName = file,
 DisplayName = "启用 Query",
 Description = "是否启用 GameSpy Query 协议\n用于服务器列表服务",
 Category = "远程管理",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "enable-rcon",
 ConfigFileName = file,
 DisplayName = "启用 RCON",
 Description = "是否启用远程控制台协议（RCON）\n允许通过 TCP 发送命令到服务器\n启用务必设置强密码！",
 Category = "远程管理",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "rcon.password",
 ConfigFileName = file,
 DisplayName = "RCON 密码",
 Description = "RCON 远程管理密码\n启用 RCON 时必须设置，否则任何人都能远程控制服务器",
 Category = "远程管理",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "check-login-time",
 ConfigFileName = file,
 DisplayName = "检查登录时间",
 Description = "是否校验玩家登录用时\n防止登录洪水攻击",
 Category = "反作弊",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "network-encryption",
 ConfigFileName = file,
 DisplayName = "网络加密",
 Description = "是否启用基岩版网络加密（基于 ECDH 握手）\n强烈建议保持 true，关闭后所有数据明文传输，存在严重安全风险",
 Category = "反作弊",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });
}
// RegisterGlowstoneConfig.cs
// 注册 Glowstone 服务器配置项（config/glowstone/glowstone.yml）
// 对应手册：docs/server-cores/36-glowstone.md
// 配置项约 60 项，10 个分类（server / console / game / creatures / folders / files / advanced / extras / world / libraries）

private void RegisterGlowstoneConfig()
{
 const string file = "config/glowstone/glowstone.yml";

 // ===== server（服务器基础设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "server.name",
 ConfigFileName = file,
 DisplayName = "服务器名称",
 Description = "仅用于日志与部分插件识别，不影响客户端显示。",
 Category = "服务器基础设置",
 DefaultValue = "Glowstone Server",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.port",
 ConfigFileName = file,
 DisplayName = "服务器端口",
 Description = "客户端连接端口，0 表示随机端口。",
 Category = "服务器基础设置",
 DefaultValue = "25565",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.ip",
 ConfigFileName = file,
 DisplayName = "监听 IP",
 Description = "留空监听所有网卡；填入具体 IP 仅监听该网卡。",
 Category = "服务器基础设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.max-players",
 ConfigFileName = file,
 DisplayName = "最大玩家数",
 Description = "同时在线上限，超出的玩家进入排队或被踢。",
 Category = "服务器基础设置",
 DefaultValue = "20",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.motd",
 ConfigFileName = file,
 DisplayName = "服务器描述",
 Description = "客户端服务器列表显示的文字，支持 § 颜色码。",
 Category = "服务器基础设置",
 DefaultValue = "A Glowstone Server",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.online-mode",
 ConfigFileName = file,
 DisplayName = "正版验证",
 Description = "true=只允许正版玩家；false=允许离线/盗版账号，注意皮肤与 UUID 会变。",
 Category = "服务器基础设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.white-list",
 ConfigFileName = file,
 DisplayName = "启用白名单",
 Description = "开启后只有 whitelist.json 中的玩家可进入。",
 Category = "服务器基础设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.log-file",
 ConfigFileName = file,
 DisplayName = "日志文件路径",
 Description = "主日志输出文件。",
 Category = "服务器基础设置",
 DefaultValue = "server.log",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.snooper-enabled",
 ConfigFileName = file,
 DisplayName = "启用信息收集",
 Description = "上报匿名数据到 Mojang，强烈建议保持 false。",
 Category = "服务器基础设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.prevent-proxy",
 ConfigFileName = file,
 DisplayName = "拒绝代理连接",
 Description = "启用后逐个反向解析玩家 IP 防止代理，可能误伤，建议关闭。",
 Category = "服务器基础设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.network-compression-threshold",
 ConfigFileName = file,
 DisplayName = "网络压缩阈值",
 Description = "数据包字节数大于该值才压缩；-1=禁用压缩；0=全部压缩。",
 Category = "服务器基础设置",
 DefaultValue = "256",
 MinValue = -1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.resource-pack",
 ConfigFileName = file,
 DisplayName = "资源包 URL",
 Description = "玩家进服时强制推送的资源包下载地址。",
 Category = "服务器基础设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.resource-pack-hash",
 ConfigFileName = file,
 DisplayName = "资源包哈希",
 Description = "资源包 SHA-1 哈希，用于校验完整性。",
 Category = "服务器基础设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.resource-pack-prompt",
 ConfigFileName = file,
 DisplayName = "资源包提示文本",
 Description = "推送资源包时弹窗显示的提示文字。",
 Category = "服务器基础设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "server.require-resource-pack",
 ConfigFileName = file,
 DisplayName = "强制资源包",
 Description = "true=拒绝加载资源包的玩家会被踢出。",
 Category = "服务器基础设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== console（控制台设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "console.history",
 ConfigFileName = file,
 DisplayName = "启用命令历史",
 Description = "控制台支持上下方向键翻阅历史命令。",
 Category = "控制台设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "console.prompts",
 ConfigFileName = file,
 DisplayName = "显示提示符",
 Description = "是否显示 > 提示符。",
 Category = "控制台设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "console.colors",
 ConfigFileName = file,
 DisplayName = "控制台彩色输出",
 Description = "日志按级别上色，Windows 旧 cmd 可能显示乱码。",
 Category = "控制台设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "console.date-format",
 ConfigFileName = file,
 DisplayName = "日期格式",
 Description = "日志时间戳格式，遵循 Java SimpleDateFormat 语法。",
 Category = "控制台设置",
 DefaultValue = "HH:mm:ss",
 ValueType = "string",
 RequiresRestart = false
 });

 // ===== game（游戏规则设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "game.gamemode",
 ConfigFileName = file,
 DisplayName = "默认游戏模式",
 Description = "新玩家首次进入的模式。",
 Category = "游戏规则设置",
 DefaultValue = "SURVIVAL",
 AllowedValues = new[] { "SURVIVAL", "CREATIVE", "ADVENTURE", "SPECTATOR" },
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.difficulty",
 ConfigFileName = file,
 DisplayName = "难度",
 Description = "PEACEFUL=和平；HARD=困难，影响刷怪与饥饿。",
 Category = "游戏规则设置",
 DefaultValue = "NORMAL",
 AllowedValues = new[] { "PEACEFUL", "EASY", "NORMAL", "HARD" },
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.hardcore",
 ConfigFileName = file,
 DisplayName = "极限模式",
 Description = "死亡后封禁该玩家，难度自动锁定 HARD。",
 Category = "游戏规则设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.pvp",
 ConfigFileName = file,
 DisplayName = "允许玩家 PvP",
 Description = "是否允许玩家间互相伤害。",
 Category = "游戏规则设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.max-build-height",
 ConfigFileName = file,
 DisplayName = "最大建筑高度",
 Description = "玩家可放置方块的最大 Y 坐标。",
 Category = "游戏规则设置",
 DefaultValue = "256",
 MinValue = 64,
 MaxValue = 256,
 ValueType = "int",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.allow-flight",
 ConfigFileName = file,
 DisplayName = "允许飞行",
 Description = "非创造模式是否允许飞行（防作弊检测）。",
 Category = "游戏规则设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.allow-nether",
 ConfigFileName = file,
 DisplayName = "启用下界",
 Description = "是否生成/加载下界维度。",
 Category = "游戏规则设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.allow-end",
 ConfigFileName = file,
 DisplayName = "启用末地",
 Description = "是否生成/加载末地维度。",
 Category = "游戏规则设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.announce-achievements",
 ConfigFileName = file,
 DisplayName = "公告成就",
 Description = "玩家获得成就时是否全服广播。",
 Category = "游戏规则设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.force-gamemode",
 ConfigFileName = file,
 DisplayName = "强制游戏模式",
 Description = "玩家每次进入都重置为默认模式，覆盖其上次模式。",
 Category = "游戏规则设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.spawn-protection",
 ConfigFileName = file,
 DisplayName = "出生点保护半径",
 Description = "出生点周围多少格内非 OP 无法破坏，0=关闭保护。",
 Category = "游戏规则设置",
 DefaultValue = "16",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "game.villager-trading",
 ConfigFileName = file,
 DisplayName = "允许村民交易",
 Description = "玩家是否可与村民交易。",
 Category = "游戏规则设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== creatures（生物生成设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.spawn-monsters",
 ConfigFileName = file,
 DisplayName = "生成怪物",
 Description = "是否生成敌对怪物。",
 Category = "生物生成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.spawn-animals",
 ConfigFileName = file,
 DisplayName = "生成动物",
 Description = "是否生成被动动物。",
 Category = "生物生成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.spawn-npcs",
 ConfigFileName = file,
 DisplayName = "生成 NPC",
 Description = "是否生成村民等 NPC。",
 Category = "生物生成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.monster-limit",
 ConfigFileName = file,
 DisplayName = "怪物上限",
 Description = "单个世界怪物实体数量上限。",
 Category = "生物生成设置",
 DefaultValue = "70",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.animal-limit",
 ConfigFileName = file,
 DisplayName = "动物上限",
 Description = "单个世界被动动物数量上限。",
 Category = "生物生成设置",
 DefaultValue = "15",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.water-animal-limit",
 ConfigFileName = file,
 DisplayName = "水生动物上限",
 Description = "单个世界水生动物数量上限。",
 Category = "生物生成设置",
 DefaultValue = "5",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.ambient-limit",
 ConfigFileName = file,
 DisplayName = "环境生物上限",
 Description = "蝙蝠等环境生物上限。",
 Category = "生物生成设置",
 DefaultValue = "15",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.ticks-per-monster-spawn",
 ConfigFileName = file,
 DisplayName = "怪物生成间隔",
 Description = "每多少 tick 尝试一次怪物生成（20 tick=1 秒）。",
 Category = "生物生成设置",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "creatures.ticks-per-animal-spawn",
 ConfigFileName = file,
 DisplayName = "动物生成间隔",
 Description = "每多少 tick 尝试一次动物生成。",
 Category = "生物生成设置",
 DefaultValue = "400",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 // ===== folders（目录设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "folders.settings",
 ConfigFileName = file,
 DisplayName = "配置目录",
 Description = "所有 YAML 配置所在目录。",
 Category = "目录设置",
 DefaultValue = "config",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "folders.plugins",
 ConfigFileName = file,
 DisplayName = "插件目录",
 Description = "Bukkit 插件 jar 放置目录。",
 Category = "目录设置",
 DefaultValue = "plugins",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "folders.worlds",
 ConfigFileName = file,
 DisplayName = "世界目录",
 Description = "世界存档数据目录。",
 Category = "目录设置",
 DefaultValue = "worlds",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "folders.cache",
 ConfigFileName = file,
 DisplayName = "缓存目录",
 Description = "运行时缓存（如皮肤）目录。",
 Category = "目录设置",
 DefaultValue = "cache",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "folders.updates",
 ConfigFileName = file,
 DisplayName = "更新目录",
 Description = "插件热更新目录，放入新 jar 重启后替换。",
 Category = "目录设置",
 DefaultValue = "update",
 ValueType = "string",
 RequiresRestart = true
 });

 // ===== files（文件设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "files.whitelist",
 ConfigFileName = file,
 DisplayName = "白名单文件",
 Description = "白名单文件名。",
 Category = "文件设置",
 DefaultValue = "whitelist.json",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "files.permissions",
 ConfigFileName = file,
 DisplayName = "权限文件",
 Description = "默认权限配置文件名。",
 Category = "文件设置",
 DefaultValue = "permissions.yml",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "files.commands",
 ConfigFileName = file,
 DisplayName = "命令文件",
 Description = "命令别名配置文件名。",
 Category = "文件设置",
 DefaultValue = "commands.yml",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "files.operators",
 ConfigFileName = file,
 DisplayName = "OP 文件",
 Description = "管理员列表文件名。",
 Category = "文件设置",
 DefaultValue = "ops.json",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "files.help",
 ConfigFileName = file,
 DisplayName = "帮助文件",
 Description = "帮助主题配置文件名。",
 Category = "文件设置",
 DefaultValue = "help.yml",
 ValueType = "string",
 RequiresRestart = false
 });

 // ===== advanced（高级设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "advanced.connection-throttle",
 ConfigFileName = file,
 DisplayName = "连接节流",
 Description = "同一玩家两次连接的最小间隔（毫秒），防刷屏。",
 Category = "高级设置",
 DefaultValue = "4000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "advanced.idle-timeout",
 ConfigFileName = file,
 DisplayName = "空闲超时",
 Description = "玩家无操作多少分钟后踢出，0=禁用。",
 Category = "高级设置",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "advanced.warn-on-overload",
 ConfigFileName = file,
 DisplayName = "过载警告",
 Description = "服务器 tick 超时时是否在控制台输出警告。",
 Category = "高级设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "advanced.exact-login-location",
 ConfigFileName = file,
 DisplayName = "精确登录位置",
 Description = "玩家上线时是否精确还原离线时位置。",
 Category = "高级设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "advanced.plugin-profiling",
 ConfigFileName = file,
 DisplayName = "插件性能分析",
 Description = "启用 /timings 命令分析插件性能。",
 Category = "高级设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "advanced.use-alternative-logger",
 ConfigFileName = file,
 DisplayName = "备用日志器",
 Description = "使用 JUL 替代默认日志框架，调试用。",
 Category = "高级设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "advanced.poor-man-listener",
 ConfigFileName = file,
 DisplayName = "简易事件监听",
 Description = "兼容旧版插件的低性能事件分发，谨慎开启。",
 Category = "高级设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== extras（额外特性设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "extras.tps-display",
 ConfigFileName = file,
 DisplayName = "显示 TPS",
 Description = "在控制台定时输出当前 TPS。",
 Category = "额外特性设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "extras.kick-on-illegal-behavior",
 ConfigFileName = file,
 DisplayName = "非法行为踢出",
 Description = "检测到客户端非法数据包时直接踢出。",
 Category = "额外特性设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "extras.auto-save-on-player-quit",
 ConfigFileName = file,
 DisplayName = "退出自动保存",
 Description = "玩家退出时立即保存其数据。",
 Category = "额外特性设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "extras.deploy-on-restart",
 ConfigFileName = file,
 DisplayName = "重启自动部署",
 Description = "重启时自动从 update 目录部署新插件。",
 Category = "额外特性设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== world（世界生成设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "world.name",
 ConfigFileName = file,
 DisplayName = "主世界名称",
 Description = "主世界存档文件夹名。",
 Category = "世界生成设置",
 DefaultValue = "world",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.seed",
 ConfigFileName = file,
 DisplayName = "世界种子",
 Description = "留空随机生成；填入固定种子可复现世界。",
 Category = "世界生成设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.type",
 ConfigFileName = file,
 DisplayName = "世界类型",
 Description = "地形生成器类型。",
 Category = "世界生成设置",
 DefaultValue = "DEFAULT",
 AllowedValues = new[] { "DEFAULT", "FLAT", "LARGEBIOMES", "AMPLIFIED" },
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.generator-settings",
 ConfigFileName = file,
 DisplayName = "生成器参数",
 Description = "自定义生成参数，例如超平坦层结构 JSON。",
 Category = "世界生成设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.generate-structures",
 ConfigFileName = file,
 DisplayName = "生成结构",
 Description = "是否生成村庄、神殿等结构。",
 Category = "世界生成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.view-distance",
 ConfigFileName = file,
 DisplayName = "视野距离",
 Description = "玩家周围加载区块半径，每 +1 增加约 15% 带宽消耗。",
 Category = "世界生成设置",
 DefaultValue = "10",
 MinValue = 3,
 MaxValue = 15,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.keep-spawn-loaded",
 ConfigFileName = file,
 DisplayName = "保持出生加载",
 Description = "出生点区块常驻内存。",
 Category = "世界生成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== libraries（依赖库设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "libraries.check-library-updates",
 ConfigFileName = file,
 DisplayName = "检查库更新",
 Description = "启动时检查依赖库是否有新版本。",
 Category = "依赖库设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "libraries.use-library-repo",
 ConfigFileName = file,
 DisplayName = "使用库仓库",
 Description = "从远程仓库下载缺失依赖，关闭则需手动放置 jar。",
 Category = "依赖库设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });


    // ===== AUTO-INJECTED by core-fetcher pipeline =====
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.compression-threshold",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "压缩阈值",
        Description = "超过多少字节的网络包才启用压缩（默认 256）",
        Category = "Glowstone 专属",
        DefaultValue = "256",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.deprecated-verbose",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "废弃 API 详细日志",
        Description = "插件调用废弃 API 时是否输出详细告警堆栈",
        Category = "Glowstone 专属",
        DefaultValue = "false",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.graphics-compute.enable",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用图形计算",
        Description = "是否启用 GPU 加速的实体光照计算（需要独显）",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.graphics-compute.use-any-device",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "允许任意 GPU",
        Description = "图形计算时是否允许使用任意可用的 GPU 设备",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.player-sample-count",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "玩家采样数",
        Description = "服务器列表 Ping 同时查询的玩家样本数量",
        Category = "Glowstone 专属",
        DefaultValue = "12",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.profile-lookup-timeout",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "Profile 查询超时",
        Description = "从 Mojang 查询玩家 UUID/皮肤的超时秒数",
        Category = "Glowstone 专属",
        DefaultValue = "5",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.proxy-support",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "代理支持",
        Description = "是否支持 BungeeCord/Velocity 等代理反代",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.region-file.cache-size",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "区域文件缓存大小",
        Description = "同时在内存中打开的 .mca 区域文件数量上限",
        Category = "Glowstone 专属",
        DefaultValue = "256",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.region-file.compression",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "区域文件压缩",
        Description = "区域文件保存时是否启用 Zlib 压缩",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "advanced.suggest-player-name-when-null-tab-completions",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "Tab 补全建议玩家",
        Description = "Tab 补全命令参数时是否自动建议在线玩家名",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "console.log-date-format",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "日志时间格式",
        Description = "写入日志文件的时间戳格式（yyyy/MM/dd HH:mm:ss）",
        Category = "Glowstone 专属",
        DefaultValue = "yyyy/MM/dd HH:mm:ss",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "console.prompt",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "控制台提示符",
        Description = "交互式控制台的提示符文字",
        Category = "Glowstone 专属",
        DefaultValue = "> ",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "console.use-jline",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用 JLine",
        Description = "使用 JLine 实现更高级的控制台行编辑（历史、Tab 补全）",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.enable.animals",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用动物",
        Description = "是否允许世界中自然生成动物（牛、羊等）",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.enable.monsters",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用怪物",
        Description = "是否允许世界中自然生成怪物（僵尸、骷髅等）",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.enable.npcs",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用 NPC",
        Description = "是否允许村民等 NPC 自然生成",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.limit.ambient",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "环境生物上限",
        Description = "每区块同时存在的环境生物（蝙蝠等）数量上限",
        Category = "Glowstone 专属",
        DefaultValue = "15",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.limit.animals",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "动物上限",
        Description = "每区块同时存在的动物数量上限",
        Category = "Glowstone 专属",
        DefaultValue = "15",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.limit.monsters",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "怪物上限",
        Description = "每区块同时存在的怪物数量上限",
        Category = "Glowstone 专属",
        DefaultValue = "70",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.limit.water",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "水生生物上限",
        Description = "每区块同时存在的水生生物数量上限",
        Category = "Glowstone 专属",
        DefaultValue = "5",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.ticks.animal",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "动物 Tick 频率",
        Description = "动物每多少 tick 执行一次 AI 判定",
        Category = "Glowstone 专属",
        DefaultValue = "400",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "creatures.ticks.monsters",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "怪物 Tick 频率",
        Description = "怪物每多少 tick 执行一次 AI 判定",
        Category = "Glowstone 专属",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.query-enabled",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用 Query",
        Description = "是否启用 Minecraft Query 协议（第三方工具查询用）",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.query-plugins",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "Query 返回插件列表",
        Description = "Query 查询时是否返回已安装插件列表",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.query-port",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "Query 端口",
        Description = "Query 协议监听的 UDP 端口",
        Category = "Glowstone 专属",
        DefaultValue = "25614",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.rcon-colors",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "RCON 颜色支持",
        Description = "RCON 远程控制台是否支持 § 颜色码",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.rcon-enabled",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用 RCON",
        Description = "是否开启 RCON 远程管理功能",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.rcon-password",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "RCON 密码",
        Description = "RCON 登录认证密码",
        Category = "Glowstone 专属",
        DefaultValue = "glowstone",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "extras.rcon-port",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "RCON 端口",
        Description = "RCON 协议监听的 TCP 端口",
        Category = "Glowstone 专属",
        DefaultValue = "25575",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "folders.libraries",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "依赖库目录",
        Description = "存放第三方依赖 jar 的子目录名",
        Category = "Glowstone 专属",
        DefaultValue = "lib",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "folders.update",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "更新目录",
        Description = "自动更新时存放临时文件的目录",
        Category = "Glowstone 专属",
        DefaultValue = "update",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.command-blocks",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用命令方块",
        Description = "是否允许玩家放置/使用命令方块",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.gamemode-force",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "强制游戏模式",
        Description = "开启后玩家无法手动切换游戏模式",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.resource-pack",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "材质包 URL",
        Description = "向玩家推送的资源包下载地址，留空关闭",
        Category = "Glowstone 专属",
        DefaultValue = "",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "game.resource-pack-hash",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "材质包校验哈希",
        Description = "资源包 SHA1 哈希，用于客户端校验完整性",
        Category = "Glowstone 专属",
        DefaultValue = "",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "libraries.checksum-validation",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "依赖校验",
        Description = "下载第三方依赖 jar 时是否校验 SHA 哈希",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "libraries.compatibility-bundle",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "兼容包类型",
        Description = "使用哪种兼容 API 包：CRAFTBUKKIT 兼容旧插件",
        Category = "Glowstone 专属",
        DefaultValue = "CRAFTBUKKIT",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "libraries.download-attempts",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "下载重试次数",
        Description = "依赖下载失败后最多重试几次",
        Category = "Glowstone 专属",
        DefaultValue = "2",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "libraries.list",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "额外依赖列表",
        Description = "需要额外下载的 Maven 依赖坐标列表",
        Category = "Glowstone 专属",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "libraries.repository-url",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "仓库地址",
        Description = "依赖下载用的 Maven 仓库地址",
        Category = "Glowstone 专属",
        DefaultValue = "https://repo.glowstone.net/repository/maven-public/",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.allow-client-mods",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "允许客户端模组",
        Description = "携带模组的客户端是否允许进入服务器",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.dns",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "DNS 服务器",
        Description = "自定义 DNS 解析服务器 IP 列表",
        Category = "Glowstone 专属",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.prevent-proxy-connections",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "拒绝代理连接",
        Description = "直接拒绝疑似来自代理/VPN 的客户端连接",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.shutdown-message",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "关停消息",
        Description = "服务器关闭时显示给在线玩家的提示",
        Category = "Glowstone 专属",
        DefaultValue = "Server shutting down.",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "server.whitelisted",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "启用白名单",
        Description = "开启后只有白名单中的玩家才能进入服务器",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.allow-end",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "允许末地",
        Description = "是否允许玩家进入末地维度",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.allow-nether",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "允许下界",
        Description = "是否允许玩家进入下界维度",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.classic-style-water",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "经典水域样式",
        Description = "使用 1.7 时代的静态水面生成方式",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.disable-generation",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "禁用地形生成",
        Description = "是否关闭新区块的地形生成（配合预制地图用）",
        Category = "Glowstone 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.gen-structures",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "生成结构",
        Description = "是否自然生成村庄、神殿、要塞等建筑结构",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.level-type",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "世界类型",
        Description = "地形类型：DEFAULT/FLAT/LARGE_BIOMES/AMPLIFIED",
        Category = "Glowstone 专属",
        DefaultValue = "DEFAULT",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.populate-anchored-chunks",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "填充锚定区块",
        Description = "对出生点锚定区块进行结构/矿物填充",
        Category = "Glowstone 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "world.spawn-radius",
        ConfigFileName = "config/glowstone.yml",
        DisplayName = "出生半径",
        Description = "新玩家首次生成时在出生点周围随机的半径",
        Category = "Glowstone 专属",
        DefaultValue = "16",
        ValueType = "int",
        RequiresRestart = false,
    });
    // ===== END AUTO-INJECTED =====
}
// RegisterSpongeGlobalConf.cs
// 注册 Sponge 全局配置项（config/sponge/global.conf，HOCON 格式）
// 对应手册：docs/server-cores/32-sponge.md
// 配置项约 90 项，25 个子节

private void RegisterSpongeGlobalConf()
{
 const string file = "config/sponge/global.conf";

 // ===== 全局根设置 =====
 Register(new ServerConfigDescriptor
 {
 Key = "sponge.target-server-ip",
 ConfigFileName = file,
 DisplayName = "目标服务器 IP",
 Description = "仅 SpongeForge/SpongeVanilla 嵌入式部署时使用。",
 Category = "全局根设置",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sponge.target-server-port",
 ConfigFileName = file,
 DisplayName = "目标服务器端口",
 Description = "嵌入式部署端口。",
 Category = "全局根设置",
 DefaultValue = "25565",
 MinValue = 1,
 MaxValue = 65535,
 ValueType = "int",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sponge.plugins-dir",
 ConfigFileName = file,
 DisplayName = "插件目录",
 Description = "Sponge 插件搜索目录，可自定义。",
 Category = "全局根设置",
 DefaultValue = "mods/plugins",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sponge.enable-plugins",
 ConfigFileName = file,
 DisplayName = "启用插件加载",
 Description = "false=不加载任何 Sponge 插件。",
 Category = "全局根设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sponge.file-watch-enabled",
 ConfigFileName = file,
 DisplayName = "文件监视",
 Description = "监视配置文件变化以支持热重载。",
 Category = "全局根设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== modules（功能模块开关） =====
 Register(new ServerConfigDescriptor
 {
 Key = "modules.block-capturing-control",
 ConfigFileName = file,
 DisplayName = "方块捕获控制",
 Description = "是否启用方块变更追踪（事务），插件 BlockEvent 依赖此。",
 Category = "功能模块",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.bungeecord",
 ConfigFileName = file,
 DisplayName = "BungeeCord 兼容",
 Description = "启用 IP 转发以兼容 BungeeCord/Velocity 代理。",
 Category = "功能模块",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.entity-activation-range",
 ConfigFileName = file,
 DisplayName = "实体活动范围优化",
 Description = "启用按距离降频实体 tick 的优化。",
 Category = "功能模块",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.entity-collisions",
 ConfigFileName = file,
 DisplayName = "实体碰撞优化",
 Description = "启用碰撞频率限制。",
 Category = "功能模块",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.exploits",
 ConfigFileName = file,
 DisplayName = "漏洞修复",
 Description = "修复若干原版漏洞（如附魔/书与笔）。",
 Category = "功能模块",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.game-fixes",
 ConfigFileName = file,
 DisplayName = "游戏修复",
 Description = "一些非紧急的游戏性 bug 修复，默认关闭以保原版行为。",
 Category = "功能模块",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.optimizations",
 ConfigFileName = file,
 DisplayName = "性能优化",
 Description = "总开关，关闭后下属所有优化失效。",
 Category = "功能模块",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.realtime",
 ConfigFileName = file,
 DisplayName = "实时时钟",
 Description = "用现实时间替代 tick，改善低 TPS 下玩家体验，不提升性能。",
 Category = "功能模块",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.tileentity-activation",
 ConfigFileName = file,
 DisplayName = "方块实体活动范围",
 Description = "按距离降频方块实体 tick，谨慎启用可能破坏模组功能。",
 Category = "功能模块",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.timings",
 ConfigFileName = file,
 DisplayName = "性能计时",
 Description = "启用 /sponge timings 性能分析。",
 Category = "功能模块",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "modules.tracking",
 ConfigFileName = file,
 DisplayName = "来源追踪",
 Description = "追踪方块/实体变更的因果来源，权限审计依赖此。",
 Category = "功能模块",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== optimizations（性能优化） =====
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.async-lighting.enabled",
 ConfigFileName = file,
 DisplayName = "异步光照计算",
 Description = "异步线程计算光照，显著降低主线程负担。",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.async-lighting.num-threads",
 ConfigFileName = file,
 DisplayName = "光照线程数",
 Description = "异步光照专用线程数，CPU 核心数较佳。",
 Category = "性能优化",
 DefaultValue = "2",
 MinValue = 1,
 MaxValue = 64,
 ValueType = "int",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.cache-tameable-owners",
 ConfigFileName = file,
 DisplayName = "缓存可驯服主",
 Description = "缓存驯化动物主人 UUID，避免频繁 DataWatcher 查询。",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.drops-pre-merge",
 ConfigFileName = file,
 DisplayName = "掉落物预合并",
 Description = "生成掉落物前先尝试合并，减少实体数量。",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.panda-redstone",
 ConfigFileName = file,
 DisplayName = "Panda 红石算法",
 Description = "替代红石更新算法，减少方块更新次数，可能引入差异。",
 Category = "性能优化",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.chunk-loading",
 ConfigFileName = file,
 DisplayName = "区块加载优化",
 Description = "优化区块加载与排队。",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.eject-from-entity",
 ConfigFileName = file,
 DisplayName = "实体弹出优化",
 Description = "优化矿车/船等载具的弹出逻辑。",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.structured-unused-entries",
 ConfigFileName = file,
 DisplayName = "清理未用条目",
 Description = "清理内部未使用的结构条目。",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.use-partial-block-updates",
 ConfigFileName = file,
 DisplayName = "部分方块更新",
 Description = "仅更新变化部分方块而非整体。",
 Category = "性能优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.vertex-operation-lighting",
 ConfigFileName = file,
 DisplayName = "顶点光照优化",
 Description = "实验性顶点级光照优化。",
 Category = "性能优化",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== block-entity-activation（方块实体活动范围） =====
 Register(new ServerConfigDescriptor
 {
 Key = "block-entity-activation.auto-populate",
 ConfigFileName = file,
 DisplayName = "自动填充",
 Description = "自动把新发现的方块实体加入配置，建议调优后关闭。",
 Category = "方块实体活动范围",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "block-entity-activation.default-block-range",
 ConfigFileName = file,
 DisplayName = "默认方块范围",
 Description = "玩家在此范围内方块实体才 tick。",
 Category = "方块实体活动范围",
 DefaultValue = "256",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "block-entity-activation.default-tick-rate",
 ConfigFileName = file,
 DisplayName = "默认 tick 频率",
 Description = "每多少 tick 给方块实体 1 次 tick，值越大越省 CPU。",
 Category = "方块实体活动范围",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 // ===== entity-activation-range（实体活动范围） =====
 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.auto-populate",
 ConfigFileName = file,
 DisplayName = "自动填充",
 Description = "自动把新发现的实体加入配置。",
 Category = "实体活动范围",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.defaults.ambient",
 ConfigFileName = file,
 DisplayName = "环境生物范围",
 Description = "蝙蝠等环境生物激活距离，0=禁用。",
 Category = "实体活动范围",
 DefaultValue = "32",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.defaults.aquatic",
 ConfigFileName = file,
 DisplayName = "水生生物范围",
 Description = "鱿鱼等水生生物激活距离。",
 Category = "实体活动范围",
 DefaultValue = "32",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.defaults.creature",
 ConfigFileName = file,
 DisplayName = "被动动物范围",
 Description = "牛、羊等被动动物激活距离。",
 Category = "实体活动范围",
 DefaultValue = "32",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.defaults.misc",
 ConfigFileName = file,
 DisplayName = "杂项实体范围",
 Description = "掉落物、经验球等杂项实体激活距离。",
 Category = "实体活动范围",
 DefaultValue = "16",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-activation-range.defaults.monster",
 ConfigFileName = file,
 DisplayName = "怪物范围",
 Description = "僵尸、骷髅等怪物激活距离。",
 Category = "实体活动范围",
 DefaultValue = "32",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 // ===== entity-collision（实体碰撞） =====
 Register(new ServerConfigDescriptor
 {
 Key = "entity-collision.auto-populate",
 ConfigFileName = file,
 DisplayName = "自动填充",
 Description = "自动把新发现的实体加入碰撞配置。",
 Category = "实体碰撞",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-collision.defaults.ambient",
 ConfigFileName = file,
 DisplayName = "环境生物碰撞上限",
 Description = "单点同时碰撞的环境生物上限。",
 Category = "实体碰撞",
 DefaultValue = "8",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-collision.defaults.aquatic",
 ConfigFileName = file,
 DisplayName = "水生生物碰撞上限",
 Description = "水生生物碰撞上限。",
 Category = "实体碰撞",
 DefaultValue = "8",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-collision.defaults.creature",
 ConfigFileName = file,
 DisplayName = "被动动物碰撞上限",
 Description = "被动动物碰撞上限。",
 Category = "实体碰撞",
 DefaultValue = "8",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-collision.defaults.misc",
 ConfigFileName = file,
 DisplayName = "杂项实体碰撞上限",
 Description = "杂项实体碰撞上限。",
 Category = "实体碰撞",
 DefaultValue = "8",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity-collision.defaults.monster",
 ConfigFileName = file,
 DisplayName = "怪物碰撞上限",
 Description = "怪物碰撞上限，调小可减少密集卡顿。",
 Category = "实体碰撞",
 DefaultValue = "8",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 // ===== entity（实体行为） =====
 Register(new ServerConfigDescriptor
 {
 Key = "entity.creature-spawn-limit",
 ConfigFileName = file,
 DisplayName = "怪物生成上限",
 Description = "0=沿用原版；正值覆盖原版上限。",
 Category = "实体行为",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity.human-player-list-allow-bypass-on-max-players",
 ConfigFileName = file,
 DisplayName = "玩家列表绕过",
 Description = "BungeeCord 转发时绕过原版 60 上限。",
 Category = "实体行为",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity.max-bounding-box-size",
 ConfigFileName = file,
 DisplayName = "最大包围盒尺寸",
 Description = "实体最大碰撞箱尺寸，过大实体被裁剪，防崩。",
 Category = "实体行为",
 DefaultValue = "2000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity.max-entity-velocity",
 ConfigFileName = file,
 DisplayName = "最大实体速度",
 Description = "实体最大速度上限，防止作弊者用速度卡服。",
 Category = "实体行为",
 DefaultValue = "100.0",
 MinValue = 0,
 ValueType = "double",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity.player-block-reach",
 ConfigFileName = file,
 DisplayName = "玩家方块触达距离",
 Description = "玩家可破坏/交互方块的最远距离。",
 Category = "实体行为",
 DefaultValue = "5.0",
 MinValue = 0,
 ValueType = "double",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity.player-entity-reach",
 ConfigFileName = file,
 DisplayName = "玩家实体触达距离",
 Description = "玩家可攻击/交互实体的最远距离。",
 Category = "实体行为",
 DefaultValue = "5.0",
 MinValue = 0,
 ValueType = "double",
 RequiresRestart = false
 });

 // ===== movement-checks（移动检查） =====
 Register(new ServerConfigDescriptor
 {
 Key = "movement-checks.auto-orientation",
 ConfigFileName = file,
 DisplayName = "自动朝向检查",
 Description = "检测玩家朝向突变（如反作弊）。",
 Category = "移动检查",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "movement-checks.invalid-rotation",
 ConfigFileName = file,
 DisplayName = "非法旋转检查",
 Description = "检查旋转角度是否超出合法范围。",
 Category = "移动检查",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "movement-checks.moved-wrongly",
 ConfigFileName = file,
 DisplayName = "异常移动检查",
 Description = "检查玩家移动距离是否异常。",
 Category = "移动检查",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "movement-checks.moved-too-quickly",
 ConfigFileName = file,
 DisplayName = "快速移动检查",
 Description = "检查玩家移动速度是否过快。",
 Category = "移动检查",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "movement-checks.speed-hack",
 ConfigFileName = file,
 DisplayName = "速度作弊检查",
 Description = "检测加速挂。",
 Category = "移动检查",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== commands（命令设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "commands.multi-world-commands",
 ConfigFileName = file,
 DisplayName = "多世界命令",
 Description = "是否按世界隔离命令权限。",
 Category = "命令设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "commands.notifications.command",
 ConfigFileName = file,
 DisplayName = "命令通知命令名",
 Description = "/sponge 主命令名。",
 Category = "命令设置",
 DefaultValue = "sponge",
 ValueType = "string",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "commands.show-name",
 ConfigFileName = file,
 DisplayName = "显示命令名",
 Description = "帮助列表中是否显示命令名。",
 Category = "命令设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== world（世界设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "world.auto-save-interval",
 ConfigFileName = file,
 DisplayName = "世界自动保存间隔",
 Description = "每多少 tick 保存所有区块，0=禁用，20 tick=1 秒。",
 Category = "世界设置",
 DefaultValue = "900",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.auto-player-save-interval",
 ConfigFileName = file,
 DisplayName = "玩家数据保存间隔",
 Description = "每多少 tick 保存全局玩家数据，0=禁用。",
 Category = "世界设置",
 DefaultValue = "900",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.game-disable-updates",
 ConfigFileName = file,
 DisplayName = "禁用游戏更新",
 Description = "调试用，禁用游戏内部更新。",
 Category = "世界设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.gen-modifiers",
 ConfigFileName = file,
 DisplayName = "生成器修饰符",
 Description = "自定义世界生成修饰符列表。",
 Category = "世界设置",
 DefaultValue = "[]",
 ValueType = "list",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "world.load-on-startup",
 ConfigFileName = file,
 DisplayName = "启动时加载",
 Description = "服务端启动时是否预加载所有世界。",
 Category = "世界设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== bungeecord（BungeeCord 代理） =====
 Register(new ServerConfigDescriptor
 {
 Key = "bungeecord.ip-forwarding",
 ConfigFileName = file,
 DisplayName = "IP 转发",
 Description = "启用 BungeeCord/Velocity IP 转发，必须与代理端一致。",
 Category = "BungeeCord 代理",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "bungeecord.online-mode",
 ConfigFileName = file,
 DisplayName = "在线模式",
 Description = "代理模式下是否做正版验证。",
 Category = "BungeeCord 代理",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== permissions（权限设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "permissions.load-on-startup",
 ConfigFileName = file,
 DisplayName = "启动加载权限",
 Description = "启动时加载权限服务。",
 Category = "权限设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "permissions.use-default-permissions",
 ConfigFileName = file,
 DisplayName = "使用默认权限",
 Description = "是否使用 Sponge 内置默认权限。",
 Category = "权限设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "permissions.default-admin-level",
 ConfigFileName = file,
 DisplayName = "默认管理员等级",
 Description = "默认权限等级（4=OP）。",
 Category = "权限设置",
 DefaultValue = "4",
 MinValue = 0,
 MaxValue = 4,
 ValueType = "int",
 RequiresRestart = false
 });

 // ===== sql（SQL 数据库） =====
 Register(new ServerConfigDescriptor
 {
 Key = "sql.enabled",
 ConfigFileName = file,
 DisplayName = "启用 SQL",
 Description = "启用 SQL 数据源。",
 Category = "SQL 数据库",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sql.driver",
 ConfigFileName = file,
 DisplayName = "数据库驱动",
 Description = "JDBC 驱动类全名。",
 Category = "SQL 数据库",
 DefaultValue = "org.h2.Driver",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sql.url",
 ConfigFileName = file,
 DisplayName = "数据库 URL",
 Description = "JDBC 连接 URL。",
 Category = "SQL 数据库",
 DefaultValue = "jdbc:h2:./config/sponge/sponge",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sql.user",
 ConfigFileName = file,
 DisplayName = "数据库用户名",
 Description = "数据库账号。",
 Category = "SQL 数据库",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sql.password",
 ConfigFileName = file,
 DisplayName = "数据库密码",
 Description = "数据库密码，建议用环境变量替代。",
 Category = "SQL 数据库",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "sql.table-prefix",
 ConfigFileName = file,
 DisplayName = "表前缀",
 Description = "数据表名前缀。",
 Category = "SQL 数据库",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = true
 });

 // ===== scheduler（调度器） =====
 Register(new ServerConfigDescriptor
 {
 Key = "scheduler.parallel-limit",
 ConfigFileName = file,
 DisplayName = "并发任务上限",
 Description = "异步任务并发上限。",
 Category = "调度器",
 DefaultValue = "8",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "scheduler.max-thread-size",
 ConfigFileName = file,
 DisplayName = "最大线程数",
 Description = "调度线程池最大线程数。",
 Category = "调度器",
 DefaultValue = "4",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 // ===== logging（日志设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "logging.log-block-break",
 ConfigFileName = file,
 DisplayName = "记录方块破坏",
 Description = "控制台输出方块破坏事件。",
 Category = "日志设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "logging.log-block-place",
 ConfigFileName = file,
 DisplayName = "记录方块放置",
 Description = "控制台输出方块放置事件。",
 Category = "日志设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "logging.log-stacktraces",
 ConfigFileName = file,
 DisplayName = "记录堆栈",
 Description = "输出异常堆栈用于调试。",
 Category = "日志设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "logging.debug",
 ConfigFileName = file,
 DisplayName = "调试日志",
 Description = "启用指定调试分类（如 [chunk-load]）。",
 Category = "日志设置",
 DefaultValue = "[]",
 ValueType = "list",
 RequiresRestart = false
 });

 // ===== exploits（漏洞修复） =====
 Register(new ServerConfigDescriptor
 {
 Key = "exploits.book-large-size",
 ConfigFileName = file,
 DisplayName = "书本大小限制",
 Description = "限制书本内容大小，防崩服。",
 Category = "漏洞修复",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "exploits.item-signature",
 ConfigFileName = file,
 DisplayName = "物品签名检查",
 Description = "检查物品 NBT 签名是否合法。",
 Category = "漏洞修复",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "exploits.sign-command",
 ConfigFileName = file,
 DisplayName = "告示牌命令限制",
 Description = "限制告示牌可执行的命令。",
 Category = "漏洞修复",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "exploits.sign-long-lines",
 ConfigFileName = file,
 DisplayName = "告示牌长行限制",
 Description = "限制告示牌每行字符数。",
 Category = "漏洞修复",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== general（通用设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "general.disable-warnings",
 ConfigFileName = file,
 DisplayName = "禁用警告",
 Description = "关闭控制台部分警告。",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "general.hide-online-players",
 ConfigFileName = file,
 DisplayName = "隐藏在线玩家",
 Description = "不向客户端发送完整玩家列表。",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "general.disable-flush-saving",
 ConfigFileName = file,
 DisplayName = "禁用刷盘保存",
 Description = "关闭定时全量刷盘，仅增量保存。",
 Category = "通用设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "general.death-message-style",
 ConfigFileName = file,
 DisplayName = "死亡消息风格",
 Description = "死亡消息显示风格。",
 Category = "通用设置",
 DefaultValue = "default",
 AllowedValues = new[] { "default", "none", "raw" },
 ValueType = "string",
 RequiresRestart = false
 });

 // ===== debug（调试设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "debug.thread-contention-monitoring",
 ConfigFileName = file,
 DisplayName = "线程竞争监视",
 Description = "启用线程竞争检测。",
 Category = "调试设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "debug.reload-internal",
 ConfigFileName = file,
 DisplayName = "内部重载",
 Description = "允许 /sponge reload 重载内部状态。",
 Category = "调试设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "debug.synchronize-chunk-writes",
 ConfigFileName = file,
 DisplayName = "同步区块写入",
 Description = "区块写入是否同步。",
 Category = "调试设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== timings（性能计时） =====
 Register(new ServerConfigDescriptor
 {
 Key = "timings.enabled",
 ConfigFileName = file,
 DisplayName = "启用 timings",
 Description = "启用 /sponge timings。",
 Category = "性能计时",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "timings.verbose",
 ConfigFileName = file,
 DisplayName = "详细模式",
 Description = "输出更详细的计时数据。",
 Category = "性能计时",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "timings.cost-ignored",
 ConfigFileName = file,
 DisplayName = "忽略成本",
 Description = "忽略微小成本计时。",
 Category = "性能计时",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "timings.history-interval",
 ConfigFileName = file,
 DisplayName = "历史间隔",
 Description = "多少秒采样一次历史。",
 Category = "性能计时",
 DefaultValue = "300",
 MinValue = 10,
 MaxValue = 3600,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "timings.history-length",
 ConfigFileName = file,
 DisplayName = "历史长度",
 Description = "历史总时长（秒）。",
 Category = "性能计时",
 DefaultValue = "3600",
 MinValue = 60,
 MaxValue = 21600,
 ValueType = "int",
 RequiresRestart = false
 });

 // ===== cause-tracker（因果追踪） =====
 Register(new ServerConfigDescriptor
 {
 Key = "cause-tracker.max-block-processed-per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 最大处理方块",
 Description = "每 tick 处理的方块事件上限。",
 Category = "因果追踪",
 DefaultValue = "50000",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "cause-tracker.max-block-processed-per-event",
 ConfigFileName = file,
 DisplayName = "每事件最大方块",
 Description = "单个事件处理方块上限。",
 Category = "因果追踪",
 DefaultValue = "50000",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "cause-tracker.report-modified-blocks",
 ConfigFileName = file,
 DisplayName = "报告修改方块",
 Description = "输出修改方块报告。",
 Category = "因果追踪",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
}
// RegisterSpongeForgeConf.cs
// 注册 SpongeForge 专属差异配置项（config/sponge/spongeforge-global.conf，HOCON 格式）
// 对应手册：docs/server-cores/33-spongeforge.md
// 仅注册与原版 Sponge 的差异项（约 30 项 Forge 专属设置），通用配置见 RegisterSpongeGlobalConf.cs

private void RegisterSpongeForgeConf()
{
 const string file = "config/sponge/spongeforge-global.conf";

 // ===== general（Forge 通用差异） =====
 Register(new ServerConfigDescriptor
 {
 Key = "general.inject-permission-into-forged-commands",
 ConfigFileName = file,
 DisplayName = "注入权限到 Forge 命令",
 Description = "是否把 Sponge 权限注入 Forge 模组注册的命令，使权限插件可管控模组命令。",
 Category = "Forge 通用差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "general.use-mod-message-channel",
 ConfigFileName = file,
 DisplayName = "使用模组消息通道",
 Description = "启用 Forge 模组消息通道以兼容 Forge 客户端模组。",
 Category = "Forge 通用差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "general.use-mod-detected-permission-for-command",
 ConfigFileName = file,
 DisplayName = "模组命令权限检测",
 Description = "检测模组命令所需权限等级（4=OP，0=所有人）。",
 Category = "Forge 通用差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "general.allow-sync-chunk-writes",
 ConfigFileName = file,
 DisplayName = "允许同步区块写入",
 Description = "Forge 模组可能强制同步写入，开启以兼容部分老模组。",
 Category = "Forge 通用差异",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "general.deobfuscate-stacktraces",
 ConfigFileName = file,
 DisplayName = "反混淆堆栈",
 Description = "异常堆栈输出时把混淆名还原为可读名，便于排查 Forge 模组问题。",
 Category = "Forge 通用差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== forge（Forge 集成设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "forge.load-early",
 ConfigFileName = file,
 DisplayName = "早期加载",
 Description = "让 SpongeForge 在 Forge 模组加载之前初始化，解决 Mixin 顺序问题，强烈建议保持 true。",
 Category = "Forge 集成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge.optimize-mod-tileentity-tracking",
 ConfigFileName = file,
 DisplayName = "优化模组方块实体追踪",
 Description = "优化 Forge 模组方块实体的因果追踪性能。",
 Category = "Forge 集成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge.use-forge-event-for-block-modification",
 ConfigFileName = file,
 DisplayName = "使用 Forge 事件处理方块修改",
 Description = "用 Forge 的 NeighborNotify 事件而非 Sponge 事件处理方块变更通知，提升模组兼容性。",
 Category = "Forge 集成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge.use-forge-player-interaction",
 ConfigFileName = file,
 DisplayName = "使用 Forge 玩家交互",
 Description = "用 Forge 玩家交互事件桥接 Sponge 事件。",
 Category = "Forge 集成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge.convert-mod-item-attributes",
 ConfigFileName = file,
 DisplayName = "转换模组物品属性",
 Description = "把 Forge 物品 NBT 属性转换为 Sponge Data API。",
 Category = "Forge 集成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge.bridge-event-bus",
 ConfigFileName = file,
 DisplayName = "桥接事件总线",
 Description = "Forge EventBus 与 Sponge EventManager 双向转发事件。",
 Category = "Forge 集成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge.convert-forge-data",
 ConfigFileName = file,
 DisplayName = "转换 Forge 数据",
 Description = "Forge NBT 数据与 Sponge DataContainer 互转。",
 Category = "Forge 集成设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== forge-mod-compatibility（模组兼容性） =====
 Register(new ServerConfigDescriptor
 {
 Key = "forge-mod-compatibility.auto-populate",
 ConfigFileName = file,
 DisplayName = "自动填充模组兼容项",
 Description = "自动为加载到的模组生成兼容性配置项。",
 Category = "模组兼容性",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge-mod-compatibility.<modid>.enabled",
 ConfigFileName = file,
 DisplayName = "启用模组兼容",
 Description = "是否对该模组启用 Sponge 桥接处理，关闭可能提升性能但失去事件。",
 Category = "模组兼容性",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge-mod-compatibility.<modid>.force-restore",
 ConfigFileName = file,
 DisplayName = "强制还原",
 Description = "模组崩溃后是否强制还原状态（高风险，调试用）。",
 Category = "模组兼容性",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== mixin（Mixin 加载设置） =====
 Register(new ServerConfigDescriptor
 {
 Key = "mixin.force-mixin-early",
 ConfigFileName = file,
 DisplayName = "强制 Mixin 早期加载",
 Description = "让 Sponge 的 Mixin 优先于其他 Coremod，解决 old mixins 警告。",
 Category = "Mixin 加载设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "mixin.ignore-mod-mixins",
 ConfigFileName = file,
 DisplayName = "忽略模组 Mixin",
 Description = "指定要忽略的模组 Mixin 配置 JSON，避免冲突。",
 Category = "Mixin 加载设置",
 DefaultValue = "[]",
 ValueType = "list",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "mixin.debug",
 ConfigFileName = file,
 DisplayName = "Mixin 调试",
 Description = "输出 Mixin 注入详细日志。",
 Category = "Mixin 加载设置",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "mixin.env.refmap",
 ConfigFileName = file,
 DisplayName = "引用映射",
 Description = "启用 Mixin refmap，影响混淆名映射。",
 Category = "Mixin 加载设置",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== forge-permissions（Forge 权限） =====
 Register(new ServerConfigDescriptor
 {
 Key = "forge-permissions.enabled",
 ConfigFileName = file,
 DisplayName = "启用 Forge 权限桥接",
 Description = "把 Forge 注册的权限转给 Sponge 权限系统，让权限插件可管理。",
 Category = "Forge 权限",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge-permissions.default-level",
 ConfigFileName = file,
 DisplayName = "默认权限等级",
 Description = "模组未声明权限时的默认等级（4=OP 专属，0=所有人）。",
 Category = "Forge 权限",
 DefaultValue = "4",
 MinValue = 0,
 MaxValue = 4,
 ValueType = "int",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge-permissions.strict-mode",
 ConfigFileName = file,
 DisplayName = "严格模式",
 Description = "严格模式下未声明权限的模组命令一律禁止。",
 Category = "Forge 权限",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== forge-events（Forge 事件桥接） =====
 Register(new ServerConfigDescriptor
 {
 Key = "forge-events.fire-cancelable",
 ConfigFileName = file,
 DisplayName = "触发可取消事件",
 Description = "把 Forge 事件转成可取消的 Sponge 事件。",
 Category = "Forge 事件桥接",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge-events.async-events",
 ConfigFileName = file,
 DisplayName = "异步事件",
 Description = "指定哪些 Forge 事件允许异步分发，谨慎使用。",
 Category = "Forge 事件桥接",
 DefaultValue = "[]",
 ValueType = "list",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "forge-events.coalesce",
 ConfigFileName = file,
 DisplayName = "事件合并",
 Description = "合并连续相同事件以减少分发次数。",
 Category = "Forge 事件桥接",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== phase-tracking（Forge 阶段追踪差异） =====
 Register(new ServerConfigDescriptor
 {
 Key = "phase-tracking.track-forge-block-creation",
 ConfigFileName = file,
 DisplayName = "追踪 Forge 方块创建",
 Description = "追踪 Forge 模组创建方块的因果链，开启略增开销。",
 Category = "Forge 阶段追踪",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "phase-tracking.track-forge-entity-creation",
 ConfigFileName = file,
 DisplayName = "追踪 Forge 实体创建",
 Description = "追踪 Forge 模组创建实体的因果链。",
 Category = "Forge 阶段追踪",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "phase-tracking.verbose-forge-phases",
 ConfigFileName = file,
 DisplayName = "详细 Forge 阶段日志",
 Description = "输出 Forge 阶段切换详细日志，调试用。",
 Category = "Forge 阶段追踪",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== optimizations（Forge 专属优化差异） =====
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.use-forge-lighting-fallback",
 ConfigFileName = file,
 DisplayName = "使用 Forge 光照回退",
 Description = "与 Phosphor 等光照模组冲突时回退到 Forge 光照。",
 Category = "Forge 专属优化",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.skip-mod-tick-on-overload",
 ConfigFileName = file,
 DisplayName = "过载时跳过模组 tick",
 Description = "TPS 低时跳过非关键模组的 tick，谨慎启用。",
 Category = "Forge 专属优化",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.cache-forge-capabilities",
 ConfigFileName = file,
 DisplayName = "缓存 Forge 能力",
 Description = "缓存 Forge Capability 查询结果，提升模组交互性能。",
 Category = "Forge 专属优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "optimizations.batch-forge-block-updates",
 ConfigFileName = file,
 DisplayName = "批量 Forge 方块更新",
 Description = "批量处理 Forge 模组的方块更新通知。",
 Category = "Forge 专属优化",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== entity（Forge 实体差异） =====
 Register(new ServerConfigDescriptor
 {
 Key = "entity.convert-forge-entity-data",
 ConfigFileName = file,
 DisplayName = "转换 Forge 实体数据",
 Description = "把 Forge 模组实体 NBT 转为 Sponge Data API。",
 Category = "Forge 实体差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity.use-forge-spawn-rules",
 ConfigFileName = file,
 DisplayName = "使用 Forge 生成规则",
 Description = "尊重 Forge 模组的 canSpawn 规则，关闭可能让某些模组怪物刷不出来。",
 Category = "Forge 实体差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "entity.max-mod-entity-per-chunk",
 ConfigFileName = file,
 DisplayName = "单区块模组实体上限",
 Description = "每区块 Forge 模组实体上限，0=禁用上限。",
 Category = "Forge 实体差异",
 DefaultValue = "100",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 // ===== commands（Forge 命令差异） =====
 Register(new ServerConfigDescriptor
 {
 Key = "commands.register-forge-commands",
 ConfigFileName = file,
 DisplayName = "注册 Forge 命令",
 Description = "把 Forge 模组的命令注册到 Sponge 命令系统。",
 Category = "Forge 命令差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "commands.tab-complete-forge-commands",
 ConfigFileName = file,
 DisplayName = "Forge 命令 Tab 补全",
 Description = "启用 Forge 模组命令的 Tab 自动补全。",
 Category = "Forge 命令差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "commands.legacy-forge-command-prefix",
 ConfigFileName = file,
 DisplayName = "旧版 Forge 命令前缀",
 Description = "兼容旧版用 /forge: 前缀调用模组命令。",
 Category = "Forge 命令差异",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });

 // ===== bungeecord（Forge 代理差异） =====
 Register(new ServerConfigDescriptor
 {
 Key = "bungeecord.forward-forge-mods",
 ConfigFileName = file,
 DisplayName = "转发 Forge 模组列表",
 Description = "通过 BungeeCord 转发 Forge 客户端模组列表，跨服模组必需。",
 Category = "Forge 代理差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });
 Register(new ServerConfigDescriptor
 {
 Key = "bungeecord.verify-forge-mods",
 ConfigFileName = file,
 DisplayName = "验证 Forge 模组",
 Description = "跨服时验证客户端 Forge 模组列表，防作弊。",
 Category = "Forge 代理差异",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = true
 });

 // ===== logging（Forge 日志差异） =====
 Register(new ServerConfigDescriptor
 {
 Key = "logging.log-forge-event-mismatch",
 ConfigFileName = file,
 DisplayName = "记录 Forge 事件不匹配",
 Description = "Forge 与 Sponge 事件桥接失败时输出警告。",
 Category = "Forge 日志差异",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "logging.log-mixin-failures",
 ConfigFileName = file,
 DisplayName = "记录 Mixin 失败",
 Description = "Mixin 注入失败时输出详细错误。",
 Category = "Forge 日志差异",
 DefaultValue = "true",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
 Register(new ServerConfigDescriptor
 {
 Key = "logging.log-forge-permission-misses",
 ConfigFileName = file,
 DisplayName = "记录 Forge 权限缺失",
 Description = "模组权限未声明时输出警告。",
 Category = "Forge 日志差异",
 DefaultValue = "false",
 AllowedValues = new[] { "true", "false" },
 ValueType = "bool",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterYatopiaYml.cs
// 功能描述: 注册 Yatopia（基于 Tuinity 的极限优化分支，已停更）配置文件的描述符
// 包含 yatopia.yml 全局 settings + 每世界 world-settings 三大部分
// 数据来源: YatopiaMC/Yatopia README.md + 默认 yatopia.yml 模板
// 适用版本: Yatopia 1.17.1 / 1.18.2（项目已停更）
// -----------------------------------------------------------------------------

private void RegisterYatopiaYml()
{
 const string file = "yatopia.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "config-version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "内部使用，不要手动修改\nYatopia 用它做配置自动升级与兼容性判断",
 Category = "信息",
 DefaultValue = "1",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== settings（全局设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.brand-name",
 ConfigFileName = file,
 DisplayName = "服务端品牌名",
 Description = "发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码，可隐藏真实核心类型",
 Category = "全局",
 DefaultValue = "Yatopia",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.disable-connection-messages",
 ConfigFileName = file,
 DisplayName = "禁用连接消息",
 Description = "是否关闭玩家加入 / 退出的全服广播\ntrue=不再显示 XXX joined the game 类消息\nfalse=原版行为",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.use-player-luck-perms",
 ConfigFileName = file,
 DisplayName = "使用 LuckPerms 玩家缓存",
 Description = "是否直接读取 LuckPerms 玩家对象缓存（绕过 Bukkit API）\ntrue=权限查询更快\nfalse=走标准 API，兼容性更好\n未安装 LuckPerms 时务必 false",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.fix-bridging",
 ConfigFileName = file,
 DisplayName = "修复速桥",
 Description = "是否修复速桥（Bridging）时方块放置位置异常\ntrue=修复\nfalse=还原原版时序，部分玩家可能更顺手",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.entities（每世界：实体优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entities.disable-skeleton-ai",
 ConfigFileName = file,
 DisplayName = "禁用骷髅 AI",
 Description = "true=骷髅不再主动寻路 / 射箭，只保持原地待机\n可显著降低骷髅密集场景的 CPU 占用，但破坏玩法",
 Category = "世界-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entities.disable-zombie-ai",
 ConfigFileName = file,
 DisplayName = "禁用僵尸 AI",
 Description = "true=僵尸不再主动追击玩家 / 拆门\n同上，仅适合刷怪塔或测试服",
 Category = "世界-实体",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entities.fast-velocity-calc",
 ConfigFileName = file,
 DisplayName = "快速速度计算",
 Description = "是否使用更快的实体速度计算算法\ntrue=省 CPU，可能与原版物理略有差异\nfalse=原版精确计算",
 Category = "世界-实体",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.ticks（每世界：tick 优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.ticks.disable-tick-scheduler",
 ConfigFileName = file,
 DisplayName = "禁用 tick 调度器",
 Description = "是否禁用原版 tick 调度器改用简化实现\ntrue=省 CPU 但部分依赖调度的红石机器可能失效\nfalse=原版调度",
 Category = "世界-tick",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.ticks.optimize-hopper",
 ConfigFileName = file,
 DisplayName = "漏斗优化",
 Description = "启用 Paper 的漏斗优化\nfalse 可还原 100% 原版漏斗行为，但会破坏大量生电红石机器\n生电服可考虑 false",
 Category = "世界-tick",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.fixes（每世界：漏洞修复） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.fixes.fix-player-z-fighting",
 ConfigFileName = file,
 DisplayName = "修复玩家 Z 闪烁",
 Description = "是否修复玩家在低 Y 高速移动时的 Z 轴闪烁问题\ntrue=修复（推荐）\nfalse=原版行为",
 Category = "世界-修复",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.fixes.disable-void-fishing",
 ConfigFileName = file,
 DisplayName = "禁用虚空钓鱼",
 Description = "是否禁用虚空钓鱼漏洞\ntrue=禁用（钓鱼浮标在虚空时不再生效）\nfalse=原版行为",
 Category = "世界-修复",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterAirplaneYml.cs
// 功能描述: 注册 Airplane（基于 Paper 的优化分支，已停更）配置文件的描述符
// 包含 airplane.yml 全局 airplane + 每世界 world-settings 三大部分
// 数据来源: TECHNOVE/Airplane README.md + 默认 airplane.yml 模板
// 适用版本: Airplane 1.17.1 / 1.18.2（项目已停更）
// -----------------------------------------------------------------------------

private void RegisterAirplaneYml()
{
 const string file = "airplane.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "config-version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "内部使用，不要手动修改\nAirplane 用它做配置自动升级与兼容性判断",
 Category = "信息",
 DefaultValue = "1",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== airplane（全局优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "airplane.brand-name",
 ConfigFileName = file,
 DisplayName = "服务端品牌名",
 Description = "发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码，可隐藏真实核心类型",
 Category = "全局",
 DefaultValue = "Airplane",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "airplane.allow-unsafe-commands",
 ConfigFileName = file,
 DisplayName = "允许不安全命令",
 Description = "是否允许执行可能引发性能问题或不安全的内置调试命令\ntrue=允许（仅适合开发 / 测试服）\nfalse=禁用（生产服保持）",
 Category = "全局",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.chunks（每世界：区块优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.chunks.chunk-load-cooldown",
 ConfigFileName = file,
 DisplayName = "区块加载冷却",
 Description = "玩家触发区块加载后再次允许加载的间隔（tick）\n0=无冷却\n正值=降低区块加载频率，可缓解突发加载导致的卡顿",
 Category = "世界-区块",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.chunks.autosave-period",
 ConfigFileName = file,
 DisplayName = "自动保存周期",
 Description = "自动保存世界数据的间隔（tick）\n6000 = 5 分钟\n调大省 IO 但崩服丢数据更多；调小反之",
 Category = "世界-区块",
 DefaultValue = "6000",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.chunks.max-chunk-sends-per-tick",
 ConfigFileName = file,
 DisplayName = "每 tick 最大区块发送数",
 Description = "每 tick 向玩家发送的最大区块包数\n0=不限制\n正值=限速，可避免进服时网络尖峰",
 Category = "世界-区块",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 // ==================== world-settings.default.entities（每世界：实体优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entities.spawn-packet-queue",
 ConfigFileName = file,
 DisplayName = "生成包排队",
 Description = "是否把实体生成数据包排队发送\ntrue=平滑网络峰值，避免一次性发送大量实体导致客户端卡顿\nfalse=原版行为",
 Category = "世界-实体",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.entities.dab.enabled",
 ConfigFileName = file,
 DisplayName = "启用 DAB 实体激活",
 Description = "是否启用 Airplane 改进的动态实体激活（DAB）\ntrue=远离玩家的实体降低 tick 频率以省 CPU\nfalse=原版固定激活范围",
 Category = "世界-实体",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 // ==================== world-settings.default.fixes（每世界：修复） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.fixes.fix-coordinate-exploit",
 ConfigFileName = file,
 DisplayName = "修复坐标泄露漏洞",
 Description = "是否修复通过传送包反推远处坐标的漏洞\ntrue=修复（推荐）\nfalse=允许玩家通过特定客户端作弊获取远距离方块位置",
 Category = "世界-修复",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.fixes.prevent-double-pistons",
 ConfigFileName = file,
 DisplayName = "防止双活塞卡服",
 Description = "是否防止双活塞同时激活导致的卡服机器\ntrue=防止（推荐）\nfalse=原版行为，可能被用于恶意卡服",
 Category = "世界-修复",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterTuinityYml.cs
// 功能描述: 注册 Tuinity（基于 Paper 的高性能分支，已合并入上游 Paper）配置文件的描述符
// 包含 tuinity.yml 每世界 chunks + tick-rates + fixes + misc 四大部分
// 数据来源: StarWishsama/Tuinity README.md + 默认 tuinity.yml 模板（社区 fork）
// 适用版本: Tuinity 1.17.1 / 1.18.2（项目已停更，社区 fork 可达 1.20+）
// -----------------------------------------------------------------------------

private void RegisterTuinityYml()
{
 const string file = "tuinity.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "config-version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "内部使用，不要手动修改\nTuinity 用它做配置自动升级与兼容性判断",
 Category = "信息",
 DefaultValue = "1",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== world-settings.default.chunks（每世界：区块加载） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.chunks.chunk-gc",
 ConfigFileName = file,
 DisplayName = "区块垃圾回收间隔",
 Description = "多久回收一次无人观察的区块（tick）\n600 = 30 秒\n调小可更快释放内存\n调大减少 IO 但内存占用高",
 Category = "世界-区块",
 DefaultValue = "600",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.chunks.delay-chunk-unloads-by",
 ConfigFileName = file,
 DisplayName = "延迟区块卸载",
 Description = "玩家离开后多久才真正卸载区块（tick）\n正值=延迟卸载，玩家短时间往返不重复加载\n0=立即卸载",
 Category = "世界-区块",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.chunks.entity-activation-range-strict-mode",
 ConfigFileName = file,
 DisplayName = "实体激活严格模式",
 Description = "是否严格按 Spigot 的实体激活范围判定\ntrue=原版行为\nfalse=使用 Tuinity 优化后的更宽松判定，可省 CPU",
 Category = "世界-区块",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.tick-rates（每世界：tick 频率） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.tick-rates.mob-spawner",
 ConfigFileName = file,
 DisplayName = "刷怪笼 tick 频率",
 Description = "刷怪笼每多少 tick 触发一次生成判定\n1=原版\n2=减半（适合大量刷怪笼的服，可大幅省 CPU）",
 Category = "世界-tick频率",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.tick-rates.sensors.behavior",
 ConfigFileName = file,
 DisplayName = "行为传感器 tick 频率",
 Description = "村民 / 生物 AI 行为传感器（如最近村民、最近玩家）的 tick 频率\n调大可降低村民密集场景的 CPU",
 Category = "世界-tick频率",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.tick-rates.grass-tick",
 ConfigFileName = file,
 DisplayName = "草生长 tick 频率",
 Description = "草方块蔓延生长的 tick 频率\n调大可省 CPU 但草生长变慢，影响自动农场产量",
 Category = "世界-tick频率",
 DefaultValue = "1",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 // ==================== world-settings.default.fixes（每世界：修复） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.fixes.fix-item-merge",
 ConfigFileName = file,
 DisplayName = "修复物品合并",
 Description = "是否修复多个相同物品无法合并的漏洞\ntrue=修复（推荐）\nfalse=原版行为，可能导致掉落物丢失或重复",
 Category = "世界-修复",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.fixes.prevent-moving-into-unloaded-chunks",
 ConfigFileName = file,
 DisplayName = "防止进入未加载区块",
 Description = "是否阻止玩家通过卡墙 / 加速进入未加载区块\ntrue=阻止（防止穿墙与崩溃）\nfalse=原版行为",
 Category = "世界-修复",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== world-settings.default.misc（每世界：杂项优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.misc.use-optimized-light",
 ConfigFileName = file,
 DisplayName = "使用优化光照",
 Description = "是否使用 Tuinity 优化的光照计算引擎\ntrue=光照计算更快、内存更省\nfalse=原版光照（仅排查光照 bug 时关）",
 Category = "世界-杂项",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.misc.redstone-implementation",
 ConfigFileName = file,
 DisplayName = "红石实现",
 Description = "红石更新算法选择\nVANILLA=原版（生电兼容）\nALTERNATE=Tuinity 替代实现（更快但可能与生电机器冲突）",
 Category = "世界-杂项",
 DefaultValue = "VANILLA",
 AllowedValues = ["VANILLA", "ALTERNATE"],
 ValueType = "enum",
 RequiresRestart = true
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterAkarinYml.cs
// 功能描述: 注册 Akarin（基于 Paper 的多线程物理分支，已归档）配置文件的描述符
// 包含 akarin.yml 全局 settings + 每世界 world-settings 三大部分
// 数据来源: Akarin-project/Akarin README.md + 默认 akarin.yml 模板（归档版本）
// 适用版本: Akarin 1.12.2 / 1.15.2（项目已 Public archive，停更）
// -----------------------------------------------------------------------------

private void RegisterAkarinYml()
{
 const string file = "akarin.yml";

 // ==================== 信息块 ====================

 Register(new ServerConfigDescriptor
 {
 Key = "config-version",
 ConfigFileName = file,
 DisplayName = "配置版本号",
 Description = "内部使用，不要手动修改\nAkarin 用它做配置自动升级与兼容性判断",
 Category = "信息",
 DefaultValue = "1",
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== settings（全局设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "settings.brand-name",
 ConfigFileName = file,
 DisplayName = "服务端品牌名",
 Description = "发送给客户端的服务端品牌名（F3 界面 Mod 字段）\n可用 § 颜色码，可隐藏真实核心类型",
 Category = "全局",
 DefaultValue = "Akarin",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.enable-multi-thread",
 ConfigFileName = file,
 DisplayName = "启用多线程物理",
 Description = "Akarin 招牌开关\ntrue=启用物理多线程，把区块 ticking 分摊到多核\nfalse=退化为单线程 Paper\n️ 关闭后 Akarin 与普通 Paper 无差异，建议保持 true",
 Category = "全局",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "settings.threads",
 ConfigFileName = file,
 DisplayName = "物理线程数",
 Description = "物理多线程使用的线程数\n0=自动（按 CPU 核心数估算）\n正值=固定值\n建议 ≤ 物理核心数，避免线程切换开销",
 Category = "全局",
 DefaultValue = "0",
 MinValue = 0,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== world-settings.default.physics（每世界：物理多线程） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.physics.async-block-physics",
 ConfigFileName = file,
 DisplayName = "异步方块物理",
 Description = "是否异步处理方块物理（沙子掉落、水流等）\ntrue=移出主线程，可省 TPS\nfalse=原版同步\n️ 异步可能与某些依赖物理事件的插件冲突",
 Category = "世界-物理",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.physics.async-entity-physics",
 ConfigFileName = file,
 DisplayName = "异步实体物理",
 Description = "是否异步处理实体物理（实体移动、碰撞等）\ntrue=多线程处理大量实体\nfalse=原版同步\n️ 异步实体可能影响反作弊判定",
 Category = "世界-物理",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = true
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.physics.max-async-tasks",
 ConfigFileName = file,
 DisplayName = "最大异步任务数",
 Description = "异步物理任务队列的最大长度\n值越大吞吐越高但延迟上升\n值小延迟低但可能堆积任务\n建议 2-8",
 Category = "世界-物理",
 DefaultValue = "4",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = true
 });

 // ==================== world-settings.default.optimizations（每世界：优化） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimizations.disable-piston-physics",
 ConfigFileName = file,
 DisplayName = "禁用活塞物理",
 Description = "是否禁用活塞推拉方块时的物理计算\ntrue=活塞推方块不再触发物理（极省 CPU 但破坏红石机器）\nfalse=原版行为",
 Category = "世界-优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "world-settings.default.optimizations.fast-leaf-decay",
 ConfigFileName = file,
 DisplayName = "快速叶子衰减",
 Description = "是否使用更快的叶子衰减算法\ntrue=省 CPU 但可能与原版叶子农场产量略有差异\nfalse=原版精确计算",
 Category = "世界-优化",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });


    // ===== AUTO-INJECTED by core-fetcher pipeline =====
    Register(new ServerConfigDescriptor
    {
        Key = "alternative.allow-spawner-modify",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "允许修改刷怪笼",
        Description = "是否允许玩家挖掉或重新放置刷怪笼方块",
        Category = "Akarin 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "alternative.disable-end-portal-create",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "禁用末地传送门创建",
        Description = "开启后玩家无法通过末影之眼激活末地传送门",
        Category = "Akarin 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "alternative.force-difficulty-on-hardcore",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "硬核强制难度",
        Description = "硬核模式下强制锁定难度为 HARD，防止绕过",
        Category = "Akarin 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "alternative.gc-before-stuck-restart",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "卡死重启前 GC",
        Description = "主线程卡死自动重启前先执行一次 Full GC",
        Category = "Akarin 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "alternative.legacy-versioning-compat",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "旧版版本兼容",
        Description = "启用对旧版插件版本号的兼容模式",
        Category = "Akarin 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "alternative.modified-server-brand-name",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "自定义服务端品牌名",
        Description = "留空则用默认值；填入后发送给客户端的品牌名（F3 界面可见）",
        Category = "Akarin 专属",
        DefaultValue = "",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "alternative.version-update-interval",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "版本更新检查间隔",
        Description = "每隔多久检查一次新版本，如 3600s=1 小时",
        Category = "Akarin 专属",
        DefaultValue = "3600s",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "bootstrap.extra-local-address",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "额外本地地址",
        Description = "启动时额外绑定的本地网卡地址列表",
        Category = "Akarin 专属",
        DefaultValue = "[]",
        ValueType = "list",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.always-silent-async-timing",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "静默异步计时",
        Description = "让异步性能计时始终保持静默，不输出耗时日志",
        Category = "Akarin 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.chunk-save-threads",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "区块保存线程数",
        Description = "并行保存区块数据的线程数，默认 2",
        Category = "Akarin 专属",
        DefaultValue = "2",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.enable-panda-redstone-wire",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "启用 Panda 红石优化",
        Description = "启用 Panda 红石优化模块（实验性）",
        Category = "Akarin 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.enable-real-time-ticking",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "实时 Tick",
        Description = "让服务器 Tick 跟随真实时间，不受 TPS 抖动影响",
        Category = "Akarin 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.keep-alive-response-timeout",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "保活响应超时",
        Description = "等待玩家回复 Keepalive 包的超时时间（默认 30s）",
        Category = "Akarin 专属",
        DefaultValue = "30s",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.optimize-chunk-unloading",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "区块卸载优化",
        Description = "启用更激进的区块卸载策略以节省内存",
        Category = "Akarin 专属",
        DefaultValue = "False",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.parallel-mode",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "并行模式",
        Description = "并行处理级别，0=关闭，1=默认多线程",
        Category = "Akarin 专属",
        DefaultValue = "1",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.players-per-chunk-io-thread",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "每区块 IO 线程玩家数",
        Description = "单个区块 IO 线程负责的玩家数量阈值，默认 50",
        Category = "Akarin 专属",
        DefaultValue = "50",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.primary-thread-priority",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "主线程优先级",
        Description = "Java 主线程的 OS 优先级（1-10），默认 7",
        Category = "Akarin 专属",
        DefaultValue = "7",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.send-light-only-chunk-sections",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "仅发送光照区块段",
        Description = "只发送有光照数据的区块段给客户端，减少带宽",
        Category = "Akarin 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.thread-safe.async-catcher.throw-on-caught",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "异步异常抛出",
        Description = "异步线程捕获到异常时是否向上抛出而不是静默吞掉",
        Category = "Akarin 专属",
        DefaultValue = "True",
        ValueType = "bool",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.tick-rate.keep-alive-packet-send-interval",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "保活包发送间隔",
        Description = "服务器向玩家发送 Keepalive 包的间隔（默认 15s）",
        Category = "Akarin 专属",
        DefaultValue = "15s",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.tick-rate.players-info-update-interval",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "玩家信息更新间隔",
        Description = "玩家列表/皮肤等信息刷新间隔（默认 30s）",
        Category = "Akarin 专属",
        DefaultValue = "30s",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "core.tick-rate.world-time-update-interval",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "世界时间更新间隔",
        Description = "世界时间广播给客户端的间隔（默认 1s）",
        Category = "Akarin 专属",
        DefaultValue = "1s",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.connect.player-join-server",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "玩家加入消息",
        Description = "%s 为玩家名；玩家加入服务器时的聊天提示",
        Category = "Akarin 专属",
        DefaultValue = "§e%s joined the game",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.connect.renamed-player-join-server",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "改名玩家加入消息",
        Description = "%s=新名，%s=旧名；改名后首次登录提示",
        Category = "Akarin 专属",
        DefaultValue = "§e%s (formerly known as %s) joined the game",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.disconnect.ban-expires",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "封禁到期提示",
        Description = "被封禁玩家尝试连接时显示的剩余时间提示",
        Category = "Akarin 专属",
        DefaultValue = "\nYour ban will be removed on ",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.disconnect.ban-player-ip",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "IP 封禁消息",
        Description = "IP 被封时玩家看到的提示文字",
        Category = "Akarin 专属",
        DefaultValue = "Your IP address is banned from this server! %s %s",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.disconnect.ban-player-name",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "玩家封禁消息",
        Description = "玩家名被封时看到的提示文字",
        Category = "Akarin 专属",
        DefaultValue = "You are banned from this server! %s %s",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.disconnect.ban-reason",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "封禁原因前缀",
        Description = "显示封禁原因时的前缀文字",
        Category = "Akarin 专属",
        DefaultValue = "\nReason: ",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.disconnect.kick-player",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "OP 踢出消息",
        Description = "被 OP 用 /kick 命令踢出时显示的文字",
        Category = "Akarin 专属",
        DefaultValue = "Kicked by an operator.",
        ValueType = "double",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.disconnect.kick-player-duplicate-login",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "异地登录踢出",
        Description = "同一账号在别处登录时玩家被踢出的提示",
        Category = "Akarin 专属",
        DefaultValue = "You logged in from another location",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.disconnect.kick-player-timeout-keep-alive",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "超时踢出消息",
        Description = "长时间未响应 Keepalive 包被踢出时的提示",
        Category = "Akarin 专属",
        DefaultValue = "Timed out",
        ValueType = "int",
        RequiresRestart = false,
    });
    Register(new ServerConfigDescriptor
    {
        Key = "messages.disconnect.player-quit-server",
        ConfigFileName = "config/akarin.yml",
        DisplayName = "玩家退出消息",
        Description = "%s 为玩家名；离开服务器时的聊天提示",
        Category = "Akarin 专属",
        DefaultValue = "§e%s left the game",
        ValueType = "int",
        RequiresRestart = false,
    });
    // ===== END AUTO-INJECTED =====
}
// -----------------------------------------------------------------------------
// 文件名: RegisterPermissionsYml.cs
// 功能描述: 注册 Bukkit permissions.yml（默认权限组配置）的描述符
// ️ permissions.yml 不定义具体权限，仅定义"权限组（permission groups）"
// 供插件通过 Permission API 引用聚合，普通权限由各插件自行注册
// 数据来源: Bukkit Wiki - permissions.yml / org.bukkit.permissions.Permission API
// 适用版本: Bukkit 1.13+ / Spigot / Paper / Purpur 等所有 Bukkit 衍生核心
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
// 并在构造函数中调用 RegisterPermissionsYml();
// -----------------------------------------------------------------------------

private void RegisterPermissionsYml()
{
 const string file = "permissions.yml";

 // ==================== 内置默认组（default） ====================
 // Bukkit 启动时自动注入一个名为 "default" 的内置权限组，default.default = true
 // 即所有玩家默认拥有。可在此组下追加权限节点，使其对所有人开放。

 Register(new ServerConfigDescriptor
 {
 Key = "default",
 ConfigFileName = file,
 DisplayName = "内置默认权限组",
 Description = "Bukkit 内置的默认权限组名，所有玩家自动归属此组\n组的 children 列表中的权限会按 default 字段策略赋给玩家\n️ 不建议在此组直接添加高权限节点，应另建自定义组",
 Category = "内置组",
 DefaultValue = "{}",
 ValueType = "map",
 RequiresRestart = false
 });

 // ==================== 通用：自定义权限组字段 ====================
 // 任何自定义权限组（如 server.vip / server.admin）均含以下字段：
 // - default：默认赋权策略（true / false / op / not-op）
 // - description：组描述
 // - children：子权限列表（权限节点 -> true/false）

 Register(new ServerConfigDescriptor
 {
 Key = "<custom-group>.default",
 ConfigFileName = file,
 DisplayName = "组默认赋权策略",
 Description = "此权限组的默认赋权策略\ntrue = 所有人都拥有此组权限\nfalse = 所有人都没有此组权限（需插件显式赋予）\nop = 仅 OP 拥有\nnot-op = 仅非 OP 拥有\n推荐：普通玩家组设 true，特权组设 false（由权限插件管理）",
 Category = "通用字段",
 DefaultValue = "op",
 AllowedValues = ["true", "false", "op", "not-op"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "<custom-group>.description",
 ConfigFileName = file,
 DisplayName = "权限组描述",
 Description = "此权限组的文字描述，便于管理员理解用途\n仅作记录，不影响实际权限判断\n例：\"VIP 玩家基础权限组\"",
 Category = "通用字段",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "<custom-group>.children",
 ConfigFileName = file,
 DisplayName = "子权限列表",
 Description = "此组包含的子权限节点映射\n键为权限节点名（如 bukkit.command.teleport），值为 true/false 表示是否赋予\n️ 可嵌套其他权限组（递归赋权）\n例：\nchildren:\n bukkit.command.help: true\n bukkit.command.tell: true\n server.basics: true # 嵌套引用其他权限组",
 Category = "通用字段",
 DefaultValue = "{}",
 ValueType = "map",
 RequiresRestart = false
 });

 // ==================== 内置 Bukkit 命令权限节点 ====================
 // Bukkit API 自带的命令权限节点，可在自定义组的 children 中引用。
 // 以下注册几个最常用的内置权限节点供管理员参考。

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.help",
 ConfigFileName = file,
 DisplayName = "Bukkit help 命令权限",
 Description = "允许玩家执行 /help 命令查看帮助页\n所有玩家默认拥有（在 default 组中）",
 Category = "Bukkit 内置权限",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.tell",
 ConfigFileName = file,
 DisplayName = "Bukkit tell 命令权限",
 Description = "允许玩家执行 /tell（/msg）私聊命令\n所有玩家默认拥有",
 Category = "Bukkit 内置权限",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.list",
 ConfigFileName = file,
 DisplayName = "Bukkit list 命令权限",
 Description = "允许玩家执行 /list 查看在线玩家列表\n所有玩家默认拥有",
 Category = "Bukkit 内置权限",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.teleport",
 ConfigFileName = file,
 DisplayName = "Bukkit teleport 命令权限",
 Description = "允许玩家执行 /tp（/teleport）传送命令\n默认仅 OP 拥有，普通玩家需显式赋予",
 Category = "Bukkit 内置权限",
 DefaultValue = "op",
 AllowedValues = ["true", "false", "op", "not-op"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.gamemode",
 ConfigFileName = file,
 DisplayName = "Bukkit gamemode 命令权限",
 Description = "允许玩家执行 /gamemode 切换游戏模式\n默认仅 OP 拥有\n️ 生存服绝不赋予普通玩家，否则可作弊",
 Category = "Bukkit 内置权限",
 DefaultValue = "op",
 AllowedValues = ["true", "false", "op", "not-op"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.give",
 ConfigFileName = file,
 DisplayName = "Bukkit give 命令权限",
 Description = "允许玩家执行 /give 给自己或其他玩家物品\n默认仅 OP 拥有\n️ 生存服绝不赋予普通玩家",
 Category = "Bukkit 内置权限",
 DefaultValue = "op",
 AllowedValues = ["true", "false", "op", "not-op"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.stop",
 ConfigFileName = file,
 DisplayName = "Bukkit stop 命令权限",
 Description = "允许玩家执行 /stop 关闭服务器\n默认仅 OP 拥有\n️ 生产环境绝不赋予普通玩家",
 Category = "Bukkit 内置权限",
 DefaultValue = "op",
 AllowedValues = ["true", "false", "op", "not-op"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.ban",
 ConfigFileName = file,
 DisplayName = "Bukkit ban 命令权限",
 Description = "允许玩家执行 /ban 封禁玩家\n默认仅 OP 拥有\n管理员组可赋予此权限",
 Category = "Bukkit 内置权限",
 DefaultValue = "op",
 AllowedValues = ["true", "false", "op", "not-op"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.whitelist",
 ConfigFileName = file,
 DisplayName = "Bukkit whitelist 命令权限",
 Description = "允许玩家执行 /whitelist 管理白名单\n默认仅 OP 拥有",
 Category = "Bukkit 内置权限",
 DefaultValue = "op",
 AllowedValues = ["true", "false", "op", "not-op"],
 ValueType = "enum",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "children.bukkit.command.op",
 ConfigFileName = file,
 DisplayName = "Bukkit op 命令权限",
 Description = "允许玩家执行 /op /deop 授予/撤销 OP 权限\n默认仅 OP 拥有\n️ 极敏感权限，绝不赋予普通玩家",
 Category = "Bukkit 内置权限",
 DefaultValue = "op",
 AllowedValues = ["true", "false", "op", "not-op"],
 ValueType = "enum",
 RequiresRestart = false
 });
}
// -----------------------------------------------------------------------------
// 文件名: RegisterCommandsYml.cs
// 功能描述: 注册 Bukkit commands.yml（命令别名与替换配置）的描述符
// ️ 1.13+ 引入，比 bukkit.yml 的 aliases 更灵活，支持参数转发
// 别名不能与现有命令同名，否则不生效
// 数据来源: Bukkit Wiki - commands.yml / org.bukkit.command.CommandMap
// 适用版本: Bukkit 1.13+ / Spigot / Paper / Purpur 等所有 Bukkit 衍生核心
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
// 并在构造函数中调用 RegisterCommandsYml();
// -----------------------------------------------------------------------------

private void RegisterCommandsYml()
{
 const string file = "commands.yml";

 // ==================== 顶层结构 ====================
 // commands.yml 仅含两个顶层键：command-block-overrides 与 aliases

 Register(new ServerConfigDescriptor
 {
 Key = "command-block-overrides",
 ConfigFileName = file,
 DisplayName = "命令方块覆盖",
 Description = "命令方块执行命令时使用的别名映射\n键为原命令名，值为别名命令\n一般留空 {}，命令方块直接执行原命令\n例：{\"gamemode\": \"minecraft:gamemode\"}",
 Category = "顶层",
 DefaultValue = "{}",
 ValueType = "map",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases",
 ConfigFileName = file,
 DisplayName = "命令别名",
 Description = "全局命令别名映射\n键为别名命令名（玩家输入的命令），值可以是字符串（直接转发到目标命令）或 map（含 i/k/p 等 flags）\n️ 别名不能与现有命令同名，否则不生效\n例：\naliases:\n gmc:\n p: \"minecraft:gamemode creative $1\"\n i:\n p: \"minecraft:give $1 $2 $3\"\n i: true",
 Category = "顶层",
 DefaultValue = "{}",
 ValueType = "map",
 RequiresRestart = false
 });

 // ==================== aliases.<别名> 通用字段 ====================
 // 每个别名条目支持三种 flags：i（忽略大小写）、k（保留原命令）、p（参数模板）

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.<alias>.p",
 ConfigFileName = file,
 DisplayName = "参数转发模板",
 Description = "别名转发的目标命令与参数模板\n占位符：\n $1 = 第一个参数\n $2 = 第二个参数\n $1- = 第一个及之后所有参数\n $@ = 所有参数\n例：\"minecraft:gamemode creative $1\" 将别名参数作为 gamemode 的第二个参数\n️ 目标命令前缀 minecraft: 强制使用 Vanilla 实现，绕过插件 Hook",
 Category = "别名字段",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.<alias>.i",
 ConfigFileName = file,
 DisplayName = "忽略大小写",
 Description = "别名匹配时是否忽略大小写\ntrue = 大小写不敏感（/GMC 与 /gmc 都触发）\nfalse = 严格大小写匹配\n推荐开启以提高容错",
 Category = "别名字段",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.<alias>.k",
 ConfigFileName = file,
 DisplayName = "保留原命令",
 Description = "true = 除执行别名命令外，原命令（如有）仍保留可用\nfalse = 别名完全替换原命令\n️ 仅当别名与现有命令同名时此项有意义",
 Category = "别名字段",
 DefaultValue = "false",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== 别名目标命令命名空间前缀 ====================
 // 别名目标命令可加命名空间前缀强制使用特定实现，绕过插件 Hook。

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.<alias>.namespace.minecraft",
 ConfigFileName = file,
 DisplayName = "minecraft: 命名空间前缀",
 Description = "目标命令前缀 minecraft: 强制使用 Vanilla 实现\n例：\"minecraft:gamemode\" 绕过所有插件的 gamemode Hook\n️ 使用此前缀可能导致插件功能失效，仅在确认无插件 Hook 此命令时使用",
 Category = "命名空间",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.<alias>.namespace.bukkit",
 ConfigFileName = file,
 DisplayName = "bukkit: 命名空间前缀",
 Description = "目标命令前缀 bukkit: 强制使用 Bukkit 实现\n例：\"bukkit:gamemode\" 强制使用 Bukkit 版本的 gamemode",
 Category = "命名空间",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 // ==================== 常用别名示例注册（参考项） ====================
 // 以下注册几个常用别名示例，供管理员参考。默认配置中 aliases 为空，需手动添加。
 // 这些示例不会自动生效，需手动复制到 commands.yml 中。

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.gamemode.p",
 ConfigFileName = file,
 DisplayName = "示例：gamemode 别名",
 Description = "示例别名：将 /gamemode 重定向到 /minecraft:gamemode\n配置：\naliases:\n gamemode:\n p: \"minecraft:gamemode $1-\"\n效果：玩家输入 /gamemode creative 等价于 /minecraft:gamemode creative\n用意：绕过插件 Hook，强制使用 Vanilla 实现",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.gmc.p",
 ConfigFileName = file,
 DisplayName = "示例：gmc 快捷创造模式",
 Description = "示例别名：/gmc <玩家> 快速切换到创造模式\n配置：\naliases:\n gmc:\n p: \"minecraft:gamemode creative $1\"\n效果：/gmc 等价于 /gamemode creative（自己），/gmc PlayerA 等价于 /gamemode creative PlayerA",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.gms.p",
 ConfigFileName = file,
 DisplayName = "示例：gms 快捷生存模式",
 Description = "示例别名：/gms <玩家> 快速切换到生存模式\n配置：\naliases:\n gms:\n p: \"minecraft:gamemode survival $1\"",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.i.p",
 ConfigFileName = file,
 DisplayName = "示例：i 快捷 give",
 Description = "示例别名：/i <物品> <数量> <数据> 等价于 /give 自己\n配置：\naliases:\n i:\n p: \"minecraft:give <player> $1 $2 $3\"\n i: true\n效果：/i diamond 64 等价于 /give <自己> diamond 64",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.t.p",
 ConfigFileName = file,
 DisplayName = "示例：t 快捷 teleport",
 Description = "示例别名：/t <玩家> 等价于 /tp\n配置：\naliases:\n t:\n p: \"minecraft:teleport $1-\"\n i: true",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "aliases.heal.p",
 ConfigFileName = file,
 DisplayName = "示例：heal 快捷治疗",
 Description = "示例别名：/heal 通过 effect 命令给自己瞬间治疗\n配置：\naliases:\n heal:\n p: \"minecraft:effect give <player> minecraft:instant_health 1 10\"",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });


    // ===== AUTO-INJECTED by core-fetcher pipeline =====
    Register(new ServerConfigDescriptor
    {
        Key = "aliases.icanhasbukkit",
        ConfigFileName = "config/commands.yml",
        DisplayName = "icanhasbukkit 别名",
        Description = "icanhasbukkit 会执行 version 和 plugins 命令",
        Category = "Glowstone",
        DefaultValue = "[\"version $1-\", \"plugins\"]",
        ValueType = "list",
        RequiresRestart = false,
    });
    // ===== END AUTO-INJECTED =====
}
// -----------------------------------------------------------------------------
// 文件名: RegisterHelpYml.cs
// 功能描述: 注册 Bukkit help.yml（帮助页配置）的描述符
// 控制 /help 命令的显示：分页大小、主题格式、自定义主题、命令描述修订
// 数据来源: Bukkit Wiki - help.yml / org.bukkit.command.defaults.HelpCommand
// 适用版本: Bukkit 1.13+ / Spigot / Paper / Purpur 等所有 Bukkit 衍生核心
// 集成位置: 应粘贴到 ConfigDescriptorRegistry.cs 的 Register() 私有方法体中，
// 并在构造函数中调用 RegisterHelpYml();
// -----------------------------------------------------------------------------

private void RegisterHelpYml()
{
 const string file = "help.yml";

 // ==================== general（通用设置） ====================

 Register(new ServerConfigDescriptor
 {
 Key = "general.command-prefix",
 ConfigFileName = file,
 DisplayName = "命令前缀",
 Description = "帮助页中命令的前缀字符\n一般保持 / (玩家输入命令的标准前缀)\n修改为其他字符仅影响显示，不影响实际命令执行",
 Category = "通用",
 DefaultValue = "/",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.console-command-prefix",
 ConfigFileName = file,
 DisplayName = "控制台命令前缀",
 Description = "控制台中命令的前缀\n留空则与 command-prefix 相同\n控制台输入命令无需 /，此值仅影响帮助页显示",
 Category = "通用",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.default-topic-format",
 ConfigFileName = file,
 DisplayName = "默认主题格式",
 Description = "默认帮助主题的输出格式模板\n可用占位符：\n <description> = 命令描述\n <usage> = 命令用法\n <aliases> = 命令别名\n <permission> = 所需权限\n默认值含两个换行（\\n）分隔描述、用法、别名三段",
 Category = "通用",
 DefaultValue = " <description>\\n\\n<usage>\\n\\n<aliases>\\n",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.search-index-listed",
 ConfigFileName = file,
 DisplayName = "搜索时列出索引",
 Description = "/help <关键词> 搜索时是否在结果中列出索引\true = 显示完整索引（信息全）\nfalse = 仅显示匹配项（更简洁）",
 Category = "通用",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.max-help-page-size",
 ConfigFileName = file,
 DisplayName = "每页最大帮助数",
 Description = "/help 每页显示多少条命令\n值越大单页内容越多（玩家翻页少）\n值越小分页越多（单页更清爽）\n建议 7-10 之间",
 Category = "通用",
 DefaultValue = "7",
 MinValue = 1,
 ValueType = "int",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.list-of-headers",
 ConfigFileName = file,
 DisplayName = "帮助页标题列表",
 Description = "各类帮助页的标题文本列表，按顺序对应：\n[0] 索引页标题\n[1] 搜索页标题\n[2] 主题页标题（<topic> 会被替换为主题名）\n[3] 主题列表页标题\n[4] 上一页按钮文本\n[5] 下一页按钮文本\n支持 § 颜色码",
 Category = "通用",
 DefaultValue = "[Help - Index, Help - Search, Help - <topic>, Help - Topics, Help - Previous, Help - Next]",
 ValueType = "list",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.default-topic-permission",
 ConfigFileName = file,
 DisplayName = "默认主题权限",
 Description = "查看默认帮助主题所需的权限节点\n留空 = 所有人可见\n填写权限节点（如 bukkit.command.help）= 仅拥有此权限的玩家可见",
 Category = "通用",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.topics-on-first-page",
 ConfigFileName = file,
 DisplayName = "首页显示主题列表",
 Description = "/help 第一页是否显示自定义主题列表\ntrue = 首页显示主题索引\nfalse = 首页直接显示命令列表",
 Category = "通用",
 DefaultValue = "true",
 AllowedValues = ["true", "false"],
 ValueType = "bool",
 RequiresRestart = false
 });

 // ==================== amendments（命令修订） ====================
 // 对已注册命令的描述进行补充修改，不影响命令实际行为，仅影响帮助页显示。

 Register(new ServerConfigDescriptor
 {
 Key = "general.amendments",
 ConfigFileName = file,
 DisplayName = "命令修订列表",
 Description = "对已注册命令的描述进行覆盖修改\n键为命令名（不含 /），值为包含 short-description/full-description/usage/permission/aliases 的 map\n仅影响帮助页显示，不影响命令实际行为\n例：\namendments:\n stop:\n short-description: 关闭服务器\n permission: bukkit.command.stop",
 Category = "命令修订",
 DefaultValue = "{}",
 ValueType = "map",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.amendments.<cmd>.short-description",
 ConfigFileName = file,
 DisplayName = "命令短描述",
 Description = "覆盖命令在帮助列表中的短描述（单行）\n仅影响显示，不影响实际命令\n例：\"关闭服务器\"",
 Category = "命令修订",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.amendments.<cmd>.full-description",
 ConfigFileName = file,
 DisplayName = "命令完整描述",
 Description = "覆盖命令的完整描述（多行）\n仅影响 /help <命令> 详情页\n例：\"关闭服务器并踢出所有玩家，需要 OP 权限\"",
 Category = "命令修订",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.amendments.<cmd>.usage",
 ConfigFileName = file,
 DisplayName = "命令用法",
 Description = "覆盖命令的用法说明\n例：\"/stop [确认]\"",
 Category = "命令修订",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.amendments.<cmd>.permission",
 ConfigFileName = file,
 DisplayName = "命令权限",
 Description = "覆盖命令所需的权限节点\n️ 仅影响帮助页显示，不影响实际权限检查\n例：\"bukkit.command.stop\"\n要让玩家真正无法使用命令，需在 permissions.yml 或权限插件中设置",
 Category = "命令修订",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "general.amendments.<cmd>.aliases",
 ConfigFileName = file,
 DisplayName = "命令别名",
 Description = "覆盖命令的别名列表\n仅影响帮助页显示，不影响实际别名（实际别名在 plugin.yml 或 commands.yml 中定义）",
 Category = "命令修订",
 DefaultValue = "[]",
 ValueType = "list",
 RequiresRestart = false
 });

 // ==================== topics（自定义主题） ====================
 // 自定义帮助主题，玩家可通过 /help <主题名> 查看。
 // 常用于显示服务器规则、玩法说明等自定义内容。

 Register(new ServerConfigDescriptor
 {
 Key = "topics",
 ConfigFileName = file,
 DisplayName = "自定义主题列表",
 Description = "自定义帮助主题映射\n键为主题名（玩家通过 /help <主题名> 查看，主题名前的 / 可省略）\n值为包含 short-description/full-description/permission 的 map\n例：\ntopics:\n /rules:\n short-description: 服务器规则\n full-description: |\n 1. 禁止作弊\n 2. 禁止恶意破坏\n permission: ''",
 Category = "自定义主题",
 DefaultValue = "{}",
 ValueType = "map",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "topics.<topic>.short-description",
 ConfigFileName = file,
 DisplayName = "主题短描述",
 Description = "自定义主题在主题列表中的短描述（单行）\n例：\"服务器规则\"",
 Category = "自定义主题",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "topics.<topic>.full-description",
 ConfigFileName = file,
 DisplayName = "主题完整描述",
 Description = "自定义主题的完整描述（多行）\n支持 \\n 换行或 YAML 的 | 块字符串\n玩家执行 /help <主题名> 时显示此内容\n例：\nfull-description: |\n 1. 禁止作弊\n 2. 禁止恶意破坏\n 3. 禁止骚扰他人",
 Category = "自定义主题",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "topics.<topic>.permission",
 ConfigFileName = file,
 DisplayName = "主题查看权限",
 Description = "查看此主题所需的权限节点\n留空 = 所有人可见\n填写权限节点 = 仅拥有此权限的玩家可见\n例：\"server.rules.vip\" 仅 VIP 可见某主题",
 Category = "自定义主题",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 // ==================== index（索引页额外项） ====================
 // 在 /help 索引页中追加额外分类项，玩家可点击查看详情。

 Register(new ServerConfigDescriptor
 {
 Key = "index",
 ConfigFileName = file,
 DisplayName = "索引页额外项",
 Description = "在 /help 索引页中追加的分类项\n键为分类名，值为包含 short-description/full-description 的 map\n玩家可通过 /help <分类名> 查看详情\n例：\nindex:\n basics:\n short-description: 基础命令\n full-description: 查看基础服务器命令",
 Category = "索引页",
 DefaultValue = "{}",
 ValueType = "map",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "index.<name>.short-description",
 ConfigFileName = file,
 DisplayName = "索引项短描述",
 Description = "索引页中某个分类项的短描述\n例：\"基础命令\"",
 Category = "索引页",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "index.<name>.full-description",
 ConfigFileName = file,
 DisplayName = "索引项完整描述",
 Description = "索引页中某个分类项的完整描述\n玩家通过 /help <名称> 查看此内容\n支持 \\n 换行或 YAML 块字符串",
 Category = "索引页",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 // ==================== 常用自定义主题示例（参考项） ====================
 // 以下注册几个常用自定义主题示例，供管理员参考。默认配置中 topics 为空，需手动添加。

 Register(new ServerConfigDescriptor
 {
 Key = "topics./rules.short-description",
 ConfigFileName = file,
 DisplayName = "示例：服务器规则主题",
 Description = "示例主题：玩家执行 /help rules 查看服务器规则\n配置：\ntopics:\n /rules:\n short-description: 服务器规则\n full-description: |\n 1. 禁止作弊\n 2. 禁止恶意破坏\n 3. 禁止骚扰他人\n permission: ''\n效果：所有人可 /help rules 查看规则",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "topics./vip.short-description",
 ConfigFileName = file,
 DisplayName = "示例：VIP 权限主题",
 Description = "示例主题：仅 VIP 可查看的特权说明\n配置：\ntopics:\n /vip:\n short-description: VIP 特权\n full-description: |\n VIP 专属特权：\n - /fly 飞行\n - /heal 治疗\n - /feed 充饥\n permission: 'group.vip'\n效果：仅 VIP 组玩家可 /help vip 查看",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });

 Register(new ServerConfigDescriptor
 {
 Key = "topics./menu.short-description",
 ConfigFileName = file,
 DisplayName = "示例：菜单导航主题",
 Description = "示例主题：列出服务器所有自定义主题导航\n配置：\ntopics:\n /menu:\n short-description: 服务器菜单\n full-description: |\n 服务器帮助主题导航：\n /help rules - 服务器规则\n /help vip - VIP 特权\n /help basics - 基础命令",
 Category = "常用示例",
 DefaultValue = "",
 ValueType = "string",
 RequiresRestart = false
 });
}
}
