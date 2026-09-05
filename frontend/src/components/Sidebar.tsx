import { useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
import { clsx } from 'clsx'
import {
  FaServer,
  FaSliders,
  FaChartLine,
  FaNetworkWired,
  FaGear,
  FaShield,
  FaChevronRight,
  FaBolt,
  FaMugHot,
  FaBell,
  FaClock,
  FaStore,
} from 'react-icons/fa6'

interface NavItem {
  path: string
  label: string
  icon: React.ReactNode
}

const navItems: NavItem[] = [
  { path: '/', label: '服务器管理', icon: <FaServer size={16} /> },
  { path: '/system', label: '系统监控', icon: <FaChartLine size={16} /> },
  { path: '/network', label: '网络监控', icon: <FaNetworkWired size={16} /> },
  { path: '/config', label: '配置编辑', icon: <FaSliders size={16} /> },
  { path: '/power', label: '电源管理', icon: <FaBolt size={16} /> },
  { path: '/java', label: 'Java 管理', icon: <FaMugHot size={16} /> },
  { path: '/notifications', label: '通知中心', icon: <FaBell size={16} /> },
  { path: '/scheduler', label: '计划任务', icon: <FaClock size={16} /> },
  { path: '/market', label: '插件市场', icon: <FaStore size={16} /> },
  { path: '/settings', label: '设置', icon: <FaGear size={16} /> },
]

export function Sidebar() {
  const [expanded, setExpanded] = useState(false)
  const location = useLocation()

  // ── [FE-DIAG] Sidebar 首次挂载时把自身状态/样式/数据全量打印
  //    用于定位"侧边栏消失但 hover 有交互"的问题（症状 B/C）
  //    如果 CSS 变量没加载 → width 会算成 auto / 空 / initial
  //    如果图标没加载 → navItems[i].icon.type 会是 undefined 而非 function
  useState(() => {
    try {
      const cs = getComputedStyle(document.documentElement)
      const cssVars = {
        md_card_background: cs.getPropertyValue('--md-card-background').trim() || '(EMPTY!)',
        md_body: cs.getPropertyValue('--md-body').trim() || '(EMPTY!)',
        md_nav_item_selected: cs.getPropertyValue('--md-nav-item-selected').trim() || '(EMPTY!)',
        sidebar_width_expanded: cs.getPropertyValue('--sidebar-width-expanded').trim() || '(EMPTY!)',
        sidebar_width_collapsed: cs.getPropertyValue('--sidebar-width-collapsed').trim() || '(EMPTY!)',
      }
      // 检查图标组件是否是有效组件（undefined 说明 react-icons chunk 没加载/tree-shake 了）
      const iconTypes = navItems.map((it, i) => {
        const node = it.icon as any
        const typeStr = node == null
          ? 'NULL!'
          : typeof node.type === 'function'
            ? `Fn(${node.type.name || 'anon'})`
            : String(node.type)
        return `[${i}] ${typeStr}`
      }).join(', ')
      const msg =
        `[FE-DIAG] Sidebar首次挂载 | expanded=${expanded} | navItems.length=${navItems.length} | location=${location.pathname} | ` +
        `CSS vars: ${JSON.stringify(cssVars)} | nav图标: ${iconTypes}`
      console.log(msg)
      // 上报 C# 日志（注意：此时桥接可能还没初始化，失败时静默忽略）
      const bridge = (window as any).__msmc_bridge__
      if (bridge && typeof bridge.invoke === 'function') {
        bridge.invoke('log:write', {
          level: 'Information', message: msg, stack: '', url: window.location.href, ua: navigator.userAgent,
        }).catch(() => {})
      }
    } catch (e: any) {
      console.warn('[FE-DIAG] Sidebar diag 失败:', e?.message || e)
    }
  })

  return (
    <aside
      className={clsx(
        'h-full flex flex-col bg-[var(--md-card-background)] border-r border-[var(--md-card-subtle-border)] relative',
        'md-sidebar-transition',
        !expanded && 'md-sidebar-collapsed'
      )}
      style={{
        width: expanded ? 'var(--sidebar-width-expanded)' : 'var(--sidebar-width-collapsed)',
      }}
      onMouseEnter={() => setExpanded(true)}
      onMouseLeave={() => setExpanded(false)}
    >
      {/* 顶部品牌区 —— ColorOS 风格呼吸光晕 */}
      <div
        className="p-3 md-stagger-item w-full"
        style={{ '--md-stagger-i': 0 } as React.CSSProperties}
      >
        <div
          className={clsx('flex items-center gap-3', expanded ? '' : 'justify-center')}
        >
          <div
            className="w-8 h-8 flex items-center justify-center flex-shrink-0 rounded-md md-brand-pulse relative"
            style={{ backgroundColor: 'var(--md-primary-subtle-background)' }}
          >
            <FaShield size={16} style={{ color: 'var(--md-nav-item-selected)' }} />
            {/* Aquamarine 辅色光晕：ColorOS AOD 流动配色点缀 */}
            <div
              aria-hidden
              className="absolute inset-0 rounded-md"
              style={{
                boxShadow: '0 0 12px 1px var(--md-aquamarine-soft)',
                pointerEvents: 'none',
              }}
            />
          </div>
          <div
            className={clsx(
              'flex-1 min-w-0 md-sidebar-text-transition overflow-hidden',
              expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
            )}
          >
            <div className="text-sm font-bold text-[var(--md-body)] whitespace-nowrap">
              MSMC
            </div>
            <div
              className="text-[10px] whitespace-nowrap"
              style={{ color: 'var(--md-body-light)', opacity: 0.7 }}
            >
              v0.1.0
            </div>
          </div>
        </div>
      </div>

      <div className="px-3">
        <div className="h-px bg-[var(--md-subtle-border)] opacity-30" />
      </div>

      {/* 导航列表 —— 交错入场（ColorOS 公式） */}
      <nav className="flex-1 px-1 py-2 overflow-y-auto w-full">
        <div className="space-y-0 w-full">
          {navItems.map((item, index) => {
            const isActive =
              item.path === '/'
                ? location.pathname === '/'
                : location.pathname.startsWith(item.path)

            return (
              <NavLink
                key={item.path}
                to={item.path}
                end={item.path === '/'}
                className={clsx(
                  'md-nav-item md-stagger-item w-full',
                  isActive && 'md-nav-item-active'
                )}
                title={item.label}
                style={{
                  // 交错入场延迟（ColorOS 公式由 CSS 计算，这里只传 index）
                  '--md-stagger-i': index + 1,
                  // 展开态左对齐；折叠态水平居中 + 左右对称 padding 保证图标磁吸居中
                  justifyContent: expanded ? 'flex-start' : 'center',
                  paddingLeft: expanded ? undefined : 0,
                  paddingRight: expanded ? undefined : 0,
                  marginLeft: expanded ? undefined : 2,
                  marginRight: expanded ? undefined : 2,
                } as React.CSSProperties}
              >
                <span
                  className={clsx('flex-shrink-0 md-nav-icon', isActive && 'md-nav-icon-active')}
                >
                  {item.icon}
                </span>
                <span
                  className={clsx(
                    'whitespace-nowrap md-sidebar-text-transition overflow-hidden',
                    expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
                  )}
                >
                  {item.label}
                </span>
                {expanded && isActive && (
                  <FaChevronRight
                    size={10}
                    className="ml-auto md-nav-chevron"
                    // ColorOS 辅色：激活态 chevron 用 Aquamarine 点缀
                    style={{ color: 'var(--md-aquamarine-light)' }}
                  />
                )}
              </NavLink>
            )
          })}
        </div>
      </nav>

      {/* 底部信息卡 —— Aquamarine 微光描边 */}
      <div className="p-3 border-t border-[var(--md-card-subtle-border)] w-full">
        <div
          className={clsx(
            'flex items-center gap-2 px-2 py-2 rounded-md md-sidebar-footer w-full',
            expanded ? '' : 'justify-center px-0'
          )}
          style={{
            backgroundColor: 'var(--md-primary-subtle-background)',
            boxShadow: 'inset 0 0 0 1px var(--md-aquamarine-soft)',
            // 折叠态：去除左右内边距，确保真正居中
            paddingLeft: expanded ? undefined : 0,
            paddingRight: expanded ? undefined : 0,
          }}
        >
          <FaShield
            size={14}
            className="flex-shrink-0 md-breathe"
            style={{ color: 'var(--md-nav-item-selected)' }}
          />
          <div
            className={clsx(
              'md-sidebar-text-transition overflow-hidden',
              expanded ? 'opacity-100 w-auto' : 'opacity-0 w-0'
            )}
          >
            <div
              className="text-[11px] font-medium whitespace-nowrap"
              style={{ color: 'var(--md-body)' }}
            >
              MSMC
            </div>
            <div
              className="text-[10px] whitespace-nowrap"
              style={{ color: 'var(--md-body-light)', opacity: 0.6 }}
            >
              服务器管理控制台
            </div>
          </div>
        </div>
      </div>
    </aside>
  )
}
