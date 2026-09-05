import { useEffect, useState, useMemo, useCallback, useRef } from 'react'
import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'
import {
  FaBolt,
  FaMemory,
  FaShieldHalved,
  FaRotate,
  FaServer,
} from 'react-icons/fa6'
import { Reveal } from '@/components/ui/Reveal'
import { IconByName } from '@/utils/icons'
import { useToastStore } from '@/stores/toastStore'
import {
  getBridge,
  getServerList,
  getSelectedServer,
  selectServer,
  getJvmDefinitions,
  getJvmState,
  addJvmArgument,
  removeJvmArgument,
  updateJvmArgument,
  setJvmMemory,
  applyJvmPreset,
  addCustomJvmArgument,
  setProcessQoS,
  pinProcessToPCores,
  setProcessPriorityBoost,
  enableTimerResolution,
  disableTimerResolution,
  getTimerResolutionState,
} from '@/utils/bridge'
import type { ProcessQoSTier } from '@/types/bridge'

const bridge = getBridge()

/**
 * 应用用户在设置页配置的 QoS 能效标签到刚启动的服务器进程。
 * 因果链：handleStart 成功 → fetchServerList 拿到新 PID → applyServerQoS →
 *         setProcessQoS(pid, tier) → cpuPower:setQoS → ICpuPowerService.SetProcessQoS →
 *         SetProcessInformation(ProcessPowerThrottling)
 */
const applyServerQoS = async (pid: number): Promise<void> => {
  if (!pid || pid <= 0) return
  let tier: ProcessQoSTier = 'High'
  try {
    const saved = localStorage.getItem('msmc_server_qos')
    if (saved === 'High' || saved === 'Eco' || saved === 'Unset') tier = saved
  } catch { /* ignore */ }
  if (tier === 'Unset') return // 用户选择系统默认，不干预
  try {
    await setProcessQoS(pid, tier)
  } catch (e) {
    // QoS 应用失败不阻断主流程，仅记录
    console.warn('[QoS] 应用失败:', e)
  }
}

/**
 * 应用 T3 用户层最大权限调度策略到刚启动的服务器进程：
 *   - CPU Set P-core 路由（仅当用户在设置页启用 + 检测到异构 CPU）
 *   - Priority Boost 禁用（仅当用户选择 'disable'）
 *
 * 因果链：handleStart 成功 → fetchServerList 拿到新 PID → applyServerTuning →
 *         pinProcessToPCores(pid) → cpuPower:pinToPCores → ICpuPowerService.PinProcessToPCores →
 *         SetProcessDefaultCpuSet(handle, P-core CPU Set IDs)
 *         setProcessPriorityBoost(pid, true) → cpuPower:setPriorityBoost →
 *         SetProcessPriorityBoost(handle, disable=true)
 */
const applyServerTuning = async (pid: number): Promise<void> => {
  if (!pid || pid <= 0) return

  // 1. P-core 路由（仅当用户启用）
  try {
    const autoPin = localStorage.getItem('msmc_auto_pin_pcores') === 'true'
    if (autoPin) {
      const r = await pinProcessToPCores(pid)
      if (!r.success) {
        console.warn('[T3] P-core 路由失败:', r.error)
      } else if (r.pinnedToPCores) {
        console.info(`[T3] 已将 PID=${pid} 路由到 P-core (${r.appliedCpuSetIds.length} 个 CPU Set)`)
      }
    }
  } catch (e) {
    console.warn('[T3] P-core 路由异常:', e)
  }

  // 2. Priority Boost 禁用（仅当用户选择 'disable'）
  try {
    const boostMode = localStorage.getItem('msmc_server_boost')
    if (boostMode === 'disable') {
      const r = await setProcessPriorityBoost(pid, true)
      if (!r.success) {
        console.warn('[T3] Priority Boost 设置失败:', r.error)
      }
    }
  } catch (e) {
    console.warn('[T3] Priority Boost 异常:', e)
  }
}

/**
 * 启动服务器时按用户配置启用 winmm 定时器精度（1ms）。
 * 因果链：handleStart 成功 → ensureTimerResolution →
 *         getTimerResolutionState() 检查当前状态 → enableTimerResolution(periodMs) →
 *         cpuPower:enableTimerResolution → ICpuPowerService.EnableTimerResolution →
 *         timeBeginPeriod(periodMs)
 *
 * 全局状态：定时器精度是系统级的，只需启用一次；多个服务器启动不会重复调用
 * （EnableTimerResolution 内部会先 timeEndPeriod 旧的再 timeBeginPeriod 新的）。
 */
const ensureTimerResolution = async (): Promise<void> => {
  try {
    const tierStr = localStorage.getItem('msmc_timer_tier')
    const tier = tierStr ? Number(tierStr) : 0
    if (tier <= 0) return // 用户选择系统默认，不干预

    // 检查当前状态，避免无谓调用
    const state = await getTimerResolutionState()
    if (state.enabled && state.periodMs === 1) return // 已经是 1ms，无需重复

    const r = await enableTimerResolution(1)
    if (!r.success) {
      console.warn('[T3] 启用定时器精度失败:', r.error)
    } else {
      console.info('[T3] 定时器精度已启用 1ms（MC TPS 抖动优化）')
    }
  } catch (e) {
    console.warn('[T3] 定时器精度异常:', e)
  }
}

/**
 * 启动服务器成功后统一应用 T1/T3 调度策略（handleStart 和 startKnown 共用）。
 * 因果链：启动成功 → getSelectedServer 拿到新 PID → applyServerQoS + applyServerTuning →
 *         ensureTimerResolution（全局定时器精度）
 *
 * 任何子步骤失败都仅 console.warn，不阻断主流程、不抛异常。
 */
const applyServerSchedulingPolicies = async (): Promise<void> => {
  try {
    const fresh = await getSelectedServer()
    if (fresh?.processId && fresh.processId > 0) {
      await applyServerQoS(fresh.processId)
      await applyServerTuning(fresh.processId)
    }
  } catch { /* 调度策略应用失败不阻断主流程 */ }
  // 全局定时器精度（与 PID 无关，系统级）
  await ensureTimerResolution()
}

/**
 * 停止服务器后检查：若已无任何运行中服务器，撤销 winmm 定时器精度
 * （恢复系统默认 15.6ms），避免空闲时持续高精度 tick 浪费功耗。
 *
 * 因果链：handleStop 成功 → getServerList 检查 running.length === 0 →
 *         disableTimerResolution → cpuPower:disableTimerResolution →
 *         ICpuPowerService.DisableTimerResolution → timeEndPeriod
 */
const maybeDisableTimerResolution = async (): Promise<void> => {
  try {
    const tierStr = localStorage.getItem('msmc_timer_tier')
    const tier = tierStr ? Number(tierStr) : 0
    if (tier <= 0) return // 用户未启用定时器精度，无需撤销

    // 拉取最新服务器列表，检查是否还有运行中的服务器
    const list = await getServerList()
    if (list.running && list.running.length === 0) {
      const r = await disableTimerResolution()
      if (r.success) {
        console.info('[T3] 所有服务器已停止，定时器精度已撤销（恢复 15.6ms）')
      } else {
        console.warn('[T3] 撤销定时器精度失败:', r.error)
      }
    }
  } catch (e) {
    console.warn('[T3] 撤销定时器精度异常:', e)
  }
}

function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs))
}

// ─── 辅助：把字节数格式化为人类可读（B/KB/MB/GB/TB）───
function formatBytes(bytes?: number | null, digits = 1): string {
  if (bytes == null || Number.isNaN(bytes) || bytes === 0) return '—'
  const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
  let i = 0
  let n = bytes as number
  while (n >= 1024 && i < units.length - 1) {
    n /= 1024
    i++
  }
  return `${n.toFixed(digits)} ${units[i]}`
}

// ─── 辅助：显示「崩溃后计划重启的倒计时」───
function useRestartCountdown(iso?: string | null): { text: string; remainingMs: number } | null {
  const [now, setNow] = useState<number>(() => Date.now())
  useEffect(() => {
    if (!iso) return
    const t = setInterval(() => setNow(Date.now()), 250)
    return () => clearInterval(t)
  }, [iso])
  if (!iso) return null
  const target = new Date(iso).getTime()
  if (Number.isNaN(target)) return null
  const remainingMs = Math.max(0, target - now)
  const totalSec = Math.ceil(remainingMs / 1000)
  const mm = Math.floor(totalSec / 60).toString().padStart(2, '0')
  const ss = (totalSec % 60).toString().padStart(2, '0')
  return { text: `${mm}:${ss}`, remainingMs }
}

// ─── 子组件：监管角标（盾牌 + 崩溃数 badge + 倒计时 + 内存/CPU 微指标）───

interface SupervisorBadgeProps {
  server: {
    isSupervised?: boolean
    crashCount?: number
    scheduledRestartAt?: string | null
    currentWorkingSetBytes?: number | null
    cpuPercent?: number | null
    supervisedPriority?: string | null
  }
  size?: 'sm' | 'md'
}

