import { useCallback, useEffect, useState } from 'react'
import {
  FaBolt,
  FaMicrochip,
  FaClock,
  FaPlug,
  FaFlask,
  FaPowerOff,
  FaRotateRight,
} from 'react-icons/fa6'
import {
  getCpuPowerCapabilities,
  applyPowerProfile,
  restorePowerProfile,
  getCpuSetTopology,
  enableTimerResolution,
  disableTimerResolution,
  getTimerResolutionState,
  startPowerRequest,
  stopPowerRequest,
  getPowerRequestState,
  getPowerManagementState,
  setPowerManagementEnabled,
} from '@/utils/bridge'
import type {
  CpuPowerCapabilities,
  PowerProfile,
  ProcessQoSTier,
  CpuSetTopology,
  TimerResolutionResult,
  PowerRequestResult,
} from '@/types/bridge'

const powerProfileOptions: Array<{ value: PowerProfile; label: string; desc: string; color: string }> = [
  { value: 'UltimatePerformance', label: '极致性能', desc: 'Aggressive 睿频 + 100% 处理器状态 + 激进升频', color: 'var(--md-danger)' },
  { value: 'Balanced', label: '平衡', desc: '标准睿频 + 100% 处理器状态', color: 'var(--md-primary-hue-mid)' },
  { value: 'Efficient', label: '能效优先', desc: '能效优先的睿频 + 90% 处理器状态', color: 'var(--md-success)' },
  { value: 'PowerSaver', label: '极限省电', desc: '禁用睿频 + 80% 处理器状态', color: 'var(--md-body-light)' },
]

const timerOptions: Array<{ tier: number; label: string; periodMs: number; desc: string }> = [
  { tier: 0, label: '系统默认 (15.6ms)', periodMs: 0, desc: '不修改系统 tick，最低功耗' },
  { tier: 1, label: '1ms (推荐 MC 服)', periodMs: 1, desc: '显著降低 20 TPS 主循环抖动，少量功耗' },
  { tier: 2, label: '0.5ms (极致)', periodMs: 1, desc: '更精细，但增加空闲功耗（实际仍受限于系统最小值）' },
]

