import type {
  BridgeMessage,
  AppInfo,
  SystemMetrics,
  HistoryPoint,
  HistoryRangeResult,
  CpuInfo,
  ServerListResponse,
  ServerInfo,
  NetworkStatus,
  PortsResponse,
  BridgeRulesResponse,
  CommonPortInfo,
  AddBridgeRequest,
  KillProcessRequest,
  HourlyHistoryResponse,
  AvailableServersResponse,
  ConfigFileTreeResponse,
  ConfigEntriesResponse,
  UpdateConfigValueRequest,
  ConfigSaveResult,
  SettingsData,
  JavaListResponse,
  ThemePreset,
  ThemeApplyResult,
  SwatchesResponse,
  PresetsResponse,
  TeamInfoResponse,
  JvmDefinitionsResponse,
  JvmStateResponse,
  JvmUpdateArgumentRequest,
  JvmSetMemoryRequest,
  JvmPresetType,
  ProcessAffinityInfo,
  ProcessQoSTier,
  PowerProfile,
  CpuPowerCapabilities,
  QoSApplyResult,
  PowerProfileApplyResult,
  CpuSetTopology,
  CpuSetPinResult,
  PriorityBoostResult,
  TimerResolutionResult,
  PowerRequestResult,
  NotificationEvent,
  NotificationDispatchResult,
  ScheduledTask,
  ExecutionRecord,
  MarketProject,
  MarketVersion,
  InstallResult,
  InstalledPlugin,
  StartupMode,
  StartupConfig,
  DiffReport,
} from '@/types/bridge'

declare global {
  interface Window {
    __msmc_bridge__?: MsmcBridge
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void
        addEventListener: (event: string, handler: (event: { data: unknown }) => void) => void
      }
    }
  }
}

export interface MsmcBridge {
  invoke: <T = unknown>(action: string, payload?: unknown) => Promise<T>
  invokeWithTimeout: <T = unknown>(action: string, payload: unknown, timeoutMs: number) => Promise<T>
  sendEvent: (action: string, payload?: unknown) => void
  on: (action: string, handler: (payload: unknown) => void) => () => void
  log: (message: unknown) => void
}

type PendingRequest = {
  resolve: (value: unknown) => void
  reject: (reason: unknown) => void
  timeout: number
}

// 直接向 C# 发送日志（绕过桥接，用于桥接初始化前的日志）
function rawLog(msg: string): void {
  console.log('[Bridge]', msg)
  if (window.chrome?.webview) {
    try {
      window.chrome.webview.postMessage({
        type: 'log',
        action: 'log',
        payload: `[JS] ${msg}`,
        timestamp: Date.now(),
      })
    } catch {
      // ignore
    }
  }
}

class Bridge implements MsmcBridge {
  private pendingRequests = new Map<string, PendingRequest>()
  private eventListeners = new Map<string, Array<(payload: unknown) => void>>()
  private requestIdCounter = 0
  private initialized = false
  private initPromise: Promise<void> | null = null
  // BugH: 桥接是否「最终可用」标记。10s 内没连上 webview 就标 false，
  //       这样 invoke 不会永远卡在 await init()，而是立刻给出明确错误
  private bridgeAvailable = false
  private bridgeAvailabilityKnown = false
  // 异步等待「桥接可用性确定」（最多 10s），之后就不卡了
  private availabilityPromise: Promise<void> | null = null
  private availabilityResolve: (() => void) | null = null

  constructor() {
    rawLog('Bridge 构造函数执行')
    // BugH: 10s 总兜底，不管 webview 有没有出现，都强制结束 init() 的等待
    const totalTimeout = window.setTimeout(() => {
      if (!this.initialized) {
        rawLog('[BugH] 桥接初始化 10s 兜底超时：chrome.webview 仍不可用，标记 bridgeAvailable=false')
        this.bridgeAvailable = false
        this.bridgeAvailabilityKnown = true
        this.availabilityResolve?.()
      }
    }, 10000)
    this.availabilityPromise = new Promise<void>((resolve) => {
      this.availabilityResolve = () => {
        window.clearTimeout(totalTimeout)
        resolve()
      }
    })
    this.init()
    // 启动周期性清理，防止超时后残留请求对象导致内存泄漏
    window.setInterval(() => this.cleanupExpiredRequests(), 30000)
  }

