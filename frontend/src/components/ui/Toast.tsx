import { useEffect, useState } from 'react'
import { FiCheck, FiX, FiAlertTriangle, FiInfo } from 'react-icons/fi'
import { useToastStore, type ToastItem as ToastItemType } from '@/stores/toastStore'

const typeStyles: Record<
  ToastItemType['type'],
  { bg: string; border: string; icon: string; iconColor: string; glow: string }
> = {
  success: {
    bg: 'var(--md-success-subtle-background)',
    border: 'var(--md-success-subtle-border)',
    icon: 'var(--md-gauge-green)',
    iconColor: 'var(--md-gauge-green)',
    // ColorOS 彩色 tinted shadow：与按钮同款
    glow: '0 8px 20px -4px rgba(34, 197, 94, 0.45), 0 4px 10px -2px rgba(34, 197, 94, 0.28)',
  },
  error: {
    bg: 'var(--md-danger-subtle-background)',
    border: 'var(--md-danger-subtle-border)',
    icon: 'var(--md-gauge-red)',
    iconColor: 'var(--md-error-text)',
    glow: '0 8px 20px -4px rgba(244, 54, 76, 0.50), 0 4px 10px -2px rgba(244, 54, 76, 0.30)',
  },
  warning: {
    bg: 'var(--md-warning-subtle-background)',
    border: 'var(--md-warning-subtle-border)',
    icon: 'var(--md-gauge-yellow)',
    iconColor: 'var(--md-gauge-yellow)',
    glow: '0 8px 20px -4px rgba(234, 179, 8, 0.45), 0 4px 10px -2px rgba(234, 179, 8, 0.28)',
  },
  info: {
    bg: 'var(--md-primary-subtle-background)',
    border: 'var(--md-primary-subtle-border)',
    icon: 'var(--md-primary-hue-mid)',
    iconColor: 'var(--md-primary-hue-light)',
    glow: '0 8px 20px -4px rgba(59, 130, 246, 0.50), 0 4px 10px -2px rgba(59, 130, 246, 0.30)',
  },
}

const ToastIcon = ({ type }: { type: ToastItemType['type'] }) => {
  const color = typeStyles[type].iconColor
  const size = 18

  switch (type) {
    case 'success':
      return <FiCheck size={size} color={color} />
    case 'error':
      return <FiX size={size} color={color} />
    case 'warning':
      return <FiAlertTriangle size={size} color={color} />
    case 'info':
      return <FiInfo size={size} color={color} />
  }
}

interface ToastItemProps {
  toast: ToastItemType
  onClose: () => void
}

const ToastItem = ({ toast, onClose }: ToastItemProps) => {
  const [isVisible, setIsVisible] = useState(false)
  const [isLeaving, setIsLeaving] = useState(false)

  useEffect(() => {
    // ColorOS 入场：spring-soft 弹簧曲线，420ms
    const timer = setTimeout(() => setIsVisible(true), 10)
    return () => clearTimeout(timer)
  }, [])

  useEffect(() => {
    if (toast.duration && toast.duration > 0) {
      const timer = setTimeout(() => {
        handleClose()
      }, toast.duration)
      return () => clearTimeout(timer)
    }
  }, [toast.duration])

  const handleClose = () => {
    setIsLeaving(true)
    // 退场动画时长与 transition 一致
    setTimeout(() => {
      onClose()
    }, 320)
  }

  const style = typeStyles[toast.type]

  return (
    <div
      onClick={handleClose}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 'var(--md-spacing-3)',
        padding: 'var(--md-spacing-3) var(--md-spacing-4)',
        backgroundColor: style.bg,
        border: `1px solid ${style.border}`,
        borderRadius: 'var(--md-radius-large)',
        // ColorOS 彩色 tinted shadow（按类型着色）
        boxShadow: style.glow,
        minWidth: '280px',
        maxWidth: '400px',
        cursor: 'pointer',
        // ColorOS 入场：从右侧弹簧滑入 + scale(0.95) 起手
        // 入场走 spring-soft（带过冲），退场走 aquario（无过冲急离）
        opacity: isLeaving ? 0 : isVisible ? 1 : 0,
        transform: isLeaving
          ? 'translateX(120%) scale(0.92)'
          : isVisible
          ? 'translateX(0) scale(1)'
          : 'translateX(120%) scale(0.95)',
        transition: `opacity var(--md-duration-elastic) var(--md-ease-aquario),
          transform var(--md-duration-elastic) ${
            isLeaving ? 'var(--md-ease-aquario)' : 'var(--md-ease-spring-soft)'
          }`,
        // ColorOS 玻璃态：16px backdrop blur
        backdropFilter: 'blur(16px)',
        WebkitBackdropFilter: 'blur(16px)',
        userSelect: 'none',
      }}
    >
      <div style={{ flexShrink: 0, display: 'flex', alignItems: 'center' }}>
        <ToastIcon type={toast.type} />
      </div>
      <span
        style={{
          flex: 1,
          color: 'var(--md-body)',
          fontSize: 'var(--md-font-size-base)',
          lineHeight: 1.4,
          wordBreak: 'break-word',
        }}
      >
        {toast.message}
      </span>
      <button
        onClick={(e) => {
          e.stopPropagation()
          handleClose()
        }}
        style={{
          flexShrink: 0,
          background: 'none',
          border: 'none',
          padding: '4px',
          margin: '-4px',
          cursor: 'pointer',
          color: 'var(--md-body-lighter)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          borderRadius: 'var(--md-radius-small)',
          transition: `background-color var(--md-duration-fast) var(--md-ease-standard),
            color var(--md-duration-fast) var(--md-ease-standard)`,
        }}
        onMouseEnter={(e) => {
          e.currentTarget.style.backgroundColor = 'var(--md-card-hover)'
          e.currentTarget.style.color = 'var(--md-body)'
        }}
        onMouseLeave={(e) => {
          e.currentTarget.style.backgroundColor = 'transparent'
          e.currentTarget.style.color = 'var(--md-body-lighter)'
        }}
      >
        <FiX size={16} />
      </button>
    </div>
  )
}

export const ToastContainer = () => {
  const toasts = useToastStore((s) => s.toasts)
  const removeToast = useToastStore((s) => s.removeToast)

  return (
    <div
      style={{
        position: 'fixed',
        top: 'var(--md-spacing-4)',
        right: 'var(--md-spacing-4)',
        zIndex: 9999,
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--md-spacing-2)',
        pointerEvents: 'none',
      }}
    >
      {toasts.map((toast) => (
        <div key={toast.id} style={{ pointerEvents: 'auto' }}>
          <ToastItem toast={toast} onClose={() => removeToast(toast.id)} />
        </div>
      ))}
    </div>
  )
}
