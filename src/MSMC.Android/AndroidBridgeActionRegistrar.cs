// -----------------------------------------------------------------------------
// 文件名: AndroidBridgeActionRegistrar.cs
// 命名空间: io.NET.ZTR_OS.Android
// 功能描述: Android 版桥接 action 注册中心 —— 把 Android 系统服务与 Shared 服务
//           注册为前端可调用的 JS→C# 动作（请求/响应模式），对齐 Linux 版契约。
//           额外提供 Android 专属 action：root 状态 / Termux/JDK 状态 / 开服自动开浏览器。
// -----------------------------------------------------------------------------
using System.Runtime.InteropServices;
using System.Text.Json;
using io.NET.ZTR_OS.Android.Monitoring;
using io.NET.ZTR_OS.Android.Notifications;
using io.NET.ZTR_OS.Android.Root;
using io.NET.ZTR_OS.Android.Runtime;
using io.NET.ZTR_OS.Android.Supervision;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using io.NET.ZTR_OS.Features.ContentMarket.Services;
using io.NET.ZTR_OS.Features.NetworkMonitor.Models;
using io.NET.ZTR_OS.Features.NetworkMonitor.Services;
using io.NET.ZTR_OS.Features.Notifications.Models;
using io.NET.ZTR_OS.Features.Notifications.Services;
using io.NET.ZTR_OS.Features.Scheduler.Models;
using io.NET.ZTR_OS.Features.Scheduler.Services;
using io.NET.ZTR_OS.Features.SystemMonitoring.Services;
using io.NET.ZTR_OS.Features.WebPanel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// Android 版桥接 action 注册中心
/// </summary>
public static class AndroidBridgeActionRegistrar
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>注册全部核心 action</summary>
    public static void RegisterAll(WebPanel panel, IServiceProvider sp, ILogger logger)
    {
        var monitor = sp.GetRequiredService<AndroidSystemMonitor>();
        var scanner = sp.GetRequiredService<AndroidProcessScanner>();
        var portMapper = sp.GetRequiredService<AndroidPortMapper>();
        var javaManager = sp.GetRequiredService<JavaRuntimeManager>();
        var termux = sp.GetRequiredService<TermuxRuntime>();
        var supervisor = sp.GetRequiredService<AndroidSupervisor>();
        var power = sp.GetRequiredService<AndroidPowerManager>();
        var network = sp.GetRequiredService<AndroidNetworkManager>();
        var metrics = sp.GetRequiredService<IMetricsPersistenceService>();
        var toast = sp.GetRequiredService<AndroidToastService>();

        // ════════════ 基础/app ════════════
        Register(panel, "ping", _ => Task.FromResult<object?>(new { pong = true, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), message = "pong" }));
        Register(panel, "app:getTime", _ => Task.FromResult<object?>(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        Register(panel, "app:getInfo", _ => Task.FromResult<object?>(new
        {
            appName = "MSMC",
            appVersion = "1.0.0-android",
            os = Environment.OSVersion.ToString(),
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            machineName = "android",
            user = "root",
            platform = "Android",
        }));
        Register(panel, "app:getReadyState", _ => Task.FromResult<object?>(new { ready = true, platform = "android", isAdmin = RootService.IsGranted }));
        Register(panel, "app:refreshAdminStatus", _ => Task.FromResult<object?>(new { success = true, isAdmin = RootService.IsGranted }));

        // ════════════ root / 运行时状态（Android 专属） ════════════
        Register(panel, "android:getRootStatus", _ => Task.FromResult<object?>(new
        {
            granted = RootService.IsGranted,
            rootManager = RootService.IsGranted ? "KernelSU / Magisk" : "未授权",
        }));
        Register(panel, "android:requestRoot", _ =>
        {
            RootService.Request();
            return Task.FromResult<object?>(new { success = true, message = "请在系统弹窗中允许 root 授权" });
        });
        Register(panel, "android:getRuntimeStatus", _ =>
        {
            var javas = javaManager.ScanInstalled();
            return Task.FromResult<object?>(new
            {
                termuxInstalled = termux.IsInstalled,
                termuxRoot = termux.RootDir,
                javas = javas.Select(j => new { major = j.Major, path = j.JavaPath, source = j.Source }).ToList(),
            });
        });
        Register(panel, "android:ensureRuntime", async payload =>
        {
            var req = ParseJson<EnsureRuntimeRequest>(payload);
            void Progress(string m) => panel.PublishEvent("runtime:progress", new { message = m });
            var termuxOk = await termux.EnsureInstalledAsync(Progress);
            if (!termuxOk)
            {
                return new { success = false, error = "Termux 环境部署失败" };
            }

            var major = req?.JdkMajor ?? 21;
            var javaPath = await javaManager.EnsureAsync(major, Progress);
            return new { success = javaPath is not null, javaPath, error = javaPath is null ? $"JDK {major} 安装失败" : null };
        });

        // ════════════ 系统监控 ════════════
        Register(panel, "systemMonitor:getMetrics", _ =>
        {
            var m = monitor.CollectSystemMetrics();
            return Task.FromResult<object?>(new
            {
                cpuUsagePercent = m.CpuUsagePercent,
                perCoreCpuUsages = m.PerCoreCpuUsages,
                totalMemoryBytes = m.TotalMemoryBytes,
                usedMemoryBytes = m.UsedMemoryBytes,
                memoryUsagePercent = m.MemoryUsagePercent,
                totalThreadCount = m.TotalThreadCount,
                diskTotalBytes = m.DiskTotalBytes,
                diskUsedBytes = m.DiskUsedBytes,
                diskFreeBytes = m.DiskFreeBytes,
                diskUsagePercent = m.DiskUsagePercent,
                diskName = m.DiskName,
                javaCpuUsagePercent = m.JavaCpuUsagePercent,
                javaWorkingSetBytes = m.JavaWorkingSetBytes,
                javaThreadCount = m.JavaThreadCount,
                timestamp = m.Timestamp,
            });
        });
        Register(panel, "systemMonitor:getCpuInfo", _ => Task.FromResult<object?>(power.GetCpuInfo()));
        Register(panel, "systemMonitor:getHistory", _ =>
        {
            var points = metrics.LoadRecentDays(1);
            return Task.FromResult<object?>(points);
        });
        Register(panel, "systemMonitor:getHistoryRange", payload =>
        {
            var days = 7;
            if (!string.IsNullOrEmpty(payload))
            {
                try
                {
                    using var doc = JsonDocument.Parse(payload);
                    if (doc.RootElement.TryGetProperty("days", out var prop)) days = prop.GetInt32();
                }
                catch (JsonException) { }
            }
            var all = metrics.LoadRecentDays(Math.Clamp(days, 1, 90));
            return Task.FromResult<object?>(new { points = all });
        });

        // ════════════ 服务器检测 ════════════
        Register(panel, "server:list", _ =>
        {
            var servers = scanner.FindJavaProcesses();
            return Task.FromResult<object?>(new { servers = servers.Select(ToServerDto).ToList() });
        });
        Register(panel, "server:getSelected", _ => Task.FromResult<object?>(null));
        Register(panel, "server:select", _ => Task.FromResult<object?>(new { success = true }));
        Register(panel, "server:start", async payload =>
        {
            var req = ParseJson<StartServerRequest>(payload);
            if (req is null || string.IsNullOrEmpty(req.Directory))
            {
                return new { success = false, error = "缺少服务器目录" };
            }

            var args = string.IsNullOrEmpty(req.LaunchArgs)
                ? "-Xmx1024M -jar server.jar nogui"
                : req.LaunchArgs;

            var result = await supervisor.StartAsync(req.Directory, req.JavaPath ?? string.Empty, args,
                req.McVersion, req.AffinityMask ?? 0);

            if (result.Success)
            {
                toast.ShowSuccess("MSMC", $"服务器已启动（PID {result.ProcessId}）");
            }
            else
            {
                toast.ShowError("MSMC", result.Message);
            }

            return new { success = result.Success, pid = result.ProcessId, message = result.Message, error = result.Success ? null : result.Message };
        });
        Register(panel, "server:stop", async payload =>
        {
            var req = ParseJson<StartServerRequest>(payload);
            var result = await supervisor.StopAsync(workDirectory: req?.Directory);
            return new { success = result.Success, message = result.Message, error = result.Success ? null : result.Message };
        });
        Register(panel, "server:getStatus", _ =>
        {
            var servers = supervisor.GetAll();
            return Task.FromResult<object?>(new
            {
                running = servers.Count > 0,
                count = servers.Count,
                servers = servers.Select(s => new { pid = s.ProcessId, workDirectory = s.WorkDirectory, status = s.Status, logFile = s.LogFile }),
            });
        });

        // ════════════ 网络/端口 ════════════
        Register(panel, "network:getPorts", _ =>
        {
            var ports = portMapper.GetListeningPorts();
            return Task.FromResult<object?>(new { ports });
        });
        Register(panel, "network:getCommonPorts", _ =>
        {
            var common = new List<CommonPort>
            {
                new() { Port = 25565, Name = "Minecraft", Description = "Minecraft Java 版默认端口", Category = "游戏" },
                new() { Port = 25566, Name = "Minecraft (Alt)", Description = "Minecraft 备用端口", Category = "游戏" },
                new() { Port = 19132, Name = "Bedrock", Description = "Minecraft Bedrock 版默认端口", Category = "游戏" },
                new() { Port = 8080, Name = "HTTP-Alt", Description = "常用 Web 服务端口", Category = "Web" },
                new() { Port = 443, Name = "HTTPS", Description = "HTTPS 加密 Web 端口", Category = "Web" },
                new() { Port = 22, Name = "SSH", Description = "SSH 远程登录端口", Category = "运维" },
            };
            return Task.FromResult<object?>(new { ports = common });
        });
        Register(panel, "network:addBridge", payload =>
        {
            var req = ParseJson<AddBridgeRequest>(payload);
            if (req is null || req.ListenPort <= 0)
            {
                return Task.FromResult<object?>(new { success = false, error = "无效的桥接参数" });
            }
            var result = network.AddPortForward(req.ListenPort, req.TargetAddress ?? "127.0.0.1", req.TargetPort ?? req.ListenPort);
            return Task.FromResult<object?>(new { success = result.Success, error = result.Success ? null : result.Error });
        });
        Register(panel, "network:removeBridge", payload =>
        {
            var req = ParseJson<RemoveBridgeRequest>(payload);
            if (req is null)
            {
                return Task.FromResult<object?>(new { success = false, error = "参数无效" });
            }
            var result = network.RemovePortForward(req.ListenPort);
            return Task.FromResult<object?>(new { success = result.Success, error = result.Success ? null : result.Error });
        });

        // ════════════ 性能（Android 专属） ════════════
        Register(panel, "power:setAffinity", payload =>
        {
            var req = ParseJson<SetAffinityRequest>(payload);
            if (req is null || req.Pid <= 0)
            {
                return Task.FromResult<object?>(new { success = false, error = "参数无效" });
            }
            var result = power.SetAffinity(req.Pid, req.AffinityMask);
            return Task.FromResult<object?>(new { success = result.Success, error = result.Success ? null : result.Error });
        });
        Register(panel, "power:setPriority", payload =>
        {
            var req = ParseJson<SetPriorityRequest>(payload);
            if (req is null || req.Pid <= 0)
            {
                return Task.FromResult<object?>(new { success = false, error = "参数无效" });
            }
            var result = power.SetPriority(req.Pid, req.Nice);
            return Task.FromResult<object?>(new { success = result.Success, error = result.Success ? null : result.Error });
        });
        Register(panel, "power:setOomProtection", payload =>
        {
            var req = ParseJson<SetOomRequest>(payload);
            if (req is null || req.Pid <= 0)
            {
                return Task.FromResult<object?>(new { success = false, error = "参数无效" });
            }
            var result = power.SetOomProtection(req.Pid, req.Score);
            return Task.FromResult<object?>(new { success = result.Success, error = result.Success ? null : result.Error });
        });

        // ════════════ 配置编辑（基于文件解析） ════════════
        Register(panel, "config:getAvailableServers", _ =>
        {
            var servers = scanner.FindJavaProcesses();
            return Task.FromResult<object?>(new { servers = servers.Select(ToServerDto).ToList() });
        });
        Register(panel, "config:getFileTree", payload =>
        {
            var req = ParseJson<ConfigContext>(payload);
            if (req is null || string.IsNullOrEmpty(req.ServerPath) || !Directory.Exists(req.ServerPath))
            {
                return Task.FromResult<object?>(new { success = false, error = "服务器目录无效" });
            }
            return Task.FromResult<object?>(new { success = true, tree = BuildConfigTree(req.ServerPath) });
        });
        Register(panel, "config:getCoreIndex", _ =>
        {
            var cores = Enumerable.Range(0, Environment.ProcessorCount)
                .Select(i => new { index = i, name = $"核心 {i + 1}", type = "cpu" });
            return Task.FromResult<object?>(new { cores });
        });

        // ════════════ 设置/Java ════════════
        Register(panel, "settings:getJavaList", _ =>
        {
            var javas = javaManager.ScanInstalled();
            return Task.FromResult<object?>(new
            {
                javaList = javas.Select(j => new { path = j.JavaPath, version = j.Version, major = j.Major, isDefault = j.JavaPath == javaManager.DefaultJavaPath }),
            });
        });
        Register(panel, "settings:rescanJava", _ =>
        {
            var javas = javaManager.ScanInstalled();
            return Task.FromResult<object?>(new { success = true, count = javas.Count });
        });
        Register(panel, "settings:get", _ => Task.FromResult<object?>(new { settings = new { } }));
        Register(panel, "settings:save", _ => Task.FromResult<object?>(new { success = true }));

        // ════════════ 通知模块（复用 Shared + Android Toast） ════════════
        Register(panel, "notify.dispatch", async payload =>
        {
            var notifService = sp.GetRequiredService<INotificationService>();
            var evt = ParseJson<NotificationEvent>(payload) ?? new NotificationEvent
            {
                EventType = NotificationEventType.ManualTest,
                Title = "通知",
                Message = payload ?? string.Empty,
            };
            var result = await notifService.DispatchAsync(evt);
            toast.ShowInfo(evt.Title, evt.Message);
            return result;
        });
        Register(panel, "notify.test", async payload =>
        {
            var notifService = sp.GetRequiredService<INotificationService>();
            var evt = new NotificationEvent
            {
                EventType = NotificationEventType.ManualTest,
                Title = "手动测试通知",
                Message = payload is string s && !string.IsNullOrEmpty(s) ? s : "这是一条测试通知",
            };
            var result = await notifService.DispatchAsync(evt);
            toast.ShowInfo(evt.Title, evt.Message);
            return result;
        });

        // ════════════ 调度模块（复用 Shared） ════════════
        Register(panel, "scheduler.list", _ =>
        {
            var svc = sp.GetRequiredService<ISchedulerService>();
            return Task.FromResult<object?>(svc.GetAllTasks());
        });
        Register(panel, "scheduler.add", payload =>
        {
            var svc = sp.GetRequiredService<ISchedulerService>();
            var task = ParseJson<ScheduledTask>(payload);
            if (task is null) return Task.FromResult<object?>(new { success = false, error = "任务参数无效" });
            svc.AddTask(task);
            return Task.FromResult<object?>(new { success = true, id = task.Id });
        });
        Register(panel, "scheduler.delete", payload =>
        {
            var svc = sp.GetRequiredService<ISchedulerService>();
            if (Guid.TryParse(payload?.Trim('"'), out var id))
            {
                return Task.FromResult<object?>(new { success = svc.DeleteTask(id) });
            }
            return Task.FromResult<object?>(new { success = false, error = "无效的任务 ID" });
        });
        Register(panel, "scheduler.runNow", async payload =>
        {
            var svc = sp.GetRequiredService<ISchedulerService>();
            if (Guid.TryParse(payload?.Trim('"'), out var id))
            {
                return new { success = await svc.RunNowAsync(id) };
            }
            return new { success = false, error = "无效的任务 ID" };
        });
        Register(panel, "scheduler.history", payload =>
        {
            var svc = sp.GetRequiredService<ISchedulerService>();
            var max = 50;
            if (int.TryParse(payload?.Trim('"'), out var m)) max = m;
            return Task.FromResult<object?>(svc.GetExecutionHistory(max));
        });

        // ════════════ 市场模块（复用 Shared 多源聚合） ════════════
        Register(panel, "market.search", async payload =>
        {
            var factory = sp.GetRequiredService<MarketProviderFactory>();
            var req = ParseJson<MarketSearchRequest>(payload);
            var query = req?.Query ?? payload?.Trim('"') ?? string.Empty;
            if (string.IsNullOrEmpty(query)) return new List<MarketProject>();
            return (object?)await factory.SearchAsync(new SearchRequest { Query = query, Limit = 20 });
        });
        Register(panel, "market.versions", async payload =>
        {
            var factory = sp.GetRequiredService<MarketProviderFactory>();
            var req = ParseJson<MarketVersionsRequest>(payload);
            if (req is null || string.IsNullOrEmpty(req.ProjectId)) return new List<MarketVersion>();
            return (object?)await factory.GetVersionsAsync(req.ProjectId, req.Source ?? "Modrinth");
        });
        Register(panel, "market.install", async payload =>
        {
            var svc = sp.GetRequiredService<PluginManagerService>();
            var req = ParseJson<MarketInstallRequest>(payload);
            if (req is null || req.Version is null || string.IsNullOrEmpty(req.ServerPath))
            {
                return new { success = false, error = "安装参数无效" };
            }
            return await svc.InstallAsync(req.Version, req.ServerPath);
        });
        Register(panel, "market.listInstalled", payload =>
        {
            var serverPath = payload?.Trim('"') ?? string.Empty;
            var installed = new List<InstalledPlugin>();
            if (!string.IsNullOrEmpty(serverPath))
            {
                var pluginsDir = Path.Combine(serverPath, "plugins");
                try
                {
                    if (Directory.Exists(pluginsDir))
                    {
                        foreach (var file in Directory.EnumerateFiles(pluginsDir, "*.jar"))
                        {
                            installed.Add(new InstalledPlugin
                            {
                                Id = Path.GetFileNameWithoutExtension(file),
                                ProjectId = Path.GetFileNameWithoutExtension(file),
                                ProjectName = Path.GetFileNameWithoutExtension(file),
                                Version = "installed",
                                InstalledAt = DateTimeOffset.UtcNow,
                                ServerPath = serverPath,
                            });
                        }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return Task.FromResult<object?>(installed);
        });

        logger.LogInformation("[BRDG-REG] Android 桥接 actions 注册完成");
    }

    private static void Register(WebPanel host, string action, Func<string?, Task<object?>> handler)
    {
        host.RegisterRequestHandler(action, handler);
    }

    private static T? ParseJson<T>(string? payload) where T : class
    {
        if (string.IsNullOrEmpty(payload)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object ToServerDto(ProcessInfo p) => new
    {
        processId = p.Pid,
        name = p.DisplayName,
        displayName = p.DisplayName,
        isRunning = true,
        javaPath = string.Empty,
        directory = string.Empty,
        cpuUsagePercent = p.CpuUsagePercent,
        memoryBytes = p.WorkingSetBytes,
        threadCount = p.ThreadCount,
    };

    private static object BuildConfigTree(string root)
    {
        var nodes = new List<object>();
        foreach (var file in SafeEnumerate(root, "*.yml").Concat(SafeEnumerate(root, "*.yaml")).Concat(SafeEnumerate(root, "*.properties")))
        {
            nodes.Add(new
            {
                path = Path.GetRelativePath(root, file).Replace('\\', '/'),
                name = Path.GetFileName(file),
                type = "file",
            });
        }
        foreach (var dir in SafeEnumerateDirs(root))
        {
            nodes.Add(new
            {
                path = Path.GetRelativePath(root, dir).Replace('\\', '/'),
                name = Path.GetFileName(dir),
                type = "dir",
            });
        }
        return new { root = root, nodes = nodes };
    }

    private static IEnumerable<string> SafeEnumerate(string root, string pattern)
    {
        try { return Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly); }
        catch (Exception) { return []; }
    }

    private static IEnumerable<string> SafeEnumerateDirs(string root)
    {
        try { return Directory.EnumerateDirectories(root); }
        catch (Exception) { return []; }
    }

    // ─────────── 请求 DTO ───────────

    private sealed class StartServerRequest
    {
        public string? Directory { get; set; }
        public string? JavaPath { get; set; }
        public string? LaunchArgs { get; set; }
        public string? McVersion { get; set; }
        public long? AffinityMask { get; set; }
    }

    private sealed class EnsureRuntimeRequest
    {
        public int? JdkMajor { get; set; }
    }

    private sealed class AddBridgeRequest
    {
        public string? ListenAddress { get; set; }
        public int ListenPort { get; set; }
        public string? TargetAddress { get; set; }
        public int? TargetPort { get; set; }
        public string? Protocol { get; set; }
    }

    private sealed class RemoveBridgeRequest
    {
        public string? ListenAddress { get; set; }
        public int ListenPort { get; set; }
        public string? Protocol { get; set; }
    }

    private sealed class ConfigContext
    {
        public string? ServerPath { get; set; }
    }

    private sealed class SetAffinityRequest
    {
        public int Pid { get; set; }
        public long AffinityMask { get; set; }
    }

    private sealed class SetPriorityRequest
    {
        public int Pid { get; set; }
        public int Nice { get; set; }
    }

    private sealed class SetOomRequest
    {
        public int Pid { get; set; }
        public int Score { get; set; } = -1000;
    }

    private sealed class MarketSearchRequest
    {
        public string? Query { get; set; }
    }

    private sealed class MarketVersionsRequest
    {
        public string? ProjectId { get; set; }
        public string? Source { get; set; }
    }

    private sealed class MarketInstallRequest
    {
        public MarketVersion? Version { get; set; }
        public string? ServerPath { get; set; }
    }
}