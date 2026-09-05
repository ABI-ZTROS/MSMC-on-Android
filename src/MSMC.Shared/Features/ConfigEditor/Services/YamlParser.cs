// -----------------------------------------------------------------------------
// 文件名: YamlParser.cs
// 命名空间: io.NET.ZTR_OS.Features.ConfigEditor.Services
// 功能描述: YAML 格式解析器，基于 YamlDotNet 实现嵌套结构的读写与 dot-path 访问
// 依赖组件: System.Collections.Generic, Serilog, YamlDotNet
// 设计模式: 解析器模式、静态工具类模式、路径访问模式
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ConfigEditor.Services;

using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// YAML 配置解析器，基于 YamlDotNet 库实现
/// </summary>
/// <remarks>
/// <para>面向 Minecraft 服务器生态中的 YAML 配置文件提供解析与序列化能力，
/// 支持的典型文件包括：spigot.yml、bukkit.yml、paper-global.yml、
/// paper-world-defaults.yml 等。</para>
/// <para>核心特性：
///   - 基于 YamlDotNet 的反序列化/序列化引擎
///   - 下划线命名约定（UnderscoredNamingConvention）
///   - 嵌套字典结构与 dot-path 表示法的双向转换
///   - 忽略未匹配属性（兼容 Minecraft 配置文件中的未知字段）
///   - 默认值省略输出（保持文件简洁性）
/// </para>
/// </remarks>
public static class YamlParser
{
    /// <summary>
    /// YAML 反序列化器实例，用于读取 YAML 文本
    /// </summary>
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// YAML 序列化器实例，用于写入 YAML 文本
    /// </summary>
    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .Build();

    /// <summary>
    /// 解析 YAML 字符串为嵌套字典结构
    /// </summary>
    /// <param name="content">YAML 格式的文本内容</param>
    /// <returns>嵌套字典结构，叶子节点为基础类型值</returns>
    /// <exception cref="ArgumentNullException">当 content 为 null 时抛出</exception>
    /// <exception cref="YamlDotNet.Core.YamlException">当 YAML 格式语法有误时抛出</exception>
    public static Dictionary<string, object?> Parse(string content)
    {
        Log.Debug("YamlParser.Parse: 解析 {Len} 字符", content.Length);
        ArgumentNullException.ThrowIfNull(content);

        return _deserializer.Deserialize<Dictionary<string, object?>>(content)
               ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// 将嵌套字典序列化为 YAML 字符串
    /// </summary>
    /// <param name="data">嵌套字典数据</param>
    /// <returns>YAML 格式文本</returns>
    /// <exception cref="ArgumentNullException">当 data 为 null 时抛出</exception>
    public static string Serialize(Dictionary<string, object?> data)
    {
        Log.Debug("YamlParser.Serialize: {Count} 个顶级键", data.Count);
        ArgumentNullException.ThrowIfNull(data);

        return _serializer.Serialize(data);
    }

    /// <summary>
    /// 通过 dot-path 路径获取嵌套字典中的值
    /// </summary>
    /// <param name="data">YAML 解析后的嵌套字典</param>
    /// <param name="path">dot 分隔的路径表达式，如 "world-settings.default.seed"</param>
    /// <returns>找到的值；路径不存在时返回 <c>null</c></returns>
    /// <exception cref="ArgumentNullException">当 data 或 path 为 null 时抛出</exception>
    public static object? GetValue(Dictionary<string, object?> data, string path)
    {
        Log.Debug("YamlParser.GetValue: 路径={Path}", path);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length == 0)
            return null;

        var segments = path.Split('.');
        object? current = data;

        foreach (var segment in segments)
        {
            if (current is not Dictionary<string, object?> dict)
                return null;

            if (!dict.TryGetValue(segment, out current))
                return null;
        }

        return current;
    }

    /// <summary>
    /// 通过 dot-path 路径设置嵌套字典中的值
    /// </summary>
    /// <param name="data">YAML 解析后的嵌套字典</param>
    /// <param name="path">dot 分隔的路径表达式，如 "a.b.c"</param>
    /// <param name="value">要设置的值</param>
    /// <exception cref="ArgumentNullException">当 data 或 path 为 null 时抛出</exception>
    /// <exception cref="ArgumentException">当 path 为空字符串时抛出</exception>
    /// <remarks>
    /// 若路径中的中间字典节点不存在，则自动创建。
    /// 若中间节点为非字典类型，则覆盖为新的字典节点。
    /// </remarks>
    public static void SetValue(Dictionary<string, object?> data, string path, object value)
    {
        Log.Debug("YamlParser.SetValue: 路径={Path} 值={Value}", path, value);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length == 0)
        {
            Log.Warning("路径为空: {Path}", path);
            throw new ArgumentException("路径不能为空字符串 —— 你想设置什么呢？空气吗？", nameof(path));
        }

        var segments = path.Split('.');
        var current = data;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];

            if (!current.TryGetValue(segment, out var next))
            {
                var newDict = new Dictionary<string, object?>();
                current[segment] = newDict;
                current = newDict;
            }
            else if (next is Dictionary<string, object?> nextDict)
            {
                current = nextDict;
            }
            else
            {
                var newDict = new Dictionary<string, object?>();
                current[segment] = newDict;
                current = newDict;
            }
        }

        current[segments[^1]] = value;
    }
}
