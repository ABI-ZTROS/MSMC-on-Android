import { useState, useEffect, useRef, useLayoutEffect, useCallback } from 'react'
import { FaIcon } from '@/components/icons/IconRegistry'
import { ParticleField } from '@/components/ui/ParticleField'

// ─────────────────────────────────────────────────────────────────────────────
// 类型定义
// ─────────────────────────────────────────────────────────────────────────────

type LogType = 'default' | 'success' | 'error' | 'warn' | 'info' | 'debug' | 'trace'

interface LogEntry {
  id: number
  message: string
  type: LogType
  tag: string
  timestamp: number
  level: number // 0=trace 1=debug 2=info 3=warn 4=error 5=success
}

let logIdCounter = 0

function extractTag(message: string): string {
  const m = message.match(/^\s*\[([A-Z0-9_]+)\]/)
  return m ? m[1] : ''
}

function levelOf(type: LogType): number {
  switch (type) {
    case 'trace': return 0
    case 'debug': return 1
    case 'info': return 2
    case 'warn': return 3
    case 'error': return 4
    case 'success': return 5
    default: return 2
  }
}

const TAG_COLOR: Record<string, string> = {
  BOOT: '#60a5fa',
  BUILD: '#a78bfa',
  LOAD: '#fbbf24',
  OK: '#34d399',
  ERR: '#f87171',
  WARN: '#fb923c',
  TIME: '#22d3ee',
  DETECT: '#60a5fa',
  NET: '#38bdf8',
  SEC: '#f472b6',
  CFG: '#a78bfa',
  METRIC: '#4ade80',
  BASE: '#94a3b8',
  VM: '#c084fc',
  FE: '#f59e0b',
  WV2: '#22d3ee',
  CLEAN: '#10b981',
  AUTH: '#ec4899',
  INIT: '#818cf8',
  READY: '#34d399',
  BRIDGE: '#06b6d4',
  SHUTDOWN: '#ef4444',
}

// ─────────────────────────────────────────────────────────────────────────────
// CSS keyframes — 极致暗黑科技+赛博朋克
// ─────────────────────────────────────────────────────────────────────────────

const KEYFRAMES = `
@keyframes cyberScan {
  0% { transform: translateY(-100%); }
  100% { transform: translateY(100vh); }
}
@keyframes cyberScanV {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100vw); }
}
@keyframes cyberGlitch {
  0%, 100% { clip-path: inset(0 0 0 0); transform: translate(0); }
  20% { clip-path: inset(20% 0 30% 0); transform: translate(-2px, 1px); }
  40% { clip-path: inset(50% 0 10% 0); transform: translate(2px, -1px); }
  60% { clip-path: inset(10% 0 60% 0); transform: translate(-1px, 2px); }
  80% { clip-path: inset(70% 0 5% 0); transform: translate(1px, -2px); }
}
@keyframes cyberGlitchRGB {
  0%, 100% { text-shadow: 0 0 0 transparent; }
  25% { text-shadow: -1px 0 #ff00ff, 1px 0 #00ffff; }
  50% { text-shadow: 2px 0 #ff00ff, -2px 0 #00ffff; }
  75% { text-shadow: -1px 0 #ff00ff, 1px 0 #00ffff; }
}
@keyframes cyberPulse {
  0%, 100% { opacity: 0.4; transform: scale(1); }
  50% { opacity: 0.8; transform: scale(1.05); }
}
@keyframes cyberFlicker {
  0%, 100% { opacity: 1; }
  3% { opacity: 0.4; }
  6% { opacity: 1; }
  7% { opacity: 0.6; }
  8% { opacity: 1; }
  47% { opacity: 1; }
  48% { opacity: 0.3; }
  49% { opacity: 1; }
}
@keyframes cyberBoot {
  0% { opacity: 0; filter: blur(20px); transform: scale(0.8); }
  50% { opacity: 0.5; filter: blur(8px); transform: scale(1.05); }
  100% { opacity: 1; filter: blur(0); transform: scale(1); }
}
@keyframes cyberGridMove {
  0% { background-position: 0 0; }
  100% { background-position: 40px 40px; }
}
@keyframes cyberNeonPulse {
  0%, 100% { box-shadow: 0 0 20px rgba(59,130,246,0.4), 0 0 40px rgba(59,130,246,0.15), inset 0 0 20px rgba(59,130,246,0.08); }
  50% { box-shadow: 0 0 50px rgba(59,130,246,0.7), 0 0 100px rgba(59,130,246,0.3), inset 0 0 35px rgba(59,130,246,0.15); }
}
@keyframes cyberTextGlow {
  0%, 100% { text-shadow: 0 0 10px rgba(96,165,250,0.6), 0 0 20px rgba(96,165,250,0.3); }
  50% { text-shadow: 0 0 25px rgba(96,165,250,0.9), 0 0 50px rgba(96,165,250,0.5); }
}
@keyframes cyberLogEntry {
  from { opacity: 0; transform: translateX(-12px); filter: blur(2px); }
  to { opacity: 1; transform: translateX(0); filter: blur(0); }
}
@keyframes cyberRingRotate {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
@keyframes cyberRingRotateRev {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(-360deg); }
}
@keyframes cyberDataFlow {
  0% { stroke-dashoffset: 0; }
  100% { stroke-dashoffset: -40; }
}
@keyframes cyberCursor {
  0%, 49% { opacity: 1; }
  50%, 100% { opacity: 0; }
}
@keyframes cyberBarFill {
  0% { width: 0%; }
  100% { width: 100%; }
}
@keyframes cyberHexSpin {
  0% { transform: rotate(0deg) scale(1); }
  50% { transform: rotate(180deg) scale(1.08); }
  100% { transform: rotate(360deg) scale(1); }
}
@keyframes cyberNoiseShift {
  0%, 100% { transform: translate(0, 0); }
  25% { transform: translate(-1px, 1px); }
  50% { transform: translate(1px, -1px); }
  75% { transform: translate(-1px, -1px); }
}
@keyframes cyberTagPop {
  0% { transform: scale(0.5) translateY(4px); opacity: 0; }
  60% { transform: scale(1.15) translateY(0); opacity: 1; }
  100% { transform: scale(1) translateY(0); opacity: 1; }
}
@keyframes cyberLevelBar {
  0% { width: 0%; opacity: 0.5; }
  50% { width: 100%; opacity: 1; }
  100% { width: 100%; opacity: 0.8; }
}
@keyframes cyberRipple {
  0% { transform: scale(0.8); opacity: 0.8; }
  100% { transform: scale(2.4); opacity: 0; }
}
@keyframes cyberVignette {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 0.75; }
}
@keyframes cyberHudCorner {
  0%, 100% { opacity: 0.4; }
  50% { opacity: 1; }
}
@keyframes cyberMatrixRain {
  0% { background-position: 0 0; }
  100% { background-position: 0 -200px; }
}
`

