import { useEffect, useRef, useState } from 'react'

interface GaugeRingProps {
  value: number
  label?: string
  unit?: string
  maximum?: number
  arcThickness?: number
  size?: number
  enableAnimation?: boolean
  animationDuration?: number
}

export function GaugeRing({
  value,
  label = '',
  unit = '%',
  maximum = 100,
  arcThickness = 12,
  size = 160,
  enableAnimation = true,
  animationDuration = 600,
}: GaugeRingProps) {
  const [displayValue, setDisplayValue] = useState(0)
  const animRef = useRef<number | null>(null)
  const startValueRef = useRef(0)
  const startTimeRef = useRef(0)

  useEffect(() => {
    if (!enableAnimation) {
      setDisplayValue(value)
      return
    }

    if (animRef.current) {
      cancelAnimationFrame(animRef.current)
    }

    startValueRef.current = displayValue
    startTimeRef.current = performance.now()

    const animate = (now: number) => {
      const elapsed = now - startTimeRef.current
      const progress = Math.min(elapsed / animationDuration, 1)
      const ease = 1 - Math.pow(1 - progress, 4)
      const current = startValueRef.current + (value - startValueRef.current) * ease
      setDisplayValue(current)

      if (progress < 1) {
        animRef.current = requestAnimationFrame(animate)
      }
    }

    animRef.current = requestAnimationFrame(animate)

    return () => {
      if (animRef.current) {
        cancelAnimationFrame(animRef.current)
      }
    }
  }, [value, enableAnimation, animationDuration])

  const max = maximum > 0 ? maximum : 100
  const clampedValue = Math.max(0, Math.min(max, displayValue))
  const percent = (clampedValue / max) * 100

  let color = 'var(--md-gauge-green)'
  if (percent >= 60 && percent < 85) {
    color = 'var(--md-gauge-yellow)'
  } else if (percent >= 85) {
    color = 'var(--md-gauge-red)'
  }

  const cx = size / 2
  const cy = size / 2
  const radius = Math.min(cx, cy) - arcThickness - 4
  const startAngle = -135
  const endAngle = 135
  const sweepAngle = (clampedValue / max) * 270

  const polarToCartesian = (angle: number) => {
    const rad = (angle * Math.PI) / 180
    return {
      x: cx + radius * Math.cos(rad),
      y: cy + radius * Math.sin(rad),
    }
  }

  const describeArc = (start: number, end: number) => {
    const startPt = polarToCartesian(start)
    const endPt = polarToCartesian(end)
    const largeArc = end - start > 180 ? 1 : 0
    return `M ${startPt.x} ${startPt.y} A ${radius} ${radius} 0 ${largeArc} 1 ${endPt.x} ${endPt.y}`
  }

  const trackPath = describeArc(startAngle, endAngle)
  const progressPath = sweepAngle > 0.1 ? describeArc(startAngle, startAngle + sweepAngle) : ''

  const numFontSize = Math.min(28, radius * 0.5)
  const labelFontSize = Math.min(12, radius * 0.22)
  const valueText = clampedValue.toFixed(1)

  return (
    <div
      className="relative inline-flex items-center justify-center"
      style={{ width: size, height: size }}
    >
      <svg width={size} height={size}>
        <path
          d={trackPath}
          fill="none"
          stroke="var(--md-card-hover)"
          strokeWidth={arcThickness}
          strokeLinecap="round"
        />
        {progressPath && (
          <path
            d={progressPath}
            fill="none"
            stroke={color}
            strokeWidth={arcThickness}
            strokeLinecap="round"
          />
        )}
      </svg>

      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <div
          className="font-bold"
          style={{
            fontSize: numFontSize,
            color: 'var(--md-body)',
            lineHeight: 1,
            marginBottom: 4,
            fontVariantNumeric: 'tabular-nums',
          }}
        >
          {valueText}
          <span style={{ fontSize: numFontSize * 0.5, opacity: 0.7 }}>{unit}</span>
        </div>
        {label && (
          <div
            style={{
              fontSize: labelFontSize,
              color: 'var(--md-body-light)',
              opacity: 0.7,
              marginTop: radius * 0.05,
            }}
          >
            {label}
          </div>
        )}
      </div>
    </div>
  )
}