  /// <summary>
  /// 清理已过期但尚未被移除的 pending 请求（兜底防护）
  /// </summary>
  private cleanupExpiredRequests(): void {
    const now = Date.now()
    let cleaned = 0
    for (const [id, req] of this.pendingRequests) {
      // 解析请求 ID 中的时间戳（格式：js_req_{counter}_{timestamp}）
      const parts = id.split('_')
      const timestamp = parts.length >= 3 ? parseInt(parts[parts.length - 1], 10) : 0
      if (timestamp > 0 && now - timestamp > 30000) {
        clearTimeout(req.timeout)
        req.reject(new Error('Request expired by cleanup'))
        this.pendingRequests.delete(id)
        cleaned++
      }
    }
    if (cleaned > 0) {
      rawLog(`[CLEAN] 清理了 ${cleaned} 个过期 pending 请求`)
    }
  }

  private generateId(): string {
    return `js_req_${++this.requestIdCounter}_${Date.now()}`
  }

  private init(): Promise<void> {
    rawLog('init() 开始')

    if (this.initPromise) {
      rawLog('initPromise 已存在，直接返回')
      return this.initPromise
    }

    this.initPromise = new Promise((resolve) => {
      const setup = () => {
        rawLog(`setup() 调用，initialized=${this.initialized}`)

        if (this.initialized) {
          rawLog('已初始化，跳过')
          return
        }

        if (window.chrome?.webview) {
          rawLog('检测到 chrome.webview，注册消息监听')
          // BugH: 标记桥接可用，并结束 10s 总兜底超时的等待
          this.bridgeAvailable = true
          this.bridgeAvailabilityKnown = true
          this.availabilityResolve?.()
          // 防篡改自检：保护 postMessage 不被外部覆写
          try {
            const _wv = window.chrome.webview
            const _orig = _wv.postMessage
            if (_orig && typeof _orig === 'function') {
              Object.defineProperty(_wv, 'postMessage', {
                get: () => _orig,
                set: () => { throw new Error('tampered') },
                configurable: false,
              })
              rawLog('[SEC] postMessage 防篡改保护已启用')
            }
          } catch {
            // 已锁定或环境不支持，忽略
          }
          window.chrome.webview.addEventListener('message', this.handleMessage.bind(this))
          this.initialized = true
          rawLog('[OK] JS 端桥接初始化完成')
          resolve()
        } else {
          rawLog('[WARN] 未检测到 chrome.webview')
        }
      }

      if (document.readyState === 'complete') {
        rawLog('document.readyState = complete，立即执行 setup')
        setup()
      } else {
        rawLog(`document.readyState = ${document.readyState}，等待 load 事件`)
        window.addEventListener('load', setup, { once: true })
      }

      // 多次重试，确保 webview 对象已注入
      setTimeout(setup, 100)
      setTimeout(setup, 500)
      setTimeout(setup, 1000)
      setTimeout(setup, 2000)
      setTimeout(setup, 5000)
    })

    return this.initPromise
  }

  private handleMessage(event: { data: unknown }): void {
    const data = event.data as BridgeMessage
    rawLog(`收到消息: type=${data?.type}, action=${data?.action}, id=${data?.id ?? '(无)'}`)

    if (!data || !data.type) return

    // 统一转小写，兼容 C# 端枚举序列化的大小写
    const type = String(data.type).toLowerCase()

    switch (type) {
      case 'response': {
        if (data.id) {
          const pending = this.pendingRequests.get(data.id)
          if (pending) {
            clearTimeout(pending.timeout)
            this.pendingRequests.delete(data.id)
            if (data.success) {
              rawLog(`[OK] 请求 ${data.action} 成功`)
              pending.resolve(data.payload)
            } else {
              rawLog(`[ERR] 请求 ${data.action} 失败: ${data.error}`)
              pending.reject(new Error(data.error || 'Unknown error'))
            }
          } else {
            rawLog(`[WARN] 未找到待处理请求: ${data.id}`)
          }
        }
        break
      }
      case 'event': {
        const listeners = this.eventListeners.get(data.action)
        if (listeners) {
          rawLog(`[MSG] 触发事件: ${data.action} (${listeners.length} 个监听器)`)
          listeners.forEach((fn) => {
            try {
              fn(data.payload)
            } catch (e) {
              console.error('Event handler error:', e)
              rawLog(`[ERR] 事件处理错误: ${data.action} - ${e}`)
            }
          })
        } else {
          rawLog(`[WARN] 事件 ${data.action} 没有监听器`)
        }
        break
      }
      case 'request': {
        rawLog('[WARN] 收到 C# 发起的请求（暂不支持）')
        break
      }
      case 'log': {
        console.log('[C#]', data.payload)
        break
      }
      default: {
        rawLog(`[WARN] 未知消息类型: ${data.type}`)
        break
      }
    }
  }

