import { Component, type ReactNode } from 'react'

interface Props {
  children: ReactNode
  pageName: string
}

interface State {
  hasError: boolean
  error: Error | null
  retryCount: number
}

/**
 * Lazy 页面加载错误边界
 *
 * 当 lazy import 失败时（chunk 文件 404、CORS 拒绝、parse 失败等），
 * React 默认会把错误冒泡到根，导致整个 App 白屏。
 * 这个边界捕获错误后显示具体原因 + 重试按钮，避免"页面空白看不到任何信息"。
 */
export class LazyPageErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props)
    this.state = { hasError: false, error: null, retryCount: 0 }
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error, retryCount: 0 }
  }

  componentDidCatch(error: Error, info: { componentStack: string }): void {
    console.error(`[LazyPageError] ${this.props.pageName} 加载失败:`, error, info)
    const bridge = (window as any).__msmc_bridge__
    if (bridge && typeof bridge.invoke === 'function') {
      bridge.invoke('log:write', {
        level: 'Error',
        message: `[LazyPageError] ${this.props.pageName} 加载失败: ${error.message}`,
        stack: error.stack || '',
        url: window.location.href,
        ua: navigator.userAgent,
      }).catch(() => {})
    }
  }

  handleRetry = (): void => {
    this.setState((prev) => ({ hasError: false, error: null, retryCount: prev.retryCount + 1 }))
  }

  render(): ReactNode {
    if (this.state.hasError) {
      const err = this.state.error
      const isChunkLoadError =
        err?.name === 'ChunkLoadError' ||
        err?.message?.includes('Loading chunk') ||
        err?.message?.includes('Failed to fetch dynamically imported module')

      return (
        <div
          className="h-full flex items-center justify-center p-8"
          style={{ backgroundColor: 'var(--md-deep-background)', color: 'var(--md-body)' }}
        >
          <div style={{ maxWidth: 480, textAlign: 'center' }}>
            <div
              style={{
                fontSize: 14,
                fontWeight: 600,
                color: 'var(--md-danger)',
                marginBottom: 12,
              }}
            >
              页面加载失败：{this.props.pageName}
            </div>
            <div
              style={{
                fontSize: 12,
                color: 'var(--md-body-light)',
                marginBottom: 16,
                lineHeight: 1.6,
              }}
            >
              {isChunkLoadError
                ? '页面资源（JS chunk）加载失败。可能原因：资源文件被杀毒软件拦截、WebView2 缓存损坏、或打包产物不完整。'
                : '页面渲染时发生异常。'}
            </div>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-lighter)',
                background: 'var(--md-card-hover)',
                padding: '8px 12px',
                borderRadius: 6,
                marginBottom: 16,
                textAlign: 'left',
                fontFamily: 'monospace',
                wordBreak: 'break-all',
                maxHeight: 120,
                overflow: 'auto',
              }}
            >
              {err?.name}: {err?.message}
            </div>
            <button
              onClick={this.handleRetry}
              style={{
                padding: '8px 20px',
                fontSize: 12,
                borderRadius: 6,
                border: '1px solid var(--md-primary-hue-mid)',
                background: 'var(--md-primary-subtle-background)',
                color: 'var(--md-primary-hue-light)',
                cursor: 'pointer',
              }}
            >
              重试加载（第 {this.state.retryCount + 1} 次）
            </button>
          </div>
        </div>
      )
    }

    return this.props.children
  }
}
