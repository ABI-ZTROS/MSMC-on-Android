// -----------------------------------------------------------------------------
// 文件名: ConfigFormatDetector.cs
// 命名空间: io.NET.ZTR_OS.Features.ConfigEditor.Services
// 功能描述: 配置文件格式探测器，基于内容特征与扩展名双重判定配置文件类型
// 依赖组件: System.Text.Json, System.Linq, System.Text.RegularExpressions
// 设计模式: 策略模式、启发式检测、多因子判定
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ConfigEditor.Services;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// 配置文件格式枚举
/// </summary>
public enum ConfigFormat
{
    /// <summary>未知格式</summary>
    Unknown,
    /// <summary>Java Properties 格式</summary>
    Properties,
    /// <summary>YAML 格式</summary>
    Yaml,
    /// <summary>JSON 格式</summary>
    Json
}

/// <summary>
/// 基于内容特征的配置文件格式探测器
/// </summary>
/// <remarks>
/// <para>当文件扩展名无法可靠确定配置格式时（如 .conf、.cfg 等通用扩展名），
/// 通过分析文件内容的语法特征进行启发式判定。</para>
/// <para>检测优先级：内容特征 → 扩展名 → 逐解析器探测回退
/// 内容特征判定权重高于扩展名判定。</para>
/// </remarks>
public static class ConfigFormatDetector
{
    /// <summary>
    /// 通过分析内容语法特征检测配置格式
    /// </summary>
    /// <param name="content">配置文件原始内容</param>
    /// <returns>检测到的配置格式枚举值</returns>
    /// <remarks>
    /// 判定优先级：JSON → YAML → Properties → Unknown
    /// </remarks>
    public static ConfigFormat Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ConfigFormat.Unknown;

        var trimmed = TrimBomAndWhitespace(content);

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return ConfigFormat.Json;

        // 分别统计 YAML / Properties 命中行数，用数量比较而不是短路
        CountFeatures(content, out var yamlCount, out var propsCount);

        // 如果内容含 --- 文档分隔符 或 列表项前缀 -  或 缩进块层级 ≥ 2，判 YAML
        bool hasYamlStrongSignal = yamlCount > 0 &&
            (trimmed.Contains("---", StringComparison.Ordinal)
             || Regex.IsMatch(content, @"^\s*-\s", RegexOptions.Multiline)
             || HasNestedIndentation(content));

        if (hasYamlStrongSignal && propsCount * 2 < yamlCount)
            return ConfigFormat.Yaml;

        // Properties 至少 1 行，且不少于 YAML 计数的 50%
        if (propsCount >= 1 && propsCount >= yamlCount * 0.5)
            return ConfigFormat.Properties;

        // YAML 单独命中时也返回 YAML
        if (hasYamlStrongSignal)
            return ConfigFormat.Yaml;

        // 兜底：只要有任一方 ≥ 1 行就选计数多的那个
        if (yamlCount >= 1 || propsCount >= 1)
            return yamlCount > propsCount ? ConfigFormat.Yaml : ConfigFormat.Properties;

