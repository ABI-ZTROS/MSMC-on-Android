import type { SettingsData } from '@/types/bridge'
import {
  generate9StepScale,
  normalizeHex,
  rgba,
  hexToRgb,
  rgbToHex,
  rgbToOklch,
  oklchToRgb,
} from '@/utils/color'

function applyPrimaryScale(baseHex: string, prefix: string, style: CSSStyleDeclaration): void {
  const scale = generate9StepScale(baseHex)
  const names = ['50', '100', '200', '300', '400', '500', '600', '700', '800', '900']
  scale.forEach((color, i) => {
    style.setProperty(`${prefix}-${names[i]}`, color)
  })
  style.setProperty(prefix, scale[5])
  style.setProperty(`${prefix}-foreground`, pickTextColor(scale[5]))
}

function applySurfaceScale(baseHex: string, style: CSSStyleDeclaration): void {
  const scale = generate9StepScale(baseHex)
  style.setProperty('--md-paper', scale[9])
  style.setProperty('--md-deep-background', darkenOklch(scale[9], 0.15))
  style.setProperty('--md-card-background', scale[8])
  style.setProperty('--md-card-hover', lightenOklch(scale[8], 0.06))
  style.setProperty('--md-terminal-background', darkenOklch(scale[8], 0.1))
  style.setProperty('--md-loading-overlay', rgba(scale[8], 0.8))
  style.setProperty('--md-surface-0', scale[9])
  style.setProperty('--md-surface-1', scale[8])
  style.setProperty('--md-surface-2', scale[7])
  style.setProperty('--md-surface-3', scale[6])
}

function applyTextScale(baseHex: string, style: CSSStyleDeclaration): void {
  style.setProperty('--md-body', baseHex)
  style.setProperty('--md-body-light', lightenOklch(baseHex, -0.25))
  style.setProperty('--md-body-lighter', lightenOklch(baseHex, -0.4))
  style.setProperty('--md-info-foreground', lightenOklch(baseHex, -0.25))
  style.setProperty('--md-white', '#FFFFFF')
}

function applyBorderColorScale(baseHex: string, style: CSSStyleDeclaration): void {
  style.setProperty('--md-subtle-border', baseHex)
  style.setProperty('--md-card-subtle-border', rgba(baseHex, 0.2))
  style.setProperty('--md-swatch-hover-border', '#FFFFFF')
}

function applyAccentColorScale(accentHex: string, style: CSSStyleDeclaration): void {
  const scale = generate9StepScale(accentHex)
  style.setProperty('--md-accent-text', scale[4])
  style.setProperty('--md-accent-subtle-border', rgba(scale[4], 0.2))
  style.setProperty('--md-accent-50', scale[0])
  style.setProperty('--md-accent-100', scale[1])
  style.setProperty('--md-accent-200', scale[2])
  style.setProperty('--md-accent-300', scale[3])
  style.setProperty('--md-accent-400', scale[4])
  style.setProperty('--md-accent-500', scale[5])
  style.setProperty('--md-accent-600', scale[6])
  style.setProperty('--md-accent-700', scale[7])
  style.setProperty('--md-accent-800', scale[8])
  style.setProperty('--md-accent-900', scale[9])
}

function applyLegacyAliases(primaryHex: string, style: CSSStyleDeclaration): void {
  const scale = generate9StepScale(primaryHex)

  style.setProperty('--md-primary-hue-lighter', scale[2])
  style.setProperty('--md-primary-hue-light', scale[4])
  style.setProperty('--md-primary-hue-mid', scale[5])
  style.setProperty('--md-primary-hue-dark', scale[6])
  style.setProperty('--md-primary-hue-darker', scale[7])

  style.setProperty('--md-nav-item-selected', scale[5])
  style.setProperty('--md-nav-item-hover', rgba(scale[5], 0.12))
  style.setProperty('--md-nav-item-selected-hover', scale[6])
  style.setProperty('--md-nav-item-selected-indicator', scale[5])

  style.setProperty('--md-primary-subtle-background', rgba(scale[5], 0.1))
  style.setProperty('--md-primary-subtle-border', rgba(scale[5], 0.2))

  style.setProperty('--md-accent-gradient-start', scale[3])
  style.setProperty('--md-accent-gradient-end', scale[5])
  style.setProperty(
    '--md-accent-gradient',
    `linear-gradient(90deg, ${scale[3]} 0%, ${scale[5]} 100%)`,
  )

  style.setProperty('--md-success-foreground', lightenOklch(scale[5], 0.3))
  style.setProperty('--md-primary-bg', rgba(scale[5], 0.1))
  style.setProperty('--md-primary-border', rgba(scale[5], 0.2))
}

