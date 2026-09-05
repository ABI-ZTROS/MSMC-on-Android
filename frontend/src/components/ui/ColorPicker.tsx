import { useState, useMemo, useCallback, useEffect } from 'react'
import { ColorWheel } from './ColorWheel'
import {
  hexToRgb,
  hexToHsv,
  hsvToHex,
  isValidHex,
  normalizeHex,
  rgbToHex,
} from '@/utils/color'

export interface ColorPickerProps {
  value: string
  onChange?: (hex: string) => void
  onChangeEnd?: (hex: string) => void
  showRgb?: boolean
  showHsv?: boolean
  presets?: string[]
  label?: string
}

export function ColorPicker({
  value,
  onChange,
  onChangeEnd,
  showRgb = true,
  showHsv = true,
  presets,
  label,
}: ColorPickerProps): JSX.Element {
  const [hexInput, setHexInput] = useState(value)

  useEffect(() => {
    setHexInput(normalizeHex(value))
  }, [value])

  const rgb = useMemo(() => {
    if (!isValidHex(value)) return { r: 0, g: 0, b: 0 }
    return hexToRgb(normalizeHex(value))
  }, [value])

  const hsv = useMemo(() => {
    if (!isValidHex(value)) return { h: 0, s: 0, v: 1 }
    return hexToHsv(normalizeHex(value))
  }, [value])

  const wheelColor = useMemo(() => hsvToHex(hsv.h, hsv.s, 1), [hsv.h, hsv.s])

  const handleWheelChange = useCallback(
    (hex: string) => {
      const newHsv = hexToHsv(hex)
      const finalHex = hsvToHex(newHsv.h, newHsv.s, hsv.v)
      setHexInput(finalHex)
      onChange?.(finalHex)
    },
    [hsv.v, onChange],
  )

  const handleWheelChangeEnd = useCallback(
    (hex: string) => {
      const newHsv = hexToHsv(hex)
      const finalHex = hsvToHex(newHsv.h, newHsv.s, hsv.v)
      onChangeEnd?.(finalHex)
    },
    [hsv.v, onChangeEnd],
  )

  const handleValueChange = useCallback(
    (v: number) => {
      const hex = hsvToHex(hsv.h, hsv.s, v)
      setHexInput(hex)
      onChange?.(hex)
    },
    [hsv.h, hsv.s, onChange],
  )

  const handleHexInputChange = (e: React.ChangeEvent<HTMLInputElement>): void => {
    const val = e.target.value
    setHexInput(val)
    if (isValidHex(val)) {
      onChange?.(normalizeHex(val))
    }
  }

  const handleHexBlur = (): void => {
    if (isValidHex(hexInput)) {
      const final = normalizeHex(hexInput)
      setHexInput(final)
      onChangeEnd?.(final)
    } else {
      setHexInput(normalizeHex(value))
    }
  }

  const handleRgbChange = (channel: 'r' | 'g' | 'b', val: number): void => {
    const newRgb = { ...rgb, [channel]: Math.max(0, Math.min(255, val)) }
    const hex = rgbToHex(newRgb.r, newRgb.g, newRgb.b)
    setHexInput(hex)
    onChange?.(hex)
  }

  const handleRgbChangeEnd = (): void => {
    onChangeEnd?.(normalizeHex(hexInput))
  }

  const displayColor = isValidHex(value) ? normalizeHex(value) : '#3B82F6'

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 12,
        padding: 14,
        background: 'var(--md-card-background)',
        borderRadius: 'var(--md-radius)',
        border: '1px solid var(--md-subtle-border)',
      }}
    >
      {label && (
        <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--md-body)' }}>
          {label}
        </div>
      )}

      <div style={{ display: 'flex', justifyContent: 'center' }}>
        <ColorWheel
          color={wheelColor}
          onChange={handleWheelChange}
          onChangeEnd={handleWheelChangeEnd}
          size={200}
        />
      </div>

      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 10,
        }}
      >
        <div
          style={{
            width: 32,
            height: 32,
            borderRadius: 8,
            background: displayColor,
            border: '1px solid var(--md-subtle-border)',
            flexShrink: 0,
            boxShadow: '0 1px 4px rgba(0,0,0,0.2)',
          }}
        />
        <div style={{ flex: 1, display: 'flex', alignItems: 'center', gap: 6 }}>
          <span style={{ fontSize: 12, color: 'var(--md-body-lighter)' }}>#</span>
          <input
            type="text"
            value={hexInput.replace('#', '')}
            onChange={handleHexInputChange}
            onBlur={handleHexBlur}
            maxLength={6}
            spellCheck={false}
            style={{
              flex: 1,
              padding: '6px 10px',
              fontSize: 13,
              fontFamily: 'ui-monospace, monospace',
              background: 'var(--md-surface-1)',
              border: '1px solid var(--md-subtle-border)',
              borderRadius: 6,
              color: 'var(--md-body)',
              outline: 'none',
              textTransform: 'uppercase',
            }}
          />
        </div>
      </div>

      <ValueSlider
        label="V"
        value={hsv.v * 100}
        min={0}
        max={100}
        onChange={(v) => handleValueChange(v / 100)}
        onChangeEnd={handleRgbChangeEnd}
        gradient={`linear-gradient(90deg, #000, ${hsvToHex(hsv.h, hsv.s, 1)})`}
      />

      {showRgb && (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, 1fr)',
            gap: 8,
          }}
        >
          <NumberInput label="R" value={rgb.r} min={0} max={255}
            onChange={(v) => handleRgbChange('r', v)}
            onChangeEnd={handleRgbChangeEnd} />
          <NumberInput label="G" value={rgb.g} min={0} max={255}
            onChange={(v) => handleRgbChange('g', v)}
            onChangeEnd={handleRgbChangeEnd} />
          <NumberInput label="B" value={rgb.b} min={0} max={255}
            onChange={(v) => handleRgbChange('b', v)}
            onChangeEnd={handleRgbChangeEnd} />
        </div>
      )}

      {showHsv && (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, 1fr)',
            gap: 8,
            fontSize: 11,
            color: 'var(--md-body-lighter)',
          }}
        >
          <div style={{ textAlign: 'center' }}>
            H {Math.round(hsv.h)}°
          </div>
          <div style={{ textAlign: 'center' }}>
            S {Math.round(hsv.s * 100)}%
          </div>
          <div style={{ textAlign: 'center' }}>
            V {Math.round(hsv.v * 100)}%
          </div>
        </div>
      )}

      {presets && presets.length > 0 && (
        <div
          style={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: 6,
            paddingTop: 8,
            borderTop: '1px solid var(--md-subtle-border)',
          }}
        >
          {presets.map((preset) => (
            <button
              key={preset}
              onClick={() => {
                setHexInput(preset)
                onChange?.(preset)
                onChangeEnd?.(preset)
              }}
              title={preset}
              style={{
                width: 20,
                height: 20,
                borderRadius: '50%',
                background: preset,
                border: `2px solid ${
                  normalizeHex(preset).toLowerCase() === displayColor.toLowerCase()
                    ? 'var(--md-primary)'
                    : 'var(--md-subtle-border)'
                }`,
                cursor: 'pointer',
                padding: 0,
                transition: 'transform 0.15s',
              }}
              onMouseEnter={(e) => {
                ;(e.currentTarget as HTMLButtonElement).style.transform = 'scale(1.15)'
              }}
              onMouseLeave={(e) => {
                ;(e.currentTarget as HTMLButtonElement).style.transform = 'scale(1)'
              }}
            />
          ))}
        </div>
      )}
    </div>
  )
}

