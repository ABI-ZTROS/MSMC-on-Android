# MSMC on Android 🐧📱

Minecraft 服务器管理 · Android 版 —— **强制 root**，内置 Termux + JDK 17/21/25/26，
无 GUI 管理页，内网网页面板 + 开服自动调起系统浏览器。

- **internal（内置版）**：捆绑完整 Termux + 4 个 JDK，离线开箱即用
- **external（非内置版）**：完整 Termux，JDK 检测已有 → 引导下载兜底

架构与方案见设计文档（MSMC 仓库 docs/superpowers/specs/2026-09-05-msmc-on-android-design.md）。

## 状态

✅ M0 完成 —— 最小 APK 可构建，CI 产出 internal/external 双 APK（无管理功能）

![CI](https://github.com/ABI-ZTROS/MSMC-on-Android/actions/workflows/ci.yml/badge.svg)

## 目录结构

```
src/MSMC.Shared/      # 跨平台核心库（复制自 MSMC-on-Linux，独立演进）
src/MSMC.Android/     # .NET for Android 应用（net9.0-android）
frontend/             # React 前端（M0 仅保管，M2 起由内网网页托管）
```

## 本地构建

```bash
# 需要 .NET 9 SDK + android workload + Android SDK + JDK 17+
dotnet build src/MSMC.Android/MSMC.Android.csproj -c Release -p:AppFlavor=internal
dotnet build src/MSMC.Android/MSMC.Android.csproj -c Release -p:AppFlavor=external
```