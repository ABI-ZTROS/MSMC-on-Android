import { Outlet, useLocation } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { LazyPageErrorBoundary } from './LazyPageErrorBoundary'
import { useAppStore } from '@/stores/appStore'
import { useEffect, useState, useRef, Suspense } from 'react'
import { FaShield } from 'react-icons/fa6'
import { ParticleField } from '@/components/ui/ParticleField'

export function AppLayout(): JSX.Element {
  const statusMessage = useAppStore((s) => s.statusMessage)
  const isReady = useAppStore((s) => s.isReady)
  const [currentTime, setCurrentTime] = useState('')
  const location = useLocation()
  // ColorOS 路由转场：维护"当前页 + 退场页"双缓冲
  // 退场页用 md-page-exit 触发模糊消散动画，动画结束后卸载
  const [pageKey, setPageKey] = useState(location.pathname)
  const [exitingKey, setExitingKey] = useState<string | null>(null)
  const prevPathRef = useRef(location.pathname)
  const exitTimerRef = useRef<number | null>(null)

  useEffect(() => {
    if (location.pathname !== prevPathRef.current) {
      // 旧页进入退场队列，新页立即挂载
      setExitingKey(prevPathRef.current)
      prevPathRef.current = location.pathname
      setPageKey(location.pathname)
      // 旧页动画结束后卸载（与 md-page-exit 时长一致：420ms * 0.8 ≈ 336ms）
      if (exitTimerRef.current) window.clearTimeout(exitTimerRef.current)
      exitTimerRef.current = window.setTimeout(() => setExitingKey(null), 360)
    }
  }, [location.pathname])

  useEffect(() => {
    return () => {
      if (exitTimerRef.current) window.clearTimeout(exitTimerRef.current)
    }
  }, [])

  useEffect(() => {
    const update = () => {
      const now = new Date()
      setCurrentTime(now.toLocaleString('zh-CN', { hour12: false }))
    }
    update()
    const timer = setInterval(update, 1000)
    return () => clearInterval(timer)
  }, [])

  // ── [FE-DIAG] AppLayout 挂载后 1 帧：打印根容器（md-app-root）的
  //    实际最终背景色/前景色。如果 CSS 变量丢了，会变成白底/透明。
  useEffect(() => {
    const t = window.setTimeout(() => {
      try {
        const root = document.querySelector('.md-app-root') as HTMLElement | null
        const paperEl = document.querySelector('.md-app-paper') as HTMLElement | null
        const sb = document.querySelector('.md-sidebar') as HTMLElement | null
        const readStyle = (el: HTMLElement | null, name: string) => {
          if (!el) return '(elem missing)'
          return getComputedStyle(el).getPropertyValue(name).trim() || '(EMPTY - CSS VAR UNDEFINED)'
        }
        const msg =
          `[FE-DIAG] AppLayout 1帧后样式快照 | ` +
          `md-app-root bgcolor=${root ? getComputedStyle(root).backgroundColor : '(elem missing)'} | ` +
          `md-app-paper bgcolor=${paperEl ? getComputedStyle(paperEl).backgroundColor : '(elem missing)'} | ` +
          `sidebar width_raw=${readStyle(sb, 'width')} / sidebar var(--md-card-background)=${readStyle(sb, '--md-card-background')} / ` +
          `:root --md-paper=${readStyle(document.documentElement, '--md-paper')}`
        console.log(msg)
        const bridge = (window as any).__msmc_bridge__
        if (bridge && typeof bridge.invoke === 'function') {
          bridge.invoke('log:write', {
            level: 'Information', message: msg, stack: '',
            url: window.location.href, ua: navigator.userAgent,
          }).catch(() => {})
        }
      } catch (e: any) {
        console.warn('[FE-DIAG] AppLayout diag 失败:', e?.message || e)
      }
    }, 16)
    return () => window.clearTimeout(t)
  }, [])

  return (
    <div
      className="h-full flex flex-col overflow-hidden relative"
      style={{ backgroundColor: 'var(--md-paper)', color: 'var(--md-body)' }}
    >
      {/* 环境粒子层：极低密度，仅在应用底层营造"系统在呼吸"的氛围 */}
      <ParticleField
        density={0.35}
        color="var(--md-primary-hue-mid)"
        connect
        connectDistance={140}
        speed={0.18}
        radiusRange={[0.5, 1.4]}
        maxOpacity={0.32}
        style={{ opacity: 0.6 }}
      />

      {/* 顶部双辉光：主色蓝 + Aquamarine 绿（ColorOS AOD 流动配色） */}
      <div
        aria-hidden
        style={{
          position: 'absolute',
          top: -120,
          left: '30%',
          transform: 'translateX(-50%)',
          width: '50%',
          height: 240,
          background:
            'radial-gradient(ellipse at center, var(--md-primary-subtle-background) 0%, transparent 70%)',
          opacity: 0.5,
          pointerEvents: 'none',
        }}
      />
      <div
        aria-hidden
        style={{
          position: 'absolute',
          top: -140,
          right: '5%',
          width: '40%',
          height: 260,
          background:
            'radial-gradient(ellipse at center, var(--md-aquamarine-soft) 0%, transparent 70%)',
          opacity: 0.35,
          pointerEvents: 'none',
          animation: 'mdBreathe 8s var(--md-ease-drift) infinite',
        }}
      />

      <div className="flex-1 flex overflow-hidden relative z-10">
        <Sidebar />

        <main className="flex-1 flex flex-col overflow-hidden relative">
          {isReady ? (
            <>
              {/* 退场页：保持挂载至动画结束，z-index 高于新页让模糊可见 */}
              {exitingKey && (
                <div
                  key={`exit-${exitingKey}`}
                  className="absolute inset-0 overflow-y-auto md-page-exit"
                  style={{ zIndex: 2 }}
                  aria-hidden
                >
                  {/* 退场页内容已切走，这里只留个"残影"层占位，实际内容由新页承载
                      为了让 blur 可见，我们让退场层叠加在新页之上但 pointer-events: none */}
                </div>
              )}
              {/* 新页：md-page-enter 触发弹簧入场 + 模糊消散 */}
              <div key={pageKey} className="flex-1 overflow-y-auto md-page-enter">
                <Suspense
                  fallback={
                    <div
                      className="h-full flex items-center justify-center"
                      style={{ backgroundColor: 'var(--md-deep-background)' }}
                    >
                      <div
                        className="md-breathe"
                        style={{
                          fontSize: 13,
                          color: 'var(--md-body-lighter)',
                          letterSpacing: '0.1em',
                        }}
                      >
                        正在加载页面...
                      </div>
                    </div>
                  }
                >
                  <LazyPageErrorBoundary pageName={pageKey}>
                    <Outlet />
                  </LazyPageErrorBoundary>
                </Suspense>
              </div>
            </>
          ) : (
            <div className="flex-1 flex items-center justify-center">
              <div className="text-center">
                {/* 双环品牌加载指示器 —— Aquario 风格 */}
                <div style={{ position: 'relative', width: 56, height: 56, margin: '0 auto 16px' }}>
                  <div
                    className="md-orbit"
                    style={{
                      position: 'absolute',
                      inset: 0,
                      borderRadius: '50%',
                      border: '2px solid transparent',
                      borderTopColor: 'var(--md-primary-hue-mid)',
                      borderRightColor: 'var(--md-aquamarine-light)',
                    }}
                  />
                  <div
                    className="md-orbit-reverse"
                    style={{
                      position: 'absolute',
                      inset: 8,
                      borderRadius: '50%',
                      border: '1.5px solid transparent',
                      borderBottomColor: 'var(--md-aquamarine)',
                      borderLeftColor: 'var(--md-primary-hue-lighter)',
                    }}
                  />
                  <div
                    className="md-breathe"
                    style={{
                      position: 'absolute',
                      inset: 20,
                      borderRadius: '50%',
                      background: 'var(--md-primary-subtle-background)',
                    }}
                  />
                </div>
                <p style={{ color: 'var(--md-body-light)' }} className="text-sm">
                  正在加载...
                </p>
              </div>
            </div>
          )}
        </main>
      </div>

      <footer
        className="flex items-center px-4 gap-4 flex-shrink-0 relative z-10"
        style={{
          height: 'var(--status-bar-height)',
          background: 'linear-gradient(90deg, var(--md-primary-hue-dark) 0%, var(--md-primary-hue-mid) 50%, var(--md-primary-hue-dark) 100%)',
          color: 'white',
          fontSize: 11,
          boxShadow: '0 -2px 12px -2px rgba(59, 130, 246, 0.45), 0 -1px 0 0 rgba(255, 255, 255, 0.06) inset',
        }}
      >
        <div className="flex items-center gap-2">
          <FaShield size={11} className="md-breathe" style={{ opacity: 0.9 }} />
          <span style={{ opacity: 0.95 }}>{statusMessage || '就绪'}</span>
        </div>

        <div className="flex-1" />

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5" style={{ opacity: 0.9 }}>
            <FaShield size={10} />
            <span className="font-medium text-[11px]">MSMC</span>
          </div>
          <div style={{ opacity: 0.85 }}>{currentTime}</div>
        </div>
      </footer>
    </div>
  )
}
