import { useCallback, useEffect, useRef, useState } from 'react'
import {
  FaArrowsRotate,
  FaPlus,
  FaTrash,
  FaXmark,
} from 'react-icons/fa6'
import { GaugeRing } from '@/components/ui/GaugeRing'
import { Reveal } from '@/components/ui/Reveal'
import { IconByName } from '@/utils/icons'
import { useToastStore } from '@/stores/toastStore'
import {
  getNetworkStatus,
  getPorts,
  getBridgeRules,
  addBridge,
  removeBridge,
  killProcess,
  getCommonPorts,
  refreshNetwork,
  getHourlyHistory,
} from '@/utils/bridge'
import type {
  NetworkStatus,
  PortInfo,
  BridgeRule,
  CommonPortInfo,
  HourlyHistoryResponse,
  AddBridgeRequest,
} from '@/types/bridge'

type TabKey = 'ports' | 'common' | 'bridge'

interface AddBridgeForm {
  listenAddress: string
  listenPort: string
  connectAddress: string
  connectPort: string
  addFirewall: boolean
  protocol: string
}

// ─────────────────────────────────────────────────────────────────────
// 端口分布饼图（SVG 实现）
// ─────────────────────────────────────────────────────────────────────
interface PortDistributionPieProps {
  systemPorts: number
  registeredPorts: number
  dynamicPorts: number
  usedPorts: number
}

function PortDistributionPie({ systemPorts, registeredPorts, dynamicPorts, usedPorts }: PortDistributionPieProps) {
  const [colors, setColors] = useState({
    red: '',
    primary: '',
    green: '',
  })

  useEffect(() => {
    const updateColors = () => {
      const styles = getComputedStyle(document.documentElement)
      setColors({
        red: styles.getPropertyValue('--md-gauge-red').trim(),
        primary: styles.getPropertyValue('--md-primary-hue-mid').trim(),
        green: styles.getPropertyValue('--md-gauge-green').trim(),
      })
    }
    updateColors()
    const observer = new MutationObserver(updateColors)
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['style', 'class'] })
    return () => observer.disconnect()
  }, [])

  const total = systemPorts + registeredPorts + dynamicPorts
  const size = 200
  const cx = size / 2
  const cy = size / 2
  const radius = 70
  const innerRadius = 45

  const segments = [
    { value: systemPorts, color: colors.red, label: '系统' },
    { value: registeredPorts, color: colors.primary, label: '注册' },
    { value: dynamicPorts, color: colors.green, label: '动态' },
  ]

  let currentAngle = -90
  const paths = segments.map((seg) => {
    // 防止 total 为 0 时除零产生 NaN
    const angle = total > 0 ? (seg.value / total) * 360 : 0
    const startAngle = currentAngle
    const endAngle = currentAngle + angle
    currentAngle = endAngle

    const startRad = (startAngle * Math.PI) / 180
    const endRad = (endAngle * Math.PI) / 180

    const x1 = cx + radius * Math.cos(startRad)
    const y1 = cy + radius * Math.sin(startRad)
    const x2 = cx + radius * Math.cos(endRad)
    const y2 = cy + radius * Math.sin(endRad)
    const x3 = cx + innerRadius * Math.cos(endRad)
    const y3 = cy + innerRadius * Math.sin(endRad)
    const x4 = cx + innerRadius * Math.cos(startRad)
    const y4 = cy + innerRadius * Math.sin(startRad)

    const largeArc = angle > 180 ? 1 : 0

    const d = `M ${x1} ${y1} A ${radius} ${radius} 0 ${largeArc} 1 ${x2} ${y2} L ${x3} ${y3} A ${innerRadius} ${innerRadius} 0 ${largeArc} 0 ${x4} ${y4} Z`

    return { d, color: seg.color, label: seg.label, value: seg.value }
  })

  return (
    <div className="flex flex-col items-center justify-center py-2">
      <div className="relative" style={{ width: size, height: size }}>
        <svg width={size} height={size}>
          {paths.map((p, i) => (
            <path key={i} d={p.d} fill={p.color} opacity={0.85} />
          ))}
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
          <div style={{ fontSize: 28, fontWeight: 700, color: 'var(--md-primary-hue-mid)', lineHeight: 1 }}>
            {usedPorts}
          </div>
          <div style={{ fontSize: 11, opacity: 0.6, color: 'var(--md-body-light)', marginTop: 4 }}>
            占用端口
          </div>
        </div>
      </div>
      <div className="flex items-center gap-4 mt-2">
        {segments.map((seg, i) => (
          <div key={i} className="flex items-center gap-1.5">
            <div style={{ width: 12, height: 12, backgroundColor: seg.color, borderRadius: 2 }} />
            <span style={{ fontSize: 11, color: 'var(--md-body)' }}>{seg.label}</span>
            <span style={{ fontSize: 11, color: 'var(--md-body-light)', opacity: 0.6 }}>
              {seg.value}
            </span>
          </div>
        ))}
      </div>
    </div>
  )
}

