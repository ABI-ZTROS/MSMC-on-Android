# MSMC on Android 🐧📱

Minecraft 服务器管理 · Android 版 —— **强制 root**，内置 Termux + JDK 17/21/25，
无 GUI 管理页，内网网页面板 + 开服自动调起系统浏览器。

- **internal（内置版）**：捆绑完整 Termux + JDK 17/21/25 + 前端，离线开箱即用（APK 大；JDK 26 视 Termux 仓库可用性走运行时兜底）
- **external（非内置版）**：捆绑完整 Termux + 前端，JDK 检测已有 → 引导下载兜底（APK 小）

架构与方案见设计文档（MSMC 仓库 docs/superpowers/specs/2026-09-05-msmc-on-android-design.md）。

## 状态

| 里程碑 | 状态 | 说明 |
|--------|------|------|
| M0 骨架 | ✅ | 最小 APK + 双 flavor CI |
| M1 root+运行时 | ✅ | libsu 绑定 / RootService / TermuxRuntime / 4×JDK 管理 |
| M2 核心开服 | ✅ | WebPanel（0.0.0.0+token）/ 多开监管 / 开服自动开浏览器 / 前台服务 |
| M3 深化 | ✅ | 监控/性能/网络/调度/通知/市场/配置 全接通 |
| M4 打磨 | 🟡 | 开机自启/保活/崩溃重启已实现；真机清单待跑；Release 流水线已配 |

![CI](https://github.com/ABI-ZTROS/MSMC-on-Android/actions/workflows/ci.yml/badge.svg)

## 目录结构

```
src/MSMC.Shared/            # 跨平台核心库（含 WebPanel 网页宿主，net9.0 纯托管可测）
src/MSMC.Libsu/             # topjohnwu/libsu 6.0.0 的 .NET 绑定（core 模块）
src/MSMC.Android/           # Android 应用（net9.0-android）
  ├── Root/                 #   RootService（libsu 门面）
  ├── Runtime/              #   TermuxRuntime / JavaRuntimeManager
  ├── Supervision/          #   AndroidSupervisor（多开）/ Power / Network
  ├── Monitoring/           #   /proc 监控三件套
  ├── Notifications/        #   Android Toast 实现
  └── SupervisorService.cs  #   前台服务：WebPanel + 运行时装配 + 开服开浏览器
frontend/                   # React 前端（CI 构建成 www.zip 打进 APK）
tests/MSMC.Android.Tests/   # WebPanel 协议级烟雾测试（CI 可跑，无需 root）
scripts/assemble-assets.sh  # CI 资产装配（前端/Termux/JDK → Assets/）
docs/REAL_DEVICE_CHECKLIST.md  # 真机冒烟清单
```

## 本地构建

```bash
# 需要 .NET 9 SDK + android workload + Android SDK + JDK 21 + Node 20
./scripts/assemble-assets.sh --skip-web   # 或先手工构建前端再装配
dotnet build src/MSMC.Android/MSMC.Android.csproj -c Release -p:AppFlavor=internal -p:AndroidKeyStore=false
dotnet build src/MSMC.Android/MSMC.Android.csproj -c Release -p:AppFlavor=external -p:AndroidKeyStore=false

# WebPanel 烟雾测试
dotnet run --project tests/MSMC.Android.Tests -c Release
```

## 真机验证

root 层能力（Termux 装配 / JDK 运行 / 开服 / iptables / 开机自启）只能在真机验证，
见 `docs/REAL_DEVICE_CHECKLIST.md` 逐项冒烟。

## Release

打 tag 触发 `.github/workflows/release.yml` 产出签名双 flavor APK 并附到 GitHub Release
（未配置签名密钥时用 debug 证书，仅测试分发）。
