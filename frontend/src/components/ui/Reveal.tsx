import { type ReactNode } from 'react'
import { useInView } from '@/hooks/useInView'
import { usePrefersReducedMotion } from '@/hooks/usePrefersReducedMotion'
import { clsx } from 'clsx'

type RevealDirection = 'up' | 'down' | 'left' | 'right' | 'fade' | 'scale'

interface RevealProps {
  children: ReactNode
  /** 揭示方向，默认 up */
  direction?: RevealDirection
  /** 延迟（ms），用于交错效果 */
  delay?: number
  /** 持续时间（ms），默认走主题变量 */
  duration?: number
  /** 仅在首次进入视口时触发，默认 true */
  once?: boolean
  /** 视口阈值，默认 0.15 */
  threshold?: number
  className?: string
  style?: React.CSSProperties
  as?: keyof JSX.IntrinsicElements
}

const directionTransform: Record<RevealDirection, string> = {
  up: 'translateY(16px)',
  down: 'translateY(-16px)',
  left: 'translateX(20px)',
  right: 'translateX(-20px)',
  fade: 'none',
  scale: 'scale(0.96)',
}

/**
 * 滚动/入场揭示容器。
 * 基于 IntersectionObserver，进入视口时从隐藏态过渡到可见态。
 * 尊重 prefers-reduced-motion：直接显示无过渡。
 */
export function Reveal({
  children,
  direction = 'up',
  delay = 0,
  duration,
  once = true,
  threshold = 0.15,
  className,
  style,
  as = 'div',
}: RevealProps) {
  const reduced = usePrefersReducedMotion()
  const { ref, inView } = useInView<HTMLDivElement>({ once, threshold })

  const Tag = as as 'div'

  // reduced motion 或无动画时直接显示
  if (reduced) {
    return (
      <Tag className={className} style={style}>
        {children}
      </Tag>
    )
  }

  const transitionDuration = duration != null ? `${duration}ms` : 'var(--md-duration-medium)'
  const transitionDelay = delay > 0 ? `${delay}ms` : '0ms'

  // 仅在未入视口时设置隐藏态 transform；入视口后省略 transform，
  // 让 CSS 类（如 md-card-elevated:hover）的 transform 能正常生效。
  const revealStyle: React.CSSProperties = {
    opacity: inView ? 1 : 0,
    transition: `opacity ${transitionDuration} var(--md-ease-emphasized) ${transitionDelay},
      transform ${transitionDuration} var(--md-ease-emphasized) ${transitionDelay},
      filter ${transitionDuration} var(--md-ease-emphasized) ${transitionDelay}`,
    willChange: inView ? 'auto' : 'opacity, transform',
  }
  if (!inView) {
    revealStyle.transform = directionTransform[direction]
  }

  return (
    <Tag
      ref={ref}
      className={clsx(className)}
      style={{
        ...revealStyle,
        ...style,
      }}
    >
      {children}
    </Tag>
  )
}
