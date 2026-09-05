// -----------------------------------------------------------------------------
// 文件名: AndroidSupervisor.cs
// 命名空间: io.NET.ZTR_OS.Android.Supervision
// 功能描述: Android 服务器监管器 —— 多开 Minecraft Java 进程：每实例独立
//           工作目录 / 内存 / CPU 亲和性 / 启动参数；setsid 进程组启动防杀；
//           日志落盘；退出监测 + 可选崩溃自动重启（默认关）；JDK 自动识别+手动覆盖。
// 设计模式: 单例 + 进程注册表 + 职责链（启动 → 保活 → 停止 → 崩溃处理）
// -----------------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using io.NET.ZTR_OS.Android.Runtime;
using Serilog;

namespace io.NET.ZTR_OS.Android.Supervision;

/// <summary>
/// Android 服务器监管器（多开）
/// </summary>
public sealed class AndroidSupervisor : IDisposable
{
    /// <summary>运行中的服务器注册表（key: 工作目录）</summary>
    private readonly ConcurrentDictionary<string, ManagedAndroidServer> _registry = new();

    /// <summary>崩溃重启配置（默认关）</summary>
    public CrashPolicy Policy { get; set; } = new();

    private readonly TermuxRuntime _termux;
    private readonly JavaRuntimeManager _javaManager;

    public AndroidSupervisor(TermuxRuntime termux, JavaRuntimeManager javaManager)
    {
        _termux = termux;
        _javaManager = javaManager;
    }

    /// <summary>启动一个 Minecraft 服务器进程（root + Termux 环境）</summary>
    /// <param name="workDirectory">服务器工作目录（含 server.jar / eula.txt）</param>
    /// <param name="javaPath">java 可执行路径；空则按 MC 版本自动识别</param>
    /// <param name="launchArgs">启动参数（-Xmx -jar server.jar nogui 等）</param>
    /// <param name="mcVersion">MC 版本（用于 JDK 自动识别）</param>
    /// <param name="affinityMask">CPU 亲和掩码（0=不设置）</param>
    public async Task<ServerLaunchResult> StartAsync(string workDirectory, string javaPath,
        string launchArgs, string? mcVersion = null, long affinityMask = 0)
    {
        var fullWorkDir = Path.GetFullPath(workDirectory);
        if (!Directory.Exists(fullWorkDir))
        {
            return ServerLaunchResult.Fail($"服务器目录不存在: {fullWorkDir}");
        }

        if (_registry.TryGetValue(fullWorkDir, out var existing) && existing.IsRunning)
        {
            return ServerLaunchResult.Ok(existing.ProcessId, "服务器已在运行");
        }

        // JDK 自动识别 + 兜底
        if (string.IsNullOrEmpty(javaPath))
        {
            var major = JavaRuntimeManager.MapMinecraftVersion(mcVersion ?? "1.20.1");
            var path = await _javaManager.EnsureAsync(major);
            javaPath = path ?? string.Empty;
        }

        if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
        {
            return ServerLaunchResult.Fail($"Java 运行时不可用: {javaPath}");
        }

        // eula 自动同意（设计：MSMC 代玩家同意，可后续做开关）
        EnsureEula(fullWorkDir);

        // setsid 启动：进程组独立，避免随 App 进程组被杀
        var cmd = BuildLaunchCommand(fullWorkDir, javaPath, launchArgs, affinityMask);
        Log.Information("[SUP] 启动命令 Dir={Dir} Cmd={Cmd}", fullWorkDir, cmd);

        var (outStr, errStr, code) = await Task.Run(() => RunRootCapture(cmd));
        if (code != 0 && !outStr.Contains("Done", StringComparison.Ordinal))
        {
            return ServerLaunchResult.Fail($"启动失败（exit={code}）: {Tail(errStr + "\n" + outStr, 200)}");
        }

        // 找到新起的 java 进程（setsid 起的是启动脚本，真正 java 是其子进程，取 cmdline 匹配）
        var pid = await DetectJavaPidAsync(fullWorkDir, javaPath);
        if (pid <= 0)
        {
            // 可能异步未就绪，先用 shell 查询
            var pids = _termux.Exec($"pgrep -f '{javaPath}' | head -1").Trim();
            int.TryParse(pids, out pid);
        }

        var managed = new ManagedAndroidServer
        {
            ProcessId = pid,
            WorkDirectory = fullWorkDir,
            JavaPath = javaPath,
            LaunchArgs = launchArgs,
            LogFile = Path.Combine(GetLogsDir(fullWorkDir), $"server-{DateTime.Now:yyyyMMdd-HHmmss}.log"),
            StartedAt = DateTimeOffset.UtcNow,
            MonitorToken = new CancellationTokenSource(),
        };
        _registry[fullWorkDir] = managed;

        // 后台：日志搬运 + 退出监测
        _ = Task.Run(() => MonitorAndRelayAsync(managed));

        return ServerLaunchResult.Ok(pid, $"服务器已启动（PID {pid}）");
    }