interface ValueSliderProps {
  label: string
  value: number
  min: number
  max: number
  onChange: (v: number) => void
  onChangeEnd?: () => void
  gradient: string
}

function ValueSlider({ label, value, min, max, onChange, onChangeEnd, gradient }: ValueSliderProps): JSX.Element {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      <span style={{ fontSize: 12, color: 'var(--md-body-light)', width: 16, flexShrink: 0 }}>
        {label}
      </span>
      <input
        type="range"
        min={min}
        max={max}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        onMouseUp={onChangeEnd}
        onTouchEnd={onChangeEnd}
        style={{
          flex: 1,
          height: 8,
          borderRadius: 4,
          background: gradient,
          appearance: 'none',
          outline: 'none',
          cursor: 'pointer',
        }}
      />
      <span style={{ fontSize: 11, color: 'var(--md-body-lighter)', width: 32, textAlign: 'right' }}>
        {Math.round(value)}
      </span>
    </div>
  )
}

interface NumberInputProps {
  label: string
  value: number
  min: number
  max: number
  onChange: (v: number) => void
  onChangeEnd?: () => void
}

function NumberInput({ label, value, min, max, onChange, onChangeEnd }: NumberInputProps): JSX.Element {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <span style={{ fontSize: 10, color: 'var(--md-body-lighter)' }}>{label}</span>
      <input
        type="number"
        min={min}
        max={max}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        onBlur={onChangeEnd}
        style={{
          padding: '4px 8px',
          fontSize: 12,
          fontFamily: 'ui-monospace, monospace',
          background: 'var(--md-surface-1)',
          border: '1px solid var(--md-subtle-border)',
          borderRadius: 4,
          color: 'var(--md-body)',
          outline: 'none',
          width: '100%',
          textAlign: 'center',
          MozAppearance: 'textfield',
        }}
      />
    </div>
  )
}
