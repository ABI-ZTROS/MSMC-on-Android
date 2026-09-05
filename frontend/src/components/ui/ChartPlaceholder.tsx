import { clsx } from 'clsx'
import { useState, useEffect, useRef } from 'react'

interface ChartPlaceholderProps {
  title?: string
  height?: number
  type?: 'line' | 'bar' | 'area'
  className?: string
  startColor?: string
  endColor?: string
}

export function ChartPlaceholder({
  title,
  height = 200,
  type = 'line',
  className,
  startColor,
  endColor,
}: ChartPlaceholderProps): JSX.Element {
  const containerRef = useRef<HTMLDivElement>(null)
  const [resolvedStartColor, setResolvedStartColor] = useState(startColor || '')
  const [resolvedEndColor, setResolvedEndColor] = useState(endColor || '')

  useEffect(() => {
    if (startColor && endColor) {
      setResolvedStartColor(startColor)
      setResolvedEndColor(endColor)
      return
    }

    if (typeof window === 'undefined') return

    const computedStyle = getComputedStyle(containerRef.current || document.documentElement)
    const cssStartColor = computedStyle.getPropertyValue('--md-primary-hue-mid').trim()
    const cssEndColor = computedStyle.getPropertyValue('--md-primary-hue-light').trim()

    if (!startColor && cssStartColor) {
      setResolvedStartColor(cssStartColor)
    } else if (startColor) {
      setResolvedStartColor(startColor)
    }

    if (!endColor && cssEndColor) {
      setResolvedEndColor(cssEndColor)
    } else if (endColor) {
      setResolvedEndColor(endColor)
    }
  }, [startColor, endColor])

  const points = [15, 35, 28, 52, 42, 68, 55, 72, 65, 80, 70, 88]
  const max = Math.max(...points)

  const getPath = () => {
    const width = 100
    const chartHeight = 100
    const stepX = width / (points.length - 1)

    if (type === 'bar') {
      return points.map((p, i) => {
        const x = i * stepX + stepX * 0.2
        const barWidth = stepX * 0.6
        const barHeight = (p / max) * chartHeight
        return `M${x},${chartHeight} L${x},${chartHeight - barHeight} L${x + barWidth},${chartHeight - barHeight} L${x + barWidth},${chartHeight}`
      }).join(' ')
    }

    let path = `M0,${chartHeight - (points[0] / max) * chartHeight}`
    for (let i = 1; i < points.length; i++) {
      const x = i * stepX
      const y = chartHeight - (points[i] / max) * chartHeight
      path += ` L${x},${y}`
    }
    if (type === 'area') {
      path += ` L100,${chartHeight} L0,${chartHeight} Z`
    }
    return path
  }

  return (
    <div ref={containerRef} className={clsx('card p-5', className)}>
      {title && (
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200 mb-4">
          {title}
        </h3>
      )}
      <div style={{ height }} className="relative w-full">
        <svg
          viewBox="0 0 100 100"
          preserveAspectRatio="none"
          className="w-full h-full"
        >
          <defs>
            <linearGradient id="chartArea" x1="0%" y1="0%" x2="0%" y2="100%">
              <stop offset="0%" stopColor={resolvedStartColor} stopOpacity="0.3" />
              <stop offset="100%" stopColor={resolvedStartColor} stopOpacity="0.02" />
            </linearGradient>
            <linearGradient id="chartLine" x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stopColor={resolvedStartColor} />
              <stop offset="100%" stopColor={resolvedEndColor} />
            </linearGradient>
          </defs>

          {[0, 25, 50, 75, 100].map((y) => (
            <line
              key={y}
              x1="0"
              y1={y}
              x2="100"
              y2={y}
              stroke="currentColor"
              strokeOpacity="0.08"
              strokeDasharray="1,1"
              className="text-slate-400"
            />
          ))}

          {type === 'area' && (
            <path
              d={getPath()}
              fill="url(#chartArea)"
              className="animate-fade-in"
            />
          )}

          {(type === 'line' || type === 'area') && (
            <path
              d={getPath()}
              fill="none"
              stroke="url(#chartLine)"
              strokeWidth="1.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="animate-fade-in"
            />
          )}

          {type === 'bar' && (
            <path
              d={getPath()}
              fill="url(#chartLine)"
              className="animate-fade-in"
            />
          )}
        </svg>

        <div className="absolute bottom-2 left-0 right-0 flex justify-between px-1 text-[10px] text-slate-400 dark:text-slate-500">
          <span>00:00</span>
          <span>06:00</span>
          <span>12:00</span>
          <span>18:00</span>
          <span>24:00</span>
        </div>
      </div>
    </div>
  )
}