  private postMessage(message: BridgeMessage): void {
    if (window.chrome?.webview) {
      rawLog(`[MSG] 发送消息: type=${message.type}, action=${message.action}`)
      window.chrome.webview.postMessage(message)
    } else {
      rawLog('[WARN] chrome.webview 不可用，无法发送消息')
    }
  }

  async invoke<T = unknown>(action: string, payload?: unknown): Promise<T> {
    return this.invokeWithTimeout<T>(action, payload, 30000)
  }

  async invokeWithTimeout<T = unknown>(action: string, payload: unknown, timeoutMs: number): Promise<T> {
    rawLog(`invoke 开始: ${action}`)
    // BugH: 先等待「桥接可用性确定」（最多 10s，可用性确定比 init() 强）。
    //       这样 10s 后如果 webview 仍然不存在，桥接不可用，立即给出明确错误，
    //       而不是永远卡在 await init()（因为 init() 的 Promise 永远 pending）。
    if (!this.bridgeAvailabilityKnown && this.availabilityPromise) {
      rawLog(`桥接可用性未确定，等待 availabilityPromise（最多 10s）: ${action}`)
      await this.availabilityPromise
    }
    if (this.bridgeAvailabilityKnown && !this.bridgeAvailable) {
      // 桥接已经确定不可用（10s 兜底超时仍无 webview）→ 立刻报错，不继续调用
      rawLog(`[BugH] 桥接不可用（未检测到 chrome.webview），拒绝请求: ${action}`)
      return Promise.reject(new Error(
        `Bridge unavailable: WebView2 host not detected. ` +
        `This action (${action}) requires running inside the desktop host.`,
      ))
    }
    await this.init()
    rawLog(`init 完成，准备发送请求: ${action}`)

    return new Promise<T>((resolve, reject) => {
      const id = this.generateId()
      rawLog(`生成请求 ID: ${id}`)

      const timeout = window.setTimeout(() => {
        this.pendingRequests.delete(id)
        rawLog(`[TIME] 请求超时: ${action}`)
        reject(new Error(`Request timeout: ${action}`))
      }, timeoutMs)

      this.pendingRequests.set(id, {
        resolve: resolve as (value: unknown) => void,
        reject,
        timeout,
      })

      this.postMessage({
        type: 'request',
        id,
        action,
        payload,
        timestamp: Date.now(),
      })

      rawLog(`请求已发送: ${action} (${id})`)
    })
  }

  sendEvent(action: string, payload?: unknown): void {
    this.init().then(() => {
      rawLog(`发送事件: ${action}`)
      this.postMessage({
        type: 'event',
        action,
        payload,
        timestamp: Date.now(),
      })
    })
  }

  on(action: string, handler: (payload: unknown) => void): () => void {
    rawLog(`注册事件监听器: ${action}`)
    if (!this.eventListeners.has(action)) {
      this.eventListeners.set(action, [])
    }
    this.eventListeners.get(action)!.push(handler)

    return () => {
      const listeners = this.eventListeners.get(action)
      if (listeners) {
        const idx = listeners.indexOf(handler)
        if (idx > -1) listeners.splice(idx, 1)
      }
    }
  }

  log(message: unknown): void {
    this.init().then(() => {
      this.postMessage({
        type: 'log',
        action: 'log',
        payload: message,
        timestamp: Date.now(),
      })
    })
  }
}

// 工厂模式：避免全局对象被逆向者直接 inspect
let _bridge: Bridge | null = null
export function getBridge(): Bridge {
  if (!_bridge) _bridge = new Bridge()
  return _bridge
}
// 模块内部引用（不导出），供下方 API 函数使用
const bridge = getBridge()

// ═════════════════════════════════════════════════════════════════════
// 基础 API
// ═════════════════════════════════════════════════════════════════════

export function ping(): Promise<{ pong: boolean; timestamp: number; message: string }> {
  return bridge.invoke<{ pong: boolean; timestamp: number; message: string }>('ping')
}

export function getAppTime(): Promise<string> {
  return bridge.invoke<string>('app:getTime')
}

export function getAppInfo(): Promise<AppInfo> {
  return bridge.invoke<AppInfo>('app:getInfo')
}

/** 强制刷新管理员权限状态（前端主动触发，如切换页面或点击刷新按钮） */
export function refreshAdminStatus(): Promise<{ success: boolean; isAdmin: boolean }> {
  return bridge.invoke<{ success: boolean; isAdmin: boolean }>('app:refreshAdminStatus')
}

// 电源管理模块开关 —— 实验性能力，默认关闭，启用后需重启 MSMC 生效
export interface PowerManagementState {
  enabled: boolean
}

