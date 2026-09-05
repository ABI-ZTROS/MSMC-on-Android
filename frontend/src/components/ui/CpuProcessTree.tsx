import { useMemo, useState } from 'react'
import type { CpuInfo, ProcessAffinityInfo } from '@/types/bridge'
import { IconByName } from '@/utils/icons'
import { setProcessAffinity } from '@/utils/bridge'

interface CpuProcessTreeProps {
  cpuInfo: CpuInfo | null
  perCoreUsages: number[]
  processAffinities: ProcessAffinityInfo[]
  onKillProcess: (pid: number) => Promise<void>
}

// 字节数转 MB
function bytesToMB(bytes: number): number {
  if (!bytes || bytes <= 0) return 0
  return bytes / (1024 * 1024)
}

type FilterKey = 'all' | 'minecraft' | 'java' | 'user' | 'system'

// 亲和性编辑面板的次级按钮（全选/清空/取消）统一样式
const affinityChipBtnStyle: React.CSSProperties = {
  padding: '4px 10px',
  fontSize: 11,
  fontWeight: 500,
  color: 'var(--md-body-light)',
  background: 'var(--md-subtle-border)',
  border: 'none',
  borderRadius: 6,
  cursor: 'pointer',
}

const FILTER_OPTIONS: { key: FilterKey; label: string; iconName: string }[] = [
  { key: 'all',       label: '全部',       iconName: 'folderTree' },
  { key: 'minecraft', label: 'Minecraft',  iconName: 'gamepad' },
  { key: 'java',      label: 'Java',       iconName: 'java' },
  { key: 'user',      label: '用户',       iconName: 'user' },
  { key: 'system',    label: '系统',       iconName: 'monitor' },
]

