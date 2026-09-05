import { useEffect } from 'react'
import { useAppStore } from '@/stores/appStore'
import { getBridge, onStatusUpdate, getSettings, onThemeChanged } from '@/utils/bridge'
import type { AppReadyEvent } from '@/types/bridge'
import { applySettingsToCss } from '@/utils/theme'

const bridge = getBridge()

// 简单的日志函数，同时输出到 console 和 C# 日志
// 【DIAG 修复】用 bridge.invoke('log:write') 而不是 postMessage，确保日志一定能到 C#
//   之前用 postMessage({type:'log'}) 在 MainWindow 里虽然 OnWebMessageReceived 会处理，
//   但如果 bridge 本身没初始化，postMessage 也发不出去 → 日志全丢
function log(msg: string): void {
  const line = `[useBridgeInit] ${msg}`
  console.log(line)
  // 同时用两种方式发，确保至少一种能到
  try {
    if (typeof window !== 'undefined' && (window as any).chrome?.webview) {
      ;(window as any).chrome.webview.postMessage({
        type: 'log',
        action: 'log',
        payload: `[JS-useBridgeInit] ${msg}`,
        timestamp: Date.now(),
      })
    }
  } catch {
    // ignore
  }
  // 用 bridge.invoke 走正常桥接通路（即使初始化中也能排队）
  try {
    const b = (window as any).__msmc_bridge__
    if (b && typeof b.invoke === 'function') {
      b.invoke('log:write', {
        level: 'Information',
        message: line,
        stack: '',
        url: window.location?.href || '',
        ua: navigator.userAgent,
      }).catch(() => {})
    }
  } catch {
    // ignore
  }
}

export function useBridgeInit(): void {
  const setReady = useAppStore((s) => s.setReady)
  const setVersion = useAppStore((s) => s.setVersion)
  const setAdmin = useAppStore((s) => s.setAdmin)
  const setTheme = useAppStore((s) => s.setTheme)
  const setStatusMessage = useAppStore((s) => s.setStatusMessage)

  useEffect(() => {
    let cancelled = false
    let retryTimer: number | null = null

    log('useEffect 执行，开始初始化桥接')

    async function init(): Promise<void> {
      log('init() 开始')

      try {
        log('调用 bridge.invoke(app:getReadyState)...')
        const data = await bridge.invoke<AppReadyEvent>('app:getReadyState')
        log(`收到响应: version=${data.version}, isAdmin=${data.isAdmin}, theme=${data.theme.mode}`)

        if (cancelled) {
          log('已取消，丢弃响应')
          return
        }

        setVersion(data.version)
        setAdmin(data.isAdmin)
        setTheme(data.theme)
        setStatusMessage(data.statusMessage ?? '')
        setReady(true)

        try {
          const settings = await getSettings()
          applySettingsToCss(settings)
          log('[OK] 设置已应用到 CSS')
        } catch (e) {
          log(`[WARN] 获取设置失败: ${e}`)
        }

        log('[OK] 应用初始化完成，isReady = true')
      } catch (e) {
        log(`[ERR] 获取就绪状态失败: ${e}`)
        // 失败后重试，最多 10 次
        let retries = 0
        const retry = () => {
          retries++
          if (retries > 10 || cancelled) {
            log(`[ERR] 重试 ${retries - 1} 次后放弃，强制 setReady(true) 让用户至少能看到主界面`)
            // 【DIAG 修复】重试 10 次都失败，强制进入主界面，避免永远卡在加载屏
            // 用户看到的"侧边栏蒸发"很可能就是这个：加载屏因为 CSS 变量没加载而白屏，
            // 但 isReady 一直是 false，主界面永远进不去
            setReady(true)
            return
          }
          log(`[RETRY] 第 ${retries} 次重试...`)
          bridge
            .invoke<AppReadyEvent>('app:getReadyState')
            .then(async (data) => {
              if (cancelled) return
              setVersion(data.version)
              setAdmin(data.isAdmin)
              setTheme(data.theme)
              setStatusMessage(data.statusMessage ?? '')
              setReady(true)

              try {
                const settings = await getSettings()
                applySettingsToCss(settings)
              } catch {
                // ignore
              }

              log(`[OK] 第 ${retries} 次重试成功`)
            })
            .catch(() => {
              retryTimer = window.setTimeout(retry, 500)
            })
        }
        retryTimer = window.setTimeout(retry, 500)
      }
    }

    init()

    const offStatus = onStatusUpdate((data) => {
      log(`收到状态更新: ${data.message}`)
      setStatusMessage(data.message)
    })

    const offTheme = onThemeChanged((settings) => {
      log(`收到主题变更`)
      applySettingsToCss(settings)
    })

    return () => {
      log('useEffect 清理')
      cancelled = true
      if (retryTimer) clearTimeout(retryTimer)
      offStatus()
      offTheme()
    }
  }, [setReady, setVersion, setAdmin, setTheme, setStatusMessage])
}
