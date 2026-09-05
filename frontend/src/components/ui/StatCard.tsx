import { useEffect, useState } from 'react'
import { clsx } from 'clsx'

interface StatCardProps {
  label: string
  value: number | string
  unit?: string
  icon?: string
  trend?: number
  trendLabel?: string
  color?: 'primary' | 'success' | 'warning' | 'danger' | 'muted' | 'accent'
  animated?: boolean
  className?: string
}

const colorStyles = {
  primary: {
    bg: 'bg-primary-50 dark:bg-primary-500/10',
    text: 'text-primary-600 dark:text-primary-400',
    iconBg: 'bg-primary-100 dark:bg-primary-500/20',
  },
  success: {
    bg: 'bg-success-50 dark:bg-success-500/10',
    text: 'text-success-600 dark:text-success-400',
    iconBg: 'bg-success-100 dark:bg-success-500/20',
  },
  warning: {
    bg: 'bg-warning-50 dark:bg-warning-500/10',
    text: 'text-warning-600 dark:text-warning-400',
    iconBg: 'bg-warning-100 dark:bg-warning-500/20',
  },
  danger: {
    bg: 'bg-danger-50 dark:bg-danger-500/10',
    text: 'text-danger-600 dark:text-danger-400',
    iconBg: 'bg-danger-100 dark:bg-danger-500/20',
  },
  muted: {
    bg: 'bg-slate-50 dark:bg-slate-700/30',
    text: 'text-slate-600 dark:text-slate-300',
    iconBg: 'bg-slate-100 dark:bg-slate-700',
  },
  accent: {
    bg: 'bg-accent-50 dark:bg-accent-500/10',
    text: 'text-accent-600 dark:text-accent-400',
    iconBg: 'bg-accent-100 dark:bg-accent-500/20',
  },
}

export function StatCard({
  label,
  value,
  unit,
  icon,
  trend,
  trendLabel,
  color = 'muted',
  animated = true,
  className,
}: StatCardProps): JSX.Element {
  const [displayValue, setDisplayValue] = useState(0)
  const numericValue = typeof value === 'number' ? value : parseFloat(value) || 0
  const isNumeric = typeof value === 'number' || !isNaN(parseFloat(value as string))

  useEffect(() => {
    if (!animated || !isNumeric) {
      if (!isNumeric) setDisplayValue(numericValue)
      return
    }

    const duration = 600
    const startTime = performance.now()
    const startValue = displayValue

    const animate = (currentTime: number) => {
      const elapsed = currentTime - startTime
      const progress = Math.min(elapsed / duration, 1)
      const eased = 1 - Math.pow(1 - progress, 3)
      setDisplayValue(startValue + (numericValue - startValue) * eased)

      if (progress < 1) {
        requestAnimationFrame(animate)
      }
    }

    requestAnimationFrame(animate)
  }, [numericValue, animated, isNumeric])

  const styles = colorStyles[color]
  const displayNum = Math.round(displayValue * 10) / 10

  return (
    <div
      className={clsx(
        'card p-5 card-hover group',
        className
      )}
    >
      <div className="flex items-start justify-between mb-3">
        <span className="text-sm font-medium text-slate-500 dark:text-slate-400">
          {label}
        </span>
        {icon && (
          <div
            className={clsx(
              'w-10 h-10 rounded-xl flex items-center justify-center text-xl transition-transform duration-300 group-hover:scale-110',
              styles.iconBg
            )}
          >
            {icon}
          </div>
        )}
      </div>

      <div className="flex items-baseline gap-1 mb-2">
        <span
          className={clsx(
            'text-3xl font-bold number-animate',
            styles.text
          )}
        >
          {isNumeric ? displayNum : value}
        </span>
        {unit && (
          <span className="text-sm font-medium text-slate-400 dark:text-slate-500">
            {unit}
          </span>
        )}
      </div>

      {trend !== undefined && (
        <div className="flex items-center gap-1.5">
          <span
            className={clsx(
              'text-xs font-medium flex items-center gap-0.5',
              trend >= 0
                ? 'text-success-500 dark:text-success-400'
                : 'text-danger-500 dark:text-danger-400'
            )}
          >
            {trend >= 0 ? '↑' : '↓'}
            {Math.abs(trend)}%
          </span>
          {trendLabel && (
            <span className="text-xs text-slate-400 dark:text-slate-500">
              {trendLabel}
            </span>
          )}
        </div>
      )}
    </div>
  )
}