export interface PowerManagementToggleResult {
  success: boolean
  enabled?: boolean
  needsRestart?: boolean
  error?: string
}

export function getPowerManagementState(): Promise<PowerManagementState> {
  return bridge.invoke<PowerManagementState>('app:getPowerManagementState')
}

export function setPowerManagementEnabled(enabled: boolean): Promise<PowerManagementToggleResult> {
  return bridge.invoke<PowerManagementToggleResult>('app:setPowerManagementEnabled', { enabled })
}

// 慢操作（如 network:*、server:*）可用更长超时调用
export function invokeWithTimeout<T = unknown>(action: string, payload: unknown, timeoutMs: number): Promise<T> {
  return bridge.invokeWithTimeout<T>(action, payload, timeoutMs)
}

export function onStatusUpdate(handler: (data: { message: string }) => void): () => void {
  return bridge.on('status:update', (payload) => handler(payload as { message: string }))
}

export function onThemeChanged(handler: (data: SettingsData) => void): () => void {
  return bridge.on('theme:changed', (payload) => handler(payload as SettingsData))
}

// ═════════════════════════════════════════════════════════════════════
// 系统监控 API
// ═════════════════════════════════════════════════════════════════════

export function getSystemMetrics(): Promise<SystemMetrics> {
  return bridge.invoke<SystemMetrics>('systemMonitor:getMetrics')
}

export function getSystemHistory(): Promise<HistoryPoint[]> {
  return bridge.invoke<HistoryPoint[]>('systemMonitor:getHistory')
}

export function getSystemHistoryRange(days: number): Promise<HistoryRangeResult> {
  return bridge.invoke<HistoryRangeResult>('systemMonitor:getHistoryRange', { days })
}

export function getCpuInfo(): Promise<CpuInfo> {
  return bridge.invoke<CpuInfo>('systemMonitor:getCpuInfo')
}

// ═════════════════════════════════════════════════════════════════════
// 服务器管理 API
// ═════════════════════════════════════════════════════════════════════

export function getServerList(): Promise<ServerListResponse> {
  // 后端在每个 server/known 对象上返回 __supervisor = { isSupervised, crashCount, scheduledRestartAt, ... }
  // 这里把它拍平到顶层（前端 interface ServerInfo / KnownServerInfo 直接声明了相同字段），
  // 这样 Dashboard / Settings 里写代码时不用判断 __supervisor 是否存在。
  return bridge.invoke<any>('server:list').then((resp: any) => {
    const flatten = (obj: any): any => {
      if (!obj || typeof obj !== 'object') return obj
      const sup = (obj as any).__supervisor
      if (!sup || typeof sup !== 'object') return obj
      const { __supervisor, ...rest } = obj as any
      return { ...rest, ...sup }
    }
    return {
      ...resp,
      running: Array.isArray(resp?.running) ? resp.running.map(flatten) : [],
      known: Array.isArray(resp?.known) ? resp.known.map(flatten) : [],
    } as ServerListResponse
  })
}

export function getSelectedServer(): Promise<ServerInfo | null> {
  return bridge.invoke<ServerInfo | null>('server:getSelected')
}

export function selectServer(displayName: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('server:select', displayName)
}

// ═════════════════════════════════════════════════════════════════════
// 网络监控 API
// ═════════════════════════════════════════════════════════════════════

export function getNetworkStatus(): Promise<NetworkStatus> {
  return bridge.invoke<NetworkStatus>('network:getStatus')
}

export function getPorts(): Promise<PortsResponse> {
  return bridge.invoke<PortsResponse>('network:getPorts')
}

export function getBridgeRules(): Promise<BridgeRulesResponse> {
  return bridge.invoke<BridgeRulesResponse>('network:getBridgeRules')
}

export function addBridge(req: AddBridgeRequest): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('network:addBridge', req)
}

export function removeBridge(listenAddress: string, listenPort: number, protocol: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('network:removeBridge', { listenAddress, listenPort, protocol })
}

export function killProcess(req: KillProcessRequest): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('network:killProcess', req)
}

export function getCommonPorts(): Promise<{ ports: CommonPortInfo[] }> {
  return bridge.invoke<{ ports: CommonPortInfo[] }>('network:getCommonPorts')
}

export function refreshNetwork(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('network:refresh')
}

export function getHourlyHistory(): Promise<HourlyHistoryResponse> {
  return bridge.invoke<HourlyHistoryResponse>('network:getHourlyHistory')
}

// ═════════════════════════════════════════════════════════════════════
// 配置编辑 API
// ═════════════════════════════════════════════════════════════════════

