// -----------------------------------------------------------------------------
// 文件名: WebPanel.cs
// 命名空间: io.NET.ZTR_OS.Features.WebPanel
// 功能描述: MSMC on Android 的网页管理面板宿主 —— 内网 HTTP 服务器（0.0.0.0 可配置），
//           静态托管 React 前端 + C# ⇄ JS 双向通信（请求/响应 + 事件推送），
//           复用 MSMC-on-Linux BridgeHost 的同源 HTTP 思路，扩展「内网 + token 鉴权」。
// 说明: 纯托管实现（net9.0），不依赖 Android —— 因此可在 CI 上跑协议级烟雾测试。
// -----------------------------------------------------------------------------
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace io.NET.ZTR_OS.Features.WebPanel;

/// <summary>
/// 网页管理面板宿主 —— 内网监听 + 静态托管 + 桥接通信（token 鉴权）
/// </summary>
public sealed class WebPanel : IDisposable
{
    /// <summary>JSON 序列化选项（驼峰命名，兼容 JS 习惯）</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ConcurrentDictionary<string, Func<string?, Task<object?>>> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentQueue<string> _eventQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private int _activeConnections;

    private TcpListener? _listener;
    private Task? _acceptLoop;

    /// <summary>监听端口（默认 8080；0 表示随机空闲端口）</summary>
    public int Port { get; private set; }

    /// <summary>监听地址（默认 0.0.0.0 内网可达）</summary>
    public IPAddress ListenAddress { get; set; } = IPAddress.Any;

    /// <summary>API 访问令牌（空则关闭鉴权，仅测试用）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>前端静态资源根目录</summary>
    public string FrontendRootDir { get; set; } = string.Empty;

