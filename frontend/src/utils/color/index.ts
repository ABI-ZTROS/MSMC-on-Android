export { rgbToOklch, oklchToRgb, clampToSrgb, generateTints, generate9StepScale } from './oklch'
export type { Oklch, Rgb, TintOptions } from './oklch'

export function hexToRgb(hex: string): { r: number; g: number; b: number } {
  const clean = normalizeHex(hex).replace('#', '')
  const r = parseInt(clean.substring(0, 2), 16)
  const g = parseInt(clean.substring(2, 4), 16)
  const b = parseInt(clean.substring(4, 6), 16)
  return { r, g, b }
}

export function rgbToHex(r: number, g: number, b: number): string {
  const toHex = (v: number) =>
    Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`.toUpperCase()
}

export function hexToHsv(hex: string): { h: number; s: number; v: number } {
  const { r, g, b } = hexToRgb(hex)
  const rn = r / 255
  const gn = g / 255
  const bn = b / 255

  const max = Math.max(rn, gn, bn)
  const min = Math.min(rn, gn, bn)
  const d = max - min

  let h = 0
  const s = max === 0 ? 0 : d / max
  const v = max

  if (d !== 0) {
    switch (max) {
      case rn:
        h = ((gn - bn) / d + (gn < bn ? 6 : 0)) * 60
        break
      case gn:
        h = ((bn - rn) / d + 2) * 60
        break
      case bn:
        h = ((rn - gn) / d + 4) * 60
        break
    }
  }

  return { h, s, v }
}

export function hsvToHex(h: number, s: number, v: number): string {
  const c = v * s
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1))
  const m = v - c

  let r = 0, g = 0, b = 0

  if (h >= 0 && h < 60) { r = c; g = x; b = 0 }
  else if (h >= 60 && h < 120) { r = x; g = c; b = 0 }
  else if (h >= 120 && h < 180) { r = 0; g = c; b = x }
  else if (h >= 180 && h < 240) { r = 0; g = x; b = c }
  else if (h >= 240 && h < 300) { r = x; g = 0; b = c }
  else if (h >= 300 && h < 360) { r = c; g = 0; b = x }

  return rgbToHex((r + m) * 255, (g + m) * 255, (b + m) * 255)
}

export function argbToRgb(hex: string): string {
  if (!hex) return hex
  if (hex.length === 9 && hex.startsWith('#')) {
    return '#' + hex.slice(3)
  }
  return hex
}

export function rgbToArgb(hex: string, alpha = 255): string {
  const clean = normalizeHex(hex).replace('#', '')
  const a = alpha.toString(16).padStart(2, '0')
  return `#${a}${clean}`.toUpperCase()
}

export function normalizeHex(hex: string): string {
  if (!hex) return ''
  let h = hex.trim().toUpperCase()
  if (!h.startsWith('#')) h = '#' + h

  if (/^#[0-9A-F]{8}$/.test(h)) {
    h = '#' + h.slice(3)
  }
  if (/^#[0-9A-F]{3}$/.test(h)) {
    h = '#' + h[1] + h[1] + h[2] + h[2] + h[3] + h[3]
  }
  return h
}

export function isValidHex(hex: string): boolean {
  if (!hex) return false
  const h = hex.trim()
  return /^#?([0-9A-F]{3}|[0-9A-F]{6})$/i.test(h)
}

export function formatHex(hex: string): string {
  return normalizeHex(hex)
}

export function rgba(hex: string, alpha: number): string {
  const { r, g, b } = hexToRgb(hex)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}

export function lighten(hex: string, amount: number): string {
  const { r, g, b } = hexToRgb(hex)
  return rgbToHex(
    r + (255 - r) * amount,
    g + (255 - g) * amount,
    b + (255 - b) * amount,
  )
}

export function darken(hex: string, amount: number): string {
  const { r, g, b } = hexToRgb(hex)
  return rgbToHex(r * (1 - amount), g * (1 - amount), b * (1 - amount))
}

export function getContrastRatio(color1: string, color2: string): number {
  const l1 = getRelativeLuminance(color1)
  const l2 = getRelativeLuminance(color2)
  const lighter = Math.max(l1, l2)
  const darker = Math.min(l1, l2)
  return (lighter + 0.05) / (darker + 0.05)
}

function getRelativeLuminance(hex: string): number {
  const { r, g, b } = hexToRgb(hex)
  const rsrgb = r / 255
  const gsrgb = g / 255
  const bsrgb = b / 255

  const rl = rsrgb <= 0.03928 ? rsrgb / 12.92 : Math.pow((rsrgb + 0.055) / 1.055, 2.4)
  const gl = gsrgb <= 0.03928 ? gsrgb / 12.92 : Math.pow((gsrgb + 0.055) / 1.055, 2.4)
  const bl = bsrgb <= 0.03928 ? bsrgb / 12.92 : Math.pow((bsrgb + 0.055) / 1.055, 2.4)

  return 0.2126 * rl + 0.7152 * gl + 0.0722 * bl
}
