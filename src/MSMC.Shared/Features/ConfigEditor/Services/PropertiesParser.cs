// -----------------------------------------------------------------------------
// 文件名: PropertiesParser.cs
// 命名空间: io.NET.ZTR_OS.Features.ConfigEditor.Services
// 功能描述: Java Properties 格式解析器，实现 server.properties 的双向序列化
// 依赖组件: System.IO, System.Text, System.Collections.Generic, Serilog
// 设计模式: 解析器模式、静态工具类模式、无损行结构回写
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.ConfigEditor.Services;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Serilog;

#region 行结构模型（无损序列化用）

/// <summary>
/// Properties 文件中的一行（注释/空行/配置项）抽象标记接口
/// </summary>
public interface IPropertiesLine { }

/// <summary>
/// 注释行
/// </summary>
/// <param name="Text">原始注释文本（含 # 前缀）</param>
/// <param name="OriginalIndex">在原文件中的行号索引</param>
public sealed record PropertiesComment(string Text, int OriginalIndex) : IPropertiesLine;

/// <summary>
/// 空白行
/// </summary>
/// <param name="OriginalIndex">在原文件中的行号索引</param>
public sealed record PropertiesBlankLine(int OriginalIndex) : IPropertiesLine;

/// <summary>
/// 配置项行
/// </summary>
/// <param name="Key">键名</param>
/// <param name="Value">当前值（有效覆盖值，对重复键取最后一个）</param>
/// <param name="OriginalIndex">在原文件中的行号索引</param>
/// <param name="Separator">原文件中使用的分隔符（= 或 :）</param>
/// <param name="IsDuplicate">是否为重复键（同名键在后面又出现了一次，本行不是最后一个）</param>
/// <param name="LeadingWhitespace">行前导空白（原样保留缩进）</param>
public sealed record PropertiesEntry(
    string Key,
    string Value,
    int OriginalIndex,
    char Separator,
    bool IsDuplicate,
    string LeadingWhitespace = "") : IPropertiesLine;

/// <summary>
/// 原始 Properties 文档结构，用于无损序列化还原
/// </summary>
public sealed class PropertiesDocument
{
    /// <summary>按原始顺序保存的所有行</summary>
    public List<IPropertiesLine> Lines { get; } = [];

    /// <summary>键 → 最后一次出现的 Entry 引用（重复键语义：last-wins）</summary>
    public Dictionary<string, PropertiesEntry> LastEntries { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Effective key-value 视图（与 Parse 返回值等价）</summary>
    public Dictionary<string, string> EffectiveValues { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}

#endregion

/// <summary>
/// Minecraft server.properties 格式解析器
/// </summary>
/// <remarks>
/// <para>实现 Java Properties 文件格式的解析与无损序列化。server.properties 作为
/// Minecraft 服务器的核心配置文件，采用简单的 key=value 或 key: value 行式结构。</para>
/// <para>格式规则：
///   - 每行一个 key=value 或 key: value 配置项
///   - 以 # 或 ! 开头的行为注释行（解析时忽略，序列化保留）
///   - 空白行直接跳过（序列化保留）
///   - 键名大小写不敏感（解析输出保留原始大小写）
///   - 分隔符取首个未转义 = 或 : 号，值中允许包含 = / : 字符
///   - 重复键：last-wins，序列化时只修改最后一条 Entry 的值
/// </para>
/// </remarks>
public static class PropertiesParser
{
    /// <summary>
    /// 解析 server.properties 格式的文本内容（对外公开 API，保持契约不变）
    /// </summary>
    /// <param name="content">配置文件的原始文本内容</param>
    /// <returns>键值对字典，不包含注释行与空白行</returns>
    /// <exception cref="ArgumentNullException">当 content 为 null 时抛出</exception>
    public static Dictionary<string, string> Parse(string content)
    {
        var doc = ParseDocument(content);
        return doc.EffectiveValues;
    }

    /// <summary>
    /// 解析 server.properties 为完整的行结构文档（保留注释、空行、重复键信息）
    /// </summary>
    public static PropertiesDocument ParseDocument(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Log.Debug("PropertiesParser.ParseDocument: {Len} 字符", content.Length);

        var doc = new PropertiesDocument();
        // 记录所有出现过的键及其最后一次索引，用于后续标记 IsDuplicate
        var firstOccurrences = new Dictionary<string, PropertiesEntry>(StringComparer.OrdinalIgnoreCase);

        int lineIndex = 0;
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var original = line;        // 保留原始（含 /r 已被 ReadLine 去）
            var trimmed = original.Trim();
            lineIndex++;

            // 1) 空行
            if (trimmed.Length == 0)
            {
                doc.Lines.Add(new PropertiesBlankLine(lineIndex));
                continue;
            }

            // 2) 注释行（# 或 ! 开头，Java Properties 规范都认可）
            if (trimmed[0] == '#' || trimmed[0] == '!')
            {
                doc.Lines.Add(new PropertiesComment(original, lineIndex));
                continue;
            }

            // 3) 找 key 结束位置：第一个未被反斜杠转义的 = 或 :
            var leadingWs = original[..^original.TrimStart().Length];
            int splitIdx = FindSplitIndex(trimmed);
            if (splitIdx < 0)
            {
                // 坏数据：既无 = 也无 :。不再 throw，降级为注释保留，避免整文件挂
                Log.Debug("PropertiesParser: 跳过无法解析的行（缺少分隔符）Line {Idx}: {Line}",
                    lineIndex, trimmed);
                doc.Lines.Add(new PropertiesComment(
                    string.IsNullOrEmpty(trimmed) ? original : $"# BAD LINE: {original}",
                    lineIndex));
                continue;
            }

            char separator = trimmed[splitIdx];
            var key = trimmed[..splitIdx].Trim();
            var value = trimmed[(splitIdx + 1)..].Trim();

            if (key.Length == 0)
            {
                // 空键名，同样降级保留为注释
                Log.Debug("PropertiesParser: 跳过空键名行 Line {Idx}: {Line}", lineIndex, trimmed);
                doc.Lines.Add(new PropertiesComment($"# EMPTY KEY: {original}", lineIndex));
                continue;
            }

            // 4) 构造 Entry，处理重复键 last-wins
            var entry = new PropertiesEntry(
                Key: key,
                Value: value,
                OriginalIndex: lineIndex,
                Separator: separator,
                IsDuplicate: false,
                LeadingWhitespace: leadingWs);

            if (doc.EffectiveValues.ContainsKey(key))
            {
                // 重复键：标记先出现的那条为 IsDuplicate
                if (firstOccurrences.TryGetValue(key, out var first))
                {
                    var firstPos = doc.Lines.IndexOf(first);
                    if (firstPos >= 0)
                        doc.Lines[firstPos] = first with { IsDuplicate = true };
                    firstOccurrences.Remove(key);
                }
                doc.LastEntries[key] = entry; // 更新为最后一条
            }
            else
            {
                firstOccurrences[key] = entry;
                doc.LastEntries[key] = entry;
            }

            doc.EffectiveValues[key] = value;  // last-wins
            doc.Lines.Add(entry);
        }

        Log.Debug("PropertiesParser.ParseDocument 完成: {LineCount} 行, {EntryCount} 有效键",
            doc.Lines.Count, doc.EffectiveValues.Count);
        return doc;
    }

    /// <summary>
    /// 找 key 结束的分隔符位置：第一个未被反斜杠转义的 '=' 或 ':'。
    /// 优先第一个出现的分隔符（= 和 : 谁先出现用谁）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindSplitIndex(ReadOnlySpan<char> trimmed)
    {
        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            // 跳过被反斜杠转义的字符
            if (c == '\\' && i + 1 < trimmed.Length)
            {
                i++;
                continue;
            }
            if (c == '=' || c == ':')
                return i;
        }
        return -1;
    }