    /// <summary>是否已启动</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// 启动面板：绑定监听地址，开始接受连接
    /// </summary>
    /// <param name="port">监听端口；0 表示随机</param>
    public void Start(int port = 8080)
    {
        if (IsRunning) return;

        _listener = new TcpListener(ListenAddress, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        IsRunning = true;

        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>注册一个请求处理程序（JS → C#，请求/响应模式）</summary>
    public void RegisterRequestHandler(string action, Func<string?, Task<object?>> handler)
    {
        _handlers[action] = handler;
    }

    /// <summary>推送一个事件到前端（C# → JS，单向通知）</summary>
    public void PublishEvent(string action, object? payload = null)
    {
        var msg = new
        {
            type = "Event",
            action,
            payload,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        _eventQueue.Enqueue(JsonSerializer.Serialize(msg, JsonOptions));

        // 弱机保护：事件队列超限时丢弃最旧事件，避免内存无界增长
        while (_eventQueue.Count > 500 && _eventQueue.TryDequeue(out _))
        {
        }
    }

    /// <summary>接收循环：不断接受新连接，每个连接独立处理</summary>
    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }

            Interlocked.Increment(ref _activeConnections);
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    /// <summary>处理单个客户端连接：读取请求 → 鉴权 → 路由 → 写响应</summary>
    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                stream.ReadTimeout = 30_000;
                stream.WriteTimeout = 30_000;

                var request = await ReadRequestAsync(stream);
                if (request is null)
                {
                    return;
                }

                var (method, path, body, headers) = request.Value;

                if (method == "OPTIONS")
                {
                    WriteResponse(stream, 204, "No Content", string.Empty, "text/plain");
                    return;
                }

                // ── 鉴权：/api/* 必须携带有效 token（静态资源免鉴权）──
                if (IsApiPath(path) && !IsAuthorized(headers, body))
                {
                    WriteResponse(stream, 401, "Unauthorized",
                        JsonSerializer.Serialize(new { type = "Response", success = false, error = "unauthorized" }, JsonOptions),
                        "application/json; charset=utf-8");
                    return;
                }

                string responseBody;
                string contentType = "application/json; charset=utf-8";
                int status = 200;

                try
                {
                    switch (path)
                    {
                        case "/api/invoke":
                            responseBody = await HandleInvokeAsync(body);
                            break;
                        case "/api/poll":
                            responseBody = HandlePollAsync();
                            break;
                        case "/health":
                            responseBody = $$"""{"ok":true,"port":{{Port}}}""";
                            break;
                        default:
                            var (content, mime) = await HandleStaticAsync(path);
                            responseBody = content;
                            contentType = mime;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    status = 500;
                    responseBody = JsonSerializer.Serialize(
                        new { error = "internal", message = ex.Message }, JsonOptions);
                }

                WriteResponse(stream, status, "OK", responseBody, contentType);
            }
        }
        catch (Exception)
        {
            // 单个连接失败（超时/断连）不影响整体服务
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);
        }
    }

    private static bool IsApiPath(string path) =>
        path.Equals("/api/invoke", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/poll", StringComparison.OrdinalIgnoreCase);

    /// <summary>校验请求是否携带有效 token（Authorization: Bearer 头 或 __token 表单/查询）</summary>
    private bool IsAuthorized(IReadOnlyDictionary<string, string> headers, string body)
    {
        if (string.IsNullOrEmpty(Token)) return true;

        if (headers.TryGetValue("authorization", out var auth)
            && auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && auth["Bearer ".Length..].Trim() == Token)
        {
            return true;
        }

        // 前端轮询/调用以 JSON 请求体附带 token（兼容 fetch 不易加头的场景）
        if (!string.IsNullOrEmpty(body) && body.Contains(Token, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>处理 JS → C# 请求调用</summary>
    private async Task<string> HandleInvokeAsync(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);
            var root = doc.RootElement;

            var action = root.TryGetProperty("action", out var a) ? a.GetString() : null;
            var id = root.TryGetProperty("id", out var i) ? i.GetString() : null;

            if (string.IsNullOrEmpty(action))
            {
                return Reply(id, false, error: "missing action");
            }

            // 前端直接上报事件（单向，无需响应成功数据）
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (string.Equals(type, "Event", StringComparison.OrdinalIgnoreCase))
            {
                return Reply(id, true, new { received = true });
            }

            if (!_handlers.TryGetValue(action, out var handler))
            {
                return Reply(id, false, error: $"unsupported action: {action}");
            }

            var payload = root.TryGetProperty("payload", out var p) && p.ValueKind != JsonValueKind.Null
                ? (p.ValueKind == JsonValueKind.String ? p.GetString() : p.GetRawText())
                : null;

            var result = await handler(payload);
            return Reply(id, true, result);
        }
        catch (Exception ex)
        {
            return Reply(null, false, error: ex.Message);
        }

        string Reply(string? id, bool success, object? payload = null, string? error = null)
        {
            return JsonSerializer.Serialize(new
            {
                type = "Response",
                id,
                success,
                payload,
                error,
            }, JsonOptions);
        }
    }

    /// <summary>轮询拉取 C# → JS 待推送事件（最多 50 条，弱机友好）</summary>
    private string HandlePollAsync()
    {
        var batch = new List<string>(50);
        while (batch.Count < 50 && _eventQueue.TryDequeue(out var msg))
        {
            batch.Add(msg);
        }

        return "[" + string.Join(",", batch) + "]";
    }

    /// <summary>托管前端静态文件，并在 index.html 中注入桥接 shim（含 token）</summary>
    private async Task<(string Content, string MimeType)> HandleStaticAsync(string path)
    {
        var safePath = path.TrimStart('/');
        if (string.IsNullOrEmpty(safePath))
        {
            safePath = "index.html";
        }

        var root = string.IsNullOrEmpty(FrontendRootDir) ? "web" : FrontendRootDir;
        var rootFull = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(rootFull, safePath.Replace('/', Path.DirectorySeparatorChar)));

        // 路径穿越防护：仅允许位于根目录内的文件
        if (!fullPath.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(fullPath, rootFull, StringComparison.Ordinal))
        {
            return ("""{"error":"forbidden"}""", "application/json");
        }

        if (!File.Exists(fullPath))
        {
            return ("""
                <!DOCTYPE html><html><head><meta charset="utf-8"><title>Frontend not built</title></head>
                <body style="background:#0a0f1e;color:#e2e8f0;font-family:monospace;padding:2rem">
                <h2>frontend/dist 尚未构建</h2>
                <p>本设备上未找到已构建的前端界面，请先构建前端并重新打包。</p>
                </body></html>
                """, "text/html; charset=utf-8");
        }

        var content = await File.ReadAllTextAsync(fullPath);

        // 仅对入口 HTML 注入桥接 shim（保证先于 Vite module 脚本执行）
        if (safePath.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        {
            content = InjectBridgeShim(content);
        }

        return (content, MimeTypeFor(fullPath));
    }

    /// <summary>根据扩展名推断 Content-Type</summary>
    private static string MimeTypeFor(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" or ".mjs" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".otf" => "font/otf",
            ".map" => "application/json; charset=utf-8",
            ".wasm" => "application/wasm",
            _ => "application/octet-stream",
        };
    }

    /// <summary>把桥接 shim 内联注入 index.html 的 head（含 token 与回环地址）</summary>
    private string InjectBridgeShim(string html)
    {
        const string shimTemplate = @"
<script>
(function () {
  if (window.__msmcShimInjected) return;
  window.__msmcShimInjected = true;
  var api = 'http://__HOST__:__PORT__';
  var token = '__TOKEN__';
  var handlers = [];
  function emit(msg) {
    if (!msg) return;
    for (var i = 0; i < handlers.length; i++) {
      try { handlers[i]({ data: msg }); } catch (e) {}
    }
  }
  function toJson(obj) {
    if (typeof obj === 'string') return obj;
    try { return JSON.stringify(obj); } catch (e) { return String(obj); }
  }
  function authed(body) {
    if (!token) return body;
    try {
      var obj = JSON.parse(body);
      obj.__token = token;
      return JSON.stringify(obj);
    } catch (e) { return body; }
  }
  var webview = {
    postMessage: function (msg) {
      var text = authed(toJson(msg));
      fetch(api + '/api/invoke', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
        body: text
      })
        .then(function (r) { return r.json(); })
        .then(function (reply) { if (reply) emit(reply); })
        .catch(function (err) {
          if (msg && msg.id) {
            emit({ type: 'Response', id: msg.id, success: false,
                   error: String((err && err.message) || err) });
          }
        });
    },
    addEventListener: function (evt, handler) {
      if (evt === 'message' && typeof handler === 'function') handlers.push(handler);
    }
  };
  window.chrome = window.chrome || {};
  window.chrome.webview = webview;
  setInterval(function () {
    fetch(api + '/api/poll', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token },
      body: authed('{}')
    })
      .then(function (r) { return r.json(); })
      .then(function (msgs) {
        if (Array.isArray(msgs)) for (var i = 0; i < msgs.length; i++) emit(msgs[i]);
      })
      .catch(function () {});
  }, 300);
})();
</script>
";
        var host = "$HOST";
        var shim = shimTemplate
            .Replace("__HOST__", host)
            .Replace("__PORT__", Port.ToString())
            .Replace("__TOKEN__", Token);

        const string headTag = "<head>";
        if (html.Contains(headTag, StringComparison.OrdinalIgnoreCase))
        {
            return html.Replace(headTag, headTag + Environment.NewLine + shim, StringComparison.OrdinalIgnoreCase);
        }

        const string moduleTag = "<script type=\"module\"";
        if (html.Contains(moduleTag, StringComparison.Ordinal))
        {
            return html.Replace(moduleTag, shim + Environment.NewLine + moduleTag, StringComparison.Ordinal);
        }

        return shim + html;
    }

    /// <summary>读取一个 HTTP 请求（请求行 + 请求头 + 可选请求体 + 头字典）</summary>
    private static async Task<(string Method, string Path, string Body, IReadOnlyDictionary<string, string> Headers)?> ReadRequestAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(requestLine))
        {
            return null;
        }

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var method = parts[0];
        var path = parts[1];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int? contentLength = null;
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            headers[name] = value;
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var len))
            {
                contentLength = len;
            }
        }

        var body = string.Empty;
        if (contentLength is > 0)
        {
            var buf = new char[contentLength.Value];
            var read = 0;
            while (read < contentLength.Value)
            {
                var n = await reader.ReadAsync(buf, read, contentLength.Value - read);
                if (n <= 0) break;
                read += n;
            }
            body = new string(buf, 0, read);
        }

        return (method, path, body, headers);
    }

    /// <summary>写 HTTP 响应</summary>
    private static void WriteResponse(NetworkStream stream, int statusCode, string reason, string body, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var header = new StringBuilder()
            .Append($"HTTP/1.1 {statusCode} {reason}\r\n")
            .Append("Server: MSMC-Panel/1.0\r\n")
            .Append("Access-Control-Allow-Origin: *\r\n")
            .AppendLine($"Content-Length: {bytes.Length}")
            .Append($"Content-Type: {contentType}\r\n")
            .AppendLine("Connection: close")
            .Append("\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(header.ToString());
        stream.Write(headerBytes, 0, headerBytes.Length);
        if (bytes.Length > 0)
        {
            stream.Write(bytes, 0, bytes.Length);
        }
        stream.Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts.Cancel();
        try { _listener?.Stop(); } catch { /* 忽略关闭异常 */ }
        _cts.Dispose();
        _listener?.Dispose();
    }
}