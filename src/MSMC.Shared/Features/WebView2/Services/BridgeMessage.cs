// -----------------------------------------------------------------------------
// 文件名: BridgeMessage.cs
// 命名空间: io.NET.ZTR_OS.Features.WebView2.Services
// 功能描述: WebView2 桥接消息模型，定义 C# 与 JS 之间的通信协议
// 依赖组件: System.Text.Json
// 设计模式: 消息模式 + 请求/响应模式
// -----------------------------------------------------------------------------
using System.Text.Json.Serialization;

namespace io.NET.ZTR_OS.Features.WebView2.Services;

/// <summary>
/// 桥接消息类型枚举，定义所有支持的消息类别
/// </summary>
public enum BridgeMessageType
{
    /// <summary>请求消息（JS → C#，需要响应）</summary>
    Request,

    /// <summary>响应消息（C# → JS，对应请求）</summary>
    Response,

    /// <summary>事件推送（C# → JS，单向通知）</summary>
    Event,

    /// <summary>日志消息（双向，调试用）</summary>
    Log
}

/// <summary>
/// 桥接消息基类，定义通用消息结构
/// </summary>
public class BridgeMessage
{
    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("type")]
    public BridgeMessageType Type { get; set; }

    /// <summary>
    /// 消息唯一标识（请求/响应用于配对）
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// 消息动作/方法名
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// 消息负载数据
    /// </summary>
    [JsonPropertyName("payload")]
    public object? Payload { get; set; }

    /// <summary>
    /// 错误信息（响应失败时）
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>
    /// 是否成功（响应消息）
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    /// <summary>
    /// 消息时间戳（毫秒级 Unix 时间）
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// 泛型桥接消息，提供类型安全的负载访问
/// </summary>
/// <typeparam name="T">负载数据类型</typeparam>
public class BridgeMessage<T> : BridgeMessage
{
    /// <summary>
    /// 类型安全的负载数据
    /// </summary>
    [JsonPropertyName("payload")]
    public new T? Payload { get; set; }
}
