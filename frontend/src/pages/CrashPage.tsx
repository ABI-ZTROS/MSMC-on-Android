import { useState, useEffect, useRef } from 'react'
import { FaIcon } from '@/components/icons/IconRegistry'
import { ParticleField } from '@/components/ui/ParticleField'
import { useRipple } from '@/hooks/useRipple'

/**
 * 灾难性故障信息（从 C# 通过桥接推送）
 */
interface CrashFrame {
  /** 故障发生的方法/类全名，如 App.OnStartup */
  location: string
  /** 文件名:行号 */
  source?: string
  /** 该帧的具体原因/消息 */
  reason: string
}

interface InnerException {
  type: string
  message: string
  stack?: string
}

interface CrashReport {
  /** 顶层异常类型 */
  type: string
  /** 顶层异常消息 */
  message: string
  /** 顶层异常堆栈（完整文本） */
  stack: string
  /** 故障点链：从最外层到最内层 */
  frames: CrashFrame[]
  /** 内部异常链 */
  inner?: InnerException[]
  /** 系统环境 */
  env: {
    os: string
    net: string
    x64: boolean
    cpu: number
    pid: number
    time: string
    version: string
    baseDir: string
  }
  /** 已生成的崩溃转储路径 */
  crashDumpPath?: string
  /** 强制死日志路径 */
  forceLogPath?: string
  /** 当前 Serilog 日志路径 */
  serilogLogPath?: string
}