// ─────────────────────────────────────────────────────────────────────────────
// 装逼 ASCII LOGO
// ─────────────────────────────────────────────────────────────────────────────

const ASCII_LOGO = String.raw`
███╗   ███╗ ██████╗ ███╗   ██╗ ██████╗     ██╗     ██╗███████╗██████╗
████╗ ████║██╔═══██╗████╗  ██║██╔═══██╗    ██║     ██║██╔════╝██╔══██╗
██╔████╔██║██║   ██║██╔██╗ ██║██║   ██║    ██║     ██║█████╗  ██████╔╝
██║╚██╔╝██║██║   ██║██║╚██╗██║██║   ██║    ██║     ██║██╔══╝  ██╔══██╗
██║ ╚═╝ ██║╚██████╔╝██║ ╚████║╚██████╔╝    ███████╗██║███████╗██║  ██║
╚═╝     ╚═╝ ╚═════╝ ╚═╝  ╚═══╝ ╚═════╝     ╚══════╝╚═╝╚══════╝╚═╝  ╚═╝
`

// ─────────────────────────────────────────────────────────────────────────────
// 主组件
// ─────────────────────────────────────────────────────────────────────────────

export function StartupPage(): JSX.Element {
  const [progress, setProgress] = useState(0)
  const [currentStatus, setCurrentStatus] = useState('正在初始化...')
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [isFailed, setIsFailed] = useState(false)
  const [isCompleted, setIsCompleted] = useState(false)
  const [version, setVersion] = useState('v1.0.0')
  const [primaryColor, setPrimaryColor] = useState('#3B82F6')
  const [bootDone, setBootDone] = useState(false)
  const [phase, setPhase] = useState<'boot' | 'running' | 'done' | 'failed'>('boot')
  const [uptime, setUptime] = useState(0)
  const [fps, setFps] = useState(60)
  const [cpu, setCpu] = useState(0)
  const [mem, setMem] = useState(0)
  const logContainerRef = useRef<HTMLDivElement>(null)
  const bridgeReadyRef = useRef(false)
  const autoScrollRef = useRef(true)
  const bootStartRef = useRef<number>(Date.now())
  const fpsCounterRef = useRef<{ frames: number; lastTs: number }>({ frames: 0, lastTs: performance.now() })

  const appendLog = useCallback((message: string, type: LogType = 'default'): void => {
    setLogs((prev) => [
      ...prev.slice(-199), // 保留最近 200 条
      {
        id: ++logIdCounter,
        message,
        type,
        tag: extractTag(message),
        timestamp: Date.now(),
        level: levelOf(type),
      },
    ])
  }, [])

  const formatTime = (ts: number): string => {
    const d = new Date(ts)
    const hh = String(d.getHours()).padStart(2, '0')
    const mm = String(d.getMinutes()).padStart(2, '0')
    const ss = String(d.getSeconds()).padStart(2, '0')
    const ms = String(d.getMilliseconds()).padStart(3, '0')
    return `${hh}:${mm}:${ss}.${ms}`
  }

  const formatUptime = (s: number): string => {
    const m = Math.floor(s / 60)
    const sec = s % 60
    return `${String(m).padStart(2, '0')}:${String(sec).padStart(2, '0')}`
  }

  // ── 桥接初始化 ──
  useEffect(() => {
    function initBridge(): void {
      if (bridgeReadyRef.current) return
      if (!window.chrome?.webview) return
      bridgeReadyRef.current = true

      window.chrome.webview.addEventListener('message', (event) => {
        const data = event.data as {
          type?: string
          action?: string
          payload?: unknown
        }
        if (!data || !data.type) return
        const type = String(data.type).toLowerCase()
        const action = data.action || ''

        if (type === 'event') {
          switch (action) {
            case 'startup:progress': {
              const payload = data.payload as { percent: number; status?: string }
              if (payload) {
                setProgress(Math.max(0, Math.min(100, payload.percent)))
                if (typeof payload.status === 'string' && payload.status.length > 0) {
                  setCurrentStatus(payload.status)
                }
              }
              break
            }
            case 'startup:log': {
              const payload = data.payload as { message: string; isError?: boolean; isSuccess?: boolean }
              if (payload) {
                const entryType: LogType = payload.isError
                  ? 'error'
                  : payload.isSuccess
                    ? 'success'
                    : 'default'
                appendLog(payload.message, entryType)
              }
              break
            }
            case 'startup:completed': {
              setIsCompleted(true)
              setProgress(100)
              setCurrentStatus('初始化完成')
              setPhase('done')
              const payload = data.payload as { message?: string }
              const msg = payload?.message || '[OK] 初始化完成，正在启动主界面...'
              appendLog(msg, 'success')
              break
            }
            case 'startup:failed': {
              setIsFailed(true)
              setProgress(100)
              setPhase('failed')
              const payload = data.payload as { message?: string }
              const msg = payload?.message || '启动失败'
              setCurrentStatus(`启动失败：${msg}`)
              appendLog(`[ERR] 启动失败：${msg}`, 'error')
              appendLog(`[SHUTDOWN] 进入受控关机流程`, 'warn')
              break
            }
            case 'startup:init': {
              const payload = data.payload as { version?: string; primaryColor?: string }
              if (payload?.version) setVersion(`v${payload.version}`)
              if (payload?.primaryColor) setPrimaryColor(payload.primaryColor)
              appendLog(`[BRIDGE] 桥接握手完成，版本 ${payload?.version || 'unknown'}`, 'info')
              break
            }
            case 'startup:themeChanged': {
              const payload = data.payload as { primaryColor?: string }
              if (payload?.primaryColor) {
                setPrimaryColor(payload.primaryColor)
                appendLog(`[INIT] 主题切换：${payload.primaryColor}`, 'debug')
              }
              break
            }
          }
        }
      })

      if (window.__msmc_bridge__) {
        window.__msmc_bridge__.sendEvent('startup:ready', { ts: Date.now() })
      }
    }

    if (document.readyState === 'complete') {
      initBridge()
    } else {
      window.addEventListener('load', initBridge, { once: true })
    }
    setTimeout(initBridge, 100)
    setTimeout(initBridge, 500)
    setTimeout(initBridge, 1000)

    const timer = setTimeout(() => {
      setBootDone(true)
      setPhase('running')
      appendLog('[BOOT] React 视图挂载完成', 'success')
      appendLog('[BRIDGE] 等待 C# 主机事件 ...', 'info')
    }, 800)
    return () => clearTimeout(timer)
  }, [appendLog])

  // ── Uptime 计时 ──
  useEffect(() => {
    const t = setInterval(() => setUptime(Math.floor((Date.now() - bootStartRef.current) / 1000)), 1000)
    return () => clearInterval(t)
  }, [])

  // ── FPS 监测 + 模拟 CPU/MEM 数据 ──
  useEffect(() => {
    let raf = 0
    const tick = (): void => {
      const now = performance.now()
      const state = fpsCounterRef.current
      state.frames++
      if (now - state.lastTs >= 1000) {
        setFps(Math.round((state.frames * 1000) / (now - state.lastTs)))
        state.frames = 0
        state.lastTs = now
        // 模拟数据：用于装逼 HUD
        setCpu(Math.round(8 + Math.random() * 18 + (isCompleted ? 0 : 12)))
        setMem(Math.round(120 + Math.random() * 60 + (isCompleted ? 0 : 40)))
      }
      raf = requestAnimationFrame(tick)
    }
    raf = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(raf)
  }, [isCompleted])

  // ── 自动滚动 ──
  useLayoutEffect(() => {
    const el = logContainerRef.current
    if (!el || !autoScrollRef.current) return
    requestAnimationFrame(() => {
      const el2 = logContainerRef.current
      if (!el2) return
      if (el2.scrollHeight - el2.clientHeight - el2.scrollTop < 24) {
        el2.scrollTo({ top: el2.scrollHeight, behavior: 'smooth' })
      }
    })
  }, [logs])

  useEffect(() => {
    const el = logContainerRef.current
    if (!el) return
    const onScroll = (): void => {
      const distanceFromBottom = el.scrollHeight - el.clientHeight - el.scrollTop
      autoScrollRef.current = distanceFromBottom < 24
    }
    el.addEventListener('scroll', onScroll, { passive: true })
    return () => el.removeEventListener('scroll', onScroll)
  }, [])

  const handleClose = (): void => {
    if (isFailed && window.__msmc_bridge__) {
      window.__msmc_bridge__.sendEvent('startup:shutdown', {})
    } else if (window.__msmc_bridge__) {
      window.__msmc_bridge__.sendEvent('startup:close', {})
    }
  }

  const handleWindowDrag = (e: React.MouseEvent<HTMLDivElement>): void => {
    if (e.button !== 0) return
    if (window.__msmc_bridge__) {
      window.__msmc_bridge__.sendEvent('startup:dragMove', {})
    }
  }

  const statusColor = isFailed ? '#f87171' : isCompleted ? '#34d399' : primaryColor
  const circumference = 2 * Math.PI * 45
  const strokeDashoffset = circumference - (progress / 100) * circumference

  // 错误/成功数统计
  const errorCount = logs.filter((l) => l.type === 'error').length
  const successCount = logs.filter((l) => l.type === 'success').length
  const warnCount = logs.filter((l) => l.type === 'warn').length

  return (
    <>
      <style>{KEYFRAMES}</style>
      <div
        className="w-full h-full flex flex-col min-h-0 relative overflow-hidden"
        style={{
          backgroundColor: '#020617',
          fontFamily: 'var(--md-font-family)',
          color: 'var(--md-body)',
        }}
      >
        {/* ═══════════════════════════════════════════════════════════════
            背景层：粒子 + 网格 + 扫描线 + 暗角 + HUD 角标
           ═══════════════════════════════════════════════════════════════ */}

        {/* 粒子场 */}
        <ParticleField
          density={1.5}
          color={primaryColor}
          connect
          connectDistance={120}
          speed={0.4}
          maxOpacity={0.5}
        />

        {/* 网格背景 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            inset: 0,
            backgroundImage: `
              linear-gradient(rgba(59,130,246,0.06) 1px, transparent 1px),
              linear-gradient(90deg, rgba(59,130,246,0.06) 1px, transparent 1px)
            `,
            backgroundSize: '40px 40px',
            animation: 'cyberGridMove 4s linear infinite',
            pointerEvents: 'none',
          }}
        />

        {/* 矩阵代码雨（极淡） */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            inset: 0,
            opacity: 0.04,
            backgroundImage:
              'repeating-linear-gradient(0deg, rgba(34,197,94,0.5) 0, rgba(34,197,94,0.5) 1px, transparent 1px, transparent 8px)',
            backgroundSize: '8px 200px',
            animation: 'cyberMatrixRain 8s linear infinite',
            pointerEvents: 'none',
          }}
        />

        {/* CRT 扫描线 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            inset: 0,
            background: 'repeating-linear-gradient(0deg, transparent 0, transparent 2px, rgba(0,0,0,0.18) 2px, rgba(0,0,0,0.18) 4px)',
            pointerEvents: 'none',
            zIndex: 1,
          }}
        />
        {/* 横向扫描光带 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            left: 0,
            right: 0,
            height: 140,
            background: `linear-gradient(to bottom, transparent, ${primaryColor}22, transparent)`,
            animation: 'cyberScan 6s linear infinite',
            pointerEvents: 'none',
            zIndex: 1,
          }}
        />
        {/* 纵向扫描光带 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            top: 0,
            bottom: 0,
            width: 120,
            background: `linear-gradient(to right, transparent, ${primaryColor}10, transparent)`,
            animation: 'cyberScanV 9s linear infinite',
            pointerEvents: 'none',
            zIndex: 1,
          }}
        />

        {/* 暗角 + 呼吸 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            inset: 0,
            background: 'radial-gradient(ellipse at center, transparent 30%, rgba(2,6,23,0.6) 80%, rgba(2,6,23,0.95) 100%)',
            pointerEvents: 'none',
            zIndex: 2,
            animation: 'cyberVignette 5s ease-in-out infinite',
          }}
        />

        {/* HUD 四角装饰 */}
        {[
          { top: 12, left: 12, borderTop: `1px solid ${primaryColor}50`, borderLeft: `1px solid ${primaryColor}50` },
          { top: 12, right: 12, borderTop: `1px solid ${primaryColor}50`, borderRight: `1px solid ${primaryColor}50` },
          { bottom: 12, left: 12, borderBottom: `1px solid ${primaryColor}50`, borderLeft: `1px solid ${primaryColor}50` },
          { bottom: 12, right: 12, borderBottom: `1px solid ${primaryColor}50`, borderRight: `1px solid ${primaryColor}50` },
        ].map((s, i) => (
          <div
            key={i}
            aria-hidden
            style={{
              position: 'absolute',
              width: 24,
              height: 24,
              animation: 'cyberHudCorner 3s ease-in-out infinite',
              animationDelay: `${i * 0.4}s`,
              pointerEvents: 'none',
              zIndex: 3,
              ...s,
            }}
          />
        ))}

        {/* 左上角 HUD：系统状态 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            top: 24,
            left: 32,
            fontSize: 9,
            fontFamily: 'Consolas, "JetBrains Mono", monospace',
            color: 'var(--md-body-lighter)',
            opacity: 0.6,
            zIndex: 5,
            pointerEvents: 'none',
            letterSpacing: '0.1em',
          }}
        >
          <div>MSMC://boot.sequence</div>
          <div style={{ marginTop: 2 }}>SESSION {bootStartRef.current.toString(36).toUpperCase()}</div>
          <div style={{ marginTop: 2, color: statusColor }}>
            ● {phase.toUpperCase()}
          </div>
        </div>

        {/* 右上角 HUD：实时监控 */}
        <div
          aria-hidden
          style={{
            position: 'absolute',
            top: 24,
            right: 32,
            fontSize: 9,
            fontFamily: 'Consolas, "JetBrains Mono", monospace',
            color: 'var(--md-body-lighter)',
            opacity: 0.7,
            zIndex: 5,
            pointerEvents: 'none',
            textAlign: 'right',
            letterSpacing: '0.1em',
          }}
        >
          <div>FPS {fps}</div>
          <div style={{ marginTop: 2, color: cpu > 30 ? '#fb923c' : '#34d399' }}>CPU {cpu}%</div>
          <div style={{ marginTop: 2, color: mem > 200 ? '#fb923c' : '#60a5fa' }}>MEM {mem}MB</div>
          <div style={{ marginTop: 2 }}>T+{formatUptime(uptime)}</div>
        </div>

        {/* ═══════════════════════════════════════════════════════════════
            主内容层：居中聚焦
           ═══════════════════════════════════════════════════════════════ */}
        <div
          className="flex-1 flex flex-col items-center justify-center px-8 relative"
          style={{ zIndex: 10 }}
          onMouseDown={handleWindowDrag}
        >
          {/* ── 三层旋转环 + 环形进度 + 六边形 Logo ── */}
          <div
            className="relative flex items-center justify-center mb-6"
            style={{
              width: 160,
              height: 160,
              animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out forwards',
            }}
          >
            {/* 最外层旋转刻度环 */}
            <svg
              width={160}
              height={160}
              style={{
                position: 'absolute',
                animation: 'cyberRingRotate 20s linear infinite',
              }}
            >
              {Array.from({ length: 60 }).map((_, i) => {
                const angle = (i / 60) * 360
                const long = i % 5 === 0
                const r1 = long ? 74 : 78
                const r2 = 80
                const rad = (angle * Math.PI) / 180
                const x1 = 80 + r1 * Math.cos(rad)
                const y1 = 80 + r1 * Math.sin(rad)
                const x2 = 80 + r2 * Math.cos(rad)
                const y2 = 80 + r2 * Math.sin(rad)
                return (
                  <line
                    key={i}
                    x1={x1}
                    y1={y1}
                    x2={x2}
                    y2={y2}
                    stroke={`${primaryColor}${long ? '70' : '30'}`}
                    strokeWidth={long ? 1.4 : 0.7}
                  />
                )
              })}
            </svg>

            {/* 第二层反向旋转的虚线环 */}
            <svg
              width={140}
              height={140}
              style={{
                position: 'absolute',
                animation: 'cyberRingRotateRev 12s linear infinite',
              }}
            >
              <circle
                cx={70}
                cy={70}
                r={62}
                fill="none"
                stroke={`${primaryColor}40`}
                strokeWidth={1}
                strokeDasharray="3 6"
              />
            </svg>

            {/* 第三层脉冲霓虹环 */}
            <div
              style={{
                position: 'absolute',
                width: 130,
                height: 130,
                borderRadius: '50%',
                border: `1px solid ${primaryColor}40`,
                animation: 'cyberPulse 2.5s ease-in-out infinite',
              }}
            />
            <div
              style={{
                position: 'absolute',
                width: 110,
                height: 110,
                borderRadius: '50%',
                border: `1px solid ${primaryColor}25`,
                animation: 'cyberPulse 2.5s ease-in-out infinite 0.5s',
              }}
            />

            {/* SVG 环形进度条 */}
            <svg width={120} height={120} style={{ position: 'absolute', transform: 'rotate(-90deg)' }}>
              <circle cx={60} cy={60} r={45} fill="none" stroke={`${primaryColor}15`} strokeWidth={2} />
              <circle
                cx={60}
                cy={60}
                r={45}
                fill="none"
                stroke={statusColor}
                strokeWidth={2.8}
                strokeLinecap="round"
                strokeDasharray={circumference}
                strokeDashoffset={strokeDashoffset}
                style={{
                  transition: 'stroke-dashoffset 400ms cubic-bezier(0.33, 1, 0.68, 1)',
                  filter: `drop-shadow(0 0 8px ${statusColor}aa)`,
                }}
              />
              {/* 数据流装饰 */}
              <circle
                cx={60}
                cy={60}
                r={52}
                fill="none"
                stroke={`${primaryColor}50`}
                strokeWidth={1}
                strokeDasharray="4 8"
                style={{ animation: 'cyberDataFlow 2s linear infinite' }}
              />
            </svg>

            {/* 中心六边形 Logo 容器 */}
            <div
              style={{
                width: 60,
                height: 60,
                position: 'relative',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                animation: 'cyberNeonPulse 2s ease-in-out infinite',
              }}
            >
              {/* 六边形 SVG */}
              <svg
                width={60}
                height={60}
                style={{
                  position: 'absolute',
                  animation: 'cyberHexSpin 8s ease-in-out infinite',
                  filter: `drop-shadow(0 0 12px ${primaryColor}80)`,
                }}
              >
                <polygon
                  points="30,2 56,16 56,44 30,58 4,44 4,16"
                  fill={`linear-gradient(135deg, ${primaryColor}, ${primaryColor}cc)`}
                  stroke={`${primaryColor}`}
                  strokeWidth={1.5}
                />
              </svg>
              {/* 渐变填充覆盖 */}
              <div
                style={{
                  position: 'absolute',
                  width: 60,
                  height: 60,
                  clipPath: 'polygon(50% 0%, 100% 25%, 100% 75%, 50% 100%, 0% 75%, 0% 25%)',
                  background: `linear-gradient(135deg, ${primaryColor}ee, ${primaryColor}88)`,
                  boxShadow: `inset 0 0 12px rgba(255,255,255,0.2)`,
                }}
              />
              <FaIcon
                kind="ShieldHalvedSolid"
                size={26}
                className="text-white"
                style={{
                  position: 'relative',
                  zIndex: 1,
                  filter: 'drop-shadow(0 0 4px rgba(255,255,255,0.8))',
                }}
              />
            </div>

            {/* 进度百分比 - 右下角带斜杠装饰 */}
            <div
              style={{
                position: 'absolute',
                bottom: -4,
                right: -14,
                fontSize: 11,
                fontFamily: 'Consolas, "JetBrains Mono", monospace',
                color: statusColor,
                fontWeight: 700,
                textShadow: `0 0 10px ${statusColor}aa`,
                animation: 'cyberFlicker 3s linear infinite',
                letterSpacing: '0.05em',
              }}
            >
              [{String(Math.round(progress)).padStart(3, '0')}%]
            </div>

            {/* 进度波纹圆 */}
            {!isCompleted && !isFailed && progress > 0 && (
              <div
                aria-hidden
                style={{
                  position: 'absolute',
                  width: 120,
                  height: 120,
                  borderRadius: '50%',
                  border: `2px solid ${primaryColor}`,
                  animation: 'cyberRipple 1.6s ease-out infinite',
                  pointerEvents: 'none',
                }}
              />
            )}
          </div>

          {/* ── 标题：带 RGB Glitch ── */}
          <div
            className="text-center mb-1"
            style={{ animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out 0.15s both' }}
          >
            <div
              style={{
                fontSize: 36,
                fontWeight: 900,
                letterSpacing: '0.18em',
                color: 'var(--md-body)',
                position: 'relative',
                display: 'inline-block',
                animation: 'cyberTextGlow 3s ease-in-out infinite, cyberGlitchRGB 6s steps(1) infinite',
              }}
            >
              MSMC
              {/* Glitch 副本（红） */}
              <span
                aria-hidden
                style={{
                  position: 'absolute',
                  inset: 0,
                  color: '#f87171',
                  opacity: 0.65,
                  animation: 'cyberGlitch 4s steps(1) infinite',
                  clipPath: 'inset(0 0 0 0)',
                }}
              >
                MSMC
              </span>
              {/* Glitch 副本（青） */}
              <span
                aria-hidden
                style={{
                  position: 'absolute',
                  inset: 0,
                  color: '#22d3ee',
                  opacity: 0.55,
                  animation: 'cyberGlitch 4s steps(1) infinite 0.2s',
                  clipPath: 'inset(0 0 0 0)',
                }}
              >
                MSMC
              </span>
            </div>
          </div>

          {/* ── 副标题 + 版本 + 状态徽章 ── */}
          <div
            className="text-center mb-5"
            style={{ animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out 0.3s both' }}
          >
            <div style={{ fontSize: 12, color: 'var(--md-body-light)', letterSpacing: '0.12em' }}>
              MINECRAFT SERVER MANAGEMENT CONSOLE
            </div>
            <div
              style={{
                fontSize: 10,
                color: 'var(--md-body-lighter)',
                marginTop: 6,
                fontFamily: 'Consolas, "JetBrains Mono", monospace',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
              }}
            >
              <span>{version}</span>
              <span style={{ opacity: 0.4 }}>·</span>
              <span
                style={{
                  padding: '1px 6px',
                  borderRadius: 2,
                  fontSize: 9,
                  fontWeight: 700,
                  letterSpacing: '0.1em',
                  color: isFailed ? '#f87171' : isCompleted ? '#34d399' : primaryColor,
                  border: `1px solid ${isFailed ? '#f8717160' : isCompleted ? '#34d39960' : `${primaryColor}60`}`,
                  background: isFailed
                    ? 'rgba(248,113,113,0.1)'
                    : isCompleted
                      ? 'rgba(52,211,153,0.1)'
                      : `${primaryColor}10`,
                }}
              >
                {isFailed ? '⚠ SYSTEM ERROR' : isCompleted ? '✓ READY' : '▶ BOOTING'}
              </span>
            </div>
          </div>

          {/* ── 状态行（带状态图标 + 数据流装饰） ── */}
          <div
            className="mb-4 flex items-center gap-2"
            style={{ animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out 0.45s both' }}
          >
            <div
              style={{
                width: 6,
                height: 6,
                borderRadius: '50%',
                backgroundColor: statusColor,
                boxShadow: `0 0 10px ${statusColor}`,
                animation: !isFailed && !isCompleted ? 'cyberPulse 1s ease-in-out infinite' : 'none',
              }}
            />
            <span
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
                fontFamily: 'Consolas, "JetBrains Mono", monospace',
                maxWidth: 420,
                overflow: 'hidden',
                whiteSpace: 'nowrap',
                textOverflow: 'ellipsis',
              }}
            >
              {currentStatus}
            </span>
            {!isFailed && !isCompleted && (
              <span
                style={{
                  fontSize: 11,
                  color: statusColor,
                  fontFamily: 'Consolas, monospace',
                  animation: 'cyberCursor 1s steps(1) infinite',
                }}
              >
                ▋
              </span>
            )}
          </div>

          {/* ═══════════════════════════════════════════════════════════════
              玻璃卡片日志区（增强版）
             ═══════════════════════════════════════════════════════════════ */}
          <div
            className="w-full max-w-[560px] flex flex-col overflow-hidden rounded-xl"
            style={{
              animation: bootDone ? 'none' : 'cyberBoot 0.8s ease-out 0.6s both',
              height: 200,
              background: 'rgba(15, 23, 42, 0.6)',
              backdropFilter: 'blur(14px)',
              WebkitBackdropFilter: 'blur(14px)',
              border: `1px solid ${primaryColor}30`,
              boxShadow: `0 4px 24px rgba(0,0,0,0.5), 0 0 60px ${primaryColor}10, inset 0 1px 0 rgba(255,255,255,0.06)`,
            }}
          >
            {/* 卡片标题栏：双窗口按钮 + 计数器 + 状态灯 */}
            <div
              className="flex items-center justify-between px-3 py-1.5 flex-shrink-0"
              style={{
                borderBottom: `1px solid ${primaryColor}20`,
                background: 'rgba(2, 6, 23, 0.4)',
              }}
            >
              <div className="flex items-center gap-2">
                {/* 三个 macOS 风格圆点 */}
                <div style={{ display: 'flex', gap: 5 }}>
                  <div style={{ width: 9, height: 9, borderRadius: '50%', background: '#f87171', boxShadow: '0 0 4px rgba(248,113,113,0.5)' }} />
                  <div style={{ width: 9, height: 9, borderRadius: '50%', background: '#fbbf24', boxShadow: '0 0 4px rgba(251,191,36,0.5)' }} />
                  <div style={{ width: 9, height: 9, borderRadius: '50%', background: '#34d399', boxShadow: '0 0 4px rgba(52,211,153,0.5)' }} />
                </div>
                <div style={{ width: 8 }} />
                <FaIcon kind="TerminalSolid" size={12} style={{ color: primaryColor }} />
                <span
                  style={{
                    fontSize: 10,
                    fontWeight: 700,
                    color: 'var(--md-body-light)',
                    letterSpacing: '0.1em',
                  }}
                >
                  SYSTEM CONSOLE / tty/msmc0
                </span>
              </div>
              <div className="flex items-center gap-3">
                {/* 错误/警告/成功计数 */}
                <div style={{ display: 'flex', gap: 8, fontFamily: 'Consolas, monospace', fontSize: 9 }}>
                  <span style={{ color: '#f87171' }}>E:{errorCount}</span>
                  <span style={{ color: '#fb923c' }}>W:{warnCount}</span>
                  <span style={{ color: '#34d399' }}>S:{successCount}</span>
                  <span style={{ color: 'var(--md-body-lighter)' }}>N:{logs.length}</span>
                </div>
                <span style={{ fontSize: 9, color: 'var(--md-body-lighter)', fontFamily: 'Consolas, monospace' }}>
                  {logs.length} entries
                </span>
                <div
                  style={{
                    width: 6,
                    height: 6,
                    borderRadius: '50%',
                    backgroundColor: statusColor,
                    boxShadow: `0 0 8px ${statusColor}`,
                    animation: !isFailed && !isCompleted ? 'cyberPulse 1.5s ease-in-out infinite' : 'none',
                  }}
                />
              </div>
            </div>

            {/* 日志列表 */}
            <div
              ref={logContainerRef}
              className="flex-1 overflow-y-auto min-h-0"
              style={{
                padding: '8px 12px',
                scrollbarWidth: 'thin',
              }}
            >
              {logs.length === 0 && (
                <div
                  style={{
                    fontSize: 11,
                    color: 'var(--md-body-lighter)',
                    opacity: 0.4,
                    textAlign: 'center',
                    padding: '20px 0',
                    fontFamily: 'Consolas, "JetBrains Mono", monospace',
                  }}
                >
                  awaiting system signals<span style={{ animation: 'cyberCursor 1s steps(1) infinite' }}>▋</span>
                </div>
              )}
              {logs.map((entry) => {
                const tagColor = TAG_COLOR[entry.tag] || 'var(--md-body)'
                const isError = entry.type === 'error'
                const isSuccess = entry.type === 'success'
                const isWarn = entry.type === 'warn'
                const isInfo = entry.type === 'info' || entry.type === 'debug' || entry.type === 'trace'
                const tagMatch = entry.message.match(/^(\s*\[[A-Z0-9_]+\])(.*)$/s)
                const tagPart = tagMatch ? tagMatch[1] : ''
                const bodyPart = tagMatch ? tagMatch[2] : entry.message
                const levelMarker = isError ? '✗' : isSuccess ? '✓' : isWarn ? '!' : isInfo ? '›' : '·'
                return (
                  <div
                    key={entry.id}
                    style={{
                      display: 'flex',
                      gap: 6,
                      alignItems: 'flex-start',
                      fontFamily: 'Consolas, "JetBrains Mono", "Cascadia Code", monospace',
                      fontSize: 11,
                      lineHeight: 1.7,
                      marginBottom: 1,
                      padding: '1px 4px',
                      borderRadius: 2,
                      animation: 'cyberLogEntry 0.25s ease-out',
                      backgroundColor: isError
                        ? 'rgba(239, 68, 68, 0.10)'
                        : isSuccess
                          ? 'rgba(52, 211, 153, 0.07)'
                          : isWarn
                            ? 'rgba(251, 146, 60, 0.07)'
                            : 'transparent',
                      color: isError
                        ? '#f87171'
                        : isSuccess
                          ? '#34d399'
                          : isWarn
                            ? '#fb923c'
                            : 'var(--md-body)',
                      wordBreak: 'break-word',
                      whiteSpace: 'pre-wrap',
                      borderLeft: tagPart ? `2px solid ${tagColor}` : 'none',
                      paddingLeft: tagPart ? 6 : 4,
                    }}
                  >
                    <span
                      style={{
                        flexShrink: 0,
                        fontSize: 9,
                        color: 'var(--md-body-lighter)',
                        opacity: 0.5,
                        userSelect: 'none',
                      }}
                    >
                      {formatTime(entry.timestamp)}
                    </span>
                    <span
                      style={{
                        flexShrink: 0,
                        color: isError ? '#f87171' : isSuccess ? '#34d399' : isWarn ? '#fb923c' : tagColor,
                        fontWeight: 700,
                        fontSize: 11,
                        userSelect: 'none',
                        width: 10,
                        textAlign: 'center',
                      }}
                    >
                      {levelMarker}
                    </span>
                    {tagPart && (
                      <span
                        style={{
                          flexShrink: 0,
                          color: tagColor,
                          fontWeight: 700,
                          fontSize: 10,
                          userSelect: 'none',
                          animation: 'cyberTagPop 0.3s ease-out',
                        }}
                      >
                        {tagPart}
                      </span>
                    )}
                    <span style={{ flex: 1 }}>{bodyPart}</span>
                  </div>
                )
              })}
            </div>

            {/* 卡片底部状态栏：命令行风格 */}
            <div
              className="flex-shrink-0 px-3 py-1"
              style={{
                borderTop: `1px solid ${primaryColor}15`,
                background: 'rgba(2, 6, 23, 0.5)',
                fontFamily: 'Consolas, "JetBrains Mono", monospace',
                fontSize: 9,
                color: 'var(--md-body-lighter)',
                display: 'flex',
                justifyContent: 'space-between',
              }}
            >
              <span>root@msmc:~#</span>
              <span style={{ opacity: 0.6 }}>{autoScrollRef.current ? 'TAIL' : 'PAUSED'} · UTF-8 · LF</span>
            </div>
          </div>

          {/* ── 退出按钮（仅失败时显示） ── */}
          <div className="mt-5" style={{ height: 40 }}>
            {isFailed && (
              <button
                onClick={handleClose}
                className="px-6 py-2 text-white font-semibold rounded cursor-pointer border-none"
                style={{
                  width: 140,
                  height: 38,
                  fontSize: 12,
                  letterSpacing: '0.15em',
                  background: 'linear-gradient(135deg, #dc2626, #991b1b)',
                  boxShadow: '0 0 24px rgba(220,38,38,0.5), inset 0 1px 0 rgba(255,255,255,0.15)',
                  transition: 'all 150ms ease',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.boxShadow = '0 0 36px rgba(220,38,38,0.7), inset 0 1px 0 rgba(255,255,255,0.2)'
                  e.currentTarget.style.transform = 'scale(1.04)'
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.boxShadow = '0 0 24px rgba(220,38,38,0.5), inset 0 1px 0 rgba(255,255,255,0.15)'
                  e.currentTarget.style.transform = 'scale(1)'
                }}
              >
                ⏻ SHUTDOWN
              </button>
            )}
          </div>
        </div>

        {/* ═══════════════════════════════════════════════════════════════
            底部水印 + ASCII 装饰
           ═══════════════════════════════════════════════════════════════ */}
        <div
          className="text-center pb-3 flex-shrink-0"
          style={{ zIndex: 10, pointerEvents: 'none' }}
        >
          <div
            style={{
              fontSize: 8,
              color: 'var(--md-body-lighter)',
              opacity: 0.25,
              fontFamily: 'Consolas, "JetBrains Mono", monospace',
              letterSpacing: '0.15em',
              whiteSpace: 'pre',
              lineHeight: 1.1,
              display: 'none', // ASCII_LOGO 太大，默认隐藏，留给开发者特殊场景启用
            }}
          >
            {ASCII_LOGO}
          </div>
          <span
            style={{
              fontSize: 9,
              color: 'var(--md-body-lighter)',
              opacity: 0.35,
              fontFamily: 'Consolas, "JetBrains Mono", monospace',
              letterSpacing: '0.2em',
            }}
          >
            io.NET.ZTR_OS · SECURED · UTC+8 · © 2026 ABI-ZTROS
          </span>
        </div>
      </div>
    </>
  )
}
