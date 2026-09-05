import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import './styles/globals.css'

// 通知诊断脚本：主入口已成功加载
;(window as any).__msmcMainScriptLoaded = true

// 上报错误到 C# 日志（通过桥接 API）
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
        .catch(() => {
          /* 静默失败，避免循环 */
        })
    }
  } catch {
    /* 静默 */
  }
}

// 全局错误捕获：同步运行时错误
window.addEventListener('error', (e) => {
  const msg =
    (e.message || '未知错误') +
    (e.filename ? ` @ ${e.filename}:${e.lineno || 0}:${e.colno || 0}` : '')
  console.error('[FE-ERR]', msg, e.error)
  reportToCsharp('Error', `[FE-ERR] ${msg}`, e.error?.stack)
})

// 全局错误捕获：未处理的 Promise 拒绝
window.addEventListener('unhandledrejection', (e) => {
  const reason = e.reason
  const msg = (reason && (reason.message || reason.toString())) || '未处理的 Promise 拒绝'
  console.error('[FE-ERR] Unhandled rejection:', reason)
  reportToCsharp('Error', `[FE-ERR] 未处理的 Promise 拒绝: ${msg}`, reason?.stack)
})

// ── [FE-DIAG] 在 React 挂载前检测 :root CSS 变量是否存在
//    如果 globals.css 没成功加载（例如 Win11 下 CORS 校验被拒绝），getComputedStyle
//    读出来的 CSS 变量是 ""（空字符串），可以精准定位"样式丢失"问题是 CSS 文件没解析
function diagRootCssVariables(): string {
  try {
    const root = document.documentElement
    const cs = getComputedStyle(root)
    const checkVars = [
      '--md-card-background',
      '--md-body',
      '--md-body-light',
      '--md-primary',
      '--sidebar-width-expanded',
      '--sidebar-width-collapsed',
    ]
    const lines: string[] = []
    for (const v of checkVars) {
      const val = cs.getPropertyValue(v).trim()
      lines.push(`${v}=${val || '(EMPTY - CSS未加载!)'}`)
    }
    // 额外看 body 的实际颜色（最终生效的）
    const bodyCs = getComputedStyle(document.body)
    lines.push(`body.color=${bodyCs.color}`)
    lines.push(`body.background=${bodyCs.backgroundColor}`)
    return lines.join(' | ')
  } catch (e: any) {
    return `(CSS diag failed: ${e?.message || e})`
  }
}
console.log('[FE-DIAG] 挂载前 :root CSS 变量: ' + diagRootCssVariables())
reportToCsharp('Information', '[FE-DIAG] 挂载前 CSS 变量诊断: ' + diagRootCssVariables(), '')

const rootEl = document.getElementById('root')
if (!rootEl) {
  reportToCsharp('Error', '[FE-ERR] #root 元素未找到，无法挂载 React')
} else {
  try {
    console.log('[FE-DIAG] ReactDOM.createRoot → 开始渲染 <App />')
    ReactDOM.createRoot(rootEl).render(<App />)
    // [OK] React 挂载成功
    ;(window as any).__msmcReactMounted = true
    console.log('[FE-DIAG] React 首帧渲染完成 → __msmcReactMounted=true')
    reportToCsharp('Information', '[FE-DIAG] React 已挂载 → 挂载后 CSS 变量: ' + diagRootCssVariables(), '')

    // ── [FE-DIAG] 挂载后再等 3 帧，检查 Sidebar/Dashboard/ServerPage 等元素是否真的在 DOM 中
    //    如果 Sidebar nav-item-mask 或 dashboard-root 不在 DOM → 是 lazy chunk 加载失败
    setTimeout(() => {
      try {
        // 修正：之前查 .md-sidebar / .md-sidebar-item / .md-sidebar-text，
        // 但 Sidebar.tsx 实际渲染的类名是 md-sidebar-transition / md-nav-item /
        // md-sidebar-text-transition，导致诊断一直误报 "Sidebar 不存在"。
        const sidebar = document.querySelector('.md-sidebar-transition')
        const navItems = document.querySelectorAll('.md-nav-item').length
        const navIcons = document.querySelectorAll('.md-nav-item svg').length
        const navTexts = document.querySelectorAll('.md-sidebar-text-transition').length
        // 额外检查 md-stagger-item 的实际计算 opacity —— 若为 0 说明入场动画未执行
        // （侧边栏按钮"蒸发"、页面卡片空白的直接表征）
        const staggerEl = document.querySelector('.md-stagger-item') as HTMLElement | null
        const staggerOpacity = staggerEl ? getComputedStyle(staggerEl).opacity : '(no stagger-item)'
        const msg = `[FE-DIAG] 3帧后 DOM 快照: aside=${sidebar ? 'EXISTS' : 'MISSING'} | navItems=${navItems} | icons=${navIcons} | texts=${navTexts} | first-stagger-opacity=${staggerOpacity}`
        console.log(msg)
        reportToCsharp('Information', msg, '')
        if (!sidebar) {
          reportToCsharp('Warning',
            '[FE-DIAG] aside.md-sidebar-transition 在挂载 3 帧后仍不存在！（lazy chunk 加载失败或 App 渲染到某处炸了）', '')
        } else if (navItems === 0) {
          reportToCsharp('Warning',
            '[FE-DIAG] aside 存在但 .md-nav-item 数量为 0（navItems 空数组或 Sidebar 渲染被 early-return）', '')
        }
        if (staggerEl && staggerOpacity === '0') {
          reportToCsharp('Warning',
            `[FE-DIAG] .md-stagger-item opacity=0 —— 入场动画未执行，元素永久透明（侧边栏蒸发/卡片空白的根因）。navItems=${navItems}`, '')
        }
      } catch (e2: any) {
        reportToCsharp('Error',
          `[FE-DIAG] 3帧后 DOM 快照异常: ${e2?.message || e2}`, '')
      }
    }, 48)

    // 关键：等待 React 渲染完成（双 rAF 确保首帧已进入 DOM），
    // 然后给诊断层加 fade-out（opacity→0 + blur→8px + 轻微上浮），
    // 过渡结束后再 remove DOM，避免「诊断框硬消失 → 黑底 → App 首帧僵硬出现」的视觉断裂。
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        const bootDiag = document.getElementById('boot-diagnostics')
        if (!bootDiag || !bootDiag.parentNode) return
        bootDiag.classList.add('fade-out')
        // transitionend 触发 remove；加 setTimeout 兜底避免某些 WebView2 不触发 transitionend
        let removed = false
        const doRemove = (): void => {
          if (removed) return
          removed = true
          if (bootDiag.parentNode) bootDiag.parentNode.removeChild(bootDiag)
        }
        bootDiag.addEventListener('transitionend', doRemove, { once: true })
        setTimeout(doRemove, 500)
      })
    })
  } catch (err) {
    const stack = err instanceof Error ? err.stack : String(err)
    reportToCsharp('Error', `[FE-ERR] React 渲染异常: ${String(err)}`, stack)
    // 保留诊断层，并在其中显示错误
    const bootLog = document.getElementById('boot-log')
    if (bootLog) {
      bootLog.textContent += `[FATAL] React 渲染失败: ${String(err)}\n${stack || ''}\n`
    }
  }
}