function applyStatusColors(
  style: CSSStyleDeclaration,
  success: string,
  warning: string,
  error: string,
  gaugeGreen: string,
  gaugeYellow: string,
  gaugeRed: string,
): void {
  // ✅ 12 色体系：语义色由 settings:get 下发的 #RRGGBB（已大写归一化）驱动
  style.setProperty('--md-gauge-green', gaugeGreen)
  style.setProperty('--md-gauge-yellow', gaugeYellow)
  style.setProperty('--md-gauge-red', gaugeRed)
  style.setProperty('--md-danger', error)
  style.setProperty('--md-error-text', lightenOklch(error, 0.35))

  style.setProperty('--md-success-subtle-background', rgba(success, 0.1))
  style.setProperty('--md-success-subtle-border', rgba(success, 0.3))
  style.setProperty('--md-warning-subtle-background', rgba(warning, 0.1))
  style.setProperty('--md-warning-subtle-border', rgba(warning, 0.3))
  style.setProperty('--md-danger-subtle-background', rgba(error, 0.1))
  style.setProperty('--md-danger-subtle-border', rgba(error, 0.3))
}

function applyMemorialColors(style: CSSStyleDeclaration): void {
  style.setProperty('--md-memorial-gold', '#D4AF37')
  style.setProperty('--md-memorial-gold-soft', '#C9A86C')
  style.setProperty('--md-memorial-gold-muted', '#B8956A')
  style.setProperty('--md-memorial-gold-bg-start', '#2D1F14')
  style.setProperty('--md-memorial-gold-bg-end', '#3D2A1A')
  style.setProperty('--md-memorial-gold-glow', 'rgba(212, 175, 55, 0.15)')
  style.setProperty('--md-memorial-gold-bg-soft', 'rgba(212, 175, 55, 0.2)')
}

function applyRadius(baseRadius: number, style: CSSStyleDeclaration): void {
  style.setProperty('--md-radius', `${baseRadius}px`)
  style.setProperty('--md-radius-small', `${Math.max(4, baseRadius - 4)}px`)
  style.setProperty('--md-radius-large', `${baseRadius + 4}px`)
}

function applyAnimation(baseDuration: number, enableAnimations: boolean, style: CSSStyleDeclaration): void {
  const ratio = baseDuration / 200
  style.setProperty('--md-duration-fast', `${Math.round(150 * ratio)}ms`)
  style.setProperty('--md-duration-normal', `${baseDuration}ms`)
  style.setProperty('--md-duration-medium', `${Math.round(280 * ratio)}ms`)
  style.setProperty('--md-duration-slow', `${Math.round(350 * ratio)}ms`)
  style.setProperty('--md-duration-elastic', `${Math.round(420 * ratio)}ms`)
  style.setProperty('--md-enable-animations', enableAnimations ? '1' : '0')
}

export function applyCornerRadius(radius: number): void {
  applyRadius(radius, document.documentElement.style)
}

export function applyAnimationSettings(duration: number, enabled: boolean): void {
  applyAnimation(duration, enabled, document.documentElement.style)
}

export function applyAccentColor(hex: string): void {
  const style = document.documentElement.style
  const accent = normalizeHex(hex)
  applyAccentColorScale(accent, style)
}

function lightenOklch(hex: string, amount: number): string {
  const { r, g, b } = hexToRgb(hex)
  const oklch = rgbToOklch(r, g, b)
  const newL = Math.max(0.02, Math.min(0.98, oklch.l + amount))
  const result = oklchToRgb(newL, oklch.c, oklch.h)
  return rgbToHex(result.r, result.g, result.b)
}

