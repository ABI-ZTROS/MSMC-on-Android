// -----------------------------------------------------------------------------
// 文件名: IconRegistry.tsx
// 功能描述: 统一图标注册表 —— 将 WPF 端 MahApps.Metro.IconPacks.FontAwesome6
//           的 Kind 名称映射到前端 react-icons 组件，实现两端图标资源统一
// 设计模式: 注册表模式 + 工厂模式
// -----------------------------------------------------------------------------

import type { IconType } from 'react-icons'
import {
  // Solid 图标
  FaArrowsRotate,
  FaXmark,
  FaPlus,
  FaTrash,
  FaTrashCan,
  FaTriangleExclamation,
  FaFolderOpen,
  FaFolderPlus,
  FaPen,
  FaRotateLeft,
  FaRotate,
  FaFloppyDisk,
  FaFolder,
  FaFileLines,
  FaLightbulb,
  FaShield,
  FaShieldHalved,
  FaWindowMinimize,
  FaGaugeHigh,
  FaGauge,
  FaTerminal,
  FaCopy,
  FaServer,
  FaPlay,
  FaStop,
  FaDatabase,
  FaSliders,
  FaRocket,
  FaBolt,
  FaGear,
  FaBell,
  FaMugHot,
  FaStar,
  FaHeart,
  FaCross,
  FaCheck,
  FaPowerOff,
  FaCircleExclamation,
  FaUser,
  FaChevronRight,
  FaNetworkWired,
  FaChartLine,
  FaBug,
  FaListOl,
  FaCode,
  FaLayerGroup,
  FaMicrochip,
  // Regular 图标
  FaRegFolderOpen,
  // Brands 图标
  FaGithub,
} from 'react-icons/fa6'
import { FiCheck, FiX, FiAlertTriangle, FiInfo } from 'react-icons/fi'

/**
 * WPF PackIconFontAwesome6 Kind → react-icons 组件映射表
 *
 * 命名规则：与 MahApps.Metro.IconPacks.FontAwesome6 的 Kind 枚举值完全一致
 * 使用方式：<FaIcon kind="ShieldSolid" size={16} />
 */
const iconMap: Record<string, IconType> = {
  // ─── Solid 图标 ───
  ArrowsRotateSolid: FaArrowsRotate,
  XmarkSolid: FaXmark,
  PlusSolid: FaPlus,
  TrashSolid: FaTrash,
  TrashCanSolid: FaTrashCan,
  TriangleExclamationSolid: FaTriangleExclamation,
  FolderOpenSolid: FaFolderOpen,
  FolderPlusSolid: FaFolderPlus,
  PenSolid: FaPen,
  RotateLeftSolid: FaRotateLeft,
  RotateSolid: FaRotate,
  FloppyDiskSolid: FaFloppyDisk,
  FolderSolid: FaFolder,
  FileLinesSolid: FaFileLines,
  LightbulbSolid: FaLightbulb,
  ShieldSolid: FaShield,
  ShieldHalvedSolid: FaShieldHalved,
  WindowMinimizeSolid: FaWindowMinimize,
  GaugeHighSolid: FaGaugeHigh,
  GaugeSolid: FaGauge,
  TerminalSolid: FaTerminal,
  CopySolid: FaCopy,
  ServerSolid: FaServer,
  PlaySolid: FaPlay,
  StopSolid: FaStop,
  DatabaseSolid: FaDatabase,
  SlidersSolid: FaSliders,
  RocketSolid: FaRocket,
  BoltSolid: FaBolt,
  GearSolid: FaGear,
  BellSolid: FaBell,
  MugHotSolid: FaMugHot,
  StarSolid: FaStar,
  HeartSolid: FaHeart,
  CrossSolid: FaCross,
  CheckSolid: FaCheck,
  PowerOffSolid: FaPowerOff,
  CircleExclamationSolid: FaCircleExclamation,
  UserSolid: FaUser,
  ChevronRightSolid: FaChevronRight,
  NetworkWiredSolid: FaNetworkWired,
  ChartLineSolid: FaChartLine,
  BugSolid: FaBug,
  ListOlSolid: FaListOl,
  CodeSolid: FaCode,
  LayerGroupSolid: FaLayerGroup,
  MicrochipSolid: FaMicrochip,

  // ─── Regular 图标 ───
  FolderOpenRegular: FaRegFolderOpen,

  // ─── Brands 图标 ───
  GithubBrands: FaGithub,

  // ─── Feather Icons（Toast 专用） ───
  FiCheck: FiCheck,
  FiX: FiX,
  FiAlertTriangle: FiAlertTriangle,
  FiInfo: FiInfo,
}

export interface FaIconProps {
  /** WPF PackIconFontAwesome6 的 Kind 名称 */
  kind: string
  /** 图标尺寸（像素），默认 16 */
  size?: number
  /** 额外 className */
  className?: string
  /** 额外 style */
  style?: React.CSSProperties
}

/**
 * 统一图标组件 —— 通过 WPF Kind 名称渲染对应的 react-icons 图标
 *
 * @example
 * <FaIcon kind="ShieldSolid" size={16} />
 * <FaIcon kind="ArrowsRotateSolid" size={14} className="md-spin" />
 */
export function FaIcon({ kind, size = 16, className, style }: FaIconProps): React.ReactNode {
  const Icon = iconMap[kind]
  if (!Icon) {
    if (import.meta.env.DEV) {
      console.warn(`[IconRegistry] 未知图标 Kind: "${kind}"，请补充映射`)
    }
    return null
  }
  return <Icon size={size} className={className} style={style} />
}

/**
 * 获取图标组件（用于需要直接引用组件的场景）
 *
 * @example
 * const ShieldIcon = getIcon('ShieldSolid')
 * {ShieldIcon && <ShieldIcon size={16} />}
 */
export function getIcon(kind: string): IconType | undefined {
  return iconMap[kind]
}

/**
 * 检查图标 Kind 是否已注册
 */
export function hasIcon(kind: string): boolean {
  return kind in iconMap
}
