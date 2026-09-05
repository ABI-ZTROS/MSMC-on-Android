// ─────────────────────────────────────────────────────────────────────
// 桥接消息基础类型
// ─────────────────────────────────────────────────────────────────────

export type BridgeMessageType = 'request' | 'response' | 'event' | 'log'

export interface BridgeMessage {
  type: BridgeMessageType
  id?: string
  action: string
  payload?: unknown
  error?: string
  success?: boolean
  timestamp?: number
}

// ─────────────────────────────────────────────────────────────────────
// 应用通用类型
// ─────────────────────────────────────────────────────────────────────

export interface AppInfo {
  version: string
  name: string
  fullName: string
}

export interface ThemeInfo {
  mode: 'light' | 'dark'
  primaryColor: string
}

export interface AppReadyEvent {
  version: string
  isAdmin: boolean
  theme: ThemeInfo
  statusMessage?: string
}

export interface StatusUpdateEvent {
  message: string
}

// ─────────────────────────────────────────────────────────────────────
// 系统监控类型
// ─────────────────────────────────────────────────────────────────────

export interface SystemMetrics {
  timestamp: number
  cpuUsagePercent: number
  memoryUsagePercent: number
  diskUsagePercent: number
  totalMemoryBytes: number
  usedMemoryBytes: number
  diskTotalBytes: number
  diskUsedBytes: number
  diskName: string
  totalThreadCount: number
  javaCpuUsagePercent: number
  javaWorkingSetBytes: number
  javaThreadCount: number
  perCoreCpuUsages: number[]
  isMonitoring: boolean
  memoryInfoText: string
  diskInfoText: string
}

export interface CpuInfo {
  modelName: string
  manufacturer: string
  physicalCores: number
  logicalCores: number
  socketCount: number
  numaNodeCount: number
  isHyperThreadingEnabled: boolean
  logicalToPhysicalCoreMap: number[]
  isRecognized: boolean
}

export interface HistoryPoint {
  timestamp: number
  cpuUsagePercent: number
  memoryUsagePercent: number
}

export interface HistoryRangeResult {
  points: HistoryPoint[]
  days: number
}

// ─────────────────────────────────────────────────────────────────────
// 服务器类型
// ─────────────────────────────────────────────────────────────────────

export interface ServerInfo {
  processId: number
  serverType: string
  workingDirectory: string
  serverJarPath: string
  serverJarName: string
  javaPath: string
  fullCommandLine: string
  serverPort: number
  isPortOpen: boolean
  portConflict: string
  displayName: string
  status: string
  maxHeapMemoryBytes: number
  initialHeapMemoryBytes: number
  usesAikarFlags: boolean
  gcType: string
  configFiles: string[]
  networkStatusText: string
  formattedMaxMemory: string
  lastSeenAt?: string
  isKnown?: boolean
  // Q3: 选中服务器的关联「已知服务器」ID。可空。
  // 仅当服务器在后端被成功关联到 KnownServers 列表时才填充，未关联时不传。
  knownServerId?: string
  // ── 监管运行时状态（可空，未被监管时不传）──
  isSupervised?: boolean
  crashCount?: number
  // 崩溃后计划下次重启的 ISO 时间戳；非重启等待阶段不传
  scheduledRestartAt?: string
  // 当前物理工作集（Working Set）字节数
  currentWorkingSetBytes?: number
  // 近几秒采样的 CPU 百分比 (0-100)，未监控时不传
  cpuPercent?: number
  // 监管内的进程优先级，对齐 ProcessPriorityClass 字符串
  supervisedPriority?: 'Idle' | 'BelowNormal' | 'Normal' | 'AboveNormal' | 'High' | 'RealTime'
}

export interface KnownServerInfo {
  // 原始字段 id 做兼容；统一推荐使用 knownServerId，与 ServerInfo 命名一致
  id?: string
  knownServerId: string
  name: string
  serverJarPath: string
  workingDirectory: string
  javaPath?: string
  port: number
  initialHeapMemoryBytes?: number
  maxHeapMemoryBytes?: number
  group?: string
  isFavorite?: boolean
  addedAt?: string
  lastSeenAt: string
  status?: string
  // ── 监管运行时状态（可空，未被监管时不传）──
  isSupervised?: boolean
  crashCount?: number
  scheduledRestartAt?: string
  currentWorkingSetBytes?: number
  cpuPercent?: number
  supervisedPriority?: 'Idle' | 'BelowNormal' | 'Normal' | 'AboveNormal' | 'High' | 'RealTime'
}

