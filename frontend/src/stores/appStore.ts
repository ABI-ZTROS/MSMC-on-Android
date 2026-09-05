import { create } from 'zustand'
import type { ThemeInfo } from '@/types/bridge'
import { applyPrimaryColor } from '@/utils/theme'

interface AppState {
  isReady: boolean
  version: string
  isAdmin: boolean
  theme: ThemeInfo
  statusMessage: string
  sidebarCollapsed: boolean

  setReady: (ready: boolean) => void
  setVersion: (version: string) => void
  setAdmin: (isAdmin: boolean) => void
  setTheme: (theme: ThemeInfo) => void
  setStatusMessage: (message: string) => void
  toggleSidebar: () => void
  setSidebarCollapsed: (collapsed: boolean) => void
}

export const useAppStore = create<AppState>((set) => ({
  isReady: false,
  version: '0.0.0',
  isAdmin: false,
  theme: {
    mode: 'dark',
    primaryColor: '#3b82f6',
  },
  statusMessage: '就绪',
  sidebarCollapsed: false,

  setReady: (ready) => set({ isReady: ready }),
  setVersion: (version) => set({ version }),
  setAdmin: (isAdmin) => set({ isAdmin }),
  setTheme: (theme) => {
    try {
      // 防御性检查：确保在浏览器环境中执行（避免 SSR/Node 环境报错）
      if (typeof document !== 'undefined' && document.documentElement) {
        if (theme.mode === 'dark') {
          document.documentElement.classList.add('dark')
        } else {
          document.documentElement.classList.remove('dark')
        }
      }
      if (theme.primaryColor) {
        applyPrimaryColor(theme.primaryColor)
      }
      set({ theme })
    } catch (error) {
      console.error('应用主题失败:', error)
      set({ theme })
    }
  },
  setStatusMessage: (message) => set({ statusMessage: message }),
  toggleSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
  setSidebarCollapsed: (collapsed) => set({ sidebarCollapsed: collapsed }),
}))