export function getAvailableServers(): Promise<AvailableServersResponse> {
  return bridge.invoke<AvailableServersResponse>('config:getAvailableServers')
}

export function getConfigFileTree(): Promise<ConfigFileTreeResponse> {
  return bridge.invoke<ConfigFileTreeResponse>('config:getFileTree')
}

export function selectConfigFile(relativePath: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('config:selectFile', relativePath)
}

export function getConfigEntries(): Promise<ConfigEntriesResponse> {
  return bridge.invoke<ConfigEntriesResponse>('config:getEntries')
}

export function updateConfigValue(req: UpdateConfigValueRequest): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('config:updateValue', req)
}

export function saveConfig(): Promise<ConfigSaveResult> {
  return bridge.invoke<ConfigSaveResult>('config:save')
}

export function resetConfig(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('config:reset')
}

export function undoConfig(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('config:undo')
}

export function redoConfig(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('config:redo')
}

export function selectConfigServer(name: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('config:selectServer', name)
}

// 手动定位 JAR 文件 —— 用户明确要求：「新增一个手动定位 Jar，然后顺着路径去遍历」
// 后端弹 OpenFileDialog 让用户选 JAR → 推导 WorkingDirectory → 赋值 cfg.Server → OnServerChanged 自动扫目录
// 返回 jarPath / workingDirectory / displayName 供前端显示
export function selectJarManually(): Promise<{
  success: boolean
  jarPath?: string
  workingDirectory?: string
  displayName?: string
  error?: string
}> {
  return bridge.invoke('config:browseJar', null)
}

// Q3: 按 Dashboard 当前选中服务器的上下文（displayName / workingDirectory / serverJarPath / knownServerId）
// 自动联动选择 ConfigEditor 的默认服务器。比 displayName 字符串精确相等匹配稳得多。
export function selectDefaultConfigServer(ctx: {
  displayName?: string
  workingDirectory?: string
  serverJarPath?: string
  knownServerId?: string
}): Promise<{ success: boolean; selected?: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; selected?: boolean; error?: string }>('config:selectDefaultServer', ctx)
}

export function rescanConfigFiles(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('config:rescan')
}

// 核心索引表 —— 查询所有服务器核心的配置文件翻译索引
export interface CoreConfigFileInfo {
  fileName: string
  format: string
  source: string
  descriptorCount: number
}

export interface CoreIndexEntry {
  coreType: string
  displayName: string
  category: string
  inheritance: string
  isDeprecated: boolean
  configFiles: CoreConfigFileInfo[]
}

export interface CoreIndexResponse {
  success: boolean
  totalCores?: number
  totalDescriptors?: number
  cores?: CoreIndexEntry[]
  error?: string
}

export function getCoreIndex(): Promise<CoreIndexResponse> {
  return bridge.invoke<CoreIndexResponse>('config:getCoreIndex')
}

// ═════════════════════════════════════════════════════════════════════
// 设置 API
// ═════════════════════════════════════════════════════════════════════

export function getSettings(): Promise<SettingsData> {
  return bridge.invoke<SettingsData>('settings:get')
}

export function setPrimaryColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setPrimaryColor', hex)
}

export function setAccentColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setAccentColor', hex)
}

// ═════════════════════════════════════════════════════════════════════
// ✅ 12 色体系 — 10 个颜色 setter
// ═════════════════════════════════════════════════════════════════════

export function setBackgroundColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setBackgroundColor', hex)
}

export function setCardColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setCardColor', hex)
}

export function setTextColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setTextColor', hex)
}

export function setBorderColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setBorderColor', hex)
}

export function setSuccessColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setSuccessColor', hex)
}

export function setWarningColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setWarningColor', hex)
}

export function setErrorColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setErrorColor', hex)
}

export function setGaugeGreenColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setGaugeGreenColor', hex)
}

export function setGaugeYellowColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setGaugeYellowColor', hex)
}

export function setGaugeRedColor(hex: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:setGaugeRedColor', hex)
}

export function applyTheme(): Promise<ThemeApplyResult> {
  return bridge.invoke<ThemeApplyResult>('settings:applyTheme')
}

export function saveSettings(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:save')
}

export function updateSettings(data: {
  cornerRadius?: number
  animationDuration?: number
  enableAnimations?: boolean
  enableWindowsNotifications?: boolean
  preferJavaw?: boolean
  supervisor?: {
    enableCrashRestart?: boolean
    maxRestartAttemptsPerHour?: number
    restartCooldownSeconds?: number
    preventSystemSleepWhenRunning?: boolean
    processPriority?: string
    maxProcessMemoryBytes?: number
    maxTotalRestartAttempts?: number
  }
}): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('settings:update', data)
}

