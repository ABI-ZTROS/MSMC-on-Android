import { useMemo, useState, useRef, useCallback, useEffect } from 'react'
import type { HistoryPoint } from '@/types/bridge'

export interface DualLineChartProps {
  data: HistoryPoint[]
  height?: number
  label?: string
  gapThresholdSec?: number
  cpuColor?: string
  memoryColor?: string
}

interface Point {
  x: number
  y: number
}

function formatTime(unixMs: number, includeDate = false): string {
  try {
    const d = new Date(unixMs)
    const hh = String(d.getHours()).padStart(2, '0')
    const mm = String(d.getMinutes()).padStart(2, '0')
    const ss = String(d.getSeconds()).padStart(2, '0')
    if (includeDate) {
      const mo = String(d.getMonth() + 1).padStart(2, '0')
      const dd = String(d.getDate()).padStart(2, '0')
      return `${mo}-${dd} ${hh}:${mm}`
    }
    return `${hh}:${mm}:${ss}`
  } catch {
    return ''
  }
}

export function DualLineChart({
  data,
  height = 260,
  label = '使用率趋势',
  gapThresholdSec = 30,
  cpuColor = 'var(--md-gauge-green)',
  memoryColor = 'var(--md-primary-500)',
}: DualLineChartProps): JSX.Element {
  const containerRef = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(600)

  useEffect(() => {
    const updateWidth = () => {
      if (containerRef.current) {
        setWidth(containerRef.current.offsetWidth || 600)
      }
    }
    updateWidth()
    window.addEventListener('resize', updateWidth)
    return () => window.removeEventListener('resize', updateWidth)
  }, [])

  const titleHeight = 32
  const padding = { top: 12, right: 16, bottom: 36, left: 40 }
  const chartHeight = height - titleHeight - padding.top - padding.bottom
  const chartWidth = width - padding.left - padding.right
  const yLabels = [0, 25, 50, 75, 100]

  const [hoverIndex, setHoverIndex] = useState<number | null>(null)
  const [tooltipPos, setTooltipPos] = useState({ x: 0, y: 0 })
  const svgRef = useRef<SVGSVGElement>(null)

  const hasData = data.length > 0
  const isCrossDay = useMemo(() => {
    if (data.length < 2) return false
    const first = new Date(data[0].timestamp).getDate()
    const last = new Date(data[data.length - 1].timestamp).getDate()
    return first !== last
  }, [data])

  // 计算时间范围，用于按时间戳映射 x 坐标
  const timeRange = useMemo(() => {
    if (data.length < 2) return { min: 0, max: 1 }
    const timestamps = data.map(d => d.timestamp)
    const min = Math.min(...timestamps)
    const max = Math.max(...timestamps)
    // 避免 max === min 导致除零
    return { min, max: max === min ? min + 1 : max }
  }, [data])

  // 按时间戳计算 x 坐标，无数据时段会自然留出空白
  const timeToX = useCallback((timestamp: number): number => {
    const ratio = (timestamp - timeRange.min) / (timeRange.max - timeRange.min)
    return padding.left + ratio * chartWidth
  }, [timeRange, padding.left, chartWidth])

  const cpuPoints = useMemo((): Point[] => {
    return data.map((item) => {
      const safeVal = Math.max(0, Math.min(100, item.cpuUsagePercent))
      const x = data.length === 1 ? padding.left + chartWidth / 2 : timeToX(item.timestamp)
      const y = padding.top + titleHeight + (1 - safeVal / 100) * chartHeight
      return { x, y }
    })
  }, [data, timeToX, chartHeight, padding.left, padding.top, titleHeight])

  const memPoints = useMemo((): Point[] => {
    return data.map((item) => {
      const safeVal = Math.max(0, Math.min(100, item.memoryUsagePercent))
      const x = data.length === 1 ? padding.left + chartWidth / 2 : timeToX(item.timestamp)
      const y = padding.top + titleHeight + (1 - safeVal / 100) * chartHeight
      return { x, y }
    })
  }, [data, timeToX, chartHeight, padding.left, padding.top, titleHeight])

  const buildLinePath = (points: Point[]): string => {
    if (points.length === 0) return ''
    const parts: string[] = []
    for (let i = 0; i < points.length; i++) {
      const isGap = i > 0 && (() => {
        const prevTs = data[i - 1].timestamp
        const currTs = data[i].timestamp
        return (currTs - prevTs) / 1000 > gapThresholdSec
      })()
      const cmd = i === 0 || isGap ? 'M' : 'L'
      parts.push(`${cmd} ${points[i].x.toFixed(1)} ${points[i].y.toFixed(1)}`)
    }
    return parts.join(' ')
  }

  const buildAreaSegments = (points: Point[]): string[] => {
    if (data.length === 0) return []
    const segments: { startIdx: number; endIdx: number }[] = []
    let segStart = 0
    for (let i = 1; i < data.length; i++) {
      const prevTs = data[i - 1].timestamp
      const currTs = data[i].timestamp
      if ((currTs - prevTs) / 1000 > gapThresholdSec) {
        segments.push({ startIdx: segStart, endIdx: i - 1 })
        segStart = i
      }
    }
    segments.push({ startIdx: segStart, endIdx: data.length - 1 })

    return segments.map(seg => {
      const segPoints = points.slice(seg.startIdx, seg.endIdx + 1)
      if (segPoints.length < 2) return ''
      const linePath = segPoints.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ')
      const bottomY = padding.top + titleHeight + chartHeight
      return `${linePath} L ${segPoints[segPoints.length - 1].x.toFixed(1)} ${bottomY} L ${segPoints[0].x.toFixed(1)} ${bottomY} Z`
    }).filter(Boolean)
  }

  const cpuPath = useMemo(() => buildLinePath(cpuPoints), [cpuPoints, data, gapThresholdSec])
  const memPath = useMemo(() => buildLinePath(memPoints), [memPoints, data, gapThresholdSec])
  const cpuAreaSegments = useMemo(() => buildAreaSegments(cpuPoints), [cpuPoints, data, gapThresholdSec])
  const memAreaSegments = useMemo(() => buildAreaSegments(memPoints), [memPoints, data, gapThresholdSec])

  const xTicks = useMemo(() => {
    if (data.length === 0) return []
    const tickCount = Math.min(6, Math.max(3, Math.floor(chartWidth / 80)))
    const ticks: { x: number; label: string }[] = []
    for (let i = 0; i < tickCount; i++) {
      const ratio = i / (tickCount - 1)
      const ts = timeRange.min + ratio * (timeRange.max - timeRange.min)
      const x = padding.left + ratio * chartWidth
      ticks.push({
        x,
        label: formatTime(ts, isCrossDay),
      })
    }
    return ticks
  }, [data, chartWidth, padding.left, timeRange, isCrossDay])

  const handleMouseMove = useCallback(
    (e: React.MouseEvent<SVGSVGElement>): void => {
      if (!svgRef.current || data.length === 0) return
      const rect = svgRef.current.getBoundingClientRect()
      // 鼠标在 SVG 坐标系中的位置（offsetX/offsetY 已相对于目标元素）
      // 但为了兼容起见，用 getBoundingClientRect 计算
      const mouseX = e.clientX - rect.left
      const mouseY = e.clientY - rect.top

      const relativeX = mouseX - padding.left
      if (relativeX < 0 || relativeX > chartWidth) {
        setHoverIndex(null)
        return
      }

      // 按时间戳反查最近的数据点
      const ratio = relativeX / chartWidth
      const targetTs = timeRange.min + ratio * (timeRange.max - timeRange.min)
      let nearestIdx = 0
      let nearestDiff = Infinity
      for (let i = 0; i < data.length; i++) {
        const ts = data[i].timestamp
        const diff = Math.abs(ts - targetTs)
        if (diff < nearestDiff) {
          nearestDiff = diff
          nearestIdx = i
        }
      }
      setHoverIndex(nearestIdx)
      // tooltip 用相对 SVG 的坐标，定位容器也是相对于外层 div
      setTooltipPos({ x: mouseX, y: mouseY })
    },
    [data, chartWidth, padding.left, timeRange],
  )

  const handleMouseLeave = useCallback((): void => {
    setHoverIndex(null)
  }, [])

  const hoverPointCpu = hoverIndex !== null ? cpuPoints[hoverIndex] : null
  const hoverPointMem = hoverIndex !== null ? memPoints[hoverIndex] : null
  const hoverData = hoverIndex !== null ? data[hoverIndex] : null

  const gridY = useMemo(() =>
    yLabels.map(val => ({
      y: padding.top + titleHeight + (1 - val / 100) * chartHeight,
      label: val,
    })),
    [chartHeight, padding.top, titleHeight],
  )

  return (
    <div ref={containerRef} style={{ width: '100%', position: 'relative' }}>
      <svg
        ref={svgRef}
        width={width}
        height={height}
        style={{ display: 'block', flexShrink: 0 }}
        onMouseMove={handleMouseMove}
        onMouseLeave={handleMouseLeave}
      >
        {/* 标题 */}
        <text
          x={padding.left}
          y={titleHeight - 6}
          fill="var(--md-body)"
          fontSize={13}
          fontWeight={600}
        >
          {label}
        </text>

        {/* 图例 */}
        <g transform={`translate(${width - padding.right - 180}, ${6})`}>
          <circle cx={8} cy={10} r={5} fill={cpuColor} />
          <text x={20} y={14} fill="var(--md-body-light)" fontSize={11}>CPU</text>
          <circle cx={72} cy={10} r={5} fill={memoryColor} />
          <text x={84} y={14} fill="var(--md-body-light)" fontSize={11}>内存</text>
        </g>

        {/* 网格线 */}
        {gridY.map((g, i) => (
          <line
            key={i}
            x1={padding.left}
            y1={g.y}
            x2={width - padding.right}
            y2={g.y}
            stroke="var(--md-subtle-border)"
            strokeWidth={1}
            strokeDasharray="3,3"
            opacity={0.4}
          />
        ))}

        {/* Y 轴标签 */}
        {gridY.map((g, i) => (
          <text
            key={`y-${i}`}
            x={padding.left - 8}
            y={g.y + 4}
            fill="var(--md-body-lighter)"
            fontSize={10}
            textAnchor="end"
          >
            {g.label}%
          </text>
        ))}

        {/* 内存面积（先画，在下层） */}
        {memAreaSegments.map((d, i) => (
          <path
            key={`mem-area-${i}`}
            d={d}
            fill={memoryColor}
            opacity={0.1}
          />
        ))}

        {/* CPU 面积（后画，在上层） */}
        {cpuAreaSegments.map((d, i) => (
          <path
            key={`cpu-area-${i}`}
            d={d}
            fill={cpuColor}
            opacity={0.15}
          />
        ))}

        {/* 内存线 */}
        <path
          d={memPath}
          fill="none"
          stroke={memoryColor}
          strokeWidth={1.8}
          strokeLinejoin="round"
          strokeLinecap="round"
          opacity={0.9}
        />

        {/* CPU 线 */}
        <path
          d={cpuPath}
          fill="none"
          stroke={cpuColor}
          strokeWidth={2}
          strokeLinejoin="round"
          strokeLinecap="round"
        />

        {/* X 轴刻度 */}
        {xTicks.map((tick, i) => (
          <g key={`xt-${i}`}>
            <line
              x1={tick.x}
              y1={padding.top + titleHeight + chartHeight}
              x2={tick.x}
              y2={padding.top + titleHeight + chartHeight + 4}
              stroke="var(--md-subtle-border)"
              strokeWidth={1}
              opacity={0.5}
            />
            <text
              x={tick.x}
              y={padding.top + titleHeight + chartHeight + 18}
              fill="var(--md-body-lighter)"
              fontSize={10}
              textAnchor="middle"
            >
              {tick.label}
            </text>
          </g>
        ))}

        {/* X 轴线 */}
        <line
          x1={padding.left}
          y1={padding.top + titleHeight + chartHeight}
          x2={width - padding.right}
          y2={padding.top + titleHeight + chartHeight}
          stroke="var(--md-subtle-border)"
          strokeWidth={1}
          opacity={0.6}
        />

        {/* 悬停指示线 */}
        {hoverIndex !== null && hoverPointCpu && (
          <line
            x1={hoverPointCpu.x}
            y1={padding.top + titleHeight}
            x2={hoverPointCpu.x}
            y2={padding.top + titleHeight + chartHeight}
            stroke="var(--md-body)"
            strokeWidth={1}
            strokeDasharray="2,2"
            opacity={0.3}
          />
        )}

        {/* 悬停点 - CPU */}
        {hoverPointCpu && (
          <circle
            cx={hoverPointCpu.x}
            cy={hoverPointCpu.y}
            r={5}
            fill={cpuColor}
            stroke="var(--md-paper)"
            strokeWidth={2}
          />
        )}

        {/* 悬停点 - 内存 */}
        {hoverPointMem && (
          <circle
            cx={hoverPointMem.x}
            cy={hoverPointMem.y}
            r={5}
            fill={memoryColor}
            stroke="var(--md-paper)"
            strokeWidth={2}
          />
        )}

        {/* 无数据提示 */}
        {!hasData && (
          <text
            x={width / 2}
            y={height / 2}
            fill="var(--md-body-lighter)"
            fontSize={12}
            textAnchor="middle"
          >
            暂无数据
          </text>
        )}
      </svg>

      {/* Tooltip —— 用屏幕坐标定位，避免 viewBox 缩放导致位置偏移 */}
      {hoverIndex !== null && hoverData && (
        <div
          style={{
            position: 'absolute',
            left: tooltipPos.x,
            top: tooltipPos.y - 10,
            transform: 'translate(-50%, -100%)',
            pointerEvents: 'none',
            zIndex: 10,
          }}
        >
          <div
            style={{
              background: 'var(--md-card-background)',
              border: '1px solid var(--md-subtle-border)',
              borderRadius: 6,
              padding: '8px 12px',
              fontSize: 11,
              whiteSpace: 'nowrap',
              boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
              color: 'var(--md-body)',
            }}
          >
            <div style={{ color: 'var(--md-body-light)', marginBottom: 4 }}>
              {formatTime(hoverData.timestamp, isCrossDay)}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 2 }}>
              <span style={{ width: 8, height: 8, borderRadius: '50%', background: cpuColor, display: 'inline-block' }} />
              <span>CPU: <b>{hoverData.cpuUsagePercent.toFixed(1)}%</b></span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 8, height: 8, borderRadius: '50%', background: memoryColor, display: 'inline-block' }} />
              <span>内存: <b>{hoverData.memoryUsagePercent.toFixed(1)}%</b></span>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
