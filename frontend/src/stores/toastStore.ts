import { create } from 'zustand'

export type ToastType = 'success' | 'error' | 'warning' | 'info'

export interface ToastItem {
  id: string
  message: string
  type: ToastType
  duration?: number
}

interface ToastState {
  toasts: ToastItem[]
  showToast: (message: string, type: ToastType, duration?: number) => void
  removeToast: (id: string) => void
}

let toastIdCounter = 0

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],

  showToast: (message, type, duration = 3000) => {
    const id = `toast-${Date.now()}-${toastIdCounter++}`
    set((state) => ({
      toasts: [...state.toasts, { id, message, type, duration }],
    }))
  },

  removeToast: (id) => {
    set((state) => ({
      toasts: state.toasts.filter((t) => t.id !== id),
    }))
  },
}))