export interface ServerListResponse {
  running: ServerInfo[]
  known: KnownServerInfo[]
  isBusy: boolean
  isAutoDetectEnabled: boolean
}

// ─────────────────────────────────────────────────────────────────────
// 网络监控类型
// ─────────────────────────────────────────────────────────────────────

export interface NetworkStatus {
  totalPorts: number
  usedPorts: number
  usedPercentage: number
  systemPorts: number
  registeredPorts: number
  dynamicPorts: number
  uploadSpeedMB: number
  downloadSpeedMB: number
  speedMaximumMB: number
  uploadSpeedText: string
  downloadSpeedText: string
  todayUploadText: string
  todayDownloadText: string
  dailyAnalysisText: string
  isRefreshing: boolean
  currentHour: number
}

export interface PortInfo {
  port: number
  protocol: string
  processId: number | null
  processName: string
  isOpen: boolean
  portRange: 'System' | 'Registered' | 'Dynamic'
}

export interface PortsResponse {
  ports: PortInfo[]
  count: number
}

export interface BridgeRule {
  listenAddress: string
  listenPort: number
  connectAddress: string
  connectPort: number
  protocol: string
  engine: string
}

export interface BridgeRulesResponse {
  rules: BridgeRule[]
  count: number
}

export interface CommonPortInfo {
  port: number
  name: string
  description: string
  category: string
}

export interface AddBridgeRequest {
  listenAddress: string
  listenPort: number
  connectAddress: string
  connectPort: number
  addFirewall: boolean
  protocol?: string
}

export interface KillProcessRequest {
  port: number
  protocol: string
}

export interface HourlyHistoryResponse {
  upload: number[]
  download: number[]
}

// ─────────────────────────────────────────────────────────────────────
// 配置编辑类型
// ─────────────────────────────────────────────────────────────────────

export interface ConfigFileItem {
  fileName: string
  fullPath: string
  relativePath: string
  isDirectory: boolean
  children: ConfigFileItem[]
}

export interface ConfigFileTreeResponse {
  tree: ConfigFileItem[]
  count: number
  configFileCountText: string
  hasServerDirectory: boolean
  serverWorkingDirectory: string
  selectedServerName: string | null
}

export interface AvailableServer {
  displayName: string
  workingDirectory: string
  serverJarName: string
  serverJarPath: string
  serverPort: number
}

export interface AvailableServersResponse {
  servers: AvailableServer[]
}

export interface ConfigEntry {
  key: string
  value: string
  originalValue: string
  displayName: string
  friendlyDisplayName: string
  description: string
  isModified: boolean
  isValid: boolean
  errorMessage: string | null
  requiresRestart: boolean
  isBoolType: boolean
  isEnumType: boolean
  isNumericType: boolean
  isStringType: boolean
  allowedValues: string[] | null
  minValue: number | null
  maxValue: number | null
  valueType: string
}

export interface ConfigEntryGroup {
  key: string
  items: ConfigEntry[]
}

export interface ConfigEntriesResponse {
  groups: ConfigEntryGroup[]
  totalCount: number
  hasUnsavedChanges: boolean
  isLoading: boolean
  loadProgress: number
  selectedConfigFile: string | null
  selectedConfigFileName: string | null
  saveStatusMessage: string | null
  isSaveError: boolean
  isCurrentServerRunning?: boolean
  modifiedCount?: number
}

export interface UpdateConfigValueRequest {
  key: string
  value: string
}

export interface ConfigSaveResult {
  success: boolean
  message: string | null
  requiresRestart?: boolean
  errorType?: string
  errorDetail?: string
}

// ─────────────────────────────────────────────────────────────────────
// 设置类型
// ─────────────────────────────────────────────────────────────────────

export interface ProcessSupervisorPolicy {
  enableCrashRestart: boolean
  maxRestartAttemptsPerHour: number
  restartCooldownSeconds: number
  preventSystemSleepWhenRunning: boolean
  // 对应 System.Diagnostics.ProcessPriorityClass，枚举化为字符串方便前端显示/选择
  processPriority: 'Idle' | 'BelowNormal' | 'Normal' | 'AboveNormal' | 'High' | 'RealTime'
  maxProcessMemoryBytes: number
  maxTotalRestartAttempts: number
}

