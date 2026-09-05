// -----------------------------------------------------------------------------
// 文件名: IConfigManager.cs
// 命名空间: io.NET.ZTR_OS.Features.ConfigEditor.Services
// 功能描述: 配置管理器接口契约，定义多格式配置文件的统一读写与验证规范
// 依赖组件: System.Collections.Generic, System.Threading.Tasks
// 设计模式: 接口契约模式、策略模式（多格式解析器抽象）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ConfigEditor.Services;

/// <summary>
/// 配置管理器接口契约，提供异构配置文件的统一访问层
/// </summary>
/// <remarks>
/// 支持的配置格式：
///   - .properties（Java Properties 格式，如 server.properties）
///   - .yml / .yaml（YAML 格式，如 spigot.yml、paper-global.yml）
///   - .json（JSON 格式，模组配置文件）
/// 
/// 核心能力：
///   1. 基于文件扩展名的解析器自动分派
///   2. 配置值约束验证（数值范围、枚举集合、正则模式、布尔类型）
///   3. 配置项分类组织（面向 UI 展示层）
///   4. 嵌套配置结构的扁平化映射（dot-path 表示法）
/// </remarks>
public interface IConfigManager
{
    /// <summary>
    /// 读取配置文件并返回扁平化键值对集合
    /// </summary>
    /// <param name="filePath">配置文件的完整路径</param>
    /// <returns>扁平化的键值对字典，采用不区分大小写的键比较</returns>
    /// <remarks>
    /// 自动根据文件扩展名选择对应解析器：
    ///   - .properties → <see cref="PropertiesParser"/>
    ///   - .yml / .yaml → YAML 解析器（嵌套结构递归压平）
    ///   - .json → JSON 解析器（嵌套结构递归压平）
    /// </remarks>
    public Task<Dictionary<string, string>> ReadConfigAsync(string filePath);

    /// <summary>
    /// 将扁平化配置写回文件
    /// </summary>
    /// <param name="filePath">目标配置文件的完整路径</param>
    /// <param name="config">扁平化的键值对字典</param>
    /// <remarks>
    /// 自动根据文件扩展名选择序列化器。
    /// 对于 YAML/JSON 格式，会将 dot-path 表示的扁平化键值对
    /// 还原为嵌套结构后再写入，以保持文件的可读性。
    /// </remarks>
    public Task SaveConfigAsync(string filePath, Dictionary<string, string> config);

    /// <summary>
    /// 获取配置项的描述符（元数据信息）
    /// </summary>
    /// <param name="key">配置项键名（支持 dot-path 表示法）</param>
    /// <param name="configFileName">配置文件名（如 "server.properties"）</param>
    /// <returns>配置描述符实例；未注册的配置项返回 <c>null</c></returns>
    public ServerConfigDescriptor? GetDescriptor(string key, string configFileName);

    /// <summary>
    /// 验证配置值是否符合约束规则
    /// </summary>
    /// <param name="key">配置项键名</param>
    /// <param name="configFileName">配置文件名</param>
    /// <param name="value">待验证的配置值</param>
    /// <returns>验证通过返回 <c>true</c>，否则返回 <c>false</c></returns>
    /// <remarks>
    /// 基于 <see cref="ServerConfigDescriptor"/> 中定义的规则执行验证：
    ///   - MinValue / MaxValue（数值范围约束）
    ///   - AllowedValues（枚举值集合约束）
    ///   - RegexPattern（正则表达式模式匹配）
    ///   - IsBoolean（布尔类型语义验证）
    /// 若不存在对应描述符，则默认通过验证（未知配置项不拦截）。
    /// </remarks>
    public bool ValidateValue(string key, string configFileName, string value);

    /// <summary>
    /// 按分类维度分组配置项描述符
    /// </summary>
    /// <param name="configFileName">配置文件名</param>
    /// <param name="config">当前配置的键值对（可选，用于标记已修改项）</param>
    /// <returns>按 Category 分组的描述符字典</returns>
    /// <remarks>
    /// 返回结构面向 UI 展示层，支持按类别（如网络、世界、玩家等）
    /// 组织配置项，提升配置界面的可读性与可操作性。
    /// </remarks>
    public Dictionary<string, List<ServerConfigDescriptor>> GroupByCategory(string configFileName, Dictionary<string, string>? config = null);

    /// <summary>
    /// 获取配置描述符翻译覆盖率报告
    /// </summary>
    /// <returns>覆盖率统计报告对象</returns>
    ConfigDescriptorRegistry.CoverageReport GetCoverageReport();

    /// <summary>
    /// 筛选未匹配描述符的配置键列表
    /// </summary>
    /// <param name="keys">待检查的配置键集合</param>
    /// <param name="configFileName">配置文件名</param>
    /// <returns>未注册描述符的键列表</returns>
    List<string> FindUnmatchedKeys(List<string> keys, string configFileName);
}