export function setPreset(preset: ThemePreset): Promise<ThemeApplyResult> {
  return bridge.invoke<ThemeApplyResult>('settings:setPreset', preset)
}

export function resetSettings(): Promise<ThemeApplyResult> {
  return bridge.invoke<ThemeApplyResult>('settings:reset')
}

export function toggleAnimations(): Promise<{ success: boolean; enableAnimations: boolean }> {
  return bridge.invoke<{ success: boolean; enableAnimations: boolean }>('settings:toggleAnimations')
}

export function testNotification(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:testNotification')
}

export function getJavaList(): Promise<JavaListResponse> {
  return bridge.invoke<JavaListResponse>('settings:getJavaList')
}

export function rescanJava(): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('settings:rescanJava')
}

// 自选 Java 路径管理
export interface JavaPathOpResult {
  success: boolean
  error?: string
  statusMessage?: string
  path?: string
}

export function addJavaPath(path: string): Promise<JavaPathOpResult> {
  return bridge.invoke<JavaPathOpResult>('settings:addJava', { path })
}

export function removeJavaPath(javaPath: string): Promise<JavaPathOpResult> {
  return bridge.invoke<JavaPathOpResult>('settings:removeJava', { javaPath })
}

export function setDefaultJava(javaPath: string): Promise<JavaPathOpResult> {
  return bridge.invoke<JavaPathOpResult>('settings:setDefaultJava', { javaPath })
}

export function browseJavaPath(): Promise<JavaPathOpResult> {
  return bridge.invoke<JavaPathOpResult>('settings:browseJavaPath')
}

export function getPresets(): Promise<PresetsResponse> {
  return bridge.invoke<PresetsResponse>('settings:getPresets')
}

export function getPrimarySwatches(): Promise<SwatchesResponse> {
  return bridge.invoke<SwatchesResponse>('settings:getPrimarySwatches')
}

export function getAccentSwatches(): Promise<SwatchesResponse> {
  return bridge.invoke<SwatchesResponse>('settings:getAccentSwatches')
}

// ═════════════════════════════════════════════════════════════════════
// 关于页面 API
// ═════════════════════════════════════════════════════════════════════

export function getTeamInfo(): Promise<TeamInfoResponse> {
  return bridge.invoke<TeamInfoResponse>('about:getTeamInfo')
}

// ═════════════════════════════════════════════════════════════════════
// JVM 参数相关 API
// ═════════════════════════════════════════════════════════════════════

export function getJvmDefinitions(): Promise<JvmDefinitionsResponse> {
  return bridge.invoke<JvmDefinitionsResponse>('jvm:getDefinitions')
}

export function getJvmState(): Promise<JvmStateResponse> {
  return bridge.invoke<JvmStateResponse>('jvm:getState')
}

export function addJvmArgument(flag: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:addArgument', flag)
}

export function removeJvmArgument(flag: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:removeArgument', flag)
}

export function updateJvmArgument(
  oldArg: string,
  newValue: string,
): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:updateArgument', {
    oldArg,
    newValue,
  } as JvmUpdateArgumentRequest)
}

export function setJvmMemory(initial?: string, max?: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:setMemory', {
    initial,
    max,
  } as JvmSetMemoryRequest)
}

export function applyJvmPreset(preset: JvmPresetType): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:applyPreset', preset)
}

export function addCustomJvmArgument(arg: string): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('jvm:addCustom', arg)
}

// ═════════════════════════════════════════════════════════════════════
// 进程管理 API
// ═════════════════════════════════════════════════════════════════════

export function getProcessAffinities(): Promise<ProcessAffinityInfo[]> {
  return bridge.invoke<ProcessAffinityInfo[]>('processManager:getAffinities')
}

export function getProcessInfo(pid: number): Promise<ProcessAffinityInfo | null> {
  return bridge.invoke<ProcessAffinityInfo | null>('processManager:getInfo', { pid })
}

export function killProcessById(pid: number, graceful: boolean = true): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('processManager:kill', { pid, graceful })
}

export function setProcessAffinity(pid: number, affinityMask: number): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('processManager:setAffinity', { pid, affinityMask })
}

// ═════════════════════════════════════════════════════════════════════
// CPU 电源与调度管控 API（T1 QoS + T2 电源档位）
// 因果链：前端按钮 → setProcessQoS/applyPowerProfile → cpuPower:* 桥接 →
//         C# ICpuPowerService → SetProcessInformation / powercfg
// ═════════════════════════════════════════════════════════════════════