export interface SettingsData {
  primaryColorHex: string
  accentColorHex: string
  backgroundColorHex: string
  cardColorHex: string
  textColorHex: string
  borderColorHex: string
  // ✅ 12 色体系新增 6 个语义色字段（#RRGGBB）
  successColorHex: string
  warningColorHex: string
  errorColorHex: string
  gaugeGreenColorHex: string
  gaugeYellowColorHex: string
  gaugeRedColorHex: string
  cornerRadius: number
  animationDuration: number
  enableAnimations: boolean
  enableWindowsNotifications: boolean
  preferJavaw: boolean
  statusMessage: string
  isDarkMode: boolean
  // ✅ 进程监管策略（崩溃重启 + 防睡眠 + 优先级 + 内存上限），本地 localStorage 持久化
  supervisor: ProcessSupervisorPolicy
}

export interface JavaInstallationInfo {
  javaPath: string
  javaHome: string
  versionString: string
  versionDisplay: string
  isDefault: boolean
  isCustom: boolean
}

export interface JavaListResponse {
  javas: JavaInstallationInfo[]
  isScanning: boolean
  selectedJava: string | null
}

export type ThemePreset =
  | 'ColorOSBlue'
  | 'FurinaBlue'
  | 'Dragonfruit'
  | 'GreenApple'
  | 'BloodRed'
  | 'SunsetYellow'
  | 'PrecePurple'

export interface ThemeApplyResult {
  success: boolean
  primaryColorHex: string
  accentColorHex?: string
  isDarkMode?: boolean
  enableAnimations?: boolean
}

export interface SwatchInfo {
  color: string
  label: string
}

export interface PresetInfo {
  key: ThemePreset
  label: string
  primary: string
  accent: string
}

export interface SwatchesResponse {
  swatches: SwatchInfo[]
}

export interface PresetsResponse {
  presets: PresetInfo[]
}

// ─────────────────────────────────────────────────────────────────────
// 关于页面 - 团队信息类型
// ─────────────────────────────────────────────────────────────────────

export interface TeamMember {
  name: string
  role: string
  github?: string
  avatar?: string
  note?: string
  isClickable?: boolean
  hasHeartIcon?: boolean
  hasCrossIcon?: boolean
  isMemorial?: boolean
  description?: string
}

export interface TeamInfoResponse {
  primaryDevelopers: TeamMember[]
  specialThanks: TeamMember[]
  memorial: TeamMember[]
  contributors: TeamMember[]
}

// ─────────────────────────────────────────────────────────────────────
// JVM 参数类型
// ─────────────────────────────────────────────────────────────────────

export type JvmArgumentValueType =
  | 'None'
  | 'Number'
  | 'MemorySize'
  | 'BooleanFlag'
  | 'String'
  | 'Enum'

export type JvmArgumentCategory =
  | 'Memory'
  | 'GarbageCollection'
  | 'Performance'
  | 'Encoding'
  | 'Security'
  | 'Debug'
  | 'ServerBehavior'
  | 'Other'

export interface JvmArgumentDefinition {
  flag: string
  name: string
  description: string
  valueType: JvmArgumentValueType
  category: JvmArgumentCategory
  defaultValue: string | null
  minimumValue: string | null
  maximumValue: string | null
  allowedValues: string[] | null
  recommended: boolean
  warning: string | null
  requiresExperimentalUnlock: boolean
}

export interface JvmDefinitionsResponse {
  definitions: JvmArgumentDefinition[]
}

export interface JvmStateResponse {
  hasServer: boolean
  isKnownServer: boolean
  isRunning: boolean
  initialMemory: string
  maxMemory: string
  selectedArguments: string[]
}

export interface JvmUpdateArgumentRequest {
  oldArg: string
  newValue: string
}

export interface JvmSetMemoryRequest {
  initial?: string
  max?: string
}

export type JvmPresetType = 'aikar' | 'g1gc' | 'zgc'

// ─────────────────────────────────────────────────────────────────────
// 进程管理类型
// ─────────────────────────────────────────────────────────────────────