function SupervisorBadge({ server, size = 'sm' }: SupervisorBadgeProps): JSX.Element | null {
  if (!server.isSupervised) return null

  const countdown = useRestartCountdown(server.scheduledRestartAt)
  const crashCount = server.crashCount ?? 0
  const priority = server.supervisedPriority ?? undefined
  const priorityShort: Record<string, string> = {
    Idle: 'IDLE',
    BelowNormal: 'BELOW',
    Normal: 'NORM',
    AboveNormal: 'ABOVE',
    High: 'HIGH',
    RealTime: 'RT',
  }

  const iconSize = size === 'sm' ? 11 : 13
  const pillBase = cn(
    'inline-flex items-center gap-1 rounded-full px-1.5',
    size === 'sm' ? 'h-5 text-[10px]' : 'h-6 text-[11px]',
    'font-semibold tracking-tight',
    crashCount > 0
      ? 'text-white'
      : countdown
        ? 'text-[var(--md-warning-contrast,white)]'
        : 'text-white',
  )
  const pillBg = crashCount > 0
    ? { background: 'linear-gradient(135deg, var(--md-danger, #EF4444), var(--md-danger-hue-mid, #B91C1C))' }
    : countdown
      ? { background: 'linear-gradient(135deg, var(--md-warning, #F59E0B), var(--md-warning-hue-mid, #D97706))' }
      : { background: 'linear-gradient(135deg, var(--md-accent, #6366F1), var(--md-accent-hue-mid, #4F46E5))' }

  return (
    <div className="flex items-center gap-1.5 flex-wrap" style={{ marginLeft: 4 }}>
      {/* 盾牌 + 崩溃数 */}
      <div className={pillBase} style={pillBg} title={`已被监管${crashCount > 0 ? `，累计崩溃 ${crashCount} 次` : '（Job Object + 崩溃重启 + 防睡眠 + 优先级）'}`}>
        <FaShieldHalved size={iconSize - 1} />
        {crashCount > 0 && <span>×{crashCount}</span>}
      </div>

      {/* 重启倒计时 */}
      {countdown && (
        <div
          className={cn(
            'inline-flex items-center gap-1 rounded-full px-1.5 h-5 text-[10px] font-semibold tracking-tight text-white md-pulse-soft',
          )}
          style={{
            background:
              'linear-gradient(135deg, var(--md-warning, #F59E0B), var(--md-accent, #6366F1))',
          }}
          title={`崩溃中，预计 ${countdown.text} 后自动重启`}
        >
          <FaRotate size={9} />
          <span>{countdown.text}</span>
        </div>
      )}

      {/* 实时 Working Set */}
      {(server.currentWorkingSetBytes ?? 0) > 0 && (
        <div
          className="inline-flex items-center gap-1 rounded-full px-1.5 h-5 text-[10px] font-medium"
          style={{
            background: 'color-mix(in srgb, var(--md-info) 15%, transparent)',
            color: 'var(--md-info-contrast, #075985)',
          }}
          title={`当前物理内存 Working Set = ${formatBytes(server.currentWorkingSetBytes, 2)}`}
        >
          <FaMemory size={9} />
          <span>{formatBytes(server.currentWorkingSetBytes, 1)}</span>
        </div>
      )}

      {/* 实时 CPU% */}
      {(server.cpuPercent ?? -1) >= 0 && (
        <div
          className="inline-flex items-center gap-1 rounded-full px-1.5 h-5 text-[10px] font-medium"
          style={{
            background: 'color-mix(in srgb, var(--md-success) 15%, transparent)',
            color: 'var(--md-success-contrast, #065F46)',
          }}
          title={`近 1 秒 CPU 占用 ≈ ${Number(server.cpuPercent).toFixed(1)}%`}
        >
          <FaBolt size={9} />
          <span>{Number(server.cpuPercent).toFixed(0)}%</span>
        </div>
      )}

      {/* 优先级标签（非 Normal 才显示，避免冗余） */}
      {priority && priority !== 'Normal' && (
        <div
          className="inline-flex items-center rounded-full px-1.5 h-5 text-[10px] font-bold tracking-tight"
          style={{
            background:
              priority === 'High' || priority === 'RealTime'
                ? 'color-mix(in srgb, var(--md-danger) 18%, transparent)'
                : 'color-mix(in srgb, var(--md-primary) 18%, transparent)',
            color:
              priority === 'High' || priority === 'RealTime'
                ? 'var(--md-danger, #B91C1C)'
                : 'var(--md-primary, #1D4ED8)',
          }}
          title={`进程优先级 = ${priority}`}
        >
          <FaServer size={8} style={{ marginRight: 3 }} />
          {priorityShort[priority] ?? priority}
        </div>
      )}
    </div>
  )
}
import type {
  ServerInfo,
  KnownServerInfo,
  ServerListResponse,
  JvmArgumentDefinition,
  JvmArgumentCategory,
  JvmStateResponse,
} from '@/types/bridge'

// ─── 辅助函数 ───

// 状态点颜色：依据端口冲突 / 端口开放状态决定（与 WPF DataTrigger 一致）
function getRunningStatusDot(server: ServerInfo): string {
  const conflictStr = String(server.portConflict ?? '').toLowerCase()
  if (conflictStr === 'true' || conflictStr === '1') return 'md-status-dot-yellow'
  if (!server.isPortOpen) return 'md-status-dot-red'
  return 'md-status-dot-green'
}

// ─── JVM 参数辅助函数 ───

// 分类中文名称映射
const categoryLabels: Record<JvmArgumentCategory, string> = {
  Memory: '内存',
  GarbageCollection: '垃圾回收',
  Performance: '性能调优',
  Encoding: '编码',
  Security: '安全',
  Debug: '调试',
  ServerBehavior: '服务器行为',
  Other: '其他',
}

// 从完整参数字符串中提取基础名（去掉值部分）
function getArgBaseName(arg: string): string {
  if (!arg) return ''
  // -XX:+UseG1GC / -XX:-UseG1GC -> -XX:UseG1GC (BooleanFlag)
  if (arg.startsWith('-XX:+') || arg.startsWith('-XX:-')) {
    return '-XX:' + arg.slice(5)
  }
  // -XX:MaxGCPauseMillis=200 -> -XX:MaxGCPauseMillis=
  if (arg.startsWith('-XX:') && arg.includes('=')) {
    return arg.substring(0, arg.indexOf('=') + 1)
  }
  // -Xmx4G -> -Xmx
  if (arg.startsWith('-Xmx') || arg.startsWith('-Xms') || arg.startsWith('-Xss')) {
    return arg.substring(0, 4)
  }
  // -Dfile.encoding=UTF-8 -> -Dfile.encoding=
  if (arg.startsWith('-D') && arg.includes('=')) {
    return arg.substring(0, arg.indexOf('=') + 1)
  }
  return arg
}

// 从完整参数字符串中提取值
function getArgValue(arg: string): string {
  if (!arg) return ''
  if (arg.startsWith('-XX:+')) return 'true'
  if (arg.startsWith('-XX:-')) return 'false'
  if (arg.startsWith('-XX:') && arg.includes('=')) {
    return arg.substring(arg.indexOf('=') + 1)
  }
  if (arg.startsWith('-Xmx') || arg.startsWith('-Xms') || arg.startsWith('-Xss')) {
    return arg.substring(4)
  }
  if (arg.startsWith('-D') && arg.includes('=')) {
    return arg.substring(arg.indexOf('=') + 1)
  }
  return ''
}

// 根据参数定义和值构建完整参数字符串
function buildFullArg(def: JvmArgumentDefinition, value: string): string {
  if (def.valueType === 'BooleanFlag') {
    const enabled = value === 'true' || value === '+'
    return def.flag.replace(/[-+]$/, enabled ? '+' : '-')
  }
  if (def.valueType === 'None') {
    return def.flag
  }
  // 带值的参数
  const base = def.flag.endsWith('=') ? def.flag : def.flag + '='
  return base + value
}

// ─── 子组件：运行中服务器列表项 ───

interface RunningItemProps {
  server: ServerInfo
  isSelected: boolean
  onSelect: () => void
  onStop: () => void
  isBusy?: boolean
}