/** 查询平台能力（支持哪些 QoS / 电源档位能力 + 当前档位 + 是否有崩溃未还原快照） */
export function getCpuPowerCapabilities(): Promise<CpuPowerCapabilities> {
  return bridge.invoke<CpuPowerCapabilities>('cpuPower:getCapabilities')
}

/** 给进程设置 QoS 能效档位（T1：High=高性能 / Eco=能效优先 / Unset=解除） */
export function setProcessQoS(pid: number, tier: ProcessQoSTier): Promise<QoSApplyResult> {
  return bridge.invoke<QoSApplyResult>('cpuPower:setQoS', { pid, tier })
}

/** 给进程设置内存优先级（T1：0=VeryLow ~ 5=Normal，默认 5） */
export function setProcessMemoryPriority(
  pid: number,
  priority: number,
): Promise<{ success: boolean; error?: string }> {
  return bridge.invoke<{ success: boolean; error?: string }>('cpuPower:setMemoryPriority', { pid, priority })
}

/** 应用系统电源档位预设（T2，需管理员：极致性能 / 平衡 / 能效优先 / 极限省电） */
export function applyPowerProfile(profile: PowerProfile): Promise<PowerProfileApplyResult> {
  return bridge.invoke<PowerProfileApplyResult>('cpuPower:applyProfile', { profile })
}

/** 还原原始电源策略（T2，基于快照） */
export function restorePowerProfile(): Promise<PowerProfileApplyResult> {
  return bridge.invoke<PowerProfileApplyResult>('cpuPower:restoreProfile')
}

/** 查询当前电源档位（推断当前 PERFBOOSTMODE） */
export function getCurrentPowerProfile(): Promise<{
  success: boolean
  error?: string
  profile?: string
  boostMode?: number
}> {
  return bridge.invoke('cpuPower:getCurrentProfile')
}

// ═════════════════════════════════════════════════════════════════════
// T3 用户层最大权限调度补齐 — CPU Set / Priority Boost / Timer / Power Request
// 因果链：前端按钮 → pinToPCores/enableTimerResolution → cpuPower:* 桥接 →
//         C# ICpuPowerService → SetProcessDefaultCpuSet / timeBeginPeriod / PowerCreateRequest
// ═════════════════════════════════════════════════════════════════════

/** 查询系统 CPU Set 拓扑（P/E 核分布，异构 CPU 检测） */
export function getCpuSetTopology(): Promise<CpuSetTopology> {
  return bridge.invoke<CpuSetTopology>('cpuPower:getCpuSetTopology')
}

/** 把进程默认调度限制到 P-core（自动选择 schedulingClass>0 的 CPU Set） */
export function pinProcessToPCores(pid: number): Promise<CpuSetPinResult> {
  return bridge.invoke<CpuSetPinResult>('cpuPower:pinToPCores', { pid })
}

/** 把进程默认调度限制到指定 CPU Set 列表（用户手动选择） */
export function pinProcessToCpuSets(pid: number, cpuSetIds: number[]): Promise<CpuSetPinResult> {
  return bridge.invoke<CpuSetPinResult>('cpuPower:pinToCpuSets', { pid, cpuSetIds })
}

/** 清除进程的 CPU Set 限制（恢复系统默认调度） */
export function clearProcessCpuSetPinning(
  pid: number,
): Promise<{ success: boolean; error?: string; pid: number }> {
  return bridge.invoke<{ success: boolean; error?: string; pid: number }>(
    'cpuPower:clearCpuSetPinning',
    { pid },
  )
}

/** 设置进程优先级 Boost 是否禁用（true=禁用前台 boost，稳定后台调度） */
export function setProcessPriorityBoost(pid: number, disableBoost: boolean): Promise<PriorityBoostResult> {
  return bridge.invoke<PriorityBoostResult>('cpuPower:setPriorityBoost', { pid, disableBoost })
}

/** 查询进程当前的 Priority Boost 状态 */
export function getProcessPriorityBoost(pid: number): Promise<PriorityBoostResult> {
  return bridge.invoke<PriorityBoostResult>('cpuPower:getPriorityBoost', { pid })
}

/** 启用全局定时器精度（timeBeginPeriod，1ms 推荐 MC 服） */
export function enableTimerResolution(periodMs: number): Promise<TimerResolutionResult> {
  return bridge.invoke<TimerResolutionResult>('cpuPower:enableTimerResolution', { periodMs })
}

/** 禁用全局定时器精度（timeEndPeriod，恢复系统默认 15.6ms） */
export function disableTimerResolution(): Promise<TimerResolutionResult> {
  return bridge.invoke<TimerResolutionResult>('cpuPower:disableTimerResolution')
}