export interface ProcessAffinityInfo {
  processId: number
  processName: string
  isMinecraftServer: boolean
  isJavaProcess: boolean
  isSystemProcess: boolean
  displayName: string
  affinityMask: number
  allowedCoreIndices: number[]
  cpuUsagePercent: number
  workingSetBytes: number
  threadCount: number
  priorityClass: string
  commandLine: string
}

export interface KillProcessByIdRequest {
  pid: number
  graceful?: boolean
}

// ─────────────────────────────────────────────────────────────────────
// CPU 电源与调度管控（T1 QoS + T2 电源档位）
// ─────────────────────────────────────────────────────────────────────

/** 进程 QoS 能效档位（对应 Windows EcoQoS） */
export type ProcessQoSTier = 'High' | 'Eco' | 'Unset'

/** 系统电源档位预设 */
export type PowerProfile = 'UltimatePerformance' | 'Balanced' | 'Efficient' | 'PowerSaver'

/** 平台能力查询结果 */
export interface CpuPowerCapabilities {
  success: boolean
  error?: string
  supportsEcoQoS: boolean
  supportsMemoryPriority: boolean
  isAdmin: boolean
  canModifyPowerProfile: boolean
  currentProfileName: string
  currentBoostMode: number
  hasPendingCrashSnapshot: boolean
}

/** QoS 应用结果 */
export interface QoSApplyResult {
  success: boolean
  error?: string
  appliedTier?: string
}

/** 电源档位应用结果 */
export interface PowerProfileApplyResult {
  success: boolean
  error?: string
  appliedProfile?: string
}

// ═══════════════════════════════════════════════════════════════════════════
// T3 用户层最大权限调度补齐 — CPU Set / Priority Boost / Timer / Power Request
// ═══════════════════════════════════════════════════════════════════════════

/** 一个 CPU Set 的描述（P-core 组或 E-core 组） */
export interface CpuSetInfo {
  id: number                       // CPU Set ID（用于 pinToCpuSets）
  group: number                    // NUMA 组
  logicalProcessorIndex: number    // 组内逻辑处理器序号
  coreIndex: number                // 物理核序号
  logicalProcessorCount: number    // 本 Set 中的逻辑处理器数
  coreCount: number                // 本 Set 中的物理核数
  schedulingClass: number          // 0=E-core，>0=P-core（值越大越偏性能）
  isParked: boolean                // 是否已停泊
  isPerformanceCore: boolean       // 推断：schedulingClass>0 → P-core
}

/** 系统 CPU Set 拓扑查询结果 */
export interface CpuSetTopology {
  success: boolean
  error?: string
  isHybridCpu: boolean             // 是否为异构 CPU（同时有 P-core 和 E-core）
  totalCpuSets: number
  performanceCpuSetCount: number   // P-core Set 数量
  efficiencyCpuSetCount: number    // E-core Set 数量
  cpuSets: CpuSetInfo[]
  performanceCpuSetIds: number[]   // P-core Set ID 列表（用于一键锁定）
  efficiencyCpuSetIds: number[]
}

/** CPU Set 路由应用结果 */
export interface CpuSetPinResult {
  success: boolean
  error?: string
  pid: number
  appliedCpuSetIds: number[]
  pinnedToPCores: boolean
}

/** Priority Boost 查询/设置结果 */
export interface PriorityBoostResult {
  success: boolean
  error?: string
  pid: number
  disablePriorityBoost: boolean    // true=已禁用前台 boost（后台服推荐）
}

/** 定时器精度设置结果 */
export interface TimerResolutionResult {
  success: boolean
  error?: string
  periodMs: number                 // 当前定时器精度（毫秒）
  enabled: boolean                 // 是否已启用
}

/** Power Request（防睡眠）操作结果 */
export interface PowerRequestResult {
  success: boolean
  error?: string
  reason: string                   // 防睡眠原因（命名化）
  active: boolean                  // 是否活跃
}

export interface SetAffinityRequest {
  pid: number
  affinityMask: number
}

// ─────────────────────────────────────────────────────────────────────
// 通知系统类型
// ─────────────────────────────────────────────────────────────────────

export type NotificationEventType =
  | 'ServerStarted'
  | 'ServerStopped'
  | 'ServerCrashed'
  | 'BackupCompleted'
  | 'BackupFailed'
  | 'PluginInstalled'
  | 'PluginUpdateAvailable'
  | 'ScheduleCompleted'
  | 'ManualTest'
  | 'SystemAlert'