    /// <summary>构造 setsid 启动命令（env -i 注入 Termux 环境，nohup 防挂断）</summary>
    private string BuildLaunchCommand(string dir, string javaPath, string args, long affinityMask)
    {
        var env = string.Join(' ', _termux.EnvVars().Select(kv => $"{kv.Key}={kv.Value}"));
        var cd = $"cd '{dir}'";
        var affinity = affinityMask > 0
            ? $"taskset -p {affinityMask} $$ >/dev/null 2>&1; "
            : string.Empty;
        var cmd = $"{env} sh -c '{affinity}exec setsid nohup \"{javaPath}\" {args} > '{GetLogsDir(dir)}/server-{DateTime.Now:yyyyMMdd-HHmmss}.log' 2>&1 &'";
        return cmd;
    }

    private static string GetLogsDir(string dir)
    {
        var logDir = Path.Combine(dir, "logs", "msmc");
        try { Directory.CreateDirectory(logDir); } catch (IOException) { }
        return logDir;
    }

    private void EnsureEula(string dir)
    {
        var eula = Path.Combine(dir, "eula.txt");
        try
        {
            if (!File.Exists(eula))
            {
                File.WriteAllText(eula, "eula=true\n");
            }
            else if (File.ReadAllText(eula).Contains("eula=false", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(eula, "eula=true\n");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[SUP] eula 处理失败 Dir={Dir}", dir);
        }
    }

    /// <summary>检测服务器目录下新起的 java 进程 PID（匹配 cmdline 含 dir + javaPath）</summary>
    private async Task<int> DetectJavaPidAsync(string dir, string javaPath)
    {
        for (var i = 0; i < 20; i++)
        {
            try
            {
                var outStr = _termux.Exec(
                    $"pgrep -f '({javaPath}|{Path.GetFileName(javaPath)})' | while read p; do "
                    + $"grep -q '{dir}' /proc/$p/cmdline 2>/dev/null && echo $p && break; done");
                var pid = outStr.Trim();
                if (int.TryParse(pid, out var p) && p > 1)
                {
                    return p;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SUP] 检测 PID 失败");
            }
            await Task.Delay(500);
        }
        return 0;
    }

    /// <summary>停止服务器（向 stdin 发 stop 优雅退出 → 超时强杀进程组）</summary>
    public async Task<ServerLaunchResult> StopAsync(int? pid = null, string? workDirectory = null)
    {
        ManagedAndroidServer? target = null;
        if (pid is not null)
        {
            target = _registry.Values.FirstOrDefault(s => s.ProcessId == pid);
        }
        else if (!string.IsNullOrEmpty(workDirectory))
        {
            _registry.TryGetValue(Path.GetFullPath(workDirectory), out target);
        }

        if (target is null)
        {
            return ServerLaunchResult.Fail("未找到运行中的服务器进程");
        }

        try
        {
            // 优雅：向 java stdin 发 stop（Termux 下通过 /proc/[pid]/fd/0 写）
            if (target.ProcessId > 0)
            {
                _ = _termux.Exec($"printf 'stop\\n' > /proc/{target.ProcessId}/fd/0 2>/dev/null || true");
                await Task.Delay(3000);
            }

            // 强杀进程组（setsid 后为独立进程组，kill -- -PGID）
            if (target.ProcessId > 0)
            {
                var pgid = _termux.Exec($"ps -o pgid= -p {target.ProcessId} 2>/dev/null").Trim();
                if (int.TryParse(pgid, out var g) && g > 1)
                {
                    _ = _termux.Exec($"kill -9 -- -{g} 2>/dev/null || kill -9 {target.ProcessId}");
                }
                else
                {
                    _ = _termux.Exec($"kill -9 {target.ProcessId} 2>/dev/null || true");
                }
            }

            _registry.TryRemove(target.WorkDirectory, out _);
            target.StopMonitor();
            Log.Information("[SUP] 服务器已停止 PID={Pid}", target.ProcessId);
            return ServerLaunchResult.Ok(target.ProcessId, "服务器已停止");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SUP] 停止失败 PID={Pid}", target.ProcessId);
            return ServerLaunchResult.Fail($"停止异常: {ex.Message}");
        }
    }

    /// <summary>后台：日志搬运 + 退出监测 + 崩溃重启</summary>
    private async Task MonitorAndRelayAsync(ManagedAndroidServer server)
    {
        // 轮询 /proc 判断 java 是否存活（Android 下进程树较脆弱，用 pgrep 判活）
        var crashCount = 0;
        try
        {
            while (!server.MonitorToken.IsCancellationRequested)
            {
                await Task.Delay(2000);

                bool alive;
                try
                {
                    var outStr = _termux.Exec($"kill -0 {server.ProcessId} 2>/dev/null && echo alive || echo dead");
                    alive = outStr.Trim() == "alive";
                }
                catch (Exception)
                {
                    alive = false;
                }

                if (!alive)
                {
                    // 拉日志尾部判定是否为"正常停止"
                    var logTail = ReadLogTail(server.LogFile, 200);
                    var normalExit = logTail.Contains("Stopping server", StringComparison.OrdinalIgnoreCase)
                        || logTail.Contains("Closing listeners", StringComparison.OrdinalIgnoreCase);

                    if (!normalExit && Policy.EnableCrashRestart && crashCount < Policy.MaxRestartAttempts)
                    {
                        crashCount++;
                        Log.Warning("[SUP] 服务器异常退出，第 {N} 次重启 Dir={Dir}", crashCount, server.WorkDirectory);
                        _ = StartAsync(server.WorkDirectory, server.JavaPath, server.LaunchArgs,
                            server.McVersion, server.AffinityMask);
                        continue;
                    }

                    _registry.TryRemove(server.WorkDirectory, out _);
                    Log.Information("[SUP] 服务器已退出 PID={Pid} Crash={Crash}", server.ProcessId, !normalExit);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SUP] 监测循环异常 Dir={Dir}", server.WorkDirectory);
        }
    }

    private static string ReadLogTail(string logFile, int maxChars)
    {
        try
        {
            if (!File.Exists(logFile)) return string.Empty;
            using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(Math.Max(0, fs.Length - maxChars), SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>获取全部被监管服务器状态</summary>
    public IReadOnlyList<ManagedAndroidServer> GetAll()
    {
        var list = new List<ManagedAndroidServer>();
        foreach (var kv in _registry)
        {
            if (kv.Value.IsRunning) list.Add(kv.Value);
            else _registry.TryRemove(kv.Key, out _);
        }
        return list;
    }

    private (string Out, string Err, int Code) RunRootCapture(string cmd)
    {
        try
        {
            return Root.RootService.ExecWithCode(cmd);
        }
        catch (Exception ex)
        {
            return (string.Empty, ex.Message, -1);
        }
    }

    private static string Tail(string s, int n) => s.Length <= n ? s : s[^n..];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var kv in _registry)
        {
            kv.Value.StopMonitor();
            if (kv.Value.ProcessId > 0)
            {
                try { _termux.Exec($"kill -9 {kv.Value.ProcessId} 2>/dev/null || true"); }
                catch (Exception) { }
            }
        }
        _registry.Clear();
    }
}

/// <summary>启动/停止操作结果</summary>
public sealed class ServerLaunchResult
{
    public bool Success { get; set; }
    public int? ProcessId { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ServerLaunchResult Ok(int pid, string message) => new()
    {
        Success = true,
        ProcessId = pid,
        Message = message,
    };

    public static ServerLaunchResult Fail(string message) => new()
    {
        Success = false,
        Message = message,
    };
}

/// <summary>崩溃重启策略（默认关）</summary>
public sealed class CrashPolicy
{
    public bool EnableCrashRestart { get; set; }
    public int MaxRestartAttempts { get; set; } = 3;
    public int RestartCooldownSeconds { get; set; } = 10;
}

/// <summary>被监管的服务器进程</summary>
public sealed class ManagedAndroidServer
{
    public int ProcessId { get; set; }
    public string WorkDirectory { get; set; } = string.Empty;
    public string JavaPath { get; set; } = string.Empty;
    public string LaunchArgs { get; set; } = string.Empty;
    public string? McVersion { get; set; }
    public long AffinityMask { get; set; }
    public string LogFile { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public int CrashCount { get; set; }

    internal CancellationTokenSource MonitorToken { get; set; } = new();
    internal void StopMonitor()
    {
        try { MonitorToken.Cancel(); } catch (ObjectDisposedException) { }
        MonitorToken.Dispose();
    }

    public bool IsRunning
    {
        get
        {
            try
            {
                var outStr = Root.RootService.ExecWithCode($"kill -0 {ProcessId} 2>/dev/null && echo alive || echo dead");
                return outStr.Stdout.Trim() == "alive";
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public string Status => IsRunning ? "Running" : "Stopped";
}