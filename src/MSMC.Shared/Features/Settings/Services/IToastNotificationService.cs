// -----------------------------------------------------------------------------
// 文件名: IToastNotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: Toast 通知服务接口（跨平台抽象）—— 从 Windows 版 ToastNotificationService.cs 抽取，
//           抽掉 UWP 实现，仅保留契约。Windows 版由原实现承载，Linux 版由 notify-send 实现承载。
// 设计模式: 策略模式（DI 容器注入平台实现）
// -----------------------------------------------------------------------------
using System;

namespace io.NET.ZTR_OS.Features.Settings.Services;

/// <summary>
/// Toast 通知服务接口
/// 定义各类系统通知的发送与清理契约（平台无关）
/// </summary>
public interface IToastNotificationService
{
    /// <summary>
    /// 初始化通知服务
    /// </summary>
    void Initialize();

    /// <summary>
    /// 显示信息类通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="onActivated">通知激活回调（平台支持时生效）</param>
    void ShowInfo(string title, string message, Action<string>? onActivated = null);

    /// <summary>
    /// 显示成功类通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="onActivated">通知激活回调（平台支持时生效）</param>
    void ShowSuccess(string title, string message, Action<string>? onActivated = null);

    /// <summary>
    /// 显示警告类通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="onActivated">通知激活回调（平台支持时生效）</param>
    void ShowWarning(string title, string message, Action<string>? onActivated = null);

    /// <summary>
    /// 显示错误类通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知内容</param>
    /// <param name="onActivated">通知激活回调（平台支持时生效）</param>
    void ShowError(string title, string message, Action<string>? onActivated = null);

    /// <summary>
    /// 清除所有通知
    /// </summary>
    void ClearAll();
}