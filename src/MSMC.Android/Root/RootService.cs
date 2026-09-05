// -----------------------------------------------------------------------------
// 文件名: RootService.cs
// 命名空间: io.NET.ZTR_OS.Android.Root
// 功能描述: root 能力门面 —— 基于 libsu（MSMC.Libsu 绑定）封装 root 探测、
//           命令执行、root 文件读写；无 root 时所有操作安全降级。
// 设计模式: 门面模式 + 惰性初始化
// 说明: 强制 root 是本 App 的核心假设；RootService 是全部 root 能力的唯一入口。
// -----------------------------------------------------------------------------
using Serilog;

namespace io.NET.ZTR_OS.Android.Root;

/// <summary>
/// root 能力门面 —— 基于 libsu 封装
/// </summary>
public static class RootService
{
    private static readonly object Sync = new();
    private static bool? _granted;

    /// <summary>
    /// 是否已获得 root 授权（对 KernelSU/Magisk 等管理器而言指本 App 已被授予）
    /// </summary>
    public static bool IsGranted
    {
        get
        {
            if (_granted is not null) return _granted.Value;

            lock (Sync)
            {
                if (_granted is not null) return _granted.Value;
                try
                {
                    var root = global::Com.Topjohnwu.Superuser.Shell.IsAppGrantedRoot();
                    _granted = root is not null && root.BooleanValue();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[ROOT] 探测 root 授权异常，按未授权处理");
                    _granted = false;
                }
                Log.Information("[ROOT] 授权状态={Granted}", _granted);
                return _granted.Value;
            }
        }
    }

    /// <summary>
    /// 刷新授权状态缓存（用户在授权页授权后调用）
    /// </summary>
    public static void Refresh()
    {
        lock (Sync)
        {
            _granted = null;
            _ = IsGranted;
        }
    }

    /// <summary>
    /// 执行一条 root 命令并返回结果（同步阻塞）
    /// </summary>
    /// <param name="command">shell 命令（多行以 \n 分隔）</param>
    /// <returns>命令输出（合并 stdout）</returns>
    /// <exception cref="InvalidOperationException">未授权或无 shell 时抛出</exception>
    public static string Exec(string command)
    {
        if (!IsGranted)
        {
            throw new InvalidOperationException("未获得 root 授权，无法执行命令");
        }

        var result = global::Com.Topjohnwu.Superuser.Shell.Cmd(command).Exec();
        if (result.Out is not null)
        {
            return string.Join("\n", result.Out);
        }
        return string.Empty;
    }

    /// <summary>
    /// 执行一条 root 命令，返回 (stdout, stderr, exitCode)
    /// </summary>
    public static (string Stdout, string Stderr, int Code) ExecWithCode(string command)
    {
        if (!IsGranted)
        {
            return (string.Empty, "root 未授权", -1);
        }

        var result = global::Com.Topjohnwu.Superuser.Shell.Cmd(command).Exec();
        return (
            string.Join("\n", result.Out ?? []),
            string.Join("\n", result.Err ?? []),
            result.Code);
    }

    /// <summary>
    /// 异步执行 root 命令（提交到 libsu 后台执行器，不阻塞调用线程）
    /// </summary>
    public static void ExecAsync(string command)
    {
        if (!IsGranted) return;
        global::Com.Topjohnwu.Superuser.Shell.Cmd(command).Submit(null);
    }

    /// <summary>
    /// 读取一个需要 root 的文件内容（cat 兜底）
    /// </summary>
    public static string ReadFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }
        catch (Exception)
        {
            // 降级走 root
        }

        try
        {
            return Exec($"cat '{path}' 2>/dev/null");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[ROOT] 读取文件失败 Path={Path}", path);
            return string.Empty;
        }
    }

    /// <summary>
    /// 写入一个需要 root 的文件内容
    /// </summary>
    public static bool WriteFile(string path, string content)
    {
        var escaped = content.Replace("'", "'\\''");
        try
        {
            var (_, _, code) = ExecWithCode($"echo '{escaped}' > '{path}'");
            return code == 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[ROOT] 写入文件失败 Path={Path}", path);
            return false;
        }
    }

    /// <summary>
    /// 执行 root 文件操作（chmod/chown/mkdir/mv/rm 等）
    /// </summary>
    public static bool FileOp(string operation, string args)
    {
        try
        {
            var (_, _, code) = ExecWithCode($"{operation} {args}");
            return code == 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[ROOT] 文件操作失败 Op={Op} Args={Args}", operation, args);
            return false;
        }
    }

    /// <summary>检查某路径是否存在（root 视角）</summary>
    public static bool Exists(string path)
    {
        try
        {
            if (File.Exists(path) || Directory.Exists(path)) return true;
            var (outStr, _, code) = ExecWithCode($"[ -e '{path}' ] && echo yes || echo no");
            return code == 0 && outStr.Contains("yes", StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[ROOT] 路径检查失败 Path={Path}", path);
            return false;
        }
    }

    /// <summary>申请 root 授权（触发 KernelSU Manager 弹窗）</summary>
    public static void Request()
    {
        try
        {
            global::Com.Topjohnwu.Superuser.Shell.GetShell(null);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[ROOT] 申请授权失败");
        }
    }
}