export function PowerPage(): JSX.Element {
  const [cpuPowerCaps, setCpuPowerCaps] = useState<CpuPowerCapabilities | null>(null)
  const [applyingProfile, setApplyingProfile] = useState<PowerProfile | null>(null)
  const [powerError, setPowerError] = useState<string | null>(null)
  const [restoringProfile, setRestoringProfile] = useState(false)

  // ═══════════════════════════════════════════════════════════════════
  // ⚠️ 以下设置项当前仅保存在浏览器 localStorage，未持久化到后端 C# AppConfig
  //    —— 它们是「服务器启动时自动应用」的偏好设置，而非实时系统状态。
  //    TODO: 待后端提供 power:saveProfile / cpuPower:savePreferences 等
  //          持久化 API 后，改为通过 bridge 调用回传后端配置。
  // ═══════════════════════════════════════════════════════════════════
  const [serverQoSTier, setServerQoSTier] = useState<ProcessQoSTier>(() => {
    try {
      const saved = localStorage.getItem('msmc_server_qos')
      if (saved === 'High' || saved === 'Eco' || saved === 'Unset') return saved
    } catch { /* ignore */ }
    return 'High'
  })

  const [cpuSetTopology, setCpuSetTopology] = useState<CpuSetTopology | null>(null)
  const [autoPinPCores, setAutoPinPCores] = useState<boolean>(() => {
    try { return localStorage.getItem('msmc_auto_pin_pcores') === 'true' } catch { return false }
  })
  const [timerTier, setTimerTier] = useState<number>(() => {
    try {
      const saved = localStorage.getItem('msmc_timer_tier')
      return saved ? Number(saved) : 0
    } catch { return 0 }
  })
  const [timerState, setTimerState] = useState<TimerResolutionResult | null>(null)
  const [serverBoostMode, setServerBoostMode] = useState<'auto' | 'disable'>(() => {
    try {
      const saved = localStorage.getItem('msmc_server_boost')
      return saved === 'disable' ? 'disable' : 'auto'
    } catch { return 'auto' }
  })
  const [powerReqState, setPowerReqState] = useState<PowerRequestResult | null>(null)
  const [timerApplying, setTimerApplying] = useState(false)
  const [powerReqApplying, setPowerReqApplying] = useState(false)

  // 电源管理模块开关状态：null=加载中, true=已启用, false=未启用
  const [pmEnabled, setPmEnabled] = useState<boolean | null>(null)
  const [pmToggling, setPmToggling] = useState(false)
  const [pmToggleMsg, setPmToggleMsg] = useState<string | null>(null)

  const refreshCpuPowerCaps = useCallback(async (): Promise<void> => {
    try {
      const caps = await getCpuPowerCapabilities()
      if (!caps.success) {
        setPowerError(caps.error ?? '获取 CPU 电源能力失败')
        return
      }
      setCpuPowerCaps(caps)
      setPowerError(null)
    } catch (e) {
      console.error('获取 CPU 电源能力失败:', e)
      setPowerError(e instanceof Error ? e.message : String(e))
    }
  }, [])

  const handleApplyPowerProfile = useCallback(async (profile: PowerProfile): Promise<void> => {
    setApplyingProfile(profile)
    setPowerError(null)
    try {
      const r = await applyPowerProfile(profile)
      if (!r.success) {
        setPowerError(r.error ?? '应用失败')
      }
      await refreshCpuPowerCaps()
    } catch (e) {
      setPowerError(e instanceof Error ? e.message : String(e))
    } finally {
      setApplyingProfile(null)
    }
  }, [refreshCpuPowerCaps])

  const handleRestorePowerProfile = useCallback(async (): Promise<void> => {
    setRestoringProfile(true)
    setPowerError(null)
    try {
      const r = await restorePowerProfile()
      if (!r.success) {
        setPowerError(r.error ?? '还原失败')
      }
      await refreshCpuPowerCaps()
    } catch (e) {
      setPowerError(e instanceof Error ? e.message : String(e))
    } finally {
      setRestoringProfile(false)
    }
  }, [refreshCpuPowerCaps])

  const handleSetServerQoS = useCallback((tier: ProcessQoSTier): void => {
    setServerQoSTier(tier)
    try { localStorage.setItem('msmc_server_qos', tier) } catch { /* ignore */ }
  }, [])

  const refreshCpuSetTopology = useCallback(async (): Promise<void> => {
    try {
      const topo = await getCpuSetTopology()
      setCpuSetTopology(topo)
    } catch (e) {
      console.error('获取 CPU Set 拓扑失败:', e)
    }
  }, [])

  const refreshTimerState = useCallback(async (): Promise<void> => {
    try {
      const r = await getTimerResolutionState()
      setTimerState(r)
    } catch (e) {
      console.error('获取定时器精度状态失败:', e)
    }
  }, [])

  const refreshPowerRequestState = useCallback(async (): Promise<void> => {
    try {
      const r = await getPowerRequestState()
      setPowerReqState(r)
    } catch (e) {
      console.error('获取 Power Request 状态失败:', e)
    }
  }, [])

  const handleToggleAutoPinPCores = useCallback((enabled: boolean): void => {
    setAutoPinPCores(enabled)
    try { localStorage.setItem('msmc_auto_pin_pcores', enabled ? 'true' : 'false') } catch { /* ignore */ }
  }, [])

  const handleSetTimerTier = useCallback(async (tier: number): Promise<void> => {
    const prevTier = timerTier
    setTimerApplying(true)
    setTimerTier(tier)
    try { localStorage.setItem('msmc_timer_tier', String(tier)) } catch { /* ignore */ }
    const opt = timerOptions.find((o) => o.tier === tier)
    if (!opt) {
      setTimerTier(prevTier)
      try { localStorage.setItem('msmc_timer_tier', String(prevTier)) } catch { /* ignore */ }
      setTimerApplying(false)
      return
    }
    try {
      if (opt.periodMs > 0) {
        const r = await enableTimerResolution(opt.periodMs)
        if (!r.success) {
          setTimerTier(prevTier)
          try { localStorage.setItem('msmc_timer_tier', String(prevTier)) } catch { /* ignore */ }
        }
      } else {
        const r = await disableTimerResolution()
        if (!r.success) {
          setTimerTier(prevTier)
          try { localStorage.setItem('msmc_timer_tier', String(prevTier)) } catch { /* ignore */ }
        }
      }
      await refreshTimerState()
    } catch (e) {
      console.error('设置定时器精度失败:', e)
      setTimerTier(prevTier)
      try { localStorage.setItem('msmc_timer_tier', String(prevTier)) } catch { /* ignore */ }
    } finally {
      setTimerApplying(false)
    }
  }, [refreshTimerState, timerTier])

  const handleSetServerBoostMode = useCallback((mode: 'auto' | 'disable'): void => {
    setServerBoostMode(mode)
    try { localStorage.setItem('msmc_server_boost', mode) } catch { /* ignore */ }
  }, [])

  const handleTogglePowerRequest = useCallback(async (): Promise<void> => {
    setPowerReqApplying(true)
    try {
      if (powerReqState?.active) {
        await stopPowerRequest()
      } else {
        await startPowerRequest('MSMC 服务器运行中')
      }
      await refreshPowerRequestState()
    } catch (e) {
      console.error('切换 Power Request 失败:', e)
    } finally {
      setPowerReqApplying(false)
    }
  }, [powerReqState?.active, refreshPowerRequestState])

  // 首次加载：先查电源管理开关状态，已启用才拉取功能数据
  useEffect(() => {
    let cancelled = false
    ;(async (): Promise<void> => {
      try {
        const st = await getPowerManagementState()
        if (cancelled) return
        setPmEnabled(!!st.enabled)
        if (st.enabled) {
          refreshCpuPowerCaps()
          refreshCpuSetTopology()
          refreshTimerState()
          refreshPowerRequestState()
        }
      } catch (e) {
        console.error('查询电源管理开关状态失败:', e)
        if (!cancelled) setPmEnabled(false)
      }
    })()
    return () => { cancelled = true }
  }, [refreshCpuPowerCaps, refreshCpuSetTopology, refreshTimerState, refreshPowerRequestState])

  const handleTogglePowerManagement = useCallback(async (enable: boolean): Promise<void> => {
    setPmToggling(true)
    setPmToggleMsg(null)
    try {
      const r = await setPowerManagementEnabled(enable)
      if (r.success) {
        setPmEnabled(enable)
        setPmToggleMsg(enable
          ? '已启用电源管理，请重启 MSMC 使其完全生效。'
          : '已关闭电源管理，重启后功能将完全卸载。')
      } else {
        setPmToggleMsg(r.error ?? '操作失败')
      }
    } catch (e) {
      setPmToggleMsg(e instanceof Error ? e.message : String(e))
    } finally {
      setPmToggling(false)
    }
  }, [])

  return (
    <div className="md-page-enter p-4 pb-8 max-w-4xl mx-auto">
      <div className="flex items-center mb-4">
        <FaBolt size={18} style={{ marginRight: 8, color: 'var(--md-warning)' }} />
        <h1 className="text-lg font-bold text-[var(--md-body)]">电源管理</h1>
      </div>

      {/* 启用开关卡片 —— 未启用时这是页面上唯一的卡片 */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item">
        <div className="flex items-center" style={{ gap: 12 }}>
          <div
            style={{
              width: 40,
              height: 40,
              borderRadius: 'var(--md-radius-small)',
              background: pmEnabled ? 'var(--md-primary-subtle-background)' : 'var(--md-card-hover)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <FaPowerOff size={20} style={{ color: pmEnabled ? 'var(--md-success)' : 'var(--md-body-light)' }} />
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>启用电源管理</div>
            <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginTop: 2, lineHeight: 1.5 }}>
              默认关闭。启用后才会加载 CPU 电源档位、QoS 调度等底层管控能力。
              {' '}
              {pmEnabled
                ? '当前已启用，下方功能可用。'
                : '当前未启用，下方所有功能均不加载、不工作。'}
            </div>
          </div>
          <label className="md-toggle" style={{ flexShrink: 0 }}>
            <input
              type="checkbox"
              checked={pmEnabled ?? false}
              disabled={pmToggling || pmEnabled === null}
              onChange={(e) => handleTogglePowerManagement(e.target.checked)}
            />
            <span className="md-toggle-slider" />
          </label>
        </div>

        {pmToggleMsg && (
          <div
            style={{
              marginTop: 12,
              padding: '8px 12px',
              borderRadius: 6,
              fontSize: 12,
              borderLeft: '3px solid var(--md-warning)',
              background: 'var(--md-card-hover)',
              color: 'var(--md-body)',
              lineHeight: 1.5,
            }}
          >
            <FaRotateRight size={12} style={{ marginRight: 6, color: 'var(--md-warning)' }} />
            {pmToggleMsg}
          </div>
        )}
      </div>

      {/* 未启用或加载中时，不渲染任何功能卡片 */}
      {pmEnabled !== true ? null : (
        <>
          <div
            className="md-card p-3 mb-4 md-stagger-item"
            style={{
              borderLeft: '3px solid var(--md-warning)',
              background: 'var(--md-primary-subtle-background)',
              fontSize: 12,
              color: 'var(--md-body)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
              <FaFlask size={13} style={{ color: 'var(--md-warning)' }} />
              <span style={{ fontWeight: 600, color: 'var(--md-warning)' }}>实验性功能</span>
            </div>
            <div style={{ color: 'var(--md-body-light)', lineHeight: 1.6 }}>
              本页涉及系统级电源策略、处理器睿频与调度参数的底层修改，属于实验性能力，不对其安全性与稳定性作保证。
              异常断电或操作不当可能导致系统电源方案损坏、处理器调度异常，请确保已了解风险后再使用，并在使用前关闭其他重要任务。
            </div>
          </div>

      {/* CPU 电源档位 */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item">
        <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 4px 0' }}>
          <FaBolt size={14} style={{ marginRight: 6, color: 'var(--md-warning)' }} />
          CPU 电源档位与睿频管控
        </h2>
        <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginBottom: 12 }}>
          仿安卓性能模式：系统级睿频档位（PERFBOOSTMODE）+ 进程级 QoS 能效标签。修改前自动快照，退出/崩溃可还原。
        </div>

        <div
          style={{
            display: 'flex',
            gap: 12,
            flexWrap: 'wrap',
            padding: '8px 12px',
            background: 'var(--md-card-bg)',
            borderRadius: 8,
            marginBottom: 16,
            fontSize: 11,
          }}
        >
          <span>
            当前档位：
            <strong style={{ color: cpuPowerCaps?.currentBoostMode === 2 ? 'var(--md-danger)' : 'var(--md-body)' }}>
              {cpuPowerCaps?.currentProfileName ?? '加载中...'}
            </strong>
            {cpuPowerCaps?.currentBoostMode !== undefined && cpuPowerCaps.currentBoostMode >= 0 && (
              <span style={{ color: 'var(--md-body-lighter)' }}> (BoostMode={cpuPowerCaps.currentBoostMode})</span>
            )}
          </span>
          <span style={{ color: cpuPowerCaps?.isAdmin ? 'var(--md-success)' : 'var(--md-warning)' }}>
            {cpuPowerCaps?.isAdmin ? '✓ 管理员' : '⚠ 非管理员（仅可查询，无法修改电源策略）'}
          </span>
          {cpuPowerCaps?.hasPendingCrashSnapshot && (
            <span style={{ color: 'var(--md-danger)' }}>⚠ 检测到未还原的崩溃快照</span>
          )}
        </div>

        <div className="md-label" style={{ marginBottom: 8 }}>
          系统电源档位（睿频激进型，需管理员）
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 8, marginBottom: 12 }}>
          {powerProfileOptions.map((opt) => {
            const isCurrent = cpuPowerCaps?.currentProfileName === opt.value
            const isApplying = applyingProfile === opt.value
            return (
              <button
                key={opt.value}
                onClick={() => handleApplyPowerProfile(opt.value)}
                disabled={isApplying || applyingProfile !== null || restoringProfile || (cpuPowerCaps !== null && !cpuPowerCaps.canModifyPowerProfile)}
                style={{
                  padding: '10px 12px',
                  borderRadius: 8,
                  border: isCurrent ? `2px solid ${opt.color}` : '1px solid var(--md-subtle-border)',
                  background: isCurrent ? `${opt.color}18` : 'var(--md-card-bg)',
                  cursor: isApplying || applyingProfile !== null || restoringProfile || (cpuPowerCaps !== null && !cpuPowerCaps.canModifyPowerProfile) ? 'not-allowed' : 'pointer',
                  opacity: isApplying || applyingProfile !== null ? 0.6 : 1,
                  textAlign: 'left',
                  transition: 'all 0.15s ease',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
                  <span style={{ width: 8, height: 8, borderRadius: '50%', background: opt.color, display: 'inline-block' }} />
                  <strong style={{ fontSize: 13, color: 'var(--md-body)' }}>{opt.label}</strong>
                  {isCurrent && <span style={{ fontSize: 10, color: opt.color, fontWeight: 700 }}>● 当前</span>}
                </div>
                <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', lineHeight: 1.4 }}>
                  {isApplying ? '应用中...' : opt.desc}
                </div>
              </button>
            )
          })}
        </div>

        <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
          <button
            onClick={handleRestorePowerProfile}
            disabled={restoringProfile || applyingProfile !== null}
            className="md-btn md-btn-outlined"
            style={{ fontSize: 12 }}
          >
            {restoringProfile ? '还原中...' : '还原原始电源策略'}
          </button>
        </div>

        {powerError && (
          <div style={{ fontSize: 11, color: 'var(--md-danger)', marginBottom: 12, padding: '6px 10px', background: 'var(--md-danger-bg, rgba(255,0,0,0.06))', borderRadius: 6 }}>
            {powerError}
          </div>
        )}

        <div style={{ borderTop: '1px solid var(--md-subtle-border)', paddingTop: 12, marginTop: 4 }}>
          <div className="md-label" style={{ marginBottom: 6 }}>
            MC 服务器进程 QoS 能效标签（启动时自动应用）
          </div>
          <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.5 }}>
            EcoQoS 等同安卓 schedtune：High=解除节流高性能 / Eco=降频调度到能效核 / Unset=系统默认
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            {([
              { value: 'High', label: 'High（高性能）', color: 'var(--md-danger)' },
              { value: 'Eco', label: 'Eco（能效优先）', color: 'var(--md-success)' },
              { value: 'Unset', label: 'Unset（系统默认）', color: 'var(--md-body-light)' },
            ] as Array<{ value: ProcessQoSTier; label: string; color: string }>).map((opt) => {
              const isSelected = serverQoSTier === opt.value
              return (
                <button
                  key={opt.value}
                  onClick={() => handleSetServerQoS(opt.value)}
                  style={{
                    padding: '6px 14px',
                    fontSize: 12,
                    fontWeight: 600,
                    color: isSelected ? '#fff' : opt.color,
                    background: isSelected ? opt.color : 'transparent',
                    border: `1px solid ${opt.color}`,
                    borderRadius: 6,
                    cursor: 'pointer',
                    transition: 'all 0.12s ease',
                  }}
                >
                  {opt.label}
                </button>
              )
            })}
          </div>
          <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginTop: 6 }}>
            当前选择：<strong>{serverQoSTier}</strong> — 将在服务器启动时自动应用到此进程
          </div>
          {/* ⚠️ 持久化提示 */}
          <div
            style={{
              marginTop: 8,
              padding: '6px 10px',
              borderRadius: 6,
              fontSize: 10,
              lineHeight: 1.5,
              background: 'var(--md-warning-subtle-background, rgba(255, 193, 7, 0.08))',
              color: 'var(--md-warning, #f39c12)',
              borderLeft: '2px solid var(--md-warning, #f39c12)',
            }}
          >
            注意：此偏好当前仅保存在本地（localStorage），尚未同步到后端配置。
            清除浏览器数据或重装 MSMC 后将恢复默认值。
          </div>
        </div>
      </div>

      {/* 用户层调度 */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item">
        <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 4px 0' }}>
          <FaMicrochip size={14} style={{ marginRight: 6, color: 'var(--md-primary)' }} />
          用户层最大权限调度（零 SDK / 零驱动）
        </h2>
        <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginBottom: 12, lineHeight: 1.5 }}>
          Win32 用户态 API 直通：CPU Set P/E 核路由 · winmm 定时器精度 · Priority Boost · Power Request 防睡眠
        </div>

        {/* CPU Set P/E 核路由 */}
        <div style={{
          padding: '10px 12px',
          background: 'var(--md-card-bg)',
          borderRadius: 8,
          marginBottom: 12,
          border: cpuSetTopology?.isHybridCpu
            ? '1px solid var(--md-primary)'
            : '1px solid var(--md-subtle-border)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 6 }}>
            <strong style={{ fontSize: 12, color: 'var(--md-body)' }}>CPU Set P/E 核路由</strong>
            {cpuSetTopology && (
              <span style={{
                fontSize: 10,
                fontWeight: 700,
                padding: '2px 8px',
                borderRadius: 10,
                background: cpuSetTopology.isHybridCpu
                  ? 'color-mix(in srgb, var(--md-primary) 18%, transparent)'
                  : 'color-mix(in srgb, var(--md-body-light) 15%, transparent)',
                color: cpuSetTopology.isHybridCpu ? 'var(--md-primary)' : 'var(--md-body-light)',
              }}>
                {cpuSetTopology.isHybridCpu ? '异构 CPU' : '同构 CPU'}
              </span>
            )}
          </div>
          <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.4 }}>
            Intel 12 代+ / AMD Ryzen 7000+ X3D 异构 CPU 可把 MC 主进程锁定到 P-core（性能核），
            避免 E-core 误调度导致 TPS 抖动。SchedulingClass&gt;0 的 CPU Set 视为 P-core。
          </div>
          {cpuSetTopology?.success && (
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', fontSize: 10, marginBottom: 8 }}>
              <span style={{ color: 'var(--md-body)' }}>
                检测到 <strong>{cpuSetTopology.totalCpuSets}</strong> 个 CPU Set
              </span>
              <span style={{ color: 'var(--md-danger)' }}>
                P-core: <strong>{cpuSetTopology.performanceCpuSetCount}</strong>
              </span>
              <span style={{ color: 'var(--md-success)' }}>
                E-core: <strong>{cpuSetTopology.efficiencyCpuSetCount}</strong>
              </span>
            </div>
          )}
          <label style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            cursor: cpuSetTopology?.isHybridCpu ? 'pointer' : 'not-allowed',
            opacity: cpuSetTopology?.isHybridCpu ? 1 : 0.5,
            padding: '6px 0',
          }}>
            <input
              type="checkbox"
              checked={autoPinPCores}
              disabled={!cpuSetTopology?.isHybridCpu}
              onChange={(e) => handleToggleAutoPinPCores(e.target.checked)}
              style={{ width: 14, height: 14, cursor: 'pointer' }}
            />
            <span style={{ fontSize: 11, color: 'var(--md-body)' }}>
              服务器启动时自动路由到 P-core
              {!cpuSetTopology?.isHybridCpu && (
                <span style={{ color: 'var(--md-warning)', marginLeft: 4 }}>
                  （当前 CPU 非异构，无需路由）
                </span>
              )}
            </span>
          </label>
          {/* ⚠️ 持久化提示 */}
          {cpuSetTopology?.isHybridCpu && (
            <div
              style={{
                marginTop: 6,
                padding: '5px 10px',
                borderRadius: 6,
                fontSize: 10,
                lineHeight: 1.5,
                background: 'var(--md-warning-subtle-background, rgba(255, 193, 7, 0.08))',
                color: 'var(--md-warning, #f39c12)',
                borderLeft: '2px solid var(--md-warning, #f39c12)',
              }}
            >
              注意：此偏好当前仅保存在本地（localStorage）。
            </div>
          )}
        </div>

        {/* winmm 定时器精度 */}
        <div style={{
          padding: '10px 12px',
          background: 'var(--md-card-bg)',
          borderRadius: 8,
          marginBottom: 12,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
            <FaClock size={11} style={{ color: 'var(--md-warning)' }} />
            <strong style={{ fontSize: 12, color: 'var(--md-body)' }}>winmm 定时器精度</strong>
            {timerState?.enabled && (
              <span style={{
                fontSize: 9,
                fontWeight: 700,
                padding: '1px 6px',
                borderRadius: 8,
                background: 'color-mix(in srgb, var(--md-success) 18%, transparent)',
                color: 'var(--md-success)',
              }}>
                ● 已启用 {timerState.periodMs}ms
              </span>
            )}
          </div>
          <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.4 }}>
            默认系统 tick 15.6ms → 提到 1ms 可显著降低 MC 20 TPS 主循环抖动。仅在服务器运行期间启用，
            应用退出时自动还原（Dispose 自动调用 timeEndPeriod）。
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 6 }}>
            {timerOptions.map((opt) => {
              const isSelected = timerTier === opt.tier
              return (
                <button
                  key={opt.tier}
                  onClick={() => handleSetTimerTier(opt.tier)}
                  disabled={timerApplying}
                  style={{
                    padding: '8px 6px',
                    borderRadius: 6,
                    border: isSelected
                      ? '2px solid var(--md-primary)'
                      : '1px solid var(--md-subtle-border)',
                    background: isSelected
                      ? 'color-mix(in srgb, var(--md-primary) 10%, transparent)'
                      : 'transparent',
                    cursor: 'pointer',
                    textAlign: 'left',
                    transition: 'all 0.12s ease',
                  }}
                >
                  <div style={{
                    fontSize: 11,
                    fontWeight: 600,
                    color: isSelected ? 'var(--md-primary)' : 'var(--md-body)',
                    marginBottom: 2,
                  }}>
                    {opt.label}
                  </div>
                  <div style={{ fontSize: 9, color: 'var(--md-body-lighter)', lineHeight: 1.3 }}>
                    {opt.desc}
                  </div>
                </button>
              )
            })}
          </div>
        </div>

        {/* Priority Boost + Power Request */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <div style={{
            padding: '10px 12px',
            background: 'var(--md-card-bg)',
            borderRadius: 8,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
              <FaBolt size={11} style={{ color: 'var(--md-warning)' }} />
              <strong style={{ fontSize: 12, color: 'var(--md-body)' }}>Priority Boost</strong>
            </div>
            <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.4 }}>
              控制服务器进程在窗口前台/输入事件时是否自动提升优先级。后台服建议禁用以稳定调度。
            </div>
            <div style={{ display: 'flex', gap: 6 }}>
              {([
                { value: 'auto', label: '系统默认' },
                { value: 'disable', label: '禁用前台 boost' },
              ] as Array<{ value: 'auto' | 'disable'; label: string }>).map((opt) => {
                const isSelected = serverBoostMode === opt.value
                return (
                  <button
                    key={opt.value}
                    onClick={() => handleSetServerBoostMode(opt.value)}
                    style={{
                      flex: 1,
                      padding: '6px 8px',
                      fontSize: 11,
                      fontWeight: 600,
                      color: isSelected ? '#fff' : 'var(--md-body)',
                      background: isSelected ? 'var(--md-primary)' : 'transparent',
                      border: `1px solid ${isSelected ? 'var(--md-primary)' : 'var(--md-subtle-border)'}`,
                      borderRadius: 6,
                      cursor: 'pointer',
                      transition: 'all 0.12s ease',
                    }}
                  >
                    {opt.label}
                  </button>
                )
              })}
            </div>
            {/* ⚠️ 持久化提示 */}
            <div
              style={{
                marginTop: 8,
                padding: '5px 10px',
                borderRadius: 6,
                fontSize: 10,
                lineHeight: 1.5,
                background: 'var(--md-warning-subtle-background, rgba(255, 193, 7, 0.08))',
                color: 'var(--md-warning, #f39c12)',
                borderLeft: '2px solid var(--md-warning, #f39c12)',
              }}
            >
              注意：此偏好当前仅保存在本地（localStorage）。
            </div>
          </div>

          <div style={{
            padding: '10px 12px',
            background: 'var(--md-card-bg)',
            borderRadius: 8,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
              <FaPlug size={11} style={{ color: 'var(--md-success)' }} />
              <strong style={{ fontSize: 12, color: 'var(--md-body)' }}>Power Request 防睡眠</strong>
              {powerReqState?.active && (
                <span style={{
                  fontSize: 9,
                  fontWeight: 700,
                  padding: '1px 6px',
                  borderRadius: 8,
                  background: 'color-mix(in srgb, var(--md-success) 18%, transparent)',
                  color: 'var(--md-success)',
                }}>
                  ● 活跃
                </span>
              )}
            </div>
            <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginBottom: 8, lineHeight: 1.4 }}>
              命名化防睡眠请求（比 SetThreadExecutionState 更可靠），崩溃时句柄自动释放。
            </div>
            <button
              onClick={handleTogglePowerRequest}
              disabled={powerReqApplying}
              style={{
                width: '100%',
                padding: '6px 8px',
                fontSize: 11,
                fontWeight: 600,
                color: powerReqState?.active ? 'var(--md-danger)' : '#fff',
                background: powerReqState?.active
                  ? 'transparent'
                  : 'var(--md-success)',
                border: `1px solid ${powerReqState?.active ? 'var(--md-danger)' : 'var(--md-success)'}`,
                borderRadius: 6,
                cursor: 'pointer',
                transition: 'all 0.12s ease',
              }}
            >
              {powerReqState?.active ? '停止 Power Request' : '启动 Power Request'}
            </button>
          </div>
        </div>
      </div>
        </>
      )}
    </div>
  )
}
