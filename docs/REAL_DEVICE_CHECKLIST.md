# MSMC on Android · 真机冒烟清单

> 在 **KernelSU 已 root** 的 GKI 设备上逐项验证。每项记录 `✅/❌ + 备注`。
> CI 只保证编译与协议层，root 层必须在真机过。

## 设备
- 机型 / 系统 / 内核 / KernelSU 版本：____
- 安装的 APK flavor（internal / external）：____

## M1 · root + 运行时
- [ ] 安装后首次打开显示「Root: 已授权」或弹 KernelSU 授权框
- [ ] 拒绝授权 → App 不崩溃，提示未授权
- [ ] 启动管理服务后，`files/termux/usr/bin/bash` 存在且可执行
- [ ] `java -version` 对内置 JDK 17/21/25/26 各输出正确版本
- [ ] external flavor：无内置 JDK 时能通过 `pkg install` 或引导下载装好 JDK 21

## M2 · 面板 + 开服
- [ ] 局域网内 `http://<手机IP>:8080` 打开面板（需 token）
- [ ] 首页 / 监控 / 服务器 / 网络 / 性能 页可渲染，API 返回 200
- [ ] 无 token 访问 `/api/invoke` 返回 401
- [ ] 新建服务器目录 + `server.jar` → 面板开服 → 日志出现 `Done`
- [ ] 开服成功后系统浏览器自动打开面板（ACTION_VIEW）
- [ ] 多开：同时起 2 个不同目录服务器互不干扰
- [ ] 停止服务器（优雅 stop → 存档）

## M3 · 深化
- [ ] 监控曲线有数据（CPU/内存/磁盘/线程）
- [ ] taskset 锁核 / renice / oom_score_adj 对目标进程生效
- [ ] iptables 端口转发后，内网其他设备能连上 MC 端口
- [ ] 调度定时启停一次触发成功
- [ ] 通知：Discord / Webhook / Android 气泡各一条到达
- [ ] 市场搜索安装一个插件，`plugins/` 出现 jar

## M4 · 保活打磨
- [ ] 重启手机 → App 自动拉起前台服务（BOOT_COMPLETED）
- [ ] 杀掉 App 进程 → 前台服务自动恢复
- [ ] 服务器崩溃 → （若开启）自动重启 N 次
- [ ] 通知栏常驻通知存在，点击可打开面板
- [ ] 释放 Release 产物安装冒烟通过

## 问题记录
| # | 现象 | 复现步骤 | 日志/截图 | 结论 |
|---|------|----------|-----------|------|
| 1 |      |          |           |      |