function darkenOklch(hex: string, amount: number): string {
  return lightenOklch(hex, -amount)
}

function pickTextColor(bgHex: string): string {
  const { r, g, b } = hexToRgb(bgHex)
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255
  return luminance > 0.6 ? '#0F172A' : '#FFFFFF'
}

export function applySettingsToCss(settings: SettingsData): void {
  const style = document.documentElement.style

  const primary = normalizeHex(settings.primaryColorHex)
  const accent = normalizeHex(settings.accentColorHex)
  const bg = normalizeHex(settings.backgroundColorHex)
  const text = normalizeHex(settings.textColorHex)
  const border = normalizeHex(settings.borderColorHex)
  // ✅ 12 色体系语义色
  const success = normalizeHex(settings.successColorHex)
  const warning = normalizeHex(settings.warningColorHex)
  const error = normalizeHex(settings.errorColorHex)
  const gaugeGreen = normalizeHex(settings.gaugeGreenColorHex)
  const gaugeYellow = normalizeHex(settings.gaugeYellowColorHex)
  const gaugeRed = normalizeHex(settings.gaugeRedColorHex)

  applyPrimaryScale(primary, '--md-primary', style)
  applyAccentColorScale(accent, style)
  applySurfaceScale(bg, style)
  applyTextScale(text, style)
  applyBorderColorScale(border, style)
  applyStatusColors(style, success, warning, error, gaugeGreen, gaugeYellow, gaugeRed)
  applyMemorialColors(style)
  applyLegacyAliases(primary, style)
  applyRadius(settings.cornerRadius, style)
  applyAnimation(settings.animationDuration, settings.enableAnimations, style)
}

export { argbToRgb } from '@/utils/color'

export function applyPrimaryColor(hex: string): void {
  const style = document.documentElement.style
  const primary = normalizeHex(hex)
  applyPrimaryScale(primary, '--md-primary', style)
  applyLegacyAliases(primary, style)
}

// ═════════════════════════════════════════════════════════════════════
// ✅ 12 色体系 — 预览函数（实时应用单颜色，不放宽/保存）
// ═════════════════════════════════════════════════════════════════════

export function applyBackgroundColor(hex: string): void {
  const style = document.documentElement.style
  applySurfaceScale(normalizeHex(hex), style)
}

export function applyCardColor(hex: string): void {
  const style = document.documentElement.style
  const scale = generate9StepScale(normalizeHex(hex))
  // ✅ 卡片 -> 终端 -> 表面层级统一由 9 步色阶驱动
  style.setProperty('--md-card-background', scale[8])
  style.setProperty('--md-card-hover', lightenOklch(scale[8], 0.06))
  style.setProperty('--md-terminal-background', darkenOklch(scale[8], 0.1))
  style.setProperty('--md-surface-1', scale[8])
  style.setProperty('--md-surface-2', scale[7])
}

export function applyTextColor(hex: string): void {
  const style = document.documentElement.style
  applyTextScale(normalizeHex(hex), style)
}

export function applyBorderColor(hex: string): void {
  const style = document.documentElement.style
  applyBorderColorScale(normalizeHex(hex), style)
}

export function applySemanticColors(semantic: {
  success: string
  warning: string
  error: string
  gaugeGreen: string
  gaugeYellow: string
  gaugeRed: string
}): void {
  // ✅ 12 色体系语义色统一预览入口
  const style = document.documentElement.style
  const success = normalizeHex(semantic.success)
  const warning = normalizeHex(semantic.warning)
  const error = normalizeHex(semantic.error)
  const gaugeGreen = normalizeHex(semantic.gaugeGreen)
  const gaugeYellow = normalizeHex(semantic.gaugeYellow)
  const gaugeRed = normalizeHex(semantic.gaugeRed)
  applyStatusColors(style, success, warning, error, gaugeGreen, gaugeYellow, gaugeRed)
}
