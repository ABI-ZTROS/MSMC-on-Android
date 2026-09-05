import { useEffect, useRef } from 'react'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'

interface ParticleFieldProps {
  /** 粒子密度系数，1 = 标准（约每 25000 像素 1 个粒子） */
  density?: number
  /** 粒子颜色，默认主色 */
  color?: string
  /** 是否绘制粒子间连线，默认 true */
  connect?: boolean
  /** 连线最大距离（像素），默认 120 */
  connectDistance?: number
  /** 粒子最大速度，默认 0.25 */
  speed?: number
  /** 粒子半径范围 [min, max]，默认 [0.6, 1.8] */
  radiusRange?: [number, number]
  /** 透明度上限 0-1，默认 0.5 */
  maxOpacity?: number
  /** 是否在标签页隐藏时暂停渲染，默认 true */
  pauseOnHidden?: boolean
  className?: string
  style?: React.CSSProperties
}

interface Particle {
  x: number
  y: number
  vx: number
  vy: number
  r: number
  baseAlpha: number
}

/**
 * 高性能 Canvas 粒子场。
 *
 * 性能设计：
 * - 单个 rAF 循环，标签页隐藏时自动暂停（visibilitychange）
 * - 粒子数量根据画布面积动态计算，density=1 时约 30-60 个
 * - 连线绘制用空间分桶减少 O(n²) 比较（粒子少时直接遍历）
 * - DPR 限制为 2 上限，避免高分屏过度渲染
 * - 尊重 prefers-reduced-motion：直接不渲染粒子
 */
export function ParticleField({
  density = 1,
  color = 'var(--md-primary-hue-mid)',
  connect = true,
  connectDistance = 120,
  speed = 0.25,
  radiusRange = [0.6, 1.8],
  maxOpacity = 0.5,
  pauseOnHidden = true,
  className,
  style,
}: ParticleFieldProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const reduced = usePrefersReducedMotion()

  useEffect(() => {
    if (reduced) return
    const canvas = canvasRef.current
    if (!canvas) return

    const ctx = canvas.getContext('2d', { alpha: true })
    if (!ctx) return

    let raf = 0
    let particles: Particle[] = []
    let width = 0
    let height = 0
    let dpr = Math.min(window.devicePixelRatio || 1, 2)
    let running = true

    // canvas 不支持 CSS 变量（ctx.fillStyle = 'var(...)' 会被拒绝并保持上一有效色），
    // 这里通过 getComputedStyle 把 var(...) 解析为具体的 rgb() 值；
    // 主题切换时 theme.ts 会改写 documentElement.style，MutationObserver 据此重新解析。
    const resolveColor = (raw: string): string => {
      try {
        canvas.style.color = raw
        const resolved = getComputedStyle(canvas).color
        return resolved || raw
      } catch {
        return raw
      }
    }
    let resolvedColor = resolveColor(color)

    const computeCount = () => {
      const area = width * height
      // 每 25000 像素 1 个粒子，density 缩放
      return Math.min(120, Math.max(12, Math.floor((area / 25000) * density)))
    }

    const initParticles = () => {
      const count = computeCount()
      particles = []
      for (let i = 0; i < count; i++) {
        const [rMin, rMax] = radiusRange
        particles.push({
          x: Math.random() * width,
          y: Math.random() * height,
          vx: (Math.random() - 0.5) * speed * 2,
          vy: (Math.random() - 0.5) * speed * 2,
          r: rMin + Math.random() * (rMax - rMin),
          baseAlpha: 0.15 + Math.random() * (maxOpacity - 0.15),
        })
      }
    }

    const resize = () => {
      const rect = canvas.getBoundingClientRect()
      width = rect.width
      height = rect.height
      dpr = Math.min(window.devicePixelRatio || 1, 2)
      canvas.width = Math.floor(width * dpr)
      canvas.height = Math.floor(height * dpr)
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
      initParticles()
    }

    const draw = () => {
      ctx.clearRect(0, 0, width, height)

      // 更新位置 + 绘制粒子
      for (const p of particles) {
        p.x += p.vx
        p.y += p.vy

        // 边界环绕
        if (p.x < -10) p.x = width + 10
        else if (p.x > width + 10) p.x = -10
        if (p.y < -10) p.y = height + 10
        else if (p.y > height + 10) p.y = -10

        ctx.beginPath()
        ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2)
        ctx.fillStyle = resolvedColor
        ctx.globalAlpha = p.baseAlpha
        ctx.fill()
      }

      // 绘制连线（粒子数少时直接 O(n²)）
      if (connect && particles.length <= 80) {
        ctx.globalAlpha = 1
        ctx.strokeStyle = resolvedColor
        ctx.lineWidth = 0.6
        const distSq = connectDistance * connectDistance
        for (let i = 0; i < particles.length; i++) {
          const a = particles[i]
          for (let j = i + 1; j < particles.length; j++) {
            const b = particles[j]
            const dx = a.x - b.x
            const dy = a.y - b.y
            const d2 = dx * dx + dy * dy
            if (d2 < distSq) {
              const alpha = (1 - Math.sqrt(d2) / connectDistance) * 0.22
              ctx.globalAlpha = alpha
              ctx.beginPath()
              ctx.moveTo(a.x, a.y)
              ctx.lineTo(b.x, b.y)
              ctx.stroke()
            }
          }
        }
      }

      ctx.globalAlpha = 1
      raf = requestAnimationFrame(draw)
    }

    const onVisibility = () => {
      const visible = !document.hidden
      if (visible && !running) {
        running = true
        raf = requestAnimationFrame(draw)
      } else if (!visible && running) {
        running = false
        cancelAnimationFrame(raf)
      }
    }

    resize()
    raf = requestAnimationFrame(draw)

    const ro = new ResizeObserver(() => resize())
    ro.observe(canvas)
    // 主题切换时重新解析 CSS 变量颜色（theme.ts 改写 documentElement.style 触发）
    const mo = new MutationObserver(() => {
      resolvedColor = resolveColor(color)
    })
    mo.observe(document.documentElement, { attributes: true, attributeFilter: ['style'] })
    if (pauseOnHidden) {
      document.addEventListener('visibilitychange', onVisibility)
    }

    return () => {
      cancelAnimationFrame(raf)
      ro.disconnect()
      mo.disconnect()
      if (pauseOnHidden) {
        document.removeEventListener('visibilitychange', onVisibility)
      }
    }
  }, [reduced, density, color, connect, connectDistance, speed, radiusRange, maxOpacity, pauseOnHidden])

  // reduced motion 时不渲染 canvas，但仍保留占位避免布局抖动
  return (
    <canvas
      ref={canvasRef}
      aria-hidden
      className={className}
      style={{
        position: 'absolute',
        inset: 0,
        width: '100%',
        height: '100%',
        pointerEvents: 'none',
        ...style,
      }}
    />
  )
}
