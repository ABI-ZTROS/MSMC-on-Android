export interface Oklch {
  l: number
  c: number
  h: number
}

export interface Rgb {
  r: number
  g: number
  b: number
}

function srgbToLinear(v: number): number {
  v /= 255
  return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4)
}

function linearToSrgb(v: number): number {
  const s = v <= 0.0031308
    ? v * 12.92
    : 1.055 * Math.pow(v, 1 / 2.4) - 0.055
  return Math.round(Math.max(0, Math.min(1, s)) * 255)
}

function linearSrgbToOklab(r: number, g: number, b: number): { l: number; a: number; b: number } {
  const l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b
  const m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b
  const s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b

  const l_ = Math.cbrt(l)
  const m_ = Math.cbrt(m)
  const s_ = Math.cbrt(s)

  return {
    l: 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
    a: 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
    b: 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_,
  }
}

function oklabToLinearSrgb(l: number, a: number, b: number): { r: number; g: number; b: number } {
  const l_ = l + 0.3963377774 * a + 0.2158037573 * b
  const m_ = l - 0.1055613458 * a - 0.0638541728 * b
  const s_ = l - 0.0894841775 * a - 1.2914855480 * b

  const l3 = l_ * l_ * l_
  const m3 = m_ * m_ * m_
  const s3 = s_ * s_ * s_

  return {
    r: 4.0767416621 * l3 - 3.3077115913 * m3 + 0.2309699292 * s3,
    g: -1.2684380046 * l3 + 2.6097574011 * m3 - 0.3413193965 * s3,
    b: -0.0041960863 * l3 - 0.7034186147 * m3 + 1.7076147010 * s3,
  }
}

export function rgbToOklch(r: number, g: number, b: number): Oklch {
  const lr = srgbToLinear(r)
  const lg = srgbToLinear(g)
  const lb = srgbToLinear(b)

  const lab = linearSrgbToOklab(lr, lg, lb)

  let h = Math.atan2(lab.b, lab.a) * 180 / Math.PI
  if (h < 0) h += 360

  const c = Math.sqrt(lab.a * lab.a + lab.b * lab.b)

  return { l: lab.l, c, h }
}

export function oklchToRgb(l: number, c: number, h: number): Rgb {
  const hr = h * Math.PI / 180
  const a = c * Math.cos(hr)
  const b = c * Math.sin(hr)

  const linear = oklabToLinearSrgb(l, a, b)

  return {
    r: linearToSrgb(linear.r),
    g: linearToSrgb(linear.g),
    b: linearToSrgb(linear.b),
  }
}

export function clampToSrgb(l: number, c: number, h: number): Rgb {
  const rgb = oklchToRgb(l, c, h)
  if (rgb.r >= 0 && rgb.r <= 255 && rgb.g >= 0 && rgb.g <= 255 && rgb.b >= 0 && rgb.b <= 255) {
    return rgb
  }

  let lo = 0
  let hi = c
  for (let i = 0; i < 16; i++) {
    const mid = (lo + hi) / 2
    const test = oklchToRgb(l, mid, h)
    if (test.r < 0 || test.r > 255 || test.g < 0 || test.g > 255 || test.b < 0 || test.b > 255) {
      hi = mid
    } else {
      lo = mid
    }
  }
  return oklchToRgb(l, lo, h)
}

export interface TintOptions {
  lightSteps?: number
  darkSteps?: number
  lightLStep?: number
  darkLStep?: number
  lightCAdjust?: number
  darkCAdjust?: number
}

export function generateTints(hex: string, options: TintOptions = {}): string[] {
  const {
    lightSteps = 4,
    darkSteps = 4,
    lightLStep = 0.08,
    darkLStep = 0.07,
    lightCAdjust = -0.03,
    darkCAdjust = 0.02,
  } = options

  const rgb = hexToRgb(hex)
  const base = rgbToOklch(rgb.r, rgb.g, rgb.b)

  const tints: string[] = []

  for (let i = lightSteps; i >= 1; i--) {
    const l = Math.min(0.96, base.l + i * lightLStep)
    const c = Math.max(0, base.c + i * lightCAdjust)
    const result = clampToSrgb(l, c, base.h)
    tints.push(rgbToHex(result.r, result.g, result.b))
  }

  tints.push(hex)

  for (let i = 1; i <= darkSteps; i++) {
    const l = Math.max(0.04, base.l - i * darkLStep)
    const c = Math.max(0, base.c + i * darkCAdjust)
    const result = clampToSrgb(l, c, base.h)
    tints.push(rgbToHex(result.r, result.g, result.b))
  }

  return tints
}

export function generate9StepScale(baseHex: string): string[] {
  return generateTints(baseHex, {
    lightSteps: 5,
    darkSteps: 3,
    lightLStep: 0.07,
    darkLStep: 0.06,
    lightCAdjust: -0.02,
    darkCAdjust: 0.015,
  })
}

function hexToRgb(hex: string): Rgb {
  const clean = hex.replace('#', '')
  const r = parseInt(clean.substring(0, 2), 16)
  const g = parseInt(clean.substring(2, 4), 16)
  const b = parseInt(clean.substring(4, 6), 16)
  return { r, g, b }
}

function rgbToHex(r: number, g: number, b: number): string {
  const toHex = (v: number) =>
    Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, '0')
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`
}

export { hexToRgb, rgbToHex }