export function CrashPage(): JSX.Element {
  const [report, setReport] = useState<CrashReport | null>(null)
  const [copied, setCopied] = useState(false)
  const [expandedFrames, setExpandedFrames] = useState<Set<number>>(new Set([0]))
  const ripple = useRipple()
  // 冲击波只播放一次：用 key 触发，挂载后置 false
  const [showShockwave, setShowShockwave] = useState(true)
  const shockTimerRef = useRef<number | null>(null)

  useEffect(() => {
    shockTimerRef.current = window.setTimeout(() => setShowShockwave(false), 950)
    return () => {
      if (shockTimerRef.current) window.clearTimeout(shockTimerRef.current)
    }
  }, [])

  useEffect(() => {
    function initBridge(): void {
      if (!window.chrome?.webview) return
      window.chrome.webview.addEventListener('message', (event) => {
        const data = event.data as { type?: string; action?: string; payload?: unknown }
        if (!data || data.type !== 'event') return
        if (data.action === 'crash:report') {
          const payload = data.payload as CrashReport
          if (payload) setReport(payload)
        }
      })
      // 通知 C# 已就绪
      if (window.__msmc_bridge__) {
        window.__msmc_bridge__.sendEvent('crash:ready', {})
      }
    }
    if (document.readyState === 'complete') initBridge()
    else window.addEventListener('load', initBridge, { once: true })
    setTimeout(initBridge, 100)
    setTimeout(initBridge, 500)
  }, [])

  const handleCopy = (): void => {
    if (!report) return
    const text = [
      '=== MSMC 灾难性故障报告 ===',
      `时间: ${report.env.time}`,
      `版本: ${report.env.version}`,
      `OS:   ${report.env.os}`,
      `.NET: ${report.env.net}  x64=${report.env.x64}  CPU=${report.env.cpu}  PID=${report.env.pid}`,
      '',
      '--- 顶层异常 ---',
      `Type:    ${report.type}`,
      `Message: ${report.message}`,
      '',
      '--- Stack ---',
      report.stack,
      '',
      '--- 故障点链 ---',
      ...report.frames.map((f, i) => `[${i}] ${f.location}${f.source ? ` (${f.source})` : ''}: ${f.reason}`),
      '',
      '--- 内部异常链 ---',
      ...(report.inner ?? []).map((e, i) => `[${i}] ${e.type}: ${e.message}${e.stack ? '\n' + e.stack : ''}`),
      '',
      '--- 日志文件 ---',
      `Serilog: ${report.serilogLogPath ?? '(未初始化)'}`,
      `ForceLog: ${report.forceLogPath ?? '(未生成)'}`,
      `CrashDump: ${report.crashDumpPath ?? '(未生成)'}`,
    ].join('\n')
    try {
      navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      /* 忽略 */
    }
  }

  const handleExit = (): void => {
    if (window.__msmc_bridge__) window.__msmc_bridge__.sendEvent('crash:exit', {})
  }

  const handleRestart = (): void => {
    if (window.__msmc_bridge__) window.__msmc_bridge__.sendEvent('crash:restart', {})
  }

  const toggleFrame = (idx: number): void => {
    setExpandedFrames((prev) => {
      const next = new Set(prev)
      if (next.has(idx)) next.delete(idx)
      else next.add(idx)
      return next
    })
  }

  if (!report) {
    return (
      <div
        className="md-page-enter"
        style={{
          width: '100%',
          height: '100%',
          overflow: 'auto',
          position: 'relative',
          backgroundColor: 'var(--md-paper)',
          fontFamily: 'var(--md-font-family)',
          color: 'var(--md-body)',
          boxSizing: 'border-box',
        }}
      >
        <CrashBackdrop />
        <div
          className="md-stagger-item"
          style={{
            position: 'relative',
            zIndex: 1,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            height: '100%',
            gap: 16,
            padding: 32,
          }}
        >
          <div style={{ position: 'relative', width: 56, height: 56 }}>
            <div
              className="md-orbit"
              style={{
                position: 'absolute',
                inset: 0,
                borderRadius: '50%',
                border: '2px solid transparent',
                borderTopColor: 'var(--md-gauge-red)',
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
                borderBottomColor: 'var(--md-accent-text)',
                borderLeftColor: 'var(--md-primary-hue-lighter)',
              }}
            />
          </div>
          <div style={{ textAlign: 'center', maxWidth: 420 }}>
            <div
              className="md-error-flicker"
              style={{ color: 'var(--md-error-text)', fontWeight: 700, fontSize: 16, marginBottom: 6 }}
            >
              等待故障报告数据
            </div>
            <div style={{ color: 'var(--md-body-light)', fontSize: 12, lineHeight: 1.7 }}>
              如果停留超过 3 秒，说明桥接通道也已损坏。
              <br />
              请直接查看日志：
              <code style={{ color: 'var(--md-aquamarine-light)', fontFamily: 'var(--md-font-mono)' }}>
                {' '}logs/crashes/crash-*.log
              </code>
            </div>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div
      className="md-page-enter"
      style={{
        width: '100%',
        height: '100%',
        overflow: 'auto',
        position: 'relative',
        backgroundColor: 'var(--md-paper)',
        fontFamily: 'var(--md-font-family)',
        color: 'var(--md-body)',
        padding: 24,
        boxSizing: 'border-box',
      }}
    >
      <CrashBackdrop />

      {/* ── 顶部标题区 —— 开场冲击震动 + 爆炸冲击波 ── */}
      <div
        className="md-crash-shake md-stagger-item"
        style={{
          '--md-stagger-i': 0,
          position: 'relative',
          zIndex: 1,
          display: 'flex',
          alignItems: 'center',
          gap: 16,
          marginBottom: 20,
          paddingBottom: 16,
          borderBottom: '1px solid var(--md-card-subtle-border)',
        } as React.CSSProperties}
      >
        <div
          className="md-alert-pulse md-alert-ring"
          style={{
            position: 'relative',
            width: 52,
            height: 52,
            borderRadius: 'var(--md-radius-large)',
            background: 'linear-gradient(135deg, var(--md-gauge-red) 0%, var(--md-danger) 100%)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
            boxShadow: '0 8px 20px -4px rgba(244, 54, 76, 0.55), 0 4px 10px -2px rgba(244, 54, 76, 0.35)',
          }}
        >
          <FaIcon kind="TriangleExclamationSolid" size={26} className="text-white" />
          {/* 爆炸冲击波：仅播放一次 */}
          {showShockwave && <span className="md-crash-shockwave" />}
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div
            className="md-error-flicker"
            style={{
              fontSize: 22,
              fontWeight: 700,
              background: 'linear-gradient(90deg, var(--md-gauge-red) 0%, var(--md-error-text) 100%)',
              WebkitBackgroundClip: 'text',
              WebkitTextFillColor: 'transparent',
              backgroundClip: 'text',
            }}
          >
            灾难性故障
          </div>
          <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginTop: 4, lineHeight: 1.5 }}>
            MSMC 启动或运行过程中发生了不可恢复的错误。下方是详细的故障定位信息。
          </div>
        </div>
        <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
          <button
            onMouseDown={ripple}
            onClick={handleCopy}
            className="md-btn md-btn-outlined md-press"
            style={{ minWidth: 92 }}
          >
            {copied ? '已复制 ✓' : '复制报告'}
          </button>
          <button
            onMouseDown={ripple}
            onClick={handleRestart}
            className="md-btn md-btn-primary md-press"
            style={{ minWidth: 92 }}
          >
            重启程序
          </button>
          <button
            onMouseDown={ripple}
            onClick={handleExit}
            className="md-btn md-btn-flat md-press"
            style={{ minWidth: 64 }}
          >
            退出
          </button>
        </div>
      </div>

      {/* ── 顶层异常卡片 ── */}
      <section className="md-stagger-item" style={{ '--md-stagger-i': 1, marginBottom: 16, position: 'relative', zIndex: 1 } as React.CSSProperties}>
        <SectionHeader icon="BugSolid" title="顶层异常" accent="var(--md-error-text)" />
        <div
          className="md-card"
          style={{
            borderLeft: '3px solid var(--md-error-text)',
            boxShadow: '0 6px 16px rgba(244, 54, 76, 0.18), var(--md-edge-hairline)',
            padding: 14,
          }}
        >
          <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr', gap: '6px 12px', fontSize: 12 }}>
            <KeyLabel>异常类型</KeyLabel>
            <MonoText color="var(--md-gauge-yellow)">{report.type}</MonoText>
            <KeyLabel>异常消息</KeyLabel>
            <MonoText color="var(--md-error-text)">{report.message}</MonoText>
          </div>
        </div>
      </section>

      {/* ── 故障点链 ── */}
      <section className="md-stagger-item" style={{ '--md-stagger-i': 2, marginBottom: 16, position: 'relative', zIndex: 1 } as React.CSSProperties}>
        <SectionHeader icon="ListOlSolid" title={`故障点链（${report.frames.length} 帧）`} accent="var(--md-primary-hue-lighter)" />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          {report.frames.map((f, i) => {
            const expanded = expandedFrames.has(i)
            const isRoot = i === report.frames.length - 1
            return (
              <div
                key={i}
                className="md-card"
                style={{
                  borderLeft: `3px solid ${isRoot ? 'var(--md-gauge-red)' : 'var(--md-subtle-border)'}`,
                  overflow: 'hidden',
                }}
              >
                <button
                  onClick={() => toggleFrame(i)}
                  style={{
                    width: '100%',
                    padding: '10px 14px',
                    backgroundColor: 'transparent',
                    border: 'none',
                    color: 'var(--md-body)',
                    textAlign: 'left',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: 10,
                    fontSize: 12,
                    fontFamily: 'var(--md-font-family)',
                    transition: 'background-color var(--md-duration-fast) var(--md-ease-standard)',
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.backgroundColor = 'var(--md-state-hover)'
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.backgroundColor = 'transparent'
                  }}
                >
                  <span style={{ color: 'var(--md-body-lighter)', fontFamily: 'var(--md-font-mono)', minWidth: 32 }}>
                    #{String(i).padStart(2, '0')}
                  </span>
                  <span
                    style={{
                      color: isRoot ? 'var(--md-error-text)' : 'var(--md-body-light)',
                      fontWeight: 600,
                      minWidth: 16,
                    }}
                  >
                    {isRoot ? '◉' : '○'}
                  </span>
                  <span style={{ flex: 1, fontFamily: 'var(--md-font-mono)', color: 'var(--md-body)' }}>
                    {f.location}
                  </span>
                  {f.source && (
                    <span style={{ color: 'var(--md-body-lighter)', fontSize: 11, fontFamily: 'var(--md-font-mono)' }}>
                      {f.source}
                    </span>
                  )}
                  <span
                    style={{
                      color: 'var(--md-body-lighter)',
                      transform: expanded ? 'rotate(0deg)' : 'rotate(-90deg)',
                      transition: 'transform var(--md-duration-normal) var(--md-ease-spring-soft)',
                      display: 'inline-block',
                    }}
                  >
                    ▲
                  </span>
                </button>
                {expanded && (
                  <div
                    className="md-fade-in"
                    style={{
                      padding: '8px 14px 12px 56px',
                      borderTop: '1px solid var(--md-card-subtle-border)',
                      fontSize: 12,
                      color: 'var(--md-body-light)',
                      fontFamily: 'var(--md-font-mono)',
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-word',
                    }}
                  >
                    <span style={{ color: 'var(--md-body-lighter)' }}>原因：</span>
                    <span style={{ color: 'var(--md-gauge-yellow)' }}>{f.reason}</span>
                  </div>
                )}
              </div>
            )
          })}
        </div>
      </section>

      {/* ── 完整堆栈 ── */}
      <section className="md-stagger-item" style={{ '--md-stagger-i': 3, marginBottom: 16, position: 'relative', zIndex: 1 } as React.CSSProperties}>
        <SectionHeader icon="CodeSolid" title="完整堆栈" accent="var(--md-aquamarine-light)" />
        <div
          className="md-card"
          style={{
            padding: 14,
            fontFamily: 'var(--md-font-mono)',
            fontSize: 11,
            lineHeight: 1.7,
            color: 'var(--md-body-light)',
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            maxHeight: 320,
            overflow: 'auto',
            backgroundColor: 'var(--md-deep-background)',
          }}
        >
          {report.stack || '(无堆栈)'}
        </div>
      </section>

      {/* ── 内部异常链 ── */}
      {report.inner && report.inner.length > 0 && (
        <section className="md-stagger-item" style={{ '--md-stagger-i': 4, marginBottom: 16, position: 'relative', zIndex: 1 } as React.CSSProperties}>
          <SectionHeader icon="LayerGroupSolid" title={`内部异常链（${report.inner.length} 层）`} accent="var(--md-gauge-yellow)" />
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {report.inner.map((e, i) => (
              <div
                key={i}
                className="md-card"
                style={{
                  borderLeft: '3px solid var(--md-gauge-yellow)',
                  padding: 12,
                  fontSize: 12,
                }}
              >
                <div style={{ marginBottom: 4 }}>
                  <span style={{ color: 'var(--md-body-lighter)', fontFamily: 'var(--md-font-mono)' }}>[{i}] </span>
                  <span style={{ color: 'var(--md-gauge-yellow)', fontFamily: 'var(--md-font-mono)', fontWeight: 600 }}>
                    {e.type}
                  </span>
                </div>
                <div style={{ color: 'var(--md-error-text)', marginBottom: 6 }}>{e.message}</div>
                {e.stack && (
                  <div
                    style={{
                      fontFamily: 'var(--md-font-mono)',
                      fontSize: 10,
                      color: 'var(--md-body-lighter)',
                      whiteSpace: 'pre-wrap',
                      wordBreak: 'break-word',
                      maxHeight: 120,
                      overflow: 'auto',
                      backgroundColor: 'var(--md-deep-background)',
                      padding: 8,
                      borderRadius: 'var(--md-radius-small)',
                    }}
                  >
                    {e.stack}
                  </div>
                )}
              </div>
            ))}
          </div>
        </section>
      )}

      {/* ── 系统环境 ── */}
      <section className="md-stagger-item" style={{ '--md-stagger-i': 5, marginBottom: 16, position: 'relative', zIndex: 1 } as React.CSSProperties}>
        <SectionHeader icon="MicrochipSolid" title="系统环境" accent="var(--md-gauge-green)" />
        <div
          className="md-card"
          style={{
            padding: 14,
            display: 'grid',
            gridTemplateColumns: 'repeat(2, 1fr)',
            gap: '6px 24px',
            fontSize: 12,
          }}
        >
          <EnvRow label="操作系统" value={report.env.os} />
          <EnvRow label=".NET 运行时" value={report.env.net} />
          <EnvRow label="进程位数" value={report.env.x64 ? 'x64' : 'x86'} />
          <EnvRow label="CPU 核心数" value={String(report.env.cpu)} />
          <EnvRow label="进程 ID" value={String(report.env.pid)} />
          <EnvRow label="故障时间" value={report.env.time} />
          <EnvRow label="程序版本" value={report.env.version} />
          <EnvRow label="工作目录" value={report.env.baseDir} />
        </div>
      </section>

      {/* ── 日志文件 ── */}
      <section className="md-stagger-item" style={{ '--md-stagger-i': 6, marginBottom: 16, position: 'relative', zIndex: 1 } as React.CSSProperties}>
        <SectionHeader icon="FileLinesSolid" title="日志与转储" accent="var(--md-body-light)" />
        <div className="md-card" style={{ padding: 14, fontSize: 12 }}>
          <LogRow label="Serilog 日志" path={report.serilogLogPath} />
          <LogRow label="强制死日志" path={report.forceLogPath} />
          <LogRow label="崩溃转储" path={report.crashDumpPath} />
        </div>
        <div style={{ marginTop: 8, fontSize: 11, color: 'var(--md-body-lighter)' }}>
          把上述文件打包发给开发者，可以加速问题定位。
        </div>
      </section>
    </div>
  )
}

