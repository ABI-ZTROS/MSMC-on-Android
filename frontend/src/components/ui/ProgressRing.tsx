import { clsx } from 'clsx'

interface ProgressRingProps {
  value: number
  max?: number
  size?: number
  strokeWidth?: number
  color?: 'primary' | 'success' | 'warning' | 'danger'
  showLabel?: boolean
  label?: string
  className?: string
}

const colorClasses = {
  primary: 'text-primary-500',
  success: 'text-success-500',
  warning: 'text-warning-500',
  danger: 'text-danger-500',
}

export function ProgressRing({
  value,
  max = 100,
  size = 32,
  strokeWidth = 3,
  color = 'primary',
  showLabel = false,
  label,
  className,
}: ProgressRingProps): JSX.Element {
  const radius = (size - strokeWidth) / 2
  const circumference = radius * 2 * Math.PI
  const progress = Math.min(Math.max(value / max, 0), 1)
  const offset = circumference - progress * circumference

  return (
    <div
      className={clsx('relative inline-flex items-center justify-center', className)}
      style={{ width: size, height: size }}
    >
      <svg
        width={size}
        height={size}
        viewBox={`0 0 ${size} ${size}`}
        className="transform -rotate-90"
      >
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          className="text-slate-100 dark:text-slate-700/50"
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          className={clsx(colorClasses[color], 'transition-all duration-500 ease-smooth')}
        />
      </svg>
      {showLabel && (
        <span className="absolute text-[10px] font-semibold text-slate-600 dark:text-slate-300">
          {label ?? `${Math.round(progress * 100)}%`}
        </span>
      )}
    </div>
  )
}
