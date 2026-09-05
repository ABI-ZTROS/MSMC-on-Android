// -----------------------------------------------------------------------------
// 文件名: Program.cs
// 命名空间: MSMC.Android.Tests
// 功能描述: WebPanel 协议级烟雾测试（CI 可跑，无需 root / Android）——
//           启动内网面板 → 静态托管 / /health / /api/invoke 鉴权与调用 / 事件推送
// -----------------------------------------------------------------------------
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using io.NET.ZTR_OS.Features.WebPanel;

var failures = 0;

void Check(string name, bool ok, string detail = "")
{
    var mark = ok ? "PASS" : "FAIL";
    Console.WriteLine($"[{mark}] {name}" + (detail.Length > 0 ? $" — {detail}" : string.Empty));
    if (!ok) failures++;
}

// 1. 静态托管
var webDir = Path.Combine(Path.GetTempPath(), $"msmc-web-{Guid.NewGuid():N}");
Directory.CreateDirectory(webDir);
File.WriteAllText(Path.Combine(webDir, "index.html"), "<!DOCTYPE html><html><head></head><body>MSMC</body></html>");
File.WriteAllText(Path.Combine(webDir, "app.js"), "console.log('hi');");

using var panel = new WebPanel
{
    FrontendRootDir = webDir,
    ListenAddress = IPAddress.Loopback,
};
panel.RegisterRequestHandler("ping", _ => Task.FromResult<object?>(new { pong = true, at = DateTime.UtcNow.Ticks }));
panel.RegisterRequestHandler("echo", p => Task.FromResult<object?>(p));
panel.Start(0);
var port = panel.Port;
var baseUrl = $"http://127.0.0.1:{port}";

using var client = new HttpClient();

// 2. /health 无需鉴权
var health = await client.GetStringAsync($"{baseUrl}/health");
Check("health 可达", health.Contains("\"ok\":true"), health);
Check("health 端口正确", health.Contains($"\"port\":{port}"));

// 3. 静态 index.html（含 shim 注入）
var idx = await client.GetStringAsync($"{baseUrl}/");
Check("index.html 托管", idx.Contains("MSMC"));
Check("shim 注入", idx.Contains("__msmcShimInjected") && idx.Contains("chrome.webview"));

// 4. 无 token 时 API 401
panel.Token = "secret-token-123";
using var unauth = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/invoke")
{
    Content = new StringContent("""{"action":"ping","id":"1"}""", Encoding.UTF8, "application/json"),
};
var unauthResp = await client.SendAsync(unauth);
Check("无 token → 401", unauthResp.StatusCode == HttpStatusCode.Unauthorized, $"{(int)unauthResp.StatusCode}");

// 5. 带 token 调用 ping
using var authReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/invoke")
{
    Content = new StringContent("""{"action":"ping","id":"2","__token":"secret-token-123"}""", Encoding.UTF8, "application/json"),
};
var authResp = await client.SendAsync(authReq);
var authBody = await authResp.Content.ReadAsStringAsync();
Check("带 token → 200", authResp.StatusCode == HttpStatusCode.OK, $"{(int)authResp.StatusCode}");
using var doc = JsonDocument.Parse(authBody);
Check("ping 返回 success", doc.RootElement.GetProperty("success").GetBoolean());
Check("ping 返回 pong", doc.RootElement.GetProperty("payload").GetProperty("pong").GetBoolean());

// 6. 事件推送 → 轮询
panel.PublishEvent("server:started", new { name = "test" });
using var pollReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/poll")
{
    Content = new StringContent("""{"__token":"secret-token-123"}""", Encoding.UTF8, "application/json"),
};
var pollBody = await (await client.SendAsync(pollReq)).Content.ReadAsStringAsync();
Check("事件轮询收到", pollBody.Contains("server:started"), pollBody);

// 7. 不支持 action
using var badReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/invoke")
{
    Content = new StringContent("""{"action":"nope","id":"3","__token":"secret-token-123"}""", Encoding.UTF8, "application/json"),
};
var badBody = await (await client.SendAsync(badReq)).Content.ReadAsStringAsync();
Check("未知 action 报错", badBody.Contains("unsupported action"), badBody);

// 8. 路径穿越防护
var trav = await client.GetStringAsync($"{baseUrl}/../../etc/passwd");
Check("路径穿越被拦截", !trav.Contains("root:"), trav[..Math.Min(40, trav.Length)]);

Directory.Delete(webDir, true);

Console.WriteLine(failures == 0 ? "\n✅ 全部通过" : $"\n❌ {failures} 项失败");
return failures == 0 ? 0 : 1;