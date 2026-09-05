import { useEffect, useRef, useCallback, useMemo } from 'react'
import { hexToHsv, hsvToHex, isValidHex, normalizeHex } from '@/utils/color'

export interface ColorWheelProps {
  color: string
  onChange?: (hex: string) => void
  onChangeEnd?: (hex: string) => void
  size?: number
}

export function ColorWheel({
  color,
  onChange,
  onChangeEnd,
  size = 200,
}: ColorWheelProps): JSX.Element {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const indicatorRef = useRef<HTMLDivElement>(null)
  const draggingRef = useRef(false)

  const hsv = useMemo(() => {
    if (!isValidHex(color)) return { h: 0, s: 0, v: 1 }
    return hexToHsv(normalizeHex(color))
  }, [color])

  const radius = size / 2
  const center = { x: radius, y: radius }

  const drawWheel = useCallback(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const ctx = canvas.getContext('2d')
    if (!ctx) return

    const dpr = window.devicePixelRatio || 1
    canvas.width = size * dpr
    canvas.height = size * dpr
    canvas.style.width = `${size}px`
    canvas.style.height = `${size}px`
    ctx.scale(dpr, dpr)

    ctx.clearRect(0, 0, size, size)

    const imageData = ctx.createImageData(size, size)
    const data = imageData.data
    const cx = radius
    const cy = radius
    const r = radius - 1

    for (let y = 0; y < size; y++) {
      for (let x = 0; x < size; x++) {
        const dx = x - cx
        const dy = y - cy
        const dist = Math.sqrt(dx * dx + dy * dy)
        const idx = (y * size + x) * 4

        if (dist > r) {
          data[idx + 3] = 0
          continue
        }

        let angle = Math.atan2(dy, dx) * 180 / Math.PI
        if (angle < 0) angle += 360
        const sat = dist / r

        const hue = angle
        const val = 1

        const c = val * sat
        const x2 = c * (1 - Math.abs(((hue / 60) % 2) - 1))
        const m = val - c

        let rp = 0, gp = 0, bp = 0
        if (hue >= 0 && hue < 60) { rp = c; gp = x2; bp = 0 }
        else if (hue >= 60 && hue < 120) { rp = x2; gp = c; bp = 0 }
        else if (hue >= 120 && hue < 180) { rp = 0; gp = c; bp = x2 }
        else if (hue >= 180 && hue < 240) { rp = 0; gp = x2; bp = c }
        else if (hue >= 240 && hue < 300) { rp = x2; gp = 0; bp = c }
        else if (hue >= 300 && hue < 360) { rp = c; gp = 0; bp = x2 }

        const alpha = dist > r - 1.5 ? (r - dist) / 1.5 : 1

        data[idx] = Math.round((rp + m) * 255)
        data[idx + 1] = Math.round((gp + m) * 255)
        data[idx + 2] = Math.round((bp + m) * 255)
        data[idx + 3] = Math.round(alpha * 255)
      }
    }

    ctx.putImageData(imageData, 0, 0)
  }, [size, radius])

  const updateIndicator = useCallback(() => {
    const indicator = indicatorRef.current
    if (!indicator) return

    const x = center.x + Math.cos(hsv.h * Math.PI / 180) * (radius - 4) * hsv.s
    const y = center.y + Math.sin(hsv.h * Math.PI / 180) * (radius - 4) * hsv.s

    indicator.style.left = `${x}px`
    indicator.style.top = `${y}px`
  }, [hsv.h, hsv.s, center.x, center.y, radius])

  useEffect(() => {
    drawWheel()
  }, [drawWheel])

  useEffect(() => {
    updateIndicator()
  }, [updateIndicator])

  const getColorFromPoint = useCallback(
    (clientX: number, clientY: number): string | null => {
      const canvas = canvasRef.current
      if (!canvas) return null

      const rect = canvas.getBoundingClientRect()
      const x = clientX - rect.left
      const y = clientY - rect.top

      const dx = x - radius
      const dy = y - radius
      const dist = Math.sqrt(dx * dx + dy * dy)
      const r = radius - 2

      if (dist > r) {
        const scale = r / dist
        const nx = dx * scale + radius
        const ny = dy * scale + radius
        return getColorFromCoords(nx, ny)
      }

      return getColorFromCoords(x, y)
    },
    [radius],
  )

  const getColorFromCoords = (x: number, y: number): string => {
    const dx = x - radius
    const dy = y - radius
    const dist = Math.sqrt(dx * dx + dy * dy)
    const r = radius - 2

    let angle = Math.atan2(dy, dx) * 180 / Math.PI
    if (angle < 0) angle += 360

    const sat = Math.min(1, dist / r)
    return hsvToHex(angle, sat, 1)
  }

  const handlePointerDown = (e: React.PointerEvent<HTMLDivElement>): void => {
    e.preventDefault()
    ;(e.target as HTMLElement).setPointerCapture(e.pointerId)
    draggingRef.current = true

    const hex = getColorFromPoint(e.clientX, e.clientY)
    if (hex && onChange) onChange(hex)
  }

  const handlePointerMove = (e: React.PointerEvent<HTMLDivElement>): void => {
    if (!draggingRef.current) return

    const hex = getColorFromPoint(e.clientX, e.clientY)
    if (hex && onChange) onChange(hex)
  }

  const handlePointerUp = (e: React.PointerEvent<HTMLDivElement>): void => {
    if (!draggingRef.current) return
    draggingRef.current = false
    ;(e.target as HTMLElement).releasePointerCapture(e.pointerId)

    const hex = getColorFromPoint(e.clientX, e.clientY)
    if (hex && onChangeEnd) onChangeEnd(hex)
  }

  const indicatorColor = hsv.s < 0.1 ? '#334155' : '#FFFFFF'

  return (
    <div
      style={{
        position: 'relative',
        width: size,
        height: size,
        touchAction: 'none',
        cursor: 'crosshair',
        userSelect: 'none',
      }}
      onPointerDown={handlePointerDown}
      onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp}
      onPointerCancel={handlePointerUp}
    >
      <canvas ref={canvasRef} style={{ display: 'block' }} />
      <div
        ref={indicatorRef}
        style={{
          position: 'absolute',
          width: 14,
          height: 14,
          borderRadius: '50%',
          border: `2px solid ${indicatorColor}`,
          boxShadow: '0 1px 4px rgba(0,0,0,0.4)',
          transform: 'translate(-50%, -50%)',
          pointerEvents: 'none',
          transition: draggingRef.current ? 'none' : 'left 0.08s ease, top 0.08s ease',
        }}
      />
    </div>
  )
}