// 将 MB 数格式化为易读的字符串（MB/GB 自动切换）
function formatMB(mb: number): string {
  if (mb >= 1024) return `${(mb / 1024).toFixed(1)} GB`
  if (mb >= 1) return `${mb.toFixed(1)} MB`
  if (mb >= 0.001) return `${(mb * 1024).toFixed(0)} KB`
  return '0 B'
}

// ─────────────────────────────────────────────────────────────────────
// 每日吞吐量柱状图（SVG 实现）
// ─────────────────────────────────────────────────────────────────────
interface HourlyThroughputChartProps {
  currentHour: number
  downloadData: number[]
  uploadData?: number[]
}

function HourlyThroughputChart({ currentHour, downloadData }: HourlyThroughputChartProps) {
  const values = downloadData

  const width = 500
  const height = 180
  const padding = { top: 20, right: 16, bottom: 24, left: 36 }
  const chartW = width - padding.left - padding.right
  const chartH = height - padding.top - padding.bottom
  const maxVal = Math.max(...values, 0.1)

  const barWidth = chartW / 24 - 4

  return (
    <div className="w-full flex flex-col px-2 py-2">
      <svg width="100%" height={height} viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="xMidYMid meet">
        {[0, 0.25, 0.5, 0.75, 1].map((ratio, i) => {
          const y = padding.top + chartH * (1 - ratio)
          return (
            <g key={i}>
              <line
                x1={padding.left}
                y1={y}
                x2={width - padding.right}
                y2={y}
                stroke="var(--md-card-hover)"
                strokeWidth={1}
              />
              <text
                x={padding.left - 6}
                y={y + 4}
                textAnchor="end"
                fontSize={10}
                fill="var(--md-body-light)"
                opacity={0.5}
              >
                {formatMB(maxVal * ratio)}
              </text>
            </g>
          )
        })}

        {values.map((v, i) => {
          const x = padding.left + i * (chartW / 24) + 2
          const barH = (v / maxVal) * chartH
          const y = padding.top + chartH - barH
          const isCurrentHour = i === currentHour
          return (
            <rect
              key={i}
              x={x}
              y={y}
              width={barWidth}
              height={barH}
              rx={2}
              fill={isCurrentHour ? 'var(--md-primary-hue-mid)' : 'var(--md-primary-hue-dark)'}
              opacity={isCurrentHour ? 1 : 0.5}
            />
          )
        })}

        {[0, 6, 12, 18, 23].map((h) => (
          <text
            key={h}
            x={padding.left + h * (chartW / 24) + barWidth / 2}
            y={height - 8}
            textAnchor="middle"
            fontSize={10}
            fill="var(--md-body-light)"
            opacity={0.5}
          >
            {h}:00
          </text>
        ))}
      </svg>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────
// 主页面组件
// ─────────────────────────────────────────────────────────────────────

export function NetworkMonitorPage(): JSX.Element {
  const showToast = useToastStore((s) => s.showToast)
  const [activeTab, setActiveTab] = useState<TabKey>('ports')
  const [status, setStatus] = useState<NetworkStatus | null>(null)
  const [ports, setPorts] = useState<PortInfo[]>([])
  const [bridgeRules, setBridgeRules] = useState<BridgeRule[]>([])
  const [commonPorts, setCommonPorts] = useState<CommonPortInfo[]>([])
  const [selectedPort, setSelectedPort] = useState<PortInfo | null>(null)
  const [loading, setLoading] = useState(true)
  const [hourlyHistory, setHourlyHistory] = useState<HourlyHistoryResponse | null>(null)

  const [form, setForm] = useState<AddBridgeForm>({
    listenAddress: '0.0.0.0',
    listenPort: '',
    connectAddress: '127.0.0.1',
    connectPort: '',
    addFirewall: true,
    protocol: 'auto',
  })

  // 常见端口搜索
  const [commonPortSearch, setCommonPortSearch] = useState('')

  // loadData 重入保护标志（避免 5 秒轮询在慢响应时堆积请求）
  const loadingRef = useRef(false)

  const loadData = useCallback(async () => {
    // 重入保护：上一个 loadData 未完成时不发起新请求，避免慢响应时请求堆积
    if (loadingRef.current) return
    loadingRef.current = true
    try {
      // 先触发后端刷新（含流量采样 + 端口扫描 + 桥接规则）
      await refreshNetwork()
      // Bug 修复：Promise.all → allSettled，单数据源失败不阻断其他数据更新
      const results = await Promise.allSettled([
        getNetworkStatus(),
        getPorts(),
        getBridgeRules(),
        getCommonPorts(),
        getHourlyHistory(),
      ])
      if (results[0].status === 'fulfilled') setStatus(results[0].value)
      if (results[1].status === 'fulfilled') setPorts(results[1].value.ports)
      if (results[2].status === 'fulfilled') setBridgeRules(results[2].value.rules)
      if (results[3].status === 'fulfilled') setCommonPorts(results[3].value.ports)
      if (results[4].status === 'fulfilled') setHourlyHistory(results[4].value)
      // 至少一个成功就标记加载完成
      if (results.some((r) => r.status === 'fulfilled')) {
        setLoading(false)
      }
    } catch (err) {
      console.error('加载网络数据失败:', err)
      setLoading(false)
    } finally {
      loadingRef.current = false
    }
  }, [])

  useEffect(() => {
    loadData()
    const timer = setInterval(loadData, 5000)
    return () => clearInterval(timer)
  }, [loadData])

  const handleAddBridge = async () => {
    // Bug 修复：端口输入校验，空/非法值不发 0
    const listenPort = parseInt(form.listenPort, 10)
    const connectPort = parseInt(form.connectPort, 10)
    if (!form.listenPort || isNaN(listenPort) || listenPort < 1 || listenPort > 65535) {
      showToast('监听端口无效（需 1-65535）', 'error')
      return
    }
    if (!form.connectPort || isNaN(connectPort) || connectPort < 1 || connectPort > 65535) {
      showToast('目标端口无效（需 1-65535）', 'error')
      return
    }
    try {
      const payload: AddBridgeRequest = {
        listenAddress: form.listenAddress,
        listenPort,
        connectAddress: form.connectAddress,
        connectPort,
        addFirewall: form.addFirewall,
      }
      if (form.protocol !== 'auto') {
        payload.protocol = form.protocol
      }
      const result = await addBridge(payload)
      if (result.success) {
        setForm({ ...form, listenPort: '', connectPort: '', protocol: 'auto' })
        showToast('桥接规则已添加', 'success')
        await loadData()
      } else {
        // Bug 修复：之前 success=false 完全静默
        showToast(`添加失败: ${result.error || '端口可能被占用或权限不足'}`, 'error')
      }
    } catch (err) {
      console.error('添加桥接失败:', err)
      showToast(`添加桥接失败: ${err instanceof Error ? err.message : String(err)}`, 'error')
    }
  }

  const handleRemoveBridge = async (rule: BridgeRule) => {
    try {
      const result = await removeBridge(rule.listenAddress, rule.listenPort, rule.protocol)
      // Bug 修复：之前不检查返回 success 字段
      if (result?.success) {
        showToast('桥接规则已删除', 'success')
        await loadData()
      } else {
        showToast(`删除失败: ${result?.error || '未知错误'}`, 'error')
      }
    } catch (err) {
      console.error('删除桥接失败:', err)
      showToast(`删除桥接失败: ${err instanceof Error ? err.message : String(err)}`, 'error')
    }
  }

  const handleKillProcess = async () => {
    if (!selectedPort) return
    try {
      const result = await killProcess({ port: selectedPort.port, protocol: selectedPort.protocol })
      if (result.success) {
        showToast('进程已结束', 'success')
        setSelectedPort(null)
        await loadData()
      } else {
        // Bug 修复：之前失败完全静默
        showToast(`结束进程失败: ${result.error || '可能权限不足或进程已退出'}`, 'error')
      }
    } catch (err) {
      console.error('结束进程失败:', err)
      showToast(`结束进程失败: ${err instanceof Error ? err.message : String(err)}`, 'error')
    }
  }

  const handleRefresh = async () => {
    // Bug 修复：之前 setLoading(true) 后若 loadData 因重入保护直接 return，
    // loading 永不复位 → 永久转圈。改为：强制重置 loadingRef 后再调用
    loadingRef.current = false
    setLoading(true)
    await loadData()
  }

  const tabs: { key: TabKey; label: string }[] = [
    { key: 'ports', label: '端口占用' },
    { key: 'common', label: '常见端口' },
    { key: 'bridge', label: '端口桥接' },
  ]

  return (
    <div className="h-full flex flex-col p-4 md-page-enter" style={{ gap: 12 }}>
      {/* ═══════════════════════════════════════════════════════ */}
      {/* 顶部仪表盘行 —— 交错揭示 */}
      {/* ═══════════════════════════════════════════════════════ */}
      <div className="flex items-center flex-wrap" style={{ gap: 12 }}>
        {/* 统计卡片 */}
        <Reveal direction="up" delay={0} className="md-stat-card md-card-elevated" style={{ width: 180 }}>
          <div className="md-stat-label">已占用端口</div>
          <div className="md-stat-value md-num-enter" style={{ color: 'var(--md-accent-text)' }}>
            {status?.usedPorts ?? 0}
          </div>
        </Reveal>

        <Reveal direction="up" delay={60} className="md-stat-card md-card-elevated" style={{ width: 200 }}>
          <div className="md-stat-label">端口占用</div>
          <div className="md-stat-value" style={{ color: 'var(--md-primary-hue-mid)' }}>
            {status?.usedPorts ?? 0} / {status?.totalPorts ?? 65536}
          </div>
          <div style={{ fontSize: 11, color: 'var(--md-body-light)', marginTop: 2 }}>
            理论极限 65536
          </div>
        </Reveal>

        {/* 上传速度仪表盘 */}
        <Reveal direction="scale" delay={120} className="md-card md-card-elevated" style={{ padding: 8 }}>
          <div className="flex flex-col items-center">
            <GaugeRing
              value={status?.uploadSpeedMB ?? 0}
              maximum={status?.speedMaximumMB ?? 1.5}
              label="上传"
              unit="MB/s"
              size={120}
              arcThickness={10}
            />
            <div style={{ fontSize: 11, opacity: 0.7, color: 'var(--md-body-light)', marginTop: 4 }}>
              {status?.uploadSpeedText ?? '0 B/s'}
            </div>
          </div>
        </Reveal>

        {/* 下载速度仪表盘 */}
        <Reveal direction="scale" delay={180} className="md-card md-card-elevated" style={{ padding: 8 }}>
          <div className="flex flex-col items-center">
            <GaugeRing
              value={status?.downloadSpeedMB ?? 0}
              maximum={status?.speedMaximumMB ?? 1.5}
              label="下载"
              unit="MB/s"
              size={120}
              arcThickness={10}
            />
            <div style={{ fontSize: 11, opacity: 0.7, color: 'var(--md-body-light)', marginTop: 4 }}>
              {status?.downloadSpeedText ?? '0 B/s'}
            </div>
          </div>
        </Reveal>

        {/* 自动刷新指示器 */}
        <Reveal direction="fade" delay={240} className="md-card md-card-elevated" style={{ padding: '8px 12px' }}>
          <div className="flex items-center" style={{ gap: 8 }}>
            <FaArrowsRotate
              size={16}
              className={loading ? 'md-spin' : ''}
              style={{
                color: 'var(--md-primary-hue-mid)',
              }}
            />
            <span style={{ fontSize: 12, opacity: 0.7, color: 'var(--md-body-light)' }}>
              自动刷新中
            </span>
            <button
              className="md-btn md-btn-flat"
              onClick={handleRefresh}
              style={{ marginLeft: 8 }}
              title="立即刷新"
            >
              刷新
            </button>
          </div>
        </Reveal>

        {/* 今日流量 */}
        <Reveal direction="up" delay={300} className="md-stat-card md-card-elevated" style={{ flex: 1, minWidth: 140 }}>
          <div className="md-stat-label">今日流量</div>
          <div style={{ display: 'flex', gap: 16, marginTop: 4 }}>
            <div>
              <div style={{ fontSize: 11, color: 'var(--md-body-light)', opacity: 0.7 }}>上传</div>
              <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-gauge-green)' }}>
                {status?.todayUploadText ?? '0 B'}
              </div>
            </div>
            <div>
              <div style={{ fontSize: 11, color: 'var(--md-body-light)', opacity: 0.7 }}>下载</div>
              <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--md-primary-hue-mid)' }}>
                {status?.todayDownloadText ?? '0 B'}
              </div>
            </div>
          </div>
        </Reveal>
      </div>

      {/* ═══════════════════════════════════════════════════════ */}
      {/* 中间：左 Tab + 右可视化面板 */}
      {/* ═══════════════════════════════════════════════════════ */}
      <div className="flex-1 flex min-h-0" style={{ gap: 16 }}>
        {/* ── 左侧 Tab 区域 ── */}
        <div className="md-card flex flex-col" style={{ width: 400, flexShrink: 0 }}>
          {/* Tab 头 */}
          <div className="md-tab-bar" style={{ borderBottom: '1px solid var(--md-card-subtle-border)' }}>
            {tabs.map((t) => (
              <button
                key={t.key}
                className={`md-tab ${activeTab === t.key ? 'md-tab-active' : ''}`}
                onClick={() => setActiveTab(t.key)}
              >
                {t.label}
              </button>
            ))}
          </div>

          {/* Tab 内容 */}
          <div className="flex-1 overflow-auto p-2">
            {/* ── 端口占用 Tab ── */}
            {activeTab === 'ports' && (
              <div className="flex flex-col h-full">
                <div className="flex-1 overflow-auto" style={{ minHeight: 0 }}>
                  <table className="md-data-table">
                    <thead>
                      <tr>
                        <th style={{ width: 60 }}>端口</th>
                        <th style={{ width: 50 }}>协议</th>
                        <th>进程名</th>
                        <th style={{ width: 60 }}>PID</th>
                        <th style={{ width: 70 }}>范围</th>
                      </tr>
                    </thead>
                    <tbody>
                      {ports.map((p, i) => (
                        <tr
                          key={i}
                          className={selectedPort?.port === p.port && selectedPort?.protocol === p.protocol ? 'md-row-selected' : ''}
                          onClick={() => setSelectedPort(p)}
                          style={{ cursor: 'pointer' }}
                        >
                          <td style={{ fontWeight: 600 }}>{p.port}</td>
                          <td>{p.protocol}</td>
                          <td>{p.processName || '-'}</td>
                          <td>{p.processId ?? '-'}</td>
                          <td>
                            <span
                              className={`md-chip ${
                                p.portRange === 'System'
                                  ? 'md-chip-danger'
                                  : p.portRange === 'Registered'
                                  ? 'md-chip-primary'
                                  : 'md-chip-success'
                              }`}
                            >
                              {p.portRange}
                            </span>
                          </td>
                        </tr>
                      ))}
                      {ports.length === 0 && !loading && (
                        <tr>
                          <td colSpan={5}>
                            <div className="md-empty-state">
                              <div className="md-empty-state-icon"><IconByName name="net" size={48} /></div>
                              <div className="md-empty-state-text">暂无端口数据</div>
                            </div>
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
                <div className="flex justify-end p-2" style={{ borderTop: '1px solid var(--md-card-subtle-border)' }}>
                  <button
                    className="md-btn md-btn-outlined"
                    onClick={handleKillProcess}
                    disabled={!selectedPort}
                    style={{ color: 'var(--md-gauge-red)', borderColor: 'var(--md-gauge-red)' }}
                  >
                    <FaXmark style={{ marginRight: 6 }} />
                    结束进程
                  </button>
                </div>
              </div>
            )}

            {/* ── 常见端口 Tab ── */}
            {activeTab === 'common' && (
              <div className="flex flex-col h-full">
                {/* 搜索框 */}
                <div className="flex items-center p-2" style={{ gap: 6, borderBottom: '1px solid var(--md-card-subtle-border)' }}>
                  <IconByName name="search" size={12} style={{ opacity: 0.6 }} />
                  <input
                    type="text"
                    value={commonPortSearch}
                    onChange={(e) => setCommonPortSearch(e.target.value)}
                    placeholder="搜索端口、名称或描述..."
                    className="flex-1 bg-transparent outline-none"
                    style={{ fontSize: 12, padding: '4px 0', color: 'var(--md-body)' }}
                  />
                  {commonPortSearch && (
                    <button onClick={() => setCommonPortSearch('')} className="md-btn md-btn-flat md-btn-icon" title="清空搜索">
                      <IconByName name="close" size={14} />
                    </button>
                  )}
                </div>
                <div className="flex-1 overflow-auto">
                  <table className="md-data-table">
                    <thead>
                      <tr>
                        <th style={{ width: 60 }}>端口</th>
                        <th style={{ width: 100 }}>名称</th>
                        <th>描述</th>
                        <th style={{ width: 80 }}>类别</th>
                      </tr>
                    </thead>
                    <tbody>
                      {commonPorts
                        .filter((p) => {
                          const kw = commonPortSearch.toLowerCase()
                          if (!kw) return true
                          return (
                            String(p.port).includes(kw) ||
                            p.name.toLowerCase().includes(kw) ||
                            p.description.toLowerCase().includes(kw) ||
                            p.category.toLowerCase().includes(kw)
                          )
                        })
                        .map((p, i) => (
                          <tr key={i}>
                            <td style={{ fontWeight: 600 }}>{p.port}</td>
                            <td>{p.name}</td>
                            <td style={{ fontSize: 12, color: 'var(--md-body-light)' }}>{p.description}</td>
                            <td>
                              <span className="md-chip md-chip-neutral">{p.category}</span>
                            </td>
                          </tr>
                        ))}
                      {commonPorts.length > 0 &&
                        commonPorts.filter((p) => {
                          const kw = commonPortSearch.toLowerCase()
                          if (!kw) return true
                          return (
                            String(p.port).includes(kw) ||
                            p.name.toLowerCase().includes(kw) ||
                            p.description.toLowerCase().includes(kw) ||
                            p.category.toLowerCase().includes(kw)
                          )
                        }).length === 0 && (
                        <tr>
                          <td colSpan={4}>
                            <div className="md-empty-state">
                              <div className="md-empty-state-text">没有匹配的端口</div>
                            </div>
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* ── 端口桥接 Tab ── */}
            {activeTab === 'bridge' && (
              <div className="flex flex-col gap-4 p-2">
                {/* 添加桥接表单 */}
                <div>
                  <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--md-body)', marginBottom: 12 }}>
                    添加桥接规则
                  </div>

                  <div className="flex flex-col gap-2">
                    <div className="flex items-center gap-2">
                      <label style={{ width: 80, fontSize: 12, color: 'var(--md-body-light)' }}>监听地址</label>
                      <select
                        className="md-select"
                        style={{ flex: 1, height: 32 }}
                        value={form.listenAddress}
                        onChange={(e) => setForm({ ...form, listenAddress: e.target.value })}
                      >
                        <option value="0.0.0.0">0.0.0.0 (全部)</option>
                        <option value="127.0.0.1">127.0.0.1 (本地)</option>
                        <option value="::">:: (IPv6全部)</option>
                      </select>
                    </div>

                    <div className="flex items-center gap-2">
                      <label style={{ width: 80, fontSize: 12, color: 'var(--md-body-light)' }}>协议类型</label>
                      <select
                        className="md-select"
                        style={{ flex: 1, height: 32 }}
                        value={form.protocol}
                        onChange={(e) => setForm({ ...form, protocol: e.target.value })}
                      >
                        <option value="auto">自动识别（推荐）</option>
                        <option value="v4tov4">IPv4 → IPv4</option>
                        <option value="v6tov6">IPv6 → IPv6</option>
                        <option value="v4tov6">IPv4 → IPv6</option>
                        <option value="v6tov4">IPv6 → IPv4</option>
                      </select>
                    </div>

                    <div className="flex items-center gap-2">
                      <label style={{ width: 80, fontSize: 12, color: 'var(--md-body-light)' }}>监听端口</label>
                      <input
                        type="number"
                        className="md-input"
                        style={{ flex: 1, height: 32 }}
                        value={form.listenPort}
                        onChange={(e) => setForm({ ...form, listenPort: e.target.value })}
                        placeholder="例如: 25565"
                      />
                    </div>

                    <div className="flex items-center gap-2">
                      <label style={{ width: 80, fontSize: 12 }}>→</label>
                      <span style={{ fontSize: 12, opacity: 0.6, color: 'var(--md-body-light)' }}>转发到</span>
                    </div>

                    <div className="flex items-center gap-2">
                      <label style={{ width: 80, fontSize: 12, color: 'var(--md-body-light)' }}>目标地址</label>
                      <select
                        className="md-select"
                        style={{ flex: 1, height: 32 }}
                        value={form.connectAddress}
                        onChange={(e) => setForm({ ...form, connectAddress: e.target.value })}
                      >
                        <option value="127.0.0.1">127.0.0.1 (本地)</option>
                        <option value="0.0.0.0">0.0.0.0 (全部)</option>
                        <option value="::1">::1 (IPv6本地)</option>
                      </select>
                    </div>

                    <div className="flex items-center gap-2">
                      <label style={{ width: 80, fontSize: 12, color: 'var(--md-body-light)' }}>目标端口</label>
                      <input
                        type="number"
                        className="md-input"
                        style={{ flex: 1, height: 32 }}
                        value={form.connectPort}
                        onChange={(e) => setForm({ ...form, connectPort: e.target.value })}
                        placeholder="例如: 25565"
                      />
                    </div>

                    <div className="flex items-center gap-2" style={{ marginLeft: 82 }}>
                      <label className="md-toggle">
                        <input
                          type="checkbox"
                          checked={form.addFirewall}
                          onChange={(e) => setForm({ ...form, addFirewall: e.target.checked })}
                        />
                        <span className="md-toggle-slider" />
                      </label>
                      <span style={{ fontSize: 12, color: 'var(--md-body-light)' }}>同时添加防火墙规则</span>
                    </div>
                  </div>

                  <div className="flex justify-end mt-4">
                    <button className="md-btn md-btn-primary" onClick={handleAddBridge}>
                      <FaPlus style={{ marginRight: 6 }} />
                      添加桥接
                    </button>
                  </div>
                </div>

                {/* 分隔线 */}
                <div style={{ borderTop: '1px solid var(--md-card-subtle-border)' }} />

                {/* 现有桥接规则列表 */}
                <div>
                  <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--md-body)', marginBottom: 8 }}>
                    现有桥接规则
                  </div>
                  <div className="overflow-auto" style={{ maxHeight: 280 }}>
                    <table className="md-data-table">
                      <thead>
                        <tr>
                          <th style={{ width: 90 }}>监听</th>
                          <th style={{ width: 50 }}>端口</th>
                          <th style={{ width: 90 }}>目标</th>
                          <th style={{ width: 50 }}>端口</th>
                          <th style={{ width: 60 }}>引擎</th>
                          <th style={{ width: 50 }}>操作</th>
                        </tr>
                      </thead>
                      <tbody>
                        {bridgeRules.map((r, i) => (
                          <tr key={i}>
                            <td style={{ fontSize: 11 }}>{r.listenAddress}</td>
                            <td>{r.listenPort}</td>
                            <td style={{ fontSize: 11 }}>{r.connectAddress}</td>
                            <td>{r.connectPort}</td>
                            <td>
                              <span className="md-chip md-chip-primary" style={{ fontSize: 10 }}>
                                {r.engine}
                              </span>
                            </td>
                            <td>
                              <button
                                className="md-btn md-btn-flat md-btn-icon"
                                onClick={() => handleRemoveBridge(r)}
                                style={{ color: 'var(--md-gauge-red)' }}
                                title="删除"
                              >
                                <FaTrash size={14} />
                              </button>
                            </td>
                          </tr>
                        ))}
                        {bridgeRules.length === 0 && (
                          <tr>
                            <td colSpan={6}>
                              <div className="md-empty-state">
                                <div className="md-empty-state-icon"><IconByName name="link" size={48} /></div>
                                <div className="md-empty-state-text">暂无桥接规则</div>
                              </div>
                            </td>
                          </tr>
                        )}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>

        {/* ── 右侧可视化面板 ── */}
        <div className="flex-1 flex flex-col min-w-0" style={{ gap: 12 }}>
          {/* 端口分布饼图 */}
          <div className="md-card flex-1 flex flex-col" style={{ minHeight: 0 }}>
            <div
              style={{
                padding: '12px 16px 4px 16px',
                fontSize: 14,
                fontWeight: 600,
                color: 'var(--md-body)',
              }}
            >
              端口占用分布
            </div>
            <div className="flex-1 flex items-center justify-center overflow-hidden">
              {status && (
                <PortDistributionPie
                  systemPorts={status.systemPorts}
                  registeredPorts={status.registeredPorts}
                  dynamicPorts={status.dynamicPorts}
                  usedPorts={status.usedPorts}
                />
              )}
            </div>
          </div>

          {/* 每日吞吐量柱状图 */}
          <div className="md-card flex-1 flex flex-col" style={{ minHeight: 0 }}>
            <div
              style={{
                padding: '12px 16px 4px 16px',
                fontSize: 14,
                fontWeight: 600,
                color: 'var(--md-body)',
              }}
            >
              每日网络吞吐量
            </div>
            <div className="flex-1 overflow-hidden">
              {hourlyHistory ? (
                <HourlyThroughputChart
                  currentHour={status?.currentHour ?? new Date().getHours()}
                  downloadData={hourlyHistory.download}
                  uploadData={hourlyHistory.upload}
                />
              ) : (
                <div className="md-empty-state h-full">
                  <div className="md-empty-state-icon"><IconByName name="chart" size={48} /></div>
                  <div className="md-empty-state-text">加载中...</div>
                </div>
              )}
            </div>
            <div
              style={{
                padding: '4px 16px 12px 16px',
                fontSize: 11,
                opacity: 0.7,
                color: 'var(--md-body-light)',
              }}
            >
              {status?.dailyAnalysisText || '正在分析网络流量...'}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