export function CpuProcessTree({
  cpuInfo,
  perCoreUsages,
  processAffinities,
  onKillProcess,
}: CpuProcessTreeProps): JSX.Element {
  const [collapsed, setCollapsed] = useState(false)
  const [expandedProcess, setExpandedProcess] = useState<number | null>(null)
  const [killing, setKilling] = useState<number | null>(null)
  const [filter, setFilter] = useState<FilterKey>('all')
  // 亲和性编辑状态：editingPid !== null 时展开核心选择面板；selectedCores 为勾选的核心索引集合
  const [editingPid, setEditingPid] = useState<number | null>(null)
  const [selectedCores, setSelectedCores] = useState<Set<number>>(new Set())
  const [affinitySaving, setAffinitySaving] = useState(false)
  const [affinityError, setAffinityError] = useState<string | null>(null)

  const logicalCores = cpuInfo?.logicalCores ?? perCoreUsages.length ?? 0
  const physicalCores = cpuInfo?.physicalCores ?? 0
  const coreMap = cpuInfo?.logicalToPhysicalCoreMap ?? []

  // 按过滤器筛选进程
  const filteredProcesses = useMemo(() => {
    switch (filter) {
      case 'minecraft':
        return processAffinities.filter(p => p.isMinecraftServer)
      case 'java':
        return processAffinities.filter(p => p.isJavaProcess)
      case 'user':
        return processAffinities.filter(p => !p.isSystemProcess && !p.isJavaProcess)
      case 'system':
        return processAffinities.filter(p => p.isSystemProcess)
      default:
        return processAffinities
    }
  }, [processAffinities, filter])

  // 构建进程占用的核心集合（用于在核心节点上标记）
  const occupiedCores = useMemo(() => {
    const map = new Map<number, ProcessAffinityInfo[]>()
    for (const proc of filteredProcesses) {
      for (const coreIdx of proc.allowedCoreIndices) {
        if (!map.has(coreIdx)) map.set(coreIdx, [])
        map.get(coreIdx)!.push(proc)
      }
    }
    return map
  }, [filteredProcesses])

  // 统计信息
  const stats = useMemo(() => {
    const minecraft = processAffinities.filter(p => p.isMinecraftServer).length
    const java = processAffinities.filter(p => p.isJavaProcess).length
    const system = processAffinities.filter(p => p.isSystemProcess).length
    const total = processAffinities.length
    return { minecraft, java, system, total }
  }, [processAffinities])

  // 按物理核分组逻辑核
  const physicalGroups = useMemo(() => {
    const groups = new Map<number, number[]>()
    for (let i = 0; i < logicalCores; i++) {
      const physical = coreMap[i] ?? Math.floor(i / 2)
      if (!groups.has(physical)) groups.set(physical, [])
      groups.get(physical)!.push(i)
    }
    return groups
  }, [logicalCores, coreMap])

  const getCoreColor = (usage: number): string => {
    if (usage < 50) return 'var(--md-gauge-green)'
    if (usage < 80) return 'var(--md-gauge-yellow)'
    return 'var(--md-gauge-red)'
  }

  // 根据进程类型返回边框颜色
  const getProcessBorderColor = (procs: ProcessAffinityInfo[]): string => {
    if (procs.some(p => p.isMinecraftServer)) return 'var(--md-gauge-red)'
    if (procs.some(p => p.isJavaProcess)) return 'var(--md-primary-hue-mid)'
    if (procs.some(p => p.isSystemProcess)) return 'var(--md-body-lighter)'
    return 'var(--md-accent-text)'
  }

  // 根据进程类型返回背景色
  const getProcessBgColor = (procs: ProcessAffinityInfo[]): string => {
    if (procs.some(p => p.isMinecraftServer)) return 'rgba(239, 68, 68, 0.08)'
    if (procs.some(p => p.isJavaProcess)) return 'rgba(59, 130, 246, 0.06)'
    if (procs.some(p => p.isSystemProcess)) return 'rgba(120, 120, 120, 0.04)'
    return 'rgba(251, 113, 133, 0.04)'
  }

  const handleKill = async (pid: number) => {
    setKilling(pid)
    try {
      await onKillProcess(pid)
      setExpandedProcess(null)
    } finally {
      setKilling(null)
    }
  }

  // 打开亲和性编辑面板：用当前进程已绑定的核心初始化勾选
  const openAffinityEditor = (proc: ProcessAffinityInfo) => {
    setEditingPid(proc.processId)
    setSelectedCores(new Set(proc.allowedCoreIndices))
    setAffinityError(null)
  }

  // 切换某个逻辑核的勾选状态
  const toggleCore = (coreIdx: number) => {
    setSelectedCores(prev => {
      const next = new Set(prev)
      if (next.has(coreIdx)) next.delete(coreIdx)
      else next.add(coreIdx)
      return next
    })
  }

  // 提交亲和性修改：核心索引集合 → 位掩码 → 调用后端
  const applyAffinity = async (pid: number) => {
    if (selectedCores.size === 0) {
      setAffinityError('至少需要保留一个核心')
      return
    }
    setAffinitySaving(true)
    setAffinityError(null)
    try {
      // 核心索引 → 位掩码（核心 N 对应 bit N）
      let mask = 0
      for (const idx of selectedCores) mask |= 1 << idx
      const res = await setProcessAffinity(pid, mask)
      if (!res.success) {
        setAffinityError(res.error ?? '设置失败')
      } else {
        setEditingPid(null)
      }
    } catch (e) {
      setAffinityError(e instanceof Error ? e.message : String(e))
    } finally {
      setAffinitySaving(false)
    }
  }

  if (logicalCores === 0) {
    return (
      <div className="md-card" style={{ padding: 16 }}>
        <div className="flex items-center" style={{ gap: 8, marginBottom: 12 }}>
          <IconByName name="folderTree" size={18} />
          <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>
            CPU 核心进程亲和性树
          </span>
        </div>
        <div className="md-empty-state" style={{ height: 80 }}>
          <div className="md-empty-state-text">正在获取 CPU 信息...</div>
        </div>
      </div>
    )
  }

  return (
    <div className="md-card" style={{ padding: 16, overflow: 'hidden' }}>
      {/* 标题栏 */}
      <div
        className="flex items-center justify-between"
        style={{ marginBottom: collapsed ? 0 : 12, cursor: 'pointer', userSelect: 'none' }}
        onClick={() => setCollapsed(c => !c)}
      >
        <div className="flex items-center" style={{ gap: 8 }}>
          <IconByName
            name="play"
            size={10}
            style={{
              color: 'var(--md-body-light)',
              transition: 'transform 0.25s ease',
              transform: collapsed ? 'rotate(-90deg)' : 'rotate(90deg)',
              display: 'inline-block',
              width: 12,
            }}
          />
          <IconByName name="folderTree" size={18} />
          <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-body)' }}>
            CPU 核心进程亲和性树
          </span>
          {/* 统计徽章 */}
          <div className="flex items-center" style={{ gap: 4, marginLeft: 8 }}>
            {stats.minecraft > 0 && (
              <span style={{ fontSize: 10, fontWeight: 600, color: '#fff', background: 'var(--md-gauge-red)', padding: '2px 6px', borderRadius: 8 }}>
                MC × {stats.minecraft}
              </span>
            )}
            {stats.java > 0 && (
              <span style={{ fontSize: 10, fontWeight: 600, color: 'var(--md-primary-foreground)', background: 'var(--md-primary-hue-mid)', padding: '2px 6px', borderRadius: 8 }}>
                Java × {stats.java}
              </span>
            )}
            <span style={{ fontSize: 10, fontWeight: 600, color: 'var(--md-body-light)', background: 'var(--md-subtle-border)', padding: '2px 6px', borderRadius: 8 }}>
              总 {stats.total}
            </span>
          </div>
        </div>
        {!collapsed && (
          <div style={{ fontSize: 11, color: 'var(--md-body-light)', opacity: 0.7 }}>
            红色=Minecraft · 蓝色=Java · 灰色=系统 · 粉色=用户
          </div>
        )}
      </div>

      {/* 树形内容 */}
      <div
        style={{
          maxHeight: collapsed ? 0 : 6000,
          overflow: 'hidden',
          transition: 'max-height 0.3s ease, opacity 0.2s ease',
          opacity: collapsed ? 0 : 1,
        }}
      >
        {/* 过滤器 */}
        <div className="flex items-center" style={{ gap: 4, marginBottom: 12, flexWrap: 'wrap' }}>
          {FILTER_OPTIONS.map(opt => {
            const count = opt.key === 'all'
              ? stats.total
              : opt.key === 'minecraft' ? stats.minecraft
              : opt.key === 'java' ? stats.java
              : opt.key === 'system' ? stats.system
              : stats.total - stats.java - stats.system
            return (
              <button
                key={opt.key}
                onClick={(e) => { e.stopPropagation(); setFilter(opt.key) }}
                style={{
                  padding: '3px 10px',
                  fontSize: 11,
                  fontWeight: filter === opt.key ? 700 : 500,
                  color: filter === opt.key ? '#fff' : 'var(--md-body-light)',
                  background: filter === opt.key ? 'var(--md-primary-hue-mid)' : 'var(--md-subtle-border)',
                  border: 'none',
                  borderRadius: 12,
                  cursor: 'pointer',
                  transition: 'all 0.15s ease',
                }}
              >
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                  <IconByName name={opt.iconName} size={12} />
                  {opt.label} ({count})
                </span>
              </button>
            )
          })}
        </div>

        {/* CPU 根节点 */}
        <div style={{ marginBottom: 8 }}>
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
              fontSize: 13,
              fontWeight: 700,
              color: 'var(--md-body)',
              padding: '4px 10px',
              background: 'var(--md-subtle-border)',
              borderRadius: 6,
            }}
          >
            <span>[HOST]</span>
            <span>CPU</span>
            <span style={{ fontSize: 11, opacity: 0.7, fontWeight: 400 }}>
              {physicalCores}P / {logicalCores}L
            </span>
          </div>
        </div>

        {/* 物理核 → 逻辑核 树 */}
        <div style={{ marginLeft: 16 }}>
          {Array.from(physicalGroups.entries()).map(([physicalCore, logicalIndices]) => (
            <div key={physicalCore} style={{ marginBottom: 6 }}>
              {/* 物理核节点 */}
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 4,
                  fontSize: 12,
                  color: 'var(--md-body-light)',
                  padding: '2px 8px',
                  borderLeft: '2px solid var(--md-subtle-border)',
                  marginBottom: 4,
                }}
              >
                <span style={{ fontSize: 10 }}>├─</span>
                <span>物理核 {physicalCore}</span>
              </div>

              {/* 逻辑核节点 */}
              <div style={{ marginLeft: 20, display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                {logicalIndices.map((logicalIdx) => {
                  const usage = perCoreUsages[logicalIdx] ?? 0
                  const procsOnCore = occupiedCores.get(logicalIdx) ?? []
                  const hasProcs = procsOnCore.length > 0
                  const color = getCoreColor(usage)
                  const borderColor = hasProcs ? getProcessBorderColor(procsOnCore) : 'transparent'
                  const borderWidth = hasProcs ? 2 : 0
                  const bgColor = hasProcs ? getProcessBgColor(procsOnCore) : 'var(--md-card-bg)'

                  return (
                    <div
                      key={logicalIdx}
                      title={`逻辑核 ${logicalIdx} · 物理核 ${physicalCore}\n负载: ${usage.toFixed(2)}%${hasProcs ? `\n进程 (${procsOnCore.length}):\n${procsOnCore.map(p => `  ${p.displayName} (PID:${p.processId}) CPU:${p.cpuUsagePercent}%`).join('\n')}` : ''}`}
                      style={{
                        padding: '6px 10px',
                        textAlign: 'center',
                        border: `${borderWidth}px solid ${borderColor}`,
                        borderRadius: 6,
                        background: bgColor,
                        minWidth: 70,
                        transition: 'transform 0.15s ease, box-shadow 0.15s ease',
                        cursor: hasProcs ? 'pointer' : 'default',
                        boxShadow: hasProcs ? `0 0 8px ${borderColor}33` : 'none',
                      }}
                      onMouseEnter={(e) => {
                        if (hasProcs) e.currentTarget.style.transform = 'translateY(-2px)'
                      }}
                      onMouseLeave={(e) => {
                        e.currentTarget.style.transform = 'translateY(0)'
                      }}
                      onClick={() => {
                        if (hasProcs && procsOnCore.length > 0) {
                          setExpandedProcess(
                            expandedProcess === procsOnCore[0].processId
                              ? null
                              : procsOnCore[0].processId
                          )
                        }
                      }}
                    >
                      <div style={{ fontSize: 10, color: 'var(--md-body-light)', opacity: 0.7 }}>
                        L{logicalIdx}
                      </div>
                      <div
                        style={{
                          fontSize: 14,
                          fontWeight: 700,
                          color: hasProcs ? borderColor : color,
                          fontVariantNumeric: 'tabular-nums',
                        }}
                      >
                        {usage.toFixed(0)}%
                      </div>
                      {hasProcs && (
                        <div style={{ fontSize: 9, color: borderColor, fontWeight: 600, marginTop: 2 }}>
                          ×{procsOnCore.length}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            </div>
          ))}
        </div>

        {/* 进程详情列表 */}
        {filteredProcesses.length > 0 && (
          <div style={{ marginTop: 16, paddingTop: 12, borderTop: '1px solid var(--md-subtle-border)' }}>
            <div className="flex items-center justify-between" style={{ marginBottom: 8 }}>
              <div style={{ fontSize: 13, fontWeight: 700, color: 'var(--md-body)' }}>
                [LOG] 进程列表（{filteredProcesses.length}）
              </div>
              <div style={{ fontSize: 10, color: 'var(--md-body-light)', opacity: 0.6 }}>
                按 CPU 占用降序
              </div>
            </div>

            {/* 进程列表（最多显示 50 条，避免过长） */}
            <div style={{ maxHeight: 600, overflowY: 'auto' }}>
              {filteredProcesses.slice(0, 50).map((proc) => {
                const isExpanded = expandedProcess === proc.processId
                const badgeColor = proc.isMinecraftServer
                  ? 'var(--md-gauge-red)'
                  : proc.isJavaProcess
                    ? 'var(--md-primary-hue-mid)'
                    : proc.isSystemProcess
                      ? 'var(--md-body-lighter)'
                      : 'var(--md-accent-text)'
                const badgeText = proc.isMinecraftServer
                  ? 'MC'
                  : proc.isJavaProcess
                    ? 'Java'
                    : proc.isSystemProcess
                      ? '系统'
                      : '用户'
                // 主色背景需用主题感知前景色（浅色主色时自动转黑字），其余饱和/灰底保持白字
                const badgeFgColor = proc.isJavaProcess
                  ? 'var(--md-primary-foreground)'
                  : '#fff'
                const canKill = !proc.isSystemProcess

                return (
                  <div key={proc.processId} style={{ marginBottom: 4 }}>
                    <div
                      onClick={() => setExpandedProcess(isExpanded ? null : proc.processId)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '6px 10px',
                        background: isExpanded ? 'var(--md-card-hover)' : 'var(--md-card-bg)',
                        borderRadius: 6,
                        cursor: 'pointer',
                        border: '1px solid transparent',
                        transition: 'background 0.15s ease, border-color 0.15s ease',
                      }}
                      onMouseEnter={(e) => {
                        if (!isExpanded) e.currentTarget.style.background = 'var(--md-card-hover)'
                      }}
                      onMouseLeave={(e) => {
                        if (!isExpanded) e.currentTarget.style.background = 'var(--md-card-bg)'
                      }}
                    >
                      <div className="flex items-center" style={{ gap: 8, minWidth: 0, flex: 1 }}>
                        <span
                          style={{
                            fontSize: 9,
                            fontWeight: 700,
                            color: badgeFgColor,
                            background: badgeColor,
                            padding: '2px 6px',
                            borderRadius: 4,
                            flexShrink: 0,
                          }}
                        >
                          {badgeText}
                        </span>
                        <span style={{
                          fontSize: 12,
                          fontWeight: 600,
                          color: 'var(--md-body)',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                        }}>
                          {proc.displayName}
                        </span>
                        <span style={{ fontSize: 10, color: 'var(--md-body-lighter)', flexShrink: 0 }}>
                          PID:{proc.processId}
                        </span>
                      </div>
                      <div className="flex items-center" style={{ gap: 10, fontSize: 10, color: 'var(--md-body-light)', flexShrink: 0 }}>
                        <span>CPU: <strong style={{ color: badgeColor }}>{proc.cpuUsagePercent.toFixed(1)}%</strong></span>
                        <span>{bytesToMB(proc.workingSetBytes).toFixed(0)}MB</span>
                        <span>线程:{proc.threadCount}</span>
                        <span>核:{proc.allowedCoreIndices.length}</span>
                        <span style={{ fontSize: 9 }}>{isExpanded ? '▲' : '▼'}</span>
                      </div>
                    </div>

                    {/* 展开的详情 */}
                    {isExpanded && (
                      <div
                        style={{
                          marginLeft: 12,
                          marginTop: 2,
                          padding: '8px 12px',
                          background: 'var(--md-subtle-border)',
                          borderRadius: 6,
                          fontSize: 11,
                        }}
                      >
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px 16px', marginBottom: 6 }}>
                          <div><span style={{ opacity: 0.6 }}>PID:</span> <strong>{proc.processId}</strong></div>
                          <div><span style={{ opacity: 0.6 }}>优先级:</span> <strong>{proc.priorityClass || '未知'}</strong></div>
                          <div><span style={{ opacity: 0.6 }}>进程名:</span> <strong>{proc.processName}</strong></div>
                          <div><span style={{ opacity: 0.6 }}>亲和性掩码:</span> <strong>0x{proc.affinityMask.toString(16).toUpperCase()}</strong></div>
                          <div><span style={{ opacity: 0.6 }}>核心列表:</span> <strong>[{proc.allowedCoreIndices.join(', ')}]</strong></div>
                          <div><span style={{ opacity: 0.6 }}>线程数:</span> <strong>{proc.threadCount}</strong></div>
                        </div>
                        {proc.commandLine && (
                          <div style={{ marginBottom: 6, fontSize: 10, opacity: 0.7, wordBreak: 'break-all' }}>
                            <span style={{ opacity: 0.6 }}>路径:</span> {proc.commandLine}
                          </div>
                        )}
                        <div className="flex items-center" style={{ gap: 8, marginTop: 6 }}>
                          {canKill ? (
                            <>
                              <button
                                onClick={(e) => {
                                  e.stopPropagation()
                                  handleKill(proc.processId)
                                }}
                                disabled={killing === proc.processId}
                                style={{
                                  padding: '5px 14px',
                                  fontSize: 11,
                                  fontWeight: 600,
                                  color: '#fff',
                                  background: killing === proc.processId
                                    ? 'var(--md-subtle-border)'
                                    : 'var(--md-gauge-red)',
                                  border: 'none',
                                  borderRadius: 6,
                                  cursor: killing === proc.processId ? 'not-allowed' : 'pointer',
                                  opacity: killing === proc.processId ? 0.6 : 1,
                                }}
                              >
                                {killing === proc.processId ? '正在终止...' : '终止进程'}
                              </button>
                              <span style={{ fontSize: 10, color: 'var(--md-body-light)', opacity: 0.6 }}>
                                优雅停止 → 3s 超时 → 强杀
                              </span>
                            </>
                          ) : (
                            <span style={{ fontSize: 10, color: 'var(--md-body-light)', opacity: 0.6, fontStyle: 'italic' }}>
                              [WARN] 系统进程，不允许终止
                            </span>
                          )}
                          {/* 设置 CPU 亲和性按钮（接通 processManager:setAffinity 桥接链路） */}
                          <button
                            onClick={(e) => {
                              e.stopPropagation()
                              if (editingPid === proc.processId) setEditingPid(null)
                              else openAffinityEditor(proc)
                            }}
                            style={{
                              marginLeft: 'auto',
                              padding: '5px 14px',
                              fontSize: 11,
                              fontWeight: 600,
                              color: editingPid === proc.processId ? '#fff' : 'var(--md-primary-hue-mid)',
                              background: editingPid === proc.processId ? 'var(--md-primary-hue-mid)' : 'transparent',
                              border: '1px solid var(--md-primary-hue-mid)',
                              borderRadius: 6,
                              cursor: 'pointer',
                            }}
                          >
                            {editingPid === proc.processId ? '收起核心选择' : '设置亲和性'}
                          </button>
                        </div>

                        {/* CPU 亲和性核心选择面板 */}
                        {editingPid === proc.processId && (
                          <div
                            style={{
                              marginTop: 8,
                              padding: 10,
                              background: 'var(--md-card-bg)',
                              borderRadius: 6,
                              border: '1px solid var(--md-subtle-border)',
                            }}
                          >
                            <div style={{ fontSize: 11, fontWeight: 600, marginBottom: 6, color: 'var(--md-body)' }}>
                              选择允许运行的核心（已选 {selectedCores.size}/{logicalCores}）
                            </div>
                            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, marginBottom: 8 }}>
                              {Array.from({ length: logicalCores }, (_, i) => {
                                const checked = selectedCores.has(i)
                                return (
                                  <button
                                    key={i}
                                    onClick={(e) => { e.stopPropagation(); toggleCore(i) }}
                                    style={{
                                      width: 38,
                                      height: 30,
                                      fontSize: 10,
                                      fontWeight: 700,
                                      color: checked ? '#fff' : 'var(--md-body-light)',
                                      background: checked ? 'var(--md-primary-hue-mid)' : 'var(--md-subtle-border)',
                                      border: 'none',
                                      borderRadius: 4,
                                      cursor: 'pointer',
                                      transition: 'all 0.12s ease',
                                    }}
                                  >
                                    L{i}
                                  </button>
                                )
                              })}
                            </div>
                            <div className="flex items-center" style={{ gap: 6, flexWrap: 'wrap' }}>
                              <button
                                onClick={(e) => { e.stopPropagation(); setSelectedCores(new Set(Array.from({ length: logicalCores }, (_, i) => i))) }}
                                style={affinityChipBtnStyle}
                              >
                                全选
                              </button>
                              <button
                                onClick={(e) => { e.stopPropagation(); setSelectedCores(new Set()) }}
                                style={affinityChipBtnStyle}
                              >
                                清空
                              </button>
                              <button
                                onClick={(e) => { e.stopPropagation(); applyAffinity(proc.processId) }}
                                disabled={affinitySaving || selectedCores.size === 0}
                                style={{
                                  padding: '4px 14px',
                                  fontSize: 11,
                                  fontWeight: 600,
                                  color: '#fff',
                                  background: affinitySaving || selectedCores.size === 0
                                    ? 'var(--md-subtle-border)'
                                    : 'var(--md-primary-hue-mid)',
                                  border: 'none',
                                  borderRadius: 6,
                                  cursor: affinitySaving || selectedCores.size === 0 ? 'not-allowed' : 'pointer',
                                }}
                              >
                                {affinitySaving ? '应用中...' : '应用'}
                              </button>
                              <button
                                onClick={(e) => { e.stopPropagation(); setEditingPid(null) }}
                                style={affinityChipBtnStyle}
                              >
                                取消
                              </button>
                              {affinityError && (
                                <span style={{ fontSize: 10, color: 'var(--md-gauge-red)' }}>{affinityError}</span>
                              )}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                )
              })}
              {filteredProcesses.length > 50 && (
                <div style={{ textAlign: 'center', padding: 8, fontSize: 11, color: 'var(--md-body-light)', opacity: 0.6 }}>
                  还有 {filteredProcesses.length - 50} 个进程未显示
                </div>
              )}
            </div>
          </div>
        )}

        {/* 无进程时的提示 */}
        {processAffinities.length === 0 && (
          <div style={{ marginTop: 12, fontSize: 12, color: 'var(--md-body-light)', opacity: 0.6, textAlign: 'center' }}>
            暂无进程信息
          </div>
        )}
      </div>
    </div>
  )
}
