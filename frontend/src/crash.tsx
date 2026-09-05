import React from 'react'
import ReactDOM from 'react-dom/client'
import { CrashPage } from './pages/CrashPage'
import './styles/globals.css'
;(window as any).__msmcCrashScriptLoaded = true

function reportToCsharp(level: string, message: string, stack?: string): void {
  try {
    const bridge = (window as any).__msmc_bridge__
    if (bridge && typeof bridge.invoke === 'function') {
      bridge
        .invoke('log:write', {
          level,
          message,
          stack: stack || '',
          url: location.href,
          ua: navigator.userAgent,
        })
        .catch(() => {})
    }
  } catch {}
}

window.addEventListener('error', (e) => {
  const msg =
    (e.message || '未知错误') +
    (e.filename ? ` @ ${e.filename}:${e.lineno || 0}:${e.colno || 0}` : '')
  console.error('[CRASH-ERR]', msg, e.error)
  reportToCsharp('Error', `[CRASH-ERR] ${msg}`, e.error?.stack)
})

window.addEventListener('unhandledrejection', (e) => {
  const reason = e.reason
  const msg = (reason && (reason.message || reason.toString())) || '未处理的 Promise 拒绝'
  console.error('[CRASH-ERR] Unhandled rejection:', reason)
  reportToCsharp('Error', `[CRASH-ERR] 未处理的 Promise 拒绝: ${msg}`, reason?.stack)
})

const rootEl = document.getElementById('root')
if (rootEl) {
  try {
    ReactDOM.createRoot(rootEl).render(
      <React.StrictMode>
        <CrashPage />
      </React.StrictMode>,
    )
    const bootDiag = document.getElementById('boot-diagnostics')
    if (bootDiag && bootDiag.parentNode) {
      bootDiag.parentNode.removeChild(bootDiag)
    }
  } catch (err) {
    const stack = err instanceof Error ? err.stack : String(err)
    reportToCsharp('Error', `[CRASH-ERR] CrashPage 渲染异常: ${String(err)}`, stack)
    const bootLog = document.getElementById('boot-log')
    if (bootLog) {
      bootLog.textContent += `[FATAL] 故障页渲染失败: ${String(err)}\n${stack || ''}\n`
    }
  }
}
