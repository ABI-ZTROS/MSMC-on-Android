import { useCallback } from 'react'

/**
 * ColorOS 风格涟漪：从点击点扩散，圆形不溢出元素边界。
 * 同时设置 --ripple-x/y（点击点百分比）和 --ripple-scale（按对角线/8 计算的扩散系数）。
 */
export function useRipple() {
  return useCallback((e: React.MouseEvent<HTMLElement>) => {
    const target = e.currentTarget
    const rect = target.getBoundingClientRect()
    const x = ((e.clientX - rect.left) / rect.width) * 100
    const y = ((e.clientY - rect.top) / rect.height) * 100
    target.style.setProperty('--ripple-x', `${x}%`)
    target.style.setProperty('--ripple-y', `${y}%`)

    // 扩散系数 = 元素对角线长度 / 涟漪初始直径(8px)
    // 保证涟漪一定能覆盖到元素最远的角落
    const diagonal = Math.sqrt(rect.width * rect.width + rect.height * rect.height)
    const scale = Math.max(8, Math.ceil(diagonal / 8))
    target.style.setProperty('--ripple-scale', String(scale))
  }, [])
}
