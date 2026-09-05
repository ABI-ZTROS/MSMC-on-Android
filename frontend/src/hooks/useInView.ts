import { useEffect, useRef, useState } from 'react'

interface UseInViewOptions {
  /** 元素进入视口多少比例时触发（0-1），默认 0.15 */
  threshold?: number
  /** 触发一次后停止观察，默认 true */
  once?: boolean
  /** 根元素，默认视口 */
  rootMargin?: string
}

/**
 * 基于 IntersectionObserver 的视口检测 hook。
 * 用于滚动揭示动画，比滚动监听性能更好。
 */
export function useInView<T extends HTMLElement = HTMLDivElement>(
  options: UseInViewOptions = {}
): { ref: React.RefObject<T>; inView: boolean } {
  const { threshold = 0.15, once = true, rootMargin = '0px 0px -10% 0px' } = options
  const ref = useRef<T>(null)
  const [inView, setInView] = useState(false)

  useEffect(() => {
    const el = ref.current
    if (!el) return

    // 不支持 IO 时直接显示，避免内容被锁死
    if (typeof IntersectionObserver === 'undefined') {
      setInView(true)
      return
    }

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            setInView(true)
            if (once) observer.unobserve(entry.target)
          } else if (!once) {
            setInView(false)
          }
        }
      },
      { threshold, rootMargin }
    )

    observer.observe(el)
    return () => observer.disconnect()
  }, [threshold, once, rootMargin])

  return { ref, inView }
}
