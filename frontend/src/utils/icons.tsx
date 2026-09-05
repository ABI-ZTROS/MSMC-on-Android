// -----------------------------------------------------------------------------
// 文件名: icons.ts
// 命名空间: src.utils
// 功能描述: 前后端约定图标字典（方案A：后端传 iconName 字符串，前端按名渲染）
//           所有用于页面 UI 的图标统一通过这里暴露，避免 emoji 出现在界面上，
//           同时让前端图标风格一致（全部 Font Awesome 6 Pro/Free Solid）。
// 依赖组件: react-icons/fa6
// 设计模式: 注册表模式（Registry）
// -----------------------------------------------------------------------------
import type { ComponentType } from 'react'
import type { IconBaseProps } from 'react-icons'
import {
  FaServer,
  FaGaugeHigh,
  FaFilePen,
  FaMicrochip,
  FaNetworkWired,
  FaGear,
  FaFolder,
  FaFolderOpen,
  FaCirclePlus,
  FaTrashCan,
  FaMagnifyingGlass,
  FaXmark,
  FaRotate,
  FaStar,
  FaCalendarDays,
  FaChartLine,
  FaMugHot,
  FaTriangleExclamation,
  FaTerminal,
  FaBook,
  FaDesktop,
  FaBolt,
  FaMemory,
  FaHardDrive,
  FaEthernet,
  FaArrowTrendUp,
  FaCircleInfo,
  FaShieldHalved,
  FaPlay,
  FaPause,
  FaStop,
  FaCopy,
  FaGamepad,
  FaFolderTree,
  FaUser,
  FaLink,
  FaSliders,
  FaFloppyDisk,
  FaPenToSquare,
  FaCheck,
} from 'react-icons/fa6'

/**
 * 前后端约定图标字典
 * 后端传 iconName 字符串时，必须命中此表的 key（或走 isKnownIconName 校验）
 */
export const ICON_MAP: Record<string, ComponentType<IconBaseProps>> = {
  // ===== 侧边栏 / 顶层 Tabs =====
  dashboard:  FaGaugeHigh,
  server:     FaServer,
  config:     FaFilePen,
  monitor:    FaMicrochip,
  network:    FaNetworkWired,
  settings:   FaGear,
  gear:       FaGear,

  // ===== Dashboard / 首页组件 =====
  desktop:    FaDesktop,
  library:    FaBook,
  folder:     FaFolder,
  folderOpen: FaFolderOpen,
  add:        FaCirclePlus,
  trash:      FaTrashCan,
  search:     FaMagnifyingGlass,
  close:      FaXmark,
  refresh:    FaRotate,
  rotate:     FaRotate,
  star:       FaStar,
  terminal:   FaTerminal,
  info:       FaCircleInfo,
  save:       FaFloppyDisk,
  floppy:     FaFloppyDisk,
  sliders:    FaSliders,
  check:      FaCheck,
  edit:       FaPenToSquare,

  // ===== 播放控制（启动/停止/暂停） =====
  play:       FaPlay,
  pause:      FaPause,
  stop:       FaStop,

  // ===== 通用操作 =====
  copy:       FaCopy,
  gamepad:    FaGamepad,
  folderTree: FaFolderTree,
  user:       FaUser,

  // ===== 配置编辑器 =====
  calendar:   FaCalendarDays,
  chart:      FaChartLine,
  java:       FaMugHot,
  warning:    FaTriangleExclamation,

  // ===== 系统监控页 =====
  bolt:       FaBolt,
  cpu:        FaMicrochip,
  memory:     FaMemory,
  disk:       FaHardDrive,
  net:        FaEthernet,
  trend:      FaArrowTrendUp,

  // ===== 通用 =====
  security:   FaShieldHalved,
  link:       FaLink,
}

/** 类型守卫：判断 iconName 是否属于已知字典（后端传过来的字符串先过这道校验） */
export function isKnownIconName(
  name?: string | null,
): name is keyof typeof ICON_MAP {
  return !!name && Object.prototype.hasOwnProperty.call(ICON_MAP, name)
}

/** 缺失图标兜底：不要白屏，用 warning 三角形代替（方便一眼发现「约定名拼错了」） */
export const ICON_FALLBACK: ComponentType<IconBaseProps> = FaTriangleExclamation

export interface IconByNameProps extends Omit<IconBaseProps, 'name'> {
  name?: string | null
  /** 当 name 未知或为空时是否显示兜底图标（默认 true，设 false 则直接不渲染） */
  fallbackOnUnknown?: boolean
}

/**
 * 按约定名字渲染图标的统一入口
 * 用法：<IconByName name="server" size={18} className="mr-2" />
 */
export function IconByName({
  name,
  fallbackOnUnknown = true,
  ...rest
}: IconByNameProps) {
  if (!isKnownIconName(name)) {
    if (!fallbackOnUnknown) return null
    const Fallback = ICON_FALLBACK
    return <Fallback aria-hidden {...rest} />
  }
  const Comp = ICON_MAP[name]
  return <Comp aria-hidden {...rest} />
}