/**
 * 崩溃页背景层：红色调粒子余烬 + 双辉光（红色主 + Aquamarine 辅）。
 * 与 AppLayout 视觉语言一致，但主色由蓝转红营造"爆炸"氛围。
 */
function CrashBackdrop(): JSX.Element {
  return (
    <>
      {/* 余烬粒子：红色调，缓慢飘散，模拟爆炸后的灰烬 */}
      <ParticleField
        density={0.4}
        color="var(--md-gauge-red)"
        connect
        connectDistance={120}
        speed={0.15}
        radiusRange={[0.5, 1.4]}
        maxOpacity={0.28}
        style={{ opacity: 0.55 }}
      />
      {/* 左上红色主辉光：爆炸源 */}
      <div
        aria-hidden
        style={{
          position: 'absolute',
          top: -100,
          left: '20%',
          transform: 'translateX(-50%)',
          width: '45%',
          height: 220,
          background:
            'radial-gradient(ellipse at center, rgba(244, 54, 76, 0.22) 0%, transparent 70%)',
          opacity: 0.7,
          pointerEvents: 'none',
        }}
      />
      {/* 右上 Aquamarine 辅色辉光：与主应用呼应的冷色调点缀 */}
      <div
        aria-hidden
        style={{
          position: 'absolute',
          top: -120,
          right: '5%',
          width: '38%',
          height: 240,
          background:
            'radial-gradient(ellipse at center, var(--md-aquamarine-soft) 0%, transparent 70%)',
          opacity: 0.3,
          pointerEvents: 'none',
          animation: 'mdBreathe 8s var(--md-ease-drift) infinite',
        }}
      />
    </>
  )
}