    /// <summary>
    /// 将键值对字典序列化为 server.properties 格式文本。
    /// 若提供 filePath 且磁盘文件存在，则按原文件结构无损回写（保留注释、行顺序、重复键）；
    /// 否则退化为按键排序的新文档。
    /// </summary>
    /// <param name="config">配置键值对字典</param>
    /// <param name="filePath">
    /// 可选的文件路径。若提供且磁盘文件存在，则按原结构无损回写；
    /// 未提供则退化为字母顺序输出（保持旧行为的兼容兜底）。
    /// </param>
    /// <returns>序列化后的 Properties 文本</returns>
    public static string Serialize(Dictionary<string, string> config, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        Log.Debug("PropertiesParser.Serialize: {Count} 键, File={Path}", config.Count, filePath);

        // 1) 尝试拿原文档结构：读磁盘当前文件
        PropertiesDocument? doc = null;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                // 极简：默认 FileShare，不管占用；读不到就走兜底字母顺序
                doc = ParseDocument(File.ReadAllText(filePath));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "PropertiesParser: 读取原文件重建结构失败，将退化为字母顺序输出。Path={Path}", filePath);
                doc = null;
            }
        }

        if (doc is null)
        {
            // 2) 无原始结构兜底：保持旧行为 —— 字母顺序输出（但不再破坏未知场景）
            return SerializeFresh(config);
        }

        // 3) 无损回写：按 doc.Lines 顺序输出，只改最后一条 Entry 的 Value
        var sb = new StringBuilder(capacity: config.Count * 40);
        var userKeysProcessed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in doc.Lines)
        {
            switch (line)
            {
                case PropertiesBlankLine:
                    sb.AppendLine();
                    break;

                case PropertiesComment c:
                    sb.AppendLine(c.Text);
                    break;

                case PropertiesEntry e:
                    if (!e.IsDuplicate && doc.LastEntries.TryGetValue(e.Key, out var last) && last == e)
                    {
                        // 最后一条：用用户修改后的值覆盖
                        if (config.TryGetValue(e.Key, out var newVal))
                        {
                            sb.Append(e.LeadingWhitespace);
                            sb.Append(e.Key);
                            sb.Append(e.Separator);
                            sb.Append(newVal);      // 值不 Trim，保留前后空格语义
                            sb.AppendLine();
                            userKeysProcessed.Add(e.Key);
                            break;
                        }
                    }
                    // 重复键的非最后一条 / 或用户未提供的键：原样输出
                    sb.Append(e.LeadingWhitespace);
                    sb.Append(e.Key);
                    sb.Append(e.Separator);
                    sb.Append(e.Value);
                    sb.AppendLine();
                    break;
            }
        }

        // 4) 用户新增键（原文件中不存在）：追加到末尾
        foreach (var kvp in config)
        {
            if (!userKeysProcessed.Contains(kvp.Key) && !doc.EffectiveValues.ContainsKey(kvp.Key))
            {
                sb.Append(kvp.Key);
                sb.Append('=');
                sb.Append(kvp.Value);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 无原始结构兜底的序列化：字母顺序输出（兼容旧行为）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SerializeFresh(Dictionary<string, string> config)
    {
        var sb = new StringBuilder(capacity: config.Count * 40);
        foreach (var kvp in config.OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(kvp.Key);
            sb.Append('=');
            sb.AppendLine(kvp.Value);
        }
        return sb.ToString();
    }

}