        return ConfigFormat.Unknown;
    }

    /// <summary>
    /// 去除 UTF-8 BOM 头与前导空白字符
    /// </summary>
    /// <param name="content">原始文本内容</param>
    /// <returns>清理后的文本</returns>
    private static string TrimBomAndWhitespace(string content)
    {
        if (content.Length == 0)
            return content;

        int start = 0;

        if (content[0] == '\uFEFF')
            start = 1;

        while (start < content.Length && char.IsWhiteSpace(content[start]))
            start++;

        return start == 0 ? content : content.Substring(start);
    }

    /// <summary>
    /// 同时统计 YAML 与 Properties 命中行数，避免多次 Split
    /// </summary>
    /// <remarks>
    /// 区分规则（Minecraft server.properties 实际只用 = 分隔，YAML 用 : 分隔）：
    ///   - 有缩进的键值行 → YAML（Properties 不使用缩进嵌套）
    ///   - 无缩进 + = 分隔符 → Properties
    ///   - 无缩进 + : 分隔符（colon-space 或行尾冒号）→ YAML mapping
    /// </remarks>
    private static void CountFeatures(string content, out int yamlCount, out int propsCount)
    {
        yamlCount = 0;
        propsCount = 0;
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith('#') || trimmed.StartsWith('!')) continue; // YAML/Properties 注释都跳过

            int leadingWs = line.Length - trimmed.Length;
            bool isIndented = leadingWs > 0;
            bool hasEquals = trimmed.Contains('=');
            bool hasColonSpace = trimmed.Contains(": ") || trimmed.EndsWith(':');

            // 有缩进的键值行 → YAML（Properties 不使用缩进嵌套）
            if (isIndented && (hasColonSpace || hasEquals))
            {
                yamlCount++;
                continue;
            }

            // 无缩进 + = 分隔符 → Properties（Minecraft server.properties 标准）
            if (!isIndented && hasEquals)
            {
                propsCount++;
                continue;
            }

            // 无缩进 + : 分隔符（colon-space 或行尾冒号）→ YAML mapping
            // （Properties 虽规范上允许 : 分隔，但实际 server.properties 一律用 =，
            //   把 : 归 YAML 更准确，避免 YAML 被误判为 Properties）
            if (!isIndented && hasColonSpace && !hasEquals)
            {
                yamlCount++;
                continue;
            }

            // YAML 列表项前缀
            if (trimmed.StartsWith("- "))
            {
                yamlCount++;
            }
        }
    }

    /// <summary>
    /// 快速判断内容是否包含缩进嵌套（至少 2 级空格缩进）
    /// </summary>
    private static bool HasNestedIndentation(string content)
    {
        int level1Count = 0;
        int level2Count = 0;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.TrimStart().StartsWith('#')) continue;

            int leading = line.Length - line.TrimStart().Length;
            if (leading >= 2) level1Count++;
            if (leading >= 4) level2Count++;
        }
        return level2Count >= 1 && level1Count >= 2;
    }

    /// <summary>
    /// 通过文件扩展名检测配置格式
    /// </summary>
    /// <param name="extension">文件扩展名（含前导点号）</param>
    /// <returns>检测到的配置格式枚举值</returns>
    public static ConfigFormat DetectByExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ConfigFormat.Unknown;

        var ext = extension.ToLowerInvariant();
        return ext switch
        {
            ".properties" => ConfigFormat.Properties,
            // .conf/.cfg/.ini 按 Properties 先试（实际 Resolve 会回退）
            ".conf" or ".cfg" or ".ini" => ConfigFormat.Properties,
            ".yml" or ".yaml" => ConfigFormat.Yaml,
            ".json" => ConfigFormat.Json,
            // .toml 暂时无解析器，仍标记为 Unknown 以触发回退
            ".toml" => ConfigFormat.Unknown,
            _ => ConfigFormat.Unknown
        };
    }

    /// <summary>
    /// 综合判定配置格式：内容特征优先 → 扩展名作为回退 → 逐解析器探测兜底
    /// </summary>
    /// <param name="content">配置文件内容</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>最终判定的配置格式</returns>
    public static ConfigFormat Resolve(string content, string extension)
    {
        var contentFormat = Detect(content);
        if (contentFormat != ConfigFormat.Unknown)
            return contentFormat;

        var extFormat = DetectByExtension(extension);
        if (extFormat != ConfigFormat.Unknown)
            return extFormat;

        // 三级回退：逐解析器试解析（仅格式判断，不做完整解析）
        if (TryProbeByParsing(content, out var probed))
            return probed;

        return ConfigFormat.Unknown;
    }

    /// <summary>
    /// 三级回退：用实际解析器做轻量探测，成功则返回对应格式
    /// </summary>
    private static bool TryProbeByParsing(string content, out ConfigFormat format)
    {
        format = ConfigFormat.Unknown;

        // 先试 JSON
        try
        {
            JsonDocument.Parse(content);
            format = ConfigFormat.Json;
            return true;
        }
        catch { /* 忽略 */ }

        // 再试 Properties（只要能成功 Parse 出 ≥ 1 条就算）
        try
        {
            var dict = PropertiesParser.Parse(content);
            if (dict.Count >= 1)
            {
                format = ConfigFormat.Properties;
                return true;
            }
        }
        catch { /* 忽略 */ }

        // 最后试 YAML
        try
        {
            var dict = YamlParser.Parse(content);
            if (dict.Count >= 1)
            {
                format = ConfigFormat.Yaml;
                return true;
            }
        }
        catch { /* 忽略 */ }

        return false;
    }
}