export interface NotificationEvent {
  id?: string
  eventType: NotificationEventType
  title: string
  message: string
  sourceModule?: string
  targetServerId?: string
}

export interface NotificationChannelConfig {
  discord?: {
    enabled: boolean
    webhookUrl: string
    botUsername?: string
    avatarUrl?: string
  }
  genericWebhook?: {
    enabled: boolean
    url: string
    authorizationHeader?: string
  }
  email?: {
    enabled: boolean
    smtpHost: string
    smtpPort: number
    username: string
    password: string
    fromAddress: string
    toAddresses: string
    useTls: boolean
  }
  windowsToast?: {
    enabled: boolean
  }
  retryMaxAttempts: number
  retryBaseDelayMs: number
}

export interface NotificationDispatchResult {
  eventId: string
  totalChannels: number
  successfulChannels: number
  channelResults?: Record<string, boolean>
  isSuccess: boolean
}

// ─────────────────────────────────────────────────────────────────────
// 调度系统类型
// ─────────────────────────────────────────────────────────────────────

export type TriggerType = 'Interval' | 'Cron' | 'OneTime'
export type ActionType = 'SendNotification' | 'RunCommand' | 'Backup'
export type TaskStatus = 'Idle' | 'Running' | 'Completed' | 'Failed'

export interface TriggerConfig {
  type: TriggerType
  interval?: string
  cronExpression?: string
  oneTimeAt?: string
}

export interface ActionConfig {
  type: ActionType
  commandOrPath?: string
}

export interface ScheduledTask {
  id: string
  name: string
  enabled: boolean
  trigger: TriggerConfig
  action: ActionConfig
  maxConsecutiveFailures: number
  consecutiveFailures: number
  totalRunCount: number
  nextRunTime?: string
  lastRunTime?: string
  lastStatus?: TaskStatus
}

export interface ExecutionRecord {
  taskId: string
  taskName: string
  status: TaskStatus
  startedAt: string
  completedAt?: string
  duration?: string
  errorMessage?: string
}

// ─────────────────────────────────────────────────────────────────────
// 插件市场类型
// ─────────────────────────────────────────────────────────────────────

export interface MarketSearchRequest {
  query: string
  limit: number
}

export interface MarketProject {
  id: string
  slug?: string
  name: string
  description?: string
  author?: string
  iconUrl?: string
  downloads?: number
  followers?: number
  source?: string            // "Modrinth" | "Hangar" | "Spiget"
  supportedLoaders?: string[]
  gameVersions?: string[]
  categories?: string[]
  updatedAt?: string
}

export interface MarketVersion {
  id: string
  projectId: string
  versionNumber: string
  name: string
  changelog?: string
  releasedAt?: string        // ISO 8601 date string
  isPreRelease?: boolean
  gameVersions?: string[]
  loaders?: string[]
  downloadUrl?: string
  sha1Hash?: string
  fileSize?: number
}

export interface MarketFileInfo {
  fileName: string
  fileUrl: string
  fileSize: number
  sha1Hash?: string
}

export interface InstalledPlugin {
  id: string
  projectId: string
  projectName: string
  version: string
  installedAt: string
  backupPath?: string
  serverPath: string
}

export interface InstallResult {
  success: boolean
  error?: string
  projectId: string
  projectName?: string
  version: string
  installedAt: string
  backupPath?: string
}

// ─── 启动脚本类型 ───

export type StartupMode = 'Manual' | 'Script'

export interface StartupConfig {
  mode: StartupMode
  scriptPath?: string
  scriptName?: string
  lastParseTime?: string
  hasAutoRestart: boolean
  jvmArgs: string[]
  jarPath?: string
  maxHeapBytes: number
  initialHeapBytes: number
}

export interface DiffReport {
  jarPathChanged: boolean
  heapMaxFrom?: string
  heapMaxTo?: string
  heapInitFrom?: string
  heapInitTo?: string
  jvmArgsAdded: string[]
  jvmArgsRemoved: string[]
}

// ─── KnownServer 扩展（支持 startup 字段） ───

export interface KnownServerWithStartup {
  id: string
  name: string
  workingDirectory?: string
  startup?: StartupConfig | null
}