function RunningServerItem({ server, isSelected, onSelect, onStop, isBusy }: RunningItemProps): JSX.Element {
  return (
    <div
      onClick={onSelect}
      style={{
        padding: '6px 8px',
        marginBottom: 2,
        cursor: 'pointer',
        borderRadius: 'var(--md-radius-small)',
        background: isSelected ? 'var(--md-primary-subtle-background)' : 'transparent',
        transition: 'background-color var(--md-duration-fast) var(--md-ease-standard)',
      }}
    >
      <div className="flex items-center" style={{ gap: 6 }}>
        <span className={clsx('md-status-dot', getRunningStatusDot(server))} />
        <div className="flex-1 min-w-0">
          <div
            style={{
              fontSize: 11,
              fontWeight: 500,
              color: 'var(--md-body)',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{server.displayName}</span>
            <SupervisorBadge server={server} size="sm" />
          </div>
          <div style={{ fontSize: 9, opacity: 0.6, color: 'var(--md-body-light)' }}>
            {`内存 ${server.formattedMaxMemory} | ${server.networkStatusText}`}
          </div>
        </div>
        <button
          onClick={(e) => {
            e.stopPropagation()
            if (isBusy) return // Bug3: 运行中 stop 也受全局 isBusy 防抖
            onStop()
          }}
          className="md-btn md-btn-flat md-btn-icon"
          title={isBusy ? '处理中，请稍候' : '停止服务器'}
          style={{ opacity: isBusy ? 0.5 : undefined, pointerEvents: isBusy ? 'none' : undefined }}
        >
          <IconByName name="stop" size={14} />
        </button>
      </div>
    </div>
  )
}

// ─── 子组件：已知服务器列表项 ───

interface KnownItemProps {
  server: KnownServerInfo
  isSelected: boolean
  onSelect: () => void
  onStart: () => void
  onDelete: () => void
  isBusy?: boolean
}

function KnownServerItem({ server, isSelected, onSelect, onStart, onDelete, isBusy }: KnownItemProps): JSX.Element {
  const running = !!server.isSupervised || server.status === 'Running'
  return (
    <div
      onClick={onSelect}
      style={{
        padding: '6px 8px',
        marginBottom: 2,
        cursor: 'pointer',
        borderRadius: 'var(--md-radius-small)',
        background: isSelected ? 'var(--md-primary-subtle-background)' : 'transparent',
        transition: 'background-color var(--md-duration-fast) var(--md-ease-standard)',
      }}
    >
      <div className="flex items-center" style={{ gap: 6 }}>
        <IconByName name="star" size={10} style={{ color: 'var(--md-accent-text)' }} />
        <div className="flex-1 min-w-0">
          <div
            style={{
              fontSize: 11,
              fontWeight: 500,
              color: 'var(--md-body)',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              display: 'flex',
              alignItems: 'center',
            }}
          >
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{server.name}</span>
            {/* 已知服务器正在运行时也显示监管角标（例如被 ProcessSupervisor 拉起的场景） */}
            <SupervisorBadge server={server} size="sm" />
          </div>
          <div style={{ fontSize: 9, opacity: 0.6, color: 'var(--md-body-light)' }}>
            {running
              ? `端口 ${server.port} · 运行中`
              : `端口 ${server.port}`}
          </div>
        </div>
        <div className="flex items-center">
          <button
            onClick={(e) => {
              e.stopPropagation()
              if (isBusy) return // Bug3: 已知服务器 start 防抖
              onStart()
            }}
            className="md-btn md-btn-flat md-btn-icon"
            title={isBusy ? '处理中，请稍候' : running ? '服务器运行中，点击切换到运行中列表' : '启动该已知服务器'}
            style={{
              color: running ? 'var(--md-success, #10B981)' : 'var(--md-primary-hue-mid)',
              opacity: isBusy ? 0.5 : undefined,
              pointerEvents: isBusy ? 'none' : undefined,
            }}
          >
          <IconByName name={running ? 'play' : 'play'} size={14} />
          </button>
          <button
            onClick={(e) => {
              e.stopPropagation()
              if (isBusy) return // Bug3: 已知服务器 delete 防抖
              onDelete()
            }}
            className="md-btn md-btn-flat md-btn-icon"
            title={isBusy ? '处理中，请稍候' : running ? '服务器运行中，无法删除' : '从已知列表删除'}
            style={{
              opacity: isBusy || running ? 0.5 : undefined,
              pointerEvents: isBusy || running ? 'none' : undefined,
            }}
          >
            <IconByName name="trash" size={14} />
          </button>
        </div>
      </div>
    </div>
  )
}

// ─── 子组件：服务器分组（Expander 风格，对应 WPF 的 Expander） ───

interface ServerGroupProps {
  title: string
  icon: string
  count: number
  defaultExpanded?: boolean
  children: React.ReactNode
}

function ServerGroup({ title, icon, count, defaultExpanded = true, children }: ServerGroupProps): JSX.Element {
  const [expanded, setExpanded] = useState(defaultExpanded)
  return (
    <div className="md-expander" style={{ marginTop: 4 }}>
      <div className="md-expander-header" onClick={() => setExpanded(!expanded)}>
        <IconByName name="play" size={10} className={clsx('md-expander-icon', expanded && 'md-expander-icon-expanded')} />
        <span style={{ fontSize: 12 }}>{icon}</span>
        <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--md-body)' }}>{title}</span>
        <span className="md-badge" style={{ marginLeft: 'auto' }}>
          {count}
        </span>
      </div>
      {expanded && <div style={{ marginTop: 4 }}>{children}</div>}
    </div>
  )
}

// ─── 主页面 ───

export function DashboardPage(): JSX.Element {
  const showToast = useToastStore((s) => s.showToast)
  // Bug10: 挂载标记，防卸载后 setState
  const mountedRef = useRef(true)
  useEffect(() => {
    return () => {
      mountedRef.current = false
    }
  }, [])
  const safeSet = useCallback(<S,>(setter: React.Dispatch<React.SetStateAction<S>>, value: S | ((prev: S) => S)): void => {
    if (mountedRef.current) setter(value as never)
  }, [])

  // Bug2: 轮询请求序列号（out-of-order 保护：旧序列号的响应直接丢弃）
  const fetchSeqRef = useRef(0)

  const [serverList, setServerList] = useState<ServerListResponse | null>(null)
  const [selectedServer, setSelectedServer] = useState<ServerInfo | null>(null)
  const [searchKeyword, setSearchKeyword] = useState('')
  const [detailTab, setDetailTab] = useState<'console' | 'jvm' | 'command'>('console')
  const [isBusy, setIsBusy] = useState(false)
  const [busyReason, setBusyReason] = useState('')
  const [operationMessage, setOperationMessage] = useState('')
  // Bug1: operationMessage 自动消失定时器 ref
  const operationMsgTimerRef = useRef<number | null>(null)
  const [autoDetectEnabled, setAutoDetectEnabled] = useState(false)

  // JVM 参数相关 state
  const [jvmDefinitions, setJvmDefinitions] = useState<JvmArgumentDefinition[]>([])
  const [jvmState, setJvmState] = useState<JvmStateResponse | null>(null)
  const [jvmCategory, setJvmCategory] = useState<JvmArgumentCategory>('GarbageCollection')
  const [editingArg, setEditingArg] = useState<{ def: JvmArgumentDefinition; value: string; mode: 'add' | 'edit'; oldArg?: string } | null>(null)
  const [customArgInput, setCustomArgInput] = useState('')
  const [jvmMemoryInitial, setJvmMemoryInitial] = useState('')
  const [jvmMemoryMax, setJvmMemoryMax] = useState('')

  // Bug1: 统一封装 operationMessage，自动 4s 后消失
  const setOpMsg = useCallback((msg: string): void => {
    if (operationMsgTimerRef.current != null) {
      window.clearTimeout(operationMsgTimerRef.current)
      operationMsgTimerRef.current = null
    }
    if (mountedRef.current) setOperationMessage(msg)
    if (msg) {
      operationMsgTimerRef.current = window.setTimeout(() => {
        if (mountedRef.current) setOperationMessage('')
        operationMsgTimerRef.current = null
      }, 4000)
    }
  }, [])

  // 拉取服务器列表
  const fetchServerList = async (seq?: number) => {
    try {
      const data = await getServerList()
      // Bug2: 如果 seq 传了但不等于最新序列号 → 是旧响应，丢弃（out-of-order 保护）
      if (seq != null && seq !== fetchSeqRef.current) return
      if (!mountedRef.current) return
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      safeSet<any>(setServerList, data)
      // 同步后端的自动检测状态，避免前端状态与后端不同步
      if (typeof data.isAutoDetectEnabled === 'boolean') {
        safeSet(setAutoDetectEnabled, data.isAutoDetectEnabled)
      }
    } catch (e) {
      console.error('获取服务器列表失败:', e)
    }
  }

  // 拉取当前选中服务器详情
  const fetchSelectedServer = async (seq?: number) => {
    try {
      const data = await getSelectedServer()
      if (seq != null && seq !== fetchSeqRef.current) return
      if (!mountedRef.current) return
      safeSet(setSelectedServer, data)
    } catch (e) {
      console.error('获取选中服务器失败:', e)
    }
  }

  // 刷新按钮（不触发 fetchSeq 递增，因为刷新是"手动触发的最新"）
  const handleRefresh = async () => {
    setIsBusy(true)
    setBusyReason('正在刷新服务器列表...')
    try {
      await bridge.invoke('server:refresh')
      // Bug2: 手动刷新前递增序列号，确保最新响应覆盖所有旧轮询
      const seq = ++fetchSeqRef.current
      await fetchServerList(seq)
      await fetchSelectedServer(seq)
    } catch (e) {
      console.error('刷新失败:', e)
      setOpMsg('刷新失败，请重试')
      showToast('刷新失败，请重试', 'error')
    } finally {
      if (mountedRef.current) {
        setIsBusy(false)
        setBusyReason('')
      }
    }
  }

  const handleSelectServer = async (displayName: string) => {
    if (isBusy) return // Bug3: 并发防抖
    setIsBusy(true)
    setBusyReason('正在切换服务器...')
    try {
      await selectServer(displayName)
      const seq = ++fetchSeqRef.current
      await fetchSelectedServer(seq)
    } catch (e) {
      // Bug8: 之前只有 console.error，用户选服务器失败完全无感知
      console.error('选择服务器失败:', e)
      const msg = `选择服务器失败: ${e instanceof Error ? e.message : String(e)}`
      setOpMsg(msg)
      showToast(msg, 'error')
    } finally {
      if (mountedRef.current) {
        setIsBusy(false)
        setBusyReason('')
      }
    }
  }

  const handleStart = async () => {
    if (isBusy) return // Bug3: 并发防抖
    setIsBusy(true)
    setBusyReason('正在启动服务器...')
    setOpMsg('')
    try {
      const result = await bridge.invoke<{ success: boolean; error?: string; message?: string }>('server:start')
      if (result?.success) {
        setOpMsg(result.message || '启动成功')
        showToast(result.message || '启动成功', 'success')
      } else {
        const msg = `启动失败: ${result?.error || result?.message || '未知错误'}`
        setOpMsg(msg)
        showToast(msg, 'error')
      }
      const seq = ++fetchSeqRef.current
      await fetchServerList(seq)
      await fetchSelectedServer(seq)
      // 启动成功后应用用户配置的 T1/T3 调度策略到刚启动的服务器进程：
      //   - T1: QoS 能效标签（High/Eco）
      //   - T3: P-core 路由 + Priority Boost 禁用 + winmm 定时器精度
      if (result?.success) {
        await applyServerSchedulingPolicies()
      }
    } catch (e) {
      const msg = `启动失败: ${e instanceof Error ? e.message : String(e)}`
      setOpMsg(msg)
      showToast(msg, 'error')
    } finally {
      if (mountedRef.current) {
        setIsBusy(false)
        setBusyReason('')
      }
    }
  }

  const handleStop = async () => {
    if (isBusy) return // Bug3: 并发防抖
    setIsBusy(true)
    setBusyReason('正在停止服务器...')
    setOpMsg('')
    try {
      const result = await bridge.invoke<{ success: boolean; error?: string; message?: string }>('server:stop')
      if (result?.success) {
        setOpMsg(result.message || '停止成功')
        showToast(result.message || '停止成功', 'success')
      } else {
        const msg = `停止失败: ${result?.error || result?.message || '未知错误'}`
        setOpMsg(msg)
        showToast(msg, 'error')
      }
      const seq = ++fetchSeqRef.current
      await fetchServerList(seq)
      await fetchSelectedServer(seq)
      // 停止成功后：若已无运行中服务器，撤销 winmm 定时器精度（恢复 15.6ms 省电）
      if (result?.success) {
        await maybeDisableTimerResolution()
      }
    } catch (e) {
      const msg = `停止失败: ${e instanceof Error ? e.message : String(e)}`
      setOpMsg(msg)
      showToast(msg, 'error')
    } finally {
      if (mountedRef.current) {
        setIsBusy(false)
        setBusyReason('')
      }
    }
  }

  const handleImport = async () => {
    if (isBusy) return // Bug3: 并发防抖
    setIsBusy(true)
    setBusyReason('正在导入服务器...')
    try {
      const result = await bridge.invoke<{ success: boolean; message?: string; error?: string }>('server:import')
      if (result.success) {
        setOpMsg(result.message || '导入服务器成功')
        showToast(result.message || '导入服务器成功', 'success')
        const seq = ++fetchSeqRef.current
        await fetchServerList(seq)
        // Bug 修复：导入的服务器可能被后端自动选中，需刷新 selectedServer 保持一致
        await fetchSelectedServer(seq)
      } else {
        const msg = `导入失败: ${result.error || result.message || '未知错误'}`
        setOpMsg(msg)
        showToast(msg, 'error')
      }
    } catch (e) {
      console.error('导入失败:', e)
      const msg = `导入失败: ${e instanceof Error ? e.message : String(e)}`
      setOpMsg(msg)
      showToast(msg, 'error')
    } finally {
      if (mountedRef.current) {
        setIsBusy(false)
        setBusyReason('')
      }
    }
  }

  const handleToggleAutoDetect = async () => {
    if (isBusy) return
    // Bug 修复：之前不设 isBusy，异步期间可与其他写操作并发
    setIsBusy(true)
    setBusyReason('正在切换自动检测...')
    try {
      const result = await bridge.invoke<{ success: boolean; isEnabled?: boolean }>('server:toggleAutoDetect')
      // Bug4: 使用后端返回的实际状态；失败时给出 toast 提示，不再悄悄翻转
      if (result?.success && typeof result.isEnabled === 'boolean') {
        safeSet(setAutoDetectEnabled, result.isEnabled)
        showToast(result.isEnabled ? '已开启自动检测' : '已停止自动检测', 'success')
      } else {
        // 后端未返回有效状态时，给出错误提示 + 兜底翻转本地
        const msg = `切换自动检测失败${result?.success === false ? '' : ': 状态未返回'}`
        showToast(msg, 'warning')
        safeSet(setAutoDetectEnabled, !autoDetectEnabled)
      }
    } catch (e) {
      // Bug4: 之前 catch 静默，现在给用户明确提示
      console.error('切换自动检测失败:', e)
      const msg = `切换自动检测失败: ${e instanceof Error ? e.message : String(e)}`
      showToast(msg, 'error')
      // 桥接抛异常时，不翻转（避免"假切换"误导）
    } finally {
      if (mountedRef.current) {
        setIsBusy(false)
        setBusyReason('')
      }
    }
  }

  const handleCopyCommand = () => {
    if (selectedServer?.fullCommandLine) {
      navigator.clipboard?.writeText(selectedServer.fullCommandLine)
        .then(() => {
          // Bug5: 之前成功无反馈，现在给个 toast
          showToast('启动命令已复制到剪贴板', 'success')
        })
        .catch((e) => {
          console.error('复制失败:', e)
          showToast('复制失败，请手动选择复制', 'error')
        })
    } else {
      showToast('当前没有可复制的启动命令', 'warning')
    }
  }

  // ─── JVM 参数方法 ───

  const fetchJvmDefinitions = useCallback(async (seq?: number) => {
    try {
      const resp = await getJvmDefinitions()
      if (seq != null && seq !== fetchSeqRef.current) return
      if (!mountedRef.current) return
      safeSet(setJvmDefinitions, resp.definitions)
    } catch (e) {
      console.error('获取 JVM 参数定义失败:', e)
    }
  }, [safeSet])

  const fetchJvmState = useCallback(async (seq?: number) => {
    try {
      const resp = await getJvmState()
      if (seq != null && seq !== fetchSeqRef.current) return
      if (!mountedRef.current) return
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      safeSet<any>(setJvmState, resp)
      if (resp.hasServer) {
        safeSet(setJvmMemoryInitial, resp.initialMemory)
        safeSet(setJvmMemoryMax, resp.maxMemory)
      }
    } catch (e) {
      console.error('获取 JVM 状态失败:', e)
    }
  }, [safeSet])

  const selectedArgBaseNames = useMemo(() => {
    if (!jvmState?.selectedArguments) return new Set<string>()
    return new Set(jvmState.selectedArguments.map((a) => getArgBaseName(a).toLowerCase()))
  }, [jvmState])

  const filteredDefinitions = useMemo(() => {
    return jvmDefinitions.filter((d) => d.category === jvmCategory)
  }, [jvmDefinitions, jvmCategory])

  const categories = useMemo(() => {
    const set = new Set(jvmDefinitions.map((d) => d.category))
    return Array.from(set) as JvmArgumentCategory[]
  }, [jvmDefinitions])

  const handleAddArgument = async (def: JvmArgumentDefinition) => {
    if (def.valueType === 'None' || def.valueType === 'BooleanFlag') {
      try {
        const res = await addJvmArgument(def.flag)
        if (res?.success === false) {
          const msg = `添加参数失败: ${res?.error || '未知错误'}`
          showToast(msg, 'error')
          setOpMsg(msg)
          return
        }
        showToast(`已添加参数: ${def.name}`, 'success')
        const seq = ++fetchSeqRef.current
        await fetchJvmState(seq)
        await fetchSelectedServer(seq)
      } catch (e) {
        // Bug7: 添加参数失败无反馈
        console.error('添加参数失败:', e)
        const msg = `添加参数失败: ${e instanceof Error ? e.message : String(e)}`
        showToast(msg, 'error')
        setOpMsg(msg)
      }
    } else {
      setEditingArg({ def, value: def.defaultValue ?? '', mode: 'add' })
    }
  }

  const handleRemoveArgument = async (arg: string) => {
    try {
      const res = await removeJvmArgument(arg)
      if (res?.success === false) {
        const msg = `移除参数失败: ${res?.error || '未知错误'}`
        showToast(msg, 'error')
        setOpMsg(msg)
        return
      }
      showToast(`已移除参数: ${arg}`, 'success')
      const seq = ++fetchSeqRef.current
      await fetchJvmState(seq)
      await fetchSelectedServer(seq)
    } catch (e) {
      // Bug7: 移除参数失败无反馈
      console.error('移除参数失败:', e)
      const msg = `移除参数失败: ${e instanceof Error ? e.message : String(e)}`
      showToast(msg, 'error')
      setOpMsg(msg)
    }
  }

  const handleEditArgument = (arg: string) => {
    const base = getArgBaseName(arg)
    const value = getArgValue(arg)
    const def = jvmDefinitions.find(
      (d) => getArgBaseName(d.flag).toLowerCase() === base.toLowerCase(),
    )
    if (def) {
      // BugF: 设置编辑前的参数值快照
      editingArgSnapshotRef.current = { baseName: base.toLowerCase(), expectedFullArg: arg }
      setEditingArg({ def, value, mode: 'edit', oldArg: arg })
    }
  }

  const handleSaveEditingArg = async () => {
    if (!editingArg) return
    const { def, value, mode, oldArg } = editingArg

    // BugF: edit 模式下保存前校验「弹窗打开期间底层参数值没变」
    if (mode === 'edit' && oldArg && editingArgSnapshotRef.current) {
      const snap = editingArgSnapshotRef.current
      // 从当前 jvmState 找这个参数的最新实际值
      const currentArg = jvmState?.selectedArguments?.find(
        (a) => getArgBaseName(a).toLowerCase() === snap.baseName,
      )
      if (currentArg !== undefined && currentArg !== snap.expectedFullArg) {
        // 参数实际值在弹窗打开期间被后台/轮询刷新了，不允许用旧快照覆盖新值
        const msg = `参数「${def.name}」已在后台更新，请关闭编辑弹窗后重新编辑`
        showToast(msg, 'warning')
        setOpMsg(msg)
        if (mountedRef.current) setEditingArg(null)
        editingArgSnapshotRef.current = null
        return
      }
    }

    let ok = false
    if (mode === 'add') {
      const full = buildFullArg(def, value)
      try {
        const res = await addJvmArgument(full)
        if (res?.success === false) {
          showToast(`添加参数失败: ${res?.error || '未知错误'}`, 'error')
          return
        }
        ok = true
        showToast(`已添加参数: ${def.name} = ${value}`, 'success')
      } catch (e) {
        console.error('添加参数失败:', e)
        showToast(`添加参数失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
        return
      }
    } else if (mode === 'edit' && oldArg) {
      try {
        const res = await updateJvmArgument(oldArg, value)
        if (res?.success === false) {
          showToast(`更新参数失败: ${res?.error || '未知错误'}`, 'error')
          return
        }
        ok = true
        showToast(`已更新参数: ${def.name} = ${value}`, 'success')
      } catch (e) {
        console.error('更新参数失败:', e)
        showToast(`更新参数失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
        return
      }
    }

    editingArgSnapshotRef.current = null
    if (mountedRef.current) setEditingArg(null)
    if (ok) {
      const seq = ++fetchSeqRef.current
      await fetchJvmState(seq)
      await fetchSelectedServer(seq)
    }
  }

  const handleApplyPreset = async (preset: 'aikar' | 'g1gc' | 'zgc') => {
    try {
      const res = await applyJvmPreset(preset)
      if (res?.success === false) {
        showToast(`应用预设失败: ${res?.error || '未知错误'}`, 'error')
        return
      }
      const presetLabel = preset === 'aikar' ? 'Aikar 优化' : preset === 'g1gc' ? 'G1GC 回收器' : 'ZGC 回收器'
      showToast(`已应用预设: ${presetLabel}`, 'success')
      const seq = ++fetchSeqRef.current
      await fetchJvmState(seq)
      await fetchSelectedServer(seq)
    } catch (e) {
      // Bug7: 之前静默失败
      console.error('应用预设失败:', e)
      showToast(`应用预设失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
    }
  }

  const handleAddCustomArg = async () => {
    if (!customArgInput.trim()) return
    try {
      const res = await addCustomJvmArgument(customArgInput.trim())
      if (res?.success === false) {
        showToast(`添加自定义参数失败: ${res?.error || '未知错误'}`, 'error')
        return
      }
      showToast(`已添加自定义参数: ${customArgInput.trim()}`, 'success')
      if (mountedRef.current) setCustomArgInput('')
      const seq = ++fetchSeqRef.current
      await fetchJvmState(seq)
      await fetchSelectedServer(seq)
    } catch (e) {
      // Bug7: 自定义参数失败无反馈
      console.error('添加自定义参数失败:', e)
      showToast(`添加自定义参数失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
    }
  }

  // 正在提交的内存修改 token（防 onBlur 多次触发重复提交）
  const memorySubmittingRef = useRef(false)
  const memoryLastValuesRef = useRef<{ initial: string; max: string } | null>(null)
  // BugF: 记录 JVM 参数编辑弹窗打开时的「参数实际值快照」，保存前校验避免旧值覆盖新值
  const editingArgSnapshotRef = useRef<{ baseName: string; expectedFullArg: string } | null>(null)

  const handleMemoryBlur = async () => {
    if (!jvmState?.hasServer) return
    // Bug6: 内存输入 -> 切 Tab/路由导致 onBlur 没触发就卸载
    //        解决方案：① 在 useEffect cleanup 时"若输入有脏值则自动提交"
    //        ② 这里加防重入（避免 blur + 卸载 cleanup 重复提交）
    const currentInitial = jvmMemoryInitial
    const currentMax = jvmMemoryMax
    if (
      memorySubmittingRef.current ||
      (memoryLastValuesRef.current?.initial === currentInitial &&
        memoryLastValuesRef.current?.max === currentMax)
    ) {
      return
    }
    memorySubmittingRef.current = true
    try {
      const res = await setJvmMemory(currentInitial, currentMax)
      if (res?.success === false) {
        showToast(`设置内存失败: ${res?.error || '未知错误'}`, 'error')
        return
      }
      memoryLastValuesRef.current = { initial: currentInitial, max: currentMax }
      showToast(
        `内存设置已更新: 初始 ${currentInitial || '(默认)'} / 最大 ${currentMax || '(默认)'}`,
        'success',
      )
      const seq = ++fetchSeqRef.current
      await fetchJvmState(seq)
      await fetchSelectedServer(seq)
    } catch (e) {
      console.error('设置内存失败:', e)
      showToast(`设置内存失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
    } finally {
      memorySubmittingRef.current = false
    }
  }

  const isArgSelected = (def: JvmArgumentDefinition): boolean => {
    const base = getArgBaseName(def.flag).toLowerCase()
    return selectedArgBaseNames.has(base)
  }

  useEffect(() => {
    // 初次加载（seq 统一递增一次，避免 out-of-order 互相覆盖）
    const seq0 = ++fetchSeqRef.current
    fetchServerList(seq0)
    fetchSelectedServer(seq0)
    fetchJvmDefinitions(seq0)
    fetchJvmState(seq0)

    // Bug2: 轮询每次都递增序列号，保证旧响应不会覆盖新的
    const interval = window.setInterval(() => {
      const seq = ++fetchSeqRef.current
      fetchServerList(seq)
      fetchSelectedServer(seq)
      fetchJvmState(seq)
    }, 3000)

    return () => {
      window.clearInterval(interval)
      // Bug6: 卸载时如果内存输入有脏值，同步提交一次（避免 onBlur 没触发导致丢修改）
      if (
        jvmState?.hasServer &&
        !memorySubmittingRef.current &&
        ((memoryLastValuesRef.current?.initial !== jvmMemoryInitial) ||
          (memoryLastValuesRef.current?.max !== jvmMemoryMax))
      ) {
        // fire-and-forget：卸载后没法 await，让后端尽力而为
        setJvmMemory(jvmMemoryInitial, jvmMemoryMax).catch(() => {})
      }
      // Bug1: 清理 operationMessage 自动消失定时器
      if (operationMsgTimerRef.current != null) {
        window.clearTimeout(operationMsgTimerRef.current)
        operationMsgTimerRef.current = null
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // 搜索过滤
  const keyword = searchKeyword.toLowerCase()
  const runningServers = (serverList?.running ?? []).filter(
    (s) => !keyword || s.displayName.toLowerCase().includes(keyword),
  )
  const knownServers = (serverList?.known ?? []).filter(
    (s) => !keyword || s.name.toLowerCase().includes(keyword),
  )

  return (
    <div className="md-page-enter h-full flex flex-col relative">
      {/* ═══ 顶部操作条 ═══ */}
      <div
        className="flex items-center"
        style={{
          background: 'var(--md-card-background)',
          borderBottom: '1px solid var(--md-card-subtle-border)',
          padding: '10px 16px',
          gap: 8,
        }}
      >
        {/* 左侧：操作按钮 */}
        <button
          onClick={handleRefresh}
          disabled={isBusy}
          className="md-btn md-btn-primary"
          title="立即刷新服务器列表"
        >
          <span className={clsx(isBusy && 'md-spin')}><IconByName name="refresh" size={14} /></span>
          <span style={{ fontWeight: 600 }}>刷新</span>
        </button>

        <button
          onClick={handleToggleAutoDetect}
          className="md-btn md-btn-outlined"
          title={autoDetectEnabled ? '点击停止自动检测' : '点击开始自动检测'}
        >
          <IconByName name={autoDetectEnabled ? 'pause' : 'play'} size={14} />
          <span>{autoDetectEnabled ? '自动检测中' : '开启自动检测'}</span>
        </button>

        <button
          onClick={handleImport}
          disabled={isBusy}
          className="md-btn md-btn-outlined"
          title="选择 JAR 文件导入到已知服务器列表"
        >
          <IconByName name="add" size={14} />
          <span>导入服务器</span>
        </button>

        {/* 中间：选中服务器状态 */}
        <div className="flex-1 flex items-center justify-center">
          {selectedServer && (
            <div className="flex items-center" style={{ gap: 6 }}>
              <span className={clsx('md-status-dot', getRunningStatusDot(selectedServer))} />
              <span style={{ fontSize: 12, fontWeight: 500, color: 'var(--md-body)' }}>
                {selectedServer.status}
              </span>
              <span style={{ fontSize: 12, opacity: 0.5 }}>·</span>
              <span
                style={{
                  fontSize: 12,
                  opacity: 0.7,
                  color: 'var(--md-body)',
                  maxWidth: 280,
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
              >
                {selectedServer.displayName}
              </span>
            </div>
          )}
        </div>

        {/* 右侧：忙碌提示 */}
        {isBusy && (
          <div
            className="flex items-center"
            style={{
              background: 'var(--md-card-hover)',
              borderRadius: 'var(--md-radius-small)',
              padding: '6px 10px',
              gap: 8,
            }}
          >
            <div
              className="md-spin"
              style={{
                width: 14,
                height: 14,
                border: '2px solid var(--md-primary-hue-mid)',
                borderTopColor: 'transparent',
                borderRadius: '50%',
              }}
            />
            <span style={{ fontSize: 12, fontWeight: 500, color: 'var(--md-body)' }}>
              {busyReason}
            </span>
          </div>
        )}
      </div>

      {/* ═══ 中间区域：左列表 + 右 Tab ═══ */}
      <div className="flex-1 flex min-h-0">
        {/* 左侧：服务器列表（280px） */}
        <div
          className="flex flex-col"
          style={{
            width: 280,
            background: 'var(--md-card-background)',
            borderRight: '1px solid var(--md-card-subtle-border)',
          }}
        >
          {/* 搜索框 */}
          <div style={{ padding: '8px 8px 4px' }}>
            <div className="flex items-center" style={{ gap: 6 }}>
              <IconByName name="search" size={12} style={{ opacity: 0.6 }} />
              <input
                type="text"
                value={searchKeyword}
                onChange={(e) => setSearchKeyword(e.target.value)}
                placeholder="搜索服务器..."
                className="flex-1 bg-transparent outline-none"
                style={{ fontSize: 12, padding: '4px 0', color: 'var(--md-body)' }}
              />
              {searchKeyword && (
                <button
                  onClick={() => setSearchKeyword('')}
                  className="md-btn md-btn-flat md-btn-icon"
                  title="清空搜索"
                >
                  <IconByName name="close" size={14} />
                </button>
              )}
            </div>
          </div>

          {/* 列表区 */}
          <div className="flex-1 overflow-y-auto" style={{ padding: '0 8px 8px' }}>
            {/* 运行中分组 */}
            <ServerGroup title="运行中" icon="" count={runningServers.length}>
              {runningServers.length === 0 ? (
                <div className="md-empty-state" style={{ padding: '12px 8px' }}>
                  <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                    {searchKeyword ? '没有匹配的服务器' : '暂无运行中的服务器'}
                  </div>
                </div>
              ) : (
                runningServers.map((server, idx) => (
                  <div
                    key={`running-${idx}`}
                    className="md-stagger-item"
                    style={{ animationDelay: `${idx * 40}ms` }}
                  >
                    <RunningServerItem
                      server={server}
                      isSelected={selectedServer?.displayName === server.displayName}
                      onSelect={() => handleSelectServer(server.displayName)}
                      onStop={handleStop}
                      isBusy={isBusy}
                    />
                  </div>
                ))
              )}
            </ServerGroup>

            {/* 已知服务器分组 */}
            <ServerGroup title="已知服务器" icon="" count={knownServers.length}>
              {knownServers.length === 0 ? (
                <div className="md-empty-state" style={{ padding: '20px 8px' }}>
                  <div className="md-empty-state-icon" style={{ fontSize: 32, opacity: 0.3 }}>
                    <IconByName name="folderOpen" size={32} />
                  </div>
                  <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                    还没有已知服务器
                  </div>
                  <div style={{ fontSize: 10, opacity: 0.5 }}>点击「导入服务器」开始</div>
                </div>
              ) : (
                knownServers.map((server, idx) => (
                  <div
                    key={`known-${idx}`}
                    className="md-stagger-item"
                    style={{ animationDelay: `${idx * 40}ms` }}
                  >
                    <KnownServerItem
                      server={server}
                      isSelected={selectedServer?.isKnown === true && selectedServer.displayName === server.name}
                      onSelect={() => handleSelectServer(server.name)}
                      isBusy={isBusy}
                      onStart={async () => {
                        // Bug3: 并发防抖
                        if (isBusy) return
                        setIsBusy(true)
                        setBusyReason(`正在启动「${server.name}」...`)
                        try {
                          const result = await bridge.invoke<{ success: boolean; error?: string; message?: string }>('server:startKnown', {
                            knownServerId: server.knownServerId,
                            id: server.id,
                            name: server.name,
                          })
                          if (!result?.success) {
                            const msg = `启动失败: ${result?.error || result?.message || '未知错误'}`
                            setOpMsg(msg)
                            showToast(msg, 'error')
                          } else {
                            // Bug9: 之前成功 case 没提示
                            const okMsg = result?.message || `已启动「${server.name}」`
                            setOpMsg(okMsg)
                            showToast(okMsg, 'success')
                          }
                          const seq = ++fetchSeqRef.current
                          await fetchServerList(seq)
                          await fetchSelectedServer(seq)
                          // 启动成功后应用 T1/T3 调度策略（与 handleStart 行为一致）
                          if (result?.success) {
                            await applyServerSchedulingPolicies()
                          }
                        } catch (e) {
                          const msg = `启动失败: ${e instanceof Error ? e.message : String(e)}`
                          setOpMsg(msg)
                          showToast(msg, 'error')
                        } finally {
                          if (mountedRef.current) {
                            setIsBusy(false)
                            setBusyReason('')
                          }
                        }
                      }}
                      onDelete={async () => {
                        // Bug3: 并发防抖
                        if (isBusy) return
                        if (!confirm(`确定要从已知服务器列表删除「${server.name}」吗？`)) return
                        setIsBusy(true)
                        setBusyReason(`正在删除「${server.name}」...`)
                        try {
                          const result = await bridge.invoke<{ success: boolean; message?: string; error?: string }>('server:removeKnown', {
                            knownServerId: server.knownServerId,
                            id: server.id,
                            name: server.name,
                          })
                          if (result.success) {
                            const okMsg = result.message || `已删除「${server.name}」`
                            setOpMsg(okMsg)
                            showToast(okMsg, 'success')
                            const seq = ++fetchSeqRef.current
                            await fetchServerList(seq)
                            // Bug 修复：若删除的是当前选中服务器，需刷新 selectedServer 避免过期
                            await fetchSelectedServer(seq)
                          } else {
                            const msg = `删除失败: ${result.error || result.message || '未知错误'}`
                            setOpMsg(msg)
                            showToast(msg, 'error')
                          }
                        } catch (e) {
                          console.error('删除失败:', e)
                          const msg = `删除失败: ${e instanceof Error ? e.message : String(e)}`
                          setOpMsg(msg)
                          showToast(msg, 'error')
                        } finally {
                          if (mountedRef.current) {
                            setIsBusy(false)
                            setBusyReason('')
                          }
                        }
                      }}
                    />
                  </div>
                ))
              )}
            </ServerGroup>
          </div>
        </div>

        {/* 右侧：Tab 详情区 */}
        <div className="flex-1 flex flex-col min-w-0">
          <div className="md-tabs">
            <div
              className={clsx('md-tab', detailTab === 'console' && 'md-tab-active')}
              onClick={() => setDetailTab('console')}
            >
              <IconByName name="sliders" size={12} /> 控制台
            </div>
            <div
              className={clsx('md-tab', detailTab === 'jvm' && 'md-tab-active')}
              onClick={() => setDetailTab('jvm')}
            >
              <IconByName name="gear" size={12} /> JVM 参数
            </div>
            <div
              className={clsx('md-tab', detailTab === 'command' && 'md-tab-active')}
              onClick={() => setDetailTab('command')}
            >
              [LOG] 命令预览
            </div>
          </div>

          <div className="flex-1 overflow-y-auto" style={{ padding: 16 }}>
            {!selectedServer ? (
                <div className="md-empty-state h-full">
                  <div className="md-empty-state-icon"><IconByName name="gamepad" size={64} /></div>
                  <div className="md-empty-state-text">选择一个服务器查看详情</div>
                </div>
            ) : (
              <>
                {/* ─── 控制台 Tab ─── */}
                {detailTab === 'console' && (
                  <div>
                    {/* 服务器控制卡片 */}
                    <Reveal direction="up" delay={0} className="md-card md-card-elevated" style={{ padding: 16, marginBottom: 12 }}>
                      <div
                        style={{
                          fontSize: 15,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        [BOOT] 服务器控制
                      </div>
                      <div className="flex items-center" style={{ gap: 8 }}>
                        <button
                          onClick={handleStart}
                          disabled={isBusy}
                          className="md-btn md-btn-primary"
                          style={{ minHeight: 36, padding: '8px 16px' }}
                        >
                          <IconByName name="play" size={14} />
                          <span style={{ fontWeight: 600 }}>启动服务器</span>
                        </button>
                        <button
                          onClick={handleStop}
                          disabled={isBusy}
                          className="md-btn md-btn-danger"
                          style={{ minHeight: 36, padding: '8px 16px' }}
                        >
                          <IconByName name="stop" size={14} />
                          <span style={{ fontWeight: 600 }}>停止服务器</span>
                        </button>
                        <button
                          onClick={async () => {
                            // BugA3: 并发防抖
                            if (isBusy) return
                            setIsBusy(true)
                            setBusyReason('正在保存到已知服务器列表...')
                            setOpMsg('')
                            try {
                              const result = await bridge.invoke<{ success: boolean; message?: string; error?: string }>('server:saveAsKnown')
                              if (result.success) {
                                // BugA1: 成功给 toast
                                const okMsg = result.message || '已保存到已知服务器列表'
                                setOpMsg(okMsg)
                                showToast(okMsg, 'success')
                                // BugA4: 成功后带 seq 刷新，避免旧轮询覆盖
                                const seq = ++fetchSeqRef.current
                                await fetchServerList(seq)
                                await fetchSelectedServer(seq)
                              } else {
                                // BugA2: 失败走 setOpMsg（自动消失）+ toast，不再直接 setOperationMessage
                                const msg = `保存失败: ${result.error || result.message || '未知错误'}`
                                setOpMsg(msg)
                                showToast(msg, 'error')
                              }
                            } catch (e) {
                              console.error('保存到已知失败:', e)
                              // BugA2: catch 也走 setOpMsg + toast
                              const msg = `保存失败: ${e instanceof Error ? e.message : String(e)}`
                              setOpMsg(msg)
                              showToast(msg, 'error')
                            } finally {
                              if (mountedRef.current) {
                                setIsBusy(false)
                                setBusyReason('')
                              }
                            }
                          }}
                          // Q1 修复：如果已经是已知服务器（isKnown=true），则不需要再显示「保存到已知」按钮
                          style={{ minHeight: 36, padding: '8px 16px', display: selectedServer && selectedServer.isKnown ? 'none' : undefined }}
                          className="md-btn md-btn-outlined"
                          disabled={isBusy}
                          // Bug17: 禁用时说明原因
                          title={isBusy ? '处理中，请稍候' : '将当前运行中的服务器保存到已知服务器列表，方便下次快速启动'}
                        >
                          <IconByName name="save" size={12} />
                          <span>保存到已知</span>
                        </button>
                      </div>
                      {operationMessage && (
                        <div
                          style={{
                            fontSize: 12,
                            marginTop: 12,
                            opacity: 0.8,
                            color: 'var(--md-body)',
                          }}
                        >
                          {operationMessage}
                        </div>
                      )}
                    </Reveal>

                    {/* 服务器详情卡片 */}
                    <Reveal direction="up" delay={80} className="md-card md-card-elevated" style={{ padding: 16, marginBottom: 12 }}>
                      <div
                        style={{
                          fontSize: 15,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        [METRIC] 服务器详情
                      </div>
                      <div style={{ display: 'grid', gridTemplateColumns: '100px 1fr', rowGap: 8 }}>
                        <div
                          style={{
                            fontSize: 12,
                            fontWeight: 500,
                            opacity: 0.7,
                            color: 'var(--md-body-light)',
                          }}
                        >
                          工作路径
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            color: 'var(--md-body)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedServer.workingDirectory}
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            fontWeight: 500,
                            opacity: 0.7,
                            color: 'var(--md-body-light)',
                          }}
                        >
                          JAR 路径
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            color: 'var(--md-body)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedServer.serverJarPath}
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            fontWeight: 500,
                            opacity: 0.7,
                            color: 'var(--md-body-light)',
                          }}
                        >
                          JAR 名称
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            color: 'var(--md-body)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedServer.serverJarName}
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            fontWeight: 500,
                            opacity: 0.7,
                            color: 'var(--md-body-light)',
                          }}
                        >
                          Java
                        </div>
                        <div
                          style={{
                            fontSize: 12,
                            color: 'var(--md-body)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedServer.javaPath}
                        </div>
                      </div>
                    </Reveal>

                    {/* 检测日志卡片 */}
                    <Reveal direction="up" delay={160} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 15,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        [LOG] 检测日志
                      </div>
                      <div
                        className="md-terminal"
                        style={{ minHeight: 120, maxHeight: 200, overflowY: 'auto' }}
                      >
                        <div style={{ opacity: 0.5 }}>[系统] 暂无检测日志</div>
                      </div>
                    </Reveal>
                  </div>
                )}

                {/* ─── JVM 参数 Tab ─── */}
                {detailTab === 'jvm' && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                    {/* 内存设置卡片 */}
                    <Reveal direction="up" delay={0} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        <IconByName name="memory" size={14} /> 内存设置
                      </div>
                      <div className="grid grid-cols-2" style={{ gap: 12 }}>
                        <div>
                          <div
                            style={{
                              fontSize: 11,
                              opacity: 0.7,
                              marginBottom: 4,
                              color: 'var(--md-body-light)',
                            }}
                          >
                            初始堆内存 (-Xms)
                          </div>
                          <input
                            value={jvmMemoryInitial}
                            onChange={(e) => setJvmMemoryInitial(e.target.value)}
                            onBlur={handleMemoryBlur}
                            className="md-input"
                            placeholder="如 2G、512M"
                            disabled={!jvmState?.hasServer || jvmState.isRunning}
                            // Bug17: 禁用提示
                            title={
                              !jvmState?.hasServer
                                ? '请先选择一个服务器'
                                : jvmState?.isRunning
                                ? '服务器运行中无法修改内存设置，请先停服'
                                : '初始堆内存大小（JVM -Xms 参数）'
                            }
                          />
                        </div>
                        <div>
                          <div
                            style={{
                              fontSize: 11,
                              opacity: 0.7,
                              marginBottom: 4,
                              color: 'var(--md-body-light)',
                            }}
                          >
                            最大堆内存 (-Xmx)
                          </div>
                          <input
                            value={jvmMemoryMax}
                            onChange={(e) => setJvmMemoryMax(e.target.value)}
                            onBlur={handleMemoryBlur}
                            className="md-input"
                            placeholder="如 4G、2048M"
                            disabled={!jvmState?.hasServer || jvmState.isRunning}
                            // Bug17: 禁用提示
                            title={
                              !jvmState?.hasServer
                                ? '请先选择一个服务器'
                                : jvmState?.isRunning
                                ? '服务器运行中无法修改内存设置，请先停服'
                                : '最大堆内存大小（JVM -Xmx 参数）'
                            }
                          />
                        </div>
                      </div>
                      {jvmState?.isRunning && (
                        <div style={{ fontSize: 11, color: 'var(--md-primary-hue-mid)', marginTop: 8 }}>
                          [WARN] 服务器运行中无法修改内存设置
                        </div>
                      )}
                    </Reveal>

                    {/* 快速预设卡片 */}
                    <Reveal direction="up" delay={70} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        [BOOT] 快速预设
                      </div>
                      <div className="flex items-center" style={{ gap: 8, flexWrap: 'wrap' }}>
                        <button
                          onClick={() => handleApplyPreset('aikar')}
                          className="md-btn md-btn-outlined"
                          style={{ fontSize: 'var(--md-font-size-sm)' }}
                          disabled={!jvmState?.hasServer || jvmState.isRunning}
                          title={
                            !jvmState?.hasServer
                              ? '请先选择一个服务器'
                              : jvmState?.isRunning
                              ? '服务器运行中无法修改 JVM 参数，请先停服'
                              : '应用 Aikar 的 Minecraft JVM 优化参数组合'
                          }
                        >
                          <IconByName name="star" size={12} /> Aikar 优化
                        </button>
                        <button
                          onClick={() => handleApplyPreset('g1gc')}
                          className="md-btn md-btn-outlined"
                          style={{ fontSize: 'var(--md-font-size-sm)' }}
                          disabled={!jvmState?.hasServer || jvmState.isRunning}
                          title={
                            !jvmState?.hasServer
                              ? '请先选择一个服务器'
                              : jvmState?.isRunning
                              ? '服务器运行中无法修改 JVM 参数，请先停服'
                              : '使用 G1 Garbage-First 垃圾回收器预设'
                          }
                        >
                          [METRIC] G1GC 回收器
                        </button>
                        <button
                          onClick={() => handleApplyPreset('zgc')}
                          className="md-btn md-btn-outlined"
                          style={{ fontSize: 'var(--md-font-size-sm)' }}
                          disabled={!jvmState?.hasServer || jvmState.isRunning}
                          title={
                            !jvmState?.hasServer
                              ? '请先选择一个服务器'
                              : jvmState?.isRunning
                              ? '服务器运行中无法修改 JVM 参数，请先停服'
                              : '使用 ZGC 低延迟垃圾回收器预设（需要 JDK 17+）'
                          }
                        >
                          [METRIC] ZGC 回收器
                        </button>
                      </div>
                    </Reveal>

                    {/* 已选参数卡片 */}
                    <Reveal direction="up" delay={140} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        [OK] 已选参数 ({jvmState?.selectedArguments?.length ?? 0})
                      </div>
                      {!jvmState?.selectedArguments || jvmState.selectedArguments.length === 0 ? (
                        <div className="md-empty-state" style={{ padding: '12px 8px' }}>
                          <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                            暂无已选参数，从下方分类中添加
                          </div>
                        </div>
                      ) : (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                          {jvmState.selectedArguments.map((arg, idx) => {
                            const base = getArgBaseName(arg)
                            const def = jvmDefinitions.find(
                              (d) => getArgBaseName(d.flag).toLowerCase() === base.toLowerCase(),
                            )
                            return (
                              <div
                                key={idx}
                                className="flex items-center"
                                style={{
                                  background: 'var(--md-card-hover)',
                                  borderRadius: 'var(--md-radius-small)',
                                  padding: '8px 10px',
                                  gap: 8,
                                }}
                              >
                                <div className="flex-1" style={{ minWidth: 0 }}>
                                  <div
                                    style={{
                                      fontSize: 12,
                                      fontWeight: 600,
                                      color: 'var(--md-body)',
                                      marginBottom: 2,
                                    }}
                                  >
                                    {def?.name || base}
                                  </div>
                                  <div
                                    style={{
                                      fontFamily: 'var(--md-font-mono)',
                                      fontSize: 11,
                                      color: 'var(--md-body-light)',
                                      whiteSpace: 'nowrap',
                                      overflow: 'hidden',
                                      textOverflow: 'ellipsis',
                                    }}
                                  >
                                    {arg}
                                  </div>
                                </div>
                                {def && def.valueType !== 'None' && (
                                  <button
                                    onClick={() => handleEditArgument(arg)}
                                    className="md-btn md-btn-flat md-btn-icon"
                                    title={jvmState.isRunning ? '服务器运行中无法编辑，请先停服' : '编辑该参数的值'}
                                    style={{ fontSize: 12 }}
                                    disabled={jvmState.isRunning}
                                  >
                                    <IconByName name="edit" size={12} />
                                  </button>
                                )}
                                <button
                                  onClick={() => handleRemoveArgument(arg)}
                                  className="md-btn md-btn-flat md-btn-icon"
                                  title={jvmState.isRunning ? '服务器运行中无法移除，请先停服' : '从已选参数中移除'}
                                  style={{ fontSize: 12, color: 'var(--md-error)' }}
                                  disabled={jvmState.isRunning}
                                >
                                  <IconByName name="close" size={12} />
                                </button>
                              </div>
                            )
                          })}
                        </div>
                      )}
                    </Reveal>

                    {/* 可选参数分类卡片 */}
                    <Reveal direction="up" delay={210} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        [ADD] 添加参数
                      </div>

                      {/* 分类标签 */}
                      <div
                        style={{
                          display: 'flex',
                          gap: 6,
                          flexWrap: 'wrap',
                          marginBottom: 12,
                        }}
                      >
                        {categories.map((cat) => (
                          <button
                            key={cat}
                            onClick={() => setJvmCategory(cat)}
                            className={clsx(
                              'md-chip',
                              jvmCategory === cat && 'md-chip-primary',
                            )}
                            style={{
                              cursor: 'pointer',
                              fontSize: 11,
                            }}
                          >
                            {categoryLabels[cat]}
                          </button>
                        ))}
                      </div>

                      {/* 可选参数列表 */}
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        {filteredDefinitions.map((def) => {
                          const selected = isArgSelected(def)
                          return (
                            <div
                              key={def.flag}
                              className="flex items-center"
                              style={{
                                background: selected
                                  ? 'var(--md-primary-tint-soft)'
                                  : 'var(--md-card-hover)',
                                borderRadius: 'var(--md-radius-small)',
                                padding: '8px 10px',
                                gap: 8,
                                opacity: selected ? 0.6 : 1,
                              }}
                            >
                              <div className="flex-1" style={{ minWidth: 0 }}>
                                <div
                                  style={{
                                    fontSize: 12,
                                    fontWeight: 600,
                                    color: 'var(--md-body)',
                                    marginBottom: 2,
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: 6,
                                  }}
                                >
                                  {def.name}
                                  {def.recommended && (
                                    <span
                                      style={{
                                        fontSize: 9,
                                        color: 'var(--md-success)',
                                        fontWeight: 700,
                                        border: '1px solid var(--md-success)',
                                        borderRadius: 3,
                                        padding: '0 4px',
                                      }}
                                    >
                                      推荐
                                    </span>
                                  )}
                                  {def.warning && (
                                    <span
                                      style={{
                                        fontSize: 9,
                                        color: 'var(--md-error)',
                                        fontWeight: 700,
                                        border: '1px solid var(--md-error)',
                                        borderRadius: 3,
                                        padding: '0 4px',
                                      }}
                                    >
                                      警告
                                    </span>
                                  )}
                                </div>
                                <div
                                  style={{
                                    fontSize: 10.5,
                                    color: 'var(--md-body-light)',
                                    lineHeight: 1.4,
                                  }}
                                >
                                  {def.description}
                                </div>
                                <div
                                  style={{
                                    fontFamily: 'var(--md-font-mono)',
                                    fontSize: 10,
                                    color: 'var(--md-muted)',
                                    marginTop: 4,
                                  }}
                                >
                                  {def.flag}
                                </div>
                              </div>
                              {selected ? (
                                <span
                                  style={{
                                    fontSize: 11,
                                    color: 'var(--md-success)',
                                    fontWeight: 600,
                                  }}
                                >
                                  已添加
                                </span>
                              ) : (
                                <button
                                  onClick={() => handleAddArgument(def)}
                                  className="md-btn md-btn-primary"
                                  style={{ fontSize: 11, padding: '4px 10px' }}
                                  disabled={!jvmState?.hasServer || jvmState.isRunning}
                                  // Bug17: 禁用提示
                                  title={
                                    !jvmState?.hasServer
                                      ? '请先选择一个服务器'
                                      : jvmState?.isRunning
                                      ? '服务器运行中无法添加 JVM 参数，请先停服'
                                      : `添加参数「${def.name}」`
                                  }
                                >
                                  + 添加
                                </button>
                              )}
                            </div>
                          )
                        })}
                        {filteredDefinitions.length === 0 && (
                          <div className="md-empty-state" style={{ padding: '12px 8px' }}>
                            <div className="md-empty-state-text" style={{ fontSize: 11 }}>
                              该分类下暂无参数
                            </div>
                          </div>
                        )}
                      </div>
                    </Reveal>

                    {/* 自定义参数卡片 */}
                    <Reveal direction="up" delay={280} className="md-card md-card-elevated" style={{ padding: 16 }}>
                      <div
                        style={{
                          fontSize: 13,
                          fontWeight: 700,
                          marginBottom: 12,
                          color: 'var(--md-body)',
                        }}
                      >
                        [CFG]️ 自定义参数
                      </div>
                      <div className="flex items-center" style={{ gap: 8 }}>
                        <input
                          value={customArgInput}
                          onChange={(e) => setCustomArgInput(e.target.value)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') handleAddCustomArg()
                          }}
                          className="md-input flex-1"
                          placeholder="输入自定义参数，如 -XX:+UnlockExperimentalVMOptions"
                          disabled={!jvmState?.hasServer || jvmState.isRunning}
                          // Bug17: 禁用提示
                          title={
                            !jvmState?.hasServer
                              ? '请先选择一个服务器'
                              : jvmState?.isRunning
                              ? '服务器运行中无法添加自定义参数，请先停服'
                              : '输入自定义 JVM 参数（按 Enter 或点击「添加」提交）'
                          }
                        />
                        <button
                          onClick={handleAddCustomArg}
                          className="md-btn md-btn-primary"
                          style={{ fontSize: 11 }}
                          disabled={!jvmState?.hasServer || jvmState.isRunning || !customArgInput.trim()}
                          // Bug17: 禁用提示
                          title={
                            !jvmState?.hasServer
                              ? '请先选择一个服务器'
                              : jvmState?.isRunning
                              ? '服务器运行中无法添加自定义参数，请先停服'
                              : !customArgInput.trim()
                              ? '请先在左侧输入自定义参数'
                              : '将自定义参数添加到已选参数列表'
                          }
                        >
                          添加
                        </button>
                      </div>
                    </Reveal>

                    {/* 参数编辑弹窗 */}
                    {editingArg && (
                      <div
                        style={{
                          position: 'fixed',
                          inset: 0,
                          background: 'rgba(0,0,0,0.5)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          zIndex: 9999,
                        }}
                        onClick={() => {
                          editingArgSnapshotRef.current = null
                          setEditingArg(null)
                        }}
                      >
                        <div
                          className="md-card"
                          style={{
                            padding: 20,
                            width: 360,
                            maxWidth: '90vw',
                          }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <div
                            style={{
                              fontSize: 14,
                              fontWeight: 700,
                              marginBottom: 4,
                              color: 'var(--md-body)',
                            }}
                          >
                            {editingArg.mode === 'add' ? '添加参数' : '编辑参数'}
                          </div>
                          <div
                            style={{
                              fontSize: 11,
                              color: 'var(--md-body-light)',
                              marginBottom: 16,
                            }}
                          >
                            {editingArg.def.name}
                          </div>

                          {editingArg.def.description && (
                            <div
                              style={{
                                fontSize: 11,
                                color: 'var(--md-body-light)',
                                marginBottom: 12,
                                padding: 8,
                                background: 'var(--md-card-hover)',
                                borderRadius: 'var(--md-radius-small)',
                              }}
                            >
                              {editingArg.def.description}
                            </div>
                          )}

                          {/* 根据值类型显示不同控件 */}
                          {editingArg.def.valueType === 'BooleanFlag' ? (
                            <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
                              <button
                                onClick={() =>
                                  setEditingArg({ ...editingArg, value: 'true' })
                                }
                                className={clsx(
                                  'md-btn',
                                  editingArg.value === 'true'
                                    ? 'md-btn-primary'
                                    : 'md-btn-outlined',
                                )}
                                style={{ flex: 1 }}
                              >
                                <IconByName name="check" size={12} /> 启用 (+)
                              </button>
                              <button
                                onClick={() =>
                                  setEditingArg({ ...editingArg, value: 'false' })
                                }
                                className={clsx(
                                  'md-btn',
                                  editingArg.value === 'false'
                                    ? 'md-btn-primary'
                                    : 'md-btn-outlined',
                                )}
                                style={{ flex: 1 }}
                              >
                                <IconByName name="close" size={12} /> 禁用 (-)
                              </button>
                            </div>
                          ) : editingArg.def.valueType === 'Enum' &&
                            editingArg.def.allowedValues ? (
                            <div
                              style={{
                                display: 'flex',
                                flexDirection: 'column',
                                gap: 4,
                                marginBottom: 16,
                              }}
                            >
                              <div
                                style={{
                                  fontSize: 11,
                                  color: 'var(--md-body-light)',
                                  marginBottom: 4,
                                }}
                              >
                                选择值：
                              </div>
                              {editingArg.def.allowedValues.map((val) => (
                                <button
                                  key={val}
                                  onClick={() => setEditingArg({ ...editingArg, value: val })}
                                  className={clsx(
                                    'md-btn',
                                    editingArg.value === val
                                      ? 'md-btn-primary'
                                      : 'md-btn-outlined',
                                  )}
                                  style={{ textAlign: 'left', fontSize: 11 }}
                                >
                                  {val}
                                </button>
                              ))}
                            </div>
                          ) : (
                            <div style={{ marginBottom: 16 }}>
                              <div
                                style={{
                                  fontSize: 11,
                                  color: 'var(--md-body-light)',
                                  marginBottom: 4,
                                }}
                              >
                                值
                                {editingArg.def.defaultValue && (
                                  <span style={{ opacity: 0.7 }}>
                                    {' '}
                                    （默认：{editingArg.def.defaultValue}）
                                  </span>
                                )}
                              </div>
                              <input
                                value={editingArg.value}
                                onChange={(e) =>
                                  setEditingArg({ ...editingArg, value: e.target.value })
                                }
                                className="md-input"
                                placeholder={editingArg.def.defaultValue ?? ''}
                              />
                              {(editingArg.def.minimumValue || editingArg.def.maximumValue) && (
                                <div
                                  style={{
                                    fontSize: 10,
                                    color: 'var(--md-muted)',
                                    marginTop: 4,
                                  }}
                                >
                                  范围：
                                  {editingArg.def.minimumValue ?? '无下限'} ~{' '}
                                  {editingArg.def.maximumValue ?? '无上限'}
                                </div>
                              )}
                            </div>
                          )}

                          {editingArg.def.warning && (
                            <div
                              style={{
                                fontSize: 11,
                                color: 'var(--md-error)',
                                marginBottom: 12,
                                padding: 8,
                                background: 'rgba(239,68,68,0.1)',
                                borderRadius: 'var(--md-radius-small)',
                              }}
                            >
                              [WARN] {editingArg.def.warning}
                            </div>
                          )}

                          <div className="flex items-center" style={{ gap: 8 }}>
                            <button
                              onClick={() => {
                                editingArgSnapshotRef.current = null
                                setEditingArg(null)
                              }}
                              className="md-btn md-btn-outlined"
                              style={{ flex: 1 }}
                            >
                              取消
                            </button>
                            <button
                              onClick={handleSaveEditingArg}
                              className="md-btn md-btn-primary"
                              style={{ flex: 1 }}
                            >
                              确定
                            </button>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* ─── 命令预览 Tab ─── */}
                {detailTab === 'command' && (
                  <div style={{ display: 'flex', flexDirection: 'column', minHeight: 400 }}>
                    <div className="flex items-center" style={{ marginBottom: 12, gap: 12 }}>
                      <div style={{ fontSize: 15, fontWeight: 700, color: 'var(--md-body)' }}>
                        完整启动命令
                      </div>
                      <button
                        onClick={handleCopyCommand}
                        className="md-btn md-btn-outlined"
                      >
                        [LOG] 复制
                      </button>
                    </div>
                    <div
                      className="md-terminal"
                      style={{
                        flex: 1,
                        padding: 16,
                        overflow: 'auto',
                        borderRadius: 'var(--md-radius)',
                      }}
                    >
                      <pre
                        style={{
                          fontFamily: 'var(--md-font-mono)',
                          fontSize: 13,
                          color: 'var(--md-success-foreground)',
                          whiteSpace: 'pre-wrap',
                          wordBreak: 'break-all',
                          margin: 0,
                        }}
                      >
                        {selectedServer.fullCommandLine}
                      </pre>
                    </div>
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </div>

      {/* ═══ 底部：启动命令预览条 ═══ */}
      <div
        className="flex items-center"
        style={{
          background: 'var(--md-terminal-background)',
          borderTop: '1px solid var(--md-card-subtle-border)',
          padding: '8px 12px',
          gap: 6,
        }}
      >
        <IconByName name="terminal" size={14} style={{ color: 'var(--md-success-foreground)' }} />
        <span style={{ color: 'var(--md-success-foreground)', fontSize: 11, fontWeight: 600 }}>
          启动命令
        </span>
        <div
          className="flex-1"
          style={{
            fontFamily: 'var(--md-font-mono)',
            fontSize: 11,
            color: 'var(--md-success-foreground)',
            margin: '0 10px',
            whiteSpace: 'nowrap',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
          }}
          title={selectedServer?.fullCommandLine || ''}
        >
          {selectedServer?.fullCommandLine || ''}
        </div>
        <button
          onClick={handleCopyCommand}
          className="md-btn md-btn-flat md-btn-icon"
          title="复制启动命令到剪贴板"
          disabled={!selectedServer}
        >
          <IconByName name="copy" size={14} />
        </button>
      </div>

      {/* ═══ 检测中遮罩 ═══ */}
      {isBusy && (
        <div
          className="absolute inset-0 flex flex-col items-center justify-center"
          style={{
            background: 'var(--md-loading-overlay)',
            borderRadius: 'var(--md-radius)',
          }}
        >
          <div
            className="md-spin"
            style={{
              width: 48,
              height: 48,
              border: '4px solid var(--md-white)',
              borderTopColor: 'transparent',
              borderRadius: '50%',
              marginBottom: 12,
            }}
          />
          <span style={{ color: 'var(--md-white)', fontSize: 14, opacity: 0.8 }}>
            {busyReason || '处理中...'}
          </span>
        </div>
      )}
    </div>
  )
}