/** 查询当前定时器精度状态 */
export function getTimerResolutionState(): Promise<TimerResolutionResult> {
  return bridge.invoke<TimerResolutionResult>('cpuPower:getTimerResolutionState')
}

/** 启动 Power Request（防睡眠，命名化，比 SetThreadExecutionState 更可靠） */
export function startPowerRequest(reason: string): Promise<PowerRequestResult> {
  return bridge.invoke<PowerRequestResult>('cpuPower:startPowerRequest', { reason })
}

/** 停止 Power Request */
export function stopPowerRequest(): Promise<PowerRequestResult> {
  return bridge.invoke<PowerRequestResult>('cpuPower:stopPowerRequest')
}

/** 查询 Power Request 当前状态 */
export function getPowerRequestState(): Promise<PowerRequestResult> {
  return bridge.invoke<PowerRequestResult>('cpuPower:getPowerRequestState')
}

// ═════════════════════════════════════════════════════════════════════
// 通知系统 API
// ═════════════════════════════════════════════════════════════════════

export function dispatchNotification(evt: NotificationEvent): Promise<NotificationDispatchResult> {
  return bridge.invoke<NotificationDispatchResult>('notify.dispatch', evt)
}

export function testNotificationChannel(message?: string): Promise<NotificationDispatchResult> {
  return bridge.invoke<NotificationDispatchResult>('notify.test', message)
}

export function getScheduledTasks(): Promise<ScheduledTask[]> {
  return bridge.invoke<ScheduledTask[]>('scheduler.list')
}

export function addScheduledTask(task: ScheduledTask): Promise<{ success: boolean; id: string }> {
  return bridge.invoke<{ success: boolean; id: string }>('scheduler.add', task)
}

export function deleteScheduledTask(id: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('scheduler.delete', id)
}

export function runScheduledTaskNow(id: string): Promise<{ success: boolean }> {
  return bridge.invoke<{ success: boolean }>('scheduler.runNow', id)
}

export function getSchedulerHistory(maxRecords: number = 50): Promise<ExecutionRecord[]> {
  return bridge.invoke<ExecutionRecord[]>('scheduler.history', maxRecords)
}

// ═════════════════════════════════════════════════════════════════════
// 插件市场 API
// ═════════════════════════════════════════════════════════════════════
// 插件市场 Bridge API
// 注意: 后端 handler 已改为直接 return 数组/对象，
// bridge.invoke 的 resolve(data.payload) 直接就是数据本身。
// ═════════════════════════════════════════════════════════════════════

export function searchMarket(
  query: string,
  limit: number = 20,
  options?: { source?: string; serverType?: string; gameVersion?: string }
): Promise<MarketProject[]> {
  const payload: Record<string, unknown> = { query, limit }
  if (options?.source) payload.source = options.source
  if (options?.serverType) payload.serverType = options.serverType
  if (options?.gameVersion) payload.gameVersion = options.gameVersion
  return bridge.invoke<MarketProject[]>('market.search', payload)
}

export function getMarketVersions(
  projectId: string,
  source?: string
): Promise<MarketVersion[]> {
  const payload = source ? { projectId, source } : projectId
  return bridge.invoke<MarketVersion[]>('market.versions', payload)
}

export function installPlugin(
  version: MarketVersion,
  serverPath: string
): Promise<InstallResult> {
  return bridge.invoke<InstallResult>('market.install', { version, serverPath })
}

export function getInstalledPlugins(serverPath: string): Promise<InstalledPlugin[]> {
  return bridge.invoke<InstalledPlugin[]>('market.listInstalled', serverPath)
}

// ─── 启动脚本 API ───

export function detectStartupScript(knownServerId: string): Promise<{
  success: boolean
  startup?: StartupConfig
  error?: string
}> {
  return bridge.invoke('server:detectStartupScript', { knownServerId })
}

export function setStartupMode(knownServerId: string, mode: StartupMode): Promise<{
  success: boolean
}> {
  return bridge.invoke('server:setStartupMode', { knownServerId, mode })
}

export function setScriptPath(knownServerId: string, scriptPath: string): Promise<{
  success: boolean
  startup?: StartupConfig
  error?: string
}> {
  return bridge.invoke('server:setScriptPath', { knownServerId, scriptPath })
}

export function reparseScript(knownServerId: string): Promise<{
  success: boolean
  startup?: StartupConfig
  diff?: DiffReport
  error?: string
}> {
  return bridge.invoke('server:reparseScript', { knownServerId })
}