function SectionHeader(props: { icon: string; title: string; accent: string }): JSX.Element {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8 }}>
      <FaIcon kind={props.icon as any} size={14} style={{ color: props.accent }} />
      <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--md-body)' }}>{props.title}</span>
      <div style={{ flex: 1, height: 1, backgroundColor: 'var(--md-card-subtle-border)', marginLeft: 8 }} />
    </div>
  )
}

function KeyLabel(props: { children: React.ReactNode }): JSX.Element {
  return (
    <span style={{ color: 'var(--md-body-lighter)', fontSize: 11, alignSelf: 'center' }}>{props.children}</span>
  )
}

function MonoText(props: { children: React.ReactNode; color?: string }): JSX.Element {
  return (
    <span
      style={{
        fontFamily: 'var(--md-font-mono)',
        color: props.color ?? 'var(--md-body)',
        wordBreak: 'break-word',
      }}
    >
      {props.children}
    </span>
  )
}

function EnvRow(props: { label: string; value: string }): JSX.Element {
  return (
    <div style={{ display: 'flex', gap: 8 }}>
      <span style={{ color: 'var(--md-body-lighter)', minWidth: 80 }}>{props.label}</span>
      <span style={{ color: 'var(--md-body)', fontFamily: 'var(--md-font-mono)', wordBreak: 'break-word' }}>
        {props.value}
      </span>
    </div>
  )
}

function LogRow(props: { label: string; path?: string }): JSX.Element {
  return (
    <div
      style={{
        display: 'flex',
        gap: 8,
        padding: '4px 0',
        borderBottom: '1px dashed var(--md-card-subtle-border)',
      }}
    >
      <span style={{ color: 'var(--md-body-lighter)', minWidth: 96 }}>{props.label}</span>
      <span
        style={{
          color: props.path ? 'var(--md-body)' : 'var(--md-body-lighter)',
          fontFamily: 'var(--md-font-mono)',
          fontSize: 11,
          wordBreak: 'break-all',
          flex: 1,
          opacity: props.path ? 1 : 0.5,
        }}
      >
        {props.path || '(未生成)'}
      </span>
    </div>
  )
}
