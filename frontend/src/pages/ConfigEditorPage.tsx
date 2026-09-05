import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate, useBlocker } from 'react-router-dom'
import {
  FaFolderOpen,
  FaFolder,
  FaFileLines,
  FaArrowsRotate,
  FaPen,
  FaRotateLeft,
  FaRotateRight,
  FaRotate,
  FaFloppyDisk,
  FaLightbulb,
  FaChevronRight,
  FaCheck,
  FaCircleExclamation,
  FaPowerOff,
  FaTriangleExclamation,
  FaMagnifyingGlass,
  FaXmark,
} from 'react-icons/fa6'
import {
  getConfigFileTree,
  selectConfigFile,
  getConfigEntries,
  updateConfigValue,
  saveConfig,
  resetConfig,
  undoConfig,
  redoConfig,
  rescanConfigFiles,
  // 手动定位 JAR
  selectJarManually,
  getBridge,
} from '@/utils/bridge'
import { Reveal } from '@/components/ui/Reveal'
import { useToastStore } from '@/stores/toastStore'
import type {
  ConfigFileItem,
  ConfigFileTreeResponse,
  ConfigEntry,
  ConfigEntryGroup,
  ConfigEntriesResponse,
} from '@/types/bridge'

// ─────────────────────────────────────────────────────────────────────
// 配置文件树节点（递归渲染，支持目录展开/折叠）
// ─────────────────────────────────────────────────────────────────────
interface ConfigTreeItemProps {
  node: ConfigFileItem
  depth: number
  selectedFile: string | null
  expandedDirs: Set<string>
  onSelectFile: (path: string) => void
  onToggleDir: (path: string) => void
}

function ConfigTreeItem({
  node,
  depth,
  selectedFile,
  expandedDirs,
  onSelectFile,
  onToggleDir,
}: ConfigTreeItemProps): JSX.Element {
  const isExpanded = expandedDirs.has(node.relativePath)
  const isSelected = selectedFile === node.relativePath
  const indent = 8 + depth * 12

  if (node.isDirectory) {
    return (
      <div className="md-tree-item">
        <div
          className="md-tree-item-header"
          style={{ paddingLeft: indent }}
          onClick={() => onToggleDir(node.relativePath)}
        >
          <FaChevronRight
            size={10}
            style={{
              color: 'var(--md-body-light)',
              transition: 'transform var(--md-duration-normal) var(--md-ease-standard)',
              transform: isExpanded ? 'rotate(90deg)' : 'none',
            }}
          />
          <FaFolder size={14} style={{ color: 'var(--md-primary-hue-mid)' }} />
          <span
            className="truncate"
            style={{
              fontSize: 'var(--md-font-size-base)',
              color: 'var(--md-body)',
            }}
          >
            {node.fileName}
          </span>
        </div>
        {isExpanded && node.children.length > 0 && (
          <div className="md-tree-item-children">
            {node.children.map((child) => (
              <ConfigTreeItem
                key={child.relativePath}
                node={child}
                depth={depth + 1}
                selectedFile={selectedFile}
                expandedDirs={expandedDirs}
                onSelectFile={onSelectFile}
                onToggleDir={onToggleDir}
              />
            ))}
          </div>
        )}
      </div>
    )
  }

  return (
    <div
      className={`md-tree-item-header ${isSelected ? 'md-tree-item-selected' : ''}`}
      style={{ paddingLeft: indent + 22 }}
      onClick={() => onSelectFile(node.relativePath)}
      title={node.relativePath}
    >
      <FaFileLines
        size={14}
        style={{ color: isSelected ? 'var(--md-primary-hue-mid)' : 'var(--md-body-light)' }}
      />
      <span
        className="truncate"
        style={{
          fontSize: 'var(--md-font-size-base)',
          color: 'var(--md-body)',
        }}
      >
        {node.fileName}
      </span>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────
// 配置项编辑控件（根据类型动态选择）
// ─────────────────────────────────────────────────────────────────────
interface ConfigEntryEditorProps {
  entry: ConfigEntry
  displayValue: string
  onChange: (value: string) => void
}

function ConfigEntryEditor({
  entry,
  displayValue,
  onChange,
}: ConfigEntryEditorProps): JSX.Element {
  const controlStyle: React.CSSProperties = {
    width: 200,
    maxWidth: 400,
    height: 36,
  }

  if (entry.isBoolType) {
    return (
      <label className="md-toggle">
        <input
          type="checkbox"
          checked={displayValue === 'true'}
          onChange={(e) => onChange(e.target.checked ? 'true' : 'false')}
        />
        <span className="md-toggle-slider" />
      </label>
    )
  }

  if (entry.isEnumType) {
    return (
      <select
        className="md-select"
        style={controlStyle}
        value={displayValue}
        onChange={(e) => onChange(e.target.value)}
      >
        {(entry.allowedValues ?? []).map((v) => (
          <option key={v} value={v}>
            {v}
          </option>
        ))}
      </select>
    )
  }

  if (entry.isNumericType) {
    const rangeTip =
      entry.minValue != null && entry.maxValue != null
        ? `默认值: ${entry.originalValue}\n范围: ${entry.minValue} - ${entry.maxValue}`
        : `默认值: ${entry.originalValue}`
    return (
      <input
        type="number"
        className="md-input"
        style={controlStyle}
        value={displayValue}
        min={entry.minValue ?? undefined}
        max={entry.maxValue ?? undefined}
        onChange={(e) => onChange(e.target.value)}
        title={rangeTip}
        placeholder="输入数值"
      />
    )
  }

  return (
    <input
      type="text"
      className="md-input"
      style={controlStyle}
      value={displayValue}
      onChange={(e) => onChange(e.target.value)}
      placeholder="输入文本"
    />
  )
}

// ─────────────────────────────────────────────────────────────────────
// 配置编辑页主组件
// ─────────────────────────────────────────────────────────────────────
export function ConfigEditorPage(): JSX.Element {
  const navigate = useNavigate()
  const showToast = useToastStore((s) => s.showToast)
  // ── 标记组件是否已挂载（防卸载后 setState） ──
  const mountedRef = useRef(true)
  useEffect(() => {
    return () => {
      mountedRef.current = false
    }
  }, [])
  // 用 mountedRef 包一层 setState，避免卸载后调用触发 React 警告
  const safeSet = useCallback(<S,>(setter: React.Dispatch<React.SetStateAction<S>>, value: S | ((prev: S) => S)): void => {
    if (mountedRef.current) setter(value as never)
  }, [])

  // 快速选择已移除：保留 selectedServerName 用于显示当前选中状态（手动定位 JAR 后从后端同步）
  const [, setSelectedServerName] = useState<string | null>(null)
  // 手动定位的 JAR 路径，显示在按钮下方供用户确认
  const [selectedJarPath, setSelectedJarPath] = useState<string | null>(null)
  const [serverWorkingDirectory, setServerWorkingDirectory] = useState('')
  const [configFileTree, setConfigFileTree] = useState<ConfigFileItem[]>([])
  const [configFileCountText, setConfigFileCountText] = useState('')
  const [hasServerDirectory, setHasServerDirectory] = useState(false)

  const [selectedConfigFile, setSelectedConfigFile] = useState<string | null>(null)
  const [selectedConfigFileName, setSelectedConfigFileName] = useState<string | null>(null)

  const [configGroups, setConfigGroups] = useState<ConfigEntryGroup[]>([])
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false)
  const [saveStatusMessage, setSaveStatusMessage] = useState<string | null>(null)
  const [isSaveError, setIsSaveError] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [loadProgress, setLoadProgress] = useState(0)
  const [isFetchingEntries, setIsFetchingEntries] = useState(false)
  const [isServerRunning, setIsServerRunning] = useState(false)
  // Bug 修复：Ctrl+S 闭包在 useEffect([]) 中绑定，isServerRunning 闭包会过期。
  // 用 ref 追踪最新值，让 Ctrl+S 能正确判断是否应拦截保存。
  const isServerRunningRef = useRef(false)
  useEffect(() => {
    isServerRunningRef.current = isServerRunning
  }, [isServerRunning])
  const [modifiedCount, setModifiedCount] = useState(0)

  const [expandedDirs, setExpandedDirs] = useState<Set<string>>(new Set())
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set())
  const [pendingValues, setPendingValues] = useState<Record<string, string>>({})

  // 配置文件扁平列表（预留补偿用，未使用时避免 lint 报错）
  const [, setConfigFilesState] = useState<string[]>([])

  const [showSaveErrorModal, setShowSaveErrorModal] = useState(false)
  const [saveErrorInfo, setSaveErrorInfo] = useState<{ type: string; detail: string } | null>(null)
  const [showRestartConfirm, setShowRestartConfirm] = useState(false)

  // ── 配置项查找功能：实时过滤显示匹配的配置项 ──
  const [searchQuery, setSearchQuery] = useState('')
  const [isSearchFocused, setIsSearchFocused] = useState(false)
  const searchInputRef = useRef<HTMLInputElement>(null)

  // 当搜索关键词改变时，自动展开所有分组以显示匹配项
  useEffect(() => {
    if (!searchQuery.trim()) return
    // 搜索时展开所有分组，让匹配项可见
    setExpandedGroups((prev) => {
      const next = new Set(prev)
      for (const g of configGroups) {
        if (g.key !== '__ERROR__') next.add(g.key)
      }
      return next
    })
  }, [searchQuery, configGroups])

  // 过滤后的配置组：按搜索关键词匹配 key/displayName/description
  const filteredConfigGroups = useMemo(() => {
    if (!searchQuery.trim()) return configGroups
    const q = searchQuery.trim().toLowerCase()
    return configGroups
      .map((g) => ({
        ...g,
        items: g.items.filter((e) => {
          if (e.key === '__ERROR__') return true // 错误条目始终显示
          return (
            e.key.toLowerCase().includes(q) ||
            (e.displayName || '').toLowerCase().includes(q) ||
            (e.friendlyDisplayName || '').toLowerCase().includes(q) ||
            (e.description || '').toLowerCase().includes(q)
          )
        }),
      }))
      .filter((g) => g.items.length > 0)
  }, [configGroups, searchQuery])

  // 统计匹配数量
  const searchMatchCount = useMemo(() => {
    if (!searchQuery.trim()) return 0
    return filteredConfigGroups.reduce((sum, g) => sum + g.items.length, 0)
  }, [filteredConfigGroups, searchQuery])

  // Ctrl+F 快捷键聚焦搜索框
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent): void => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
        const active = document.activeElement
        // 如果焦点已经在输入框中，不拦截
        if (active && active.tagName === 'INPUT') return
        e.preventDefault()
        searchInputRef.current?.focus()
        searchInputRef.current?.select()
      }
      if (e.key === 'Escape' && isSearchFocused) {
        setSearchQuery('')
        searchInputRef.current?.blur()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [isSearchFocused])

  // ── 修复：loadFileTree 拆成"获取数据"和"可选地同步 selectedServerName"两步，
  //    后端返回的 selectedServerName 只作为兜底，绝不覆盖本地已设值（除非本地是空）。
  const fetchFileTree = useCallback(async (): Promise<ConfigFileTreeResponse> => {
    const resp = await getConfigFileTree()
    return resp
  }, [])

  const loadFileTree = useCallback(async (): Promise<void> => {
    try {
      const resp: ConfigFileTreeResponse = await fetchFileTree()
      if (!mountedRef.current) return
      safeSet(setConfigFileTree, resp.tree)
      safeSet(setConfigFileCountText, resp.configFileCountText)
      safeSet(setServerWorkingDirectory, resp.serverWorkingDirectory)
      safeSet(setHasServerDirectory, resp.hasServerDirectory)
      // ── 关键：不覆盖本地已有的 selectedServerName ──
      setSelectedServerName((prev) => {
        if (prev != null) return prev
        selectedServerNameRef.current = resp.selectedServerName ?? null
        return resp.selectedServerName ?? null
      })
    } catch (e) {
      console.error('获取配置文件树失败:', e)
    }
  }, [fetchFileTree, safeSet])

  const loadEntries = useCallback(async (): Promise<ConfigEntriesResponse | null> => {
    setIsFetchingEntries(true)
    try {
      let resp = await getConfigEntries()

      // ── 后端 LoadConfigAsync 是 fire-and-forget，config:selectFile 立即返回不等待。
      let attempts = 0
      while (resp.isLoading && attempts < 50) {
        if (!mountedRef.current) return null
        setIsLoading(true)
        setLoadProgress(resp.loadProgress)
        await new Promise((resolve) => setTimeout(resolve, 200))
        if (!mountedRef.current) return null
        resp = await getConfigEntries()
        attempts++
      }

      if (!mountedRef.current) return null

      // Bug12: 若 50 次轮询后仍 isLoading=true → 提示加载超时
      if (resp.isLoading) {
        showToast('配置加载超时，请稍后重试', 'error')
      }

      // ── 加载完成后（或超时），一次性更新所有状态
      safeSet(setConfigGroups, resp.groups)
      safeSet(setHasUnsavedChanges, resp.hasUnsavedChanges)
      safeSet(setSaveStatusMessage, resp.saveStatusMessage)
      safeSet(setIsSaveError, resp.isSaveError)
      safeSet(setIsLoading, resp.isLoading)
      safeSet(setLoadProgress, resp.loadProgress)
      safeSet(setSelectedConfigFile, resp.selectedConfigFile)
      safeSet(setSelectedConfigFileName, resp.selectedConfigFileName)
      safeSet(setIsServerRunning, resp.isCurrentServerRunning ?? false)
      safeSet(setModifiedCount, resp.modifiedCount ?? 0)
      // BugJ: 同步 currentFileRef，保证 handleValueChange 的防抖快照正确
      if (resp.selectedConfigFile) currentFileRef.current = resp.selectedConfigFile
      return resp
    } catch (e) {
      console.error('获取配置条目失败:', e)
      // Bug11: 抛异常时给用户可见的错误提示，而不是静默
      if (mountedRef.current) {
        showToast('获取配置条目失败，请刷新重试', 'error')
      }
      return null
    } finally {
      if (mountedRef.current) setIsFetchingEntries(false)
    }
  }, [safeSet, showToast])

  // 防抖定时器引用，用于配置项值变更
  const debounceTimerRef = useRef<Record<string, number>>({})
  // BugJ: 跟踪当前选中配置文件（防抖触发时校验，避免切换文件后防抖跨文件更新）
  const currentFileRef = useRef<string | null>(null)
  // 跟踪当前选中的服务器名，供 loadFileTree 兜底判断
  const selectedServerNameRef = useRef<string | null>(null)
  // 跟踪当前手动定位的 JAR 路径，供 setTimeout 回调判断（避免 set state 闭包）
  const selectedJarPathRef = useRef<string | null>(null)
  // Bug15: handleBrowseJar 内的 150ms 重查定时器集合（卸载时统一 clear）
  const browseJarTimersRef = useRef<number[]>([])

  // ── BugD: 立即 flush 所有 pending debounces（Ctrl+S 保存前调用，避免保存旧值） ──
  const flushDebouncedUpdates = useCallback(async (): Promise<void> => {
    const timers = debounceTimerRef.current
    const keys = Object.keys(timers)
    if (keys.length === 0) return
    // 收集所有待提交的 (key, value) 对，然后清定时器
    const pending: Array<{ key: string; value: string }> = []
    for (const k of keys) {
      const timerId = timers[k]
      window.clearTimeout(timerId)
      // 必须通过 pendingValues 取值（因为 debounce 提交的就是 pendingValues 里存的值）
      if (k in pendingValues) {
        pending.push({ key: k, value: pendingValues[k] })
      }
      delete timers[k]
    }
    if (pending.length === 0) return
    // BugJ: flush 前再次校验「当前选中文件」是否与防抖启动时一致（currentFileRef）
    //       与 handleValueChange 中保持一致，由 updateConfigValue 后端根据上下文判断
    console.log('[ConfigEditor][FLUSH] 立即提交', pending.length, '个 pending 配置值')
    // 并发批量提交
    await Promise.all(
      pending.map(({ key, value }) =>
        updateConfigValue({ key, value })
          .then((res) => {
            if (!res?.success && mountedRef.current) {
              setPendingValues((prev) => {
                const next = { ...prev }
                delete next[key]
                return next
              })
              showToast(`修改提交失败: ${res?.error || '未知错误'}`, 'error')
            }
          })
          .catch((e) => {
            console.error('[FLUSH] updateConfigValue error:', e)
            if (mountedRef.current) {
              setPendingValues((prev) => {
                const next = { ...prev }
                delete next[key]
                return next
              })
              showToast('修改提交失败，已回滚本地 pending 状态', 'error')
            }
          }),
      ),
    )
  }, [pendingValues, showToast])

  // ── BugB: React Router 导航级别的脏数据拦截（beforeunload 只处理浏览器级别的刷新/关闭） ──
  const hasDirty = hasUnsavedChanges || Object.keys(pendingValues).length > 0
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) => {
      // 同一页面不拦截
      if (currentLocation.pathname === nextLocation.pathname) return false
      // 有脏数据时拦截
      return hasDirty
    },
  )
  // 拦截确认弹窗状态
  const [showNavConfirm, setShowNavConfirm] = useState<{ targetPath: string | null }>({ targetPath: null })
  useEffect(() => {
    if (blocker.state === 'blocked') {
      setShowNavConfirm({ targetPath: blocker.location?.pathname ?? null })
    }
  }, [blocker.state, blocker.location])

  // ── BugE: 定期同步「当前服务器是否正在运行」（ConfigEditor 进入时只取一次，之后靠轮询保持实时） ──
  // 只更新 isServerRunning / hasUnsavedChanges / modifiedCount 这几个「轻量状态」，
  // 避免重拉 configGroups 导致编辑中的输入框抖动/失焦
  useEffect(() => {
    const poll = async (): Promise<void> => {
      try {
        const resp = await getConfigEntries()
        if (!mountedRef.current) return
        // 只同步 isServerRunning + hasUnsavedChanges + modifiedCount，其它字段不动（避免打断用户输入）
        safeSet(setIsServerRunning, resp.isCurrentServerRunning ?? false)
        safeSet(setHasUnsavedChanges, resp.hasUnsavedChanges)
        if (typeof resp.modifiedCount === 'number') safeSet(setModifiedCount, resp.modifiedCount)
      } catch {
        // 轮询失败静默即可，下一轮再试
      }
    }
    const id = window.setInterval(poll, 5000)
    return () => window.clearInterval(id)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // 初始化：快速选择已移除，只做一次 loadFileTree 兜底（用户从 Dashboard 联动进入时，
  // 后端可能已通过 config:selectDefaultServer 设好了 Server，前端拉一次文件树即可显示）
  useEffect(() => {
    const init = async (): Promise<void> => {
      try {
        await loadFileTree()
      } catch (e) {
        console.error('初始化配置编辑器失败:', e)
      }
    }
    init()

    // Bug19: 真·绑定 Ctrl+S（按钮标题说了但之前没写，是"假绑定"）
    // BugD: 保存前强制 flush 所有 pending debounces，杜绝「300ms 防抖」和「320ms 后 save」
    //       的同时触发竞态，同时保证实际保存的内容 = 屏幕上用户看到的最新内容
    const doSave = async (): Promise<void> => {
      // 1) 先 flush 所有 pending 防抖提交（串行等它完成）
      try {
        await flushDebouncedUpdates()
      } catch (e) {
        console.error('[Ctrl+S] flush debounces 失败:', e)
        // 继续往下：save 应该仍能把当前已落到后端的改动保存
      }
      // 2) 再 saveConfig 写入文件
      await handleSave()
    }
    const onCtrlS = (e: KeyboardEvent): void => {
      if ((e.ctrlKey || e.metaKey) && e.key === 's') {
        // Bug 修复：Ctrl+S 快捷键无 isServerRunning 守卫，与按钮 disabled 不一致。
        // 服务器运行时禁止通过快捷键保存配置（与保存按钮 disabled 行为一致）。
        if (isServerRunningRef.current) {
          e.preventDefault()
          return
        }
        const active = document.activeElement
        if (active && (active.tagName === 'INPUT' || active.tagName === 'TEXTAREA')) {
          // 焦点在输入框中：先 blur 让 onChange 事件走完（把 pendingValues 写入，这样 flush 才能拿到）
          ;(active as HTMLElement).blur()
          // 给 React 的 state 更新留一帧（比 300ms 短很多，但已足够保证 state 写入）
          // 真正防止竞态靠 doSave 里的 flushDebouncedUpdates()，所以这里等 20ms 即可
          setTimeout(doSave, 20)
        } else {
          doSave()
        }
        e.preventDefault()
      }
    }
    window.addEventListener('keydown', onCtrlS)

    // Bug21: 浏览器级别 beforeunload，有脏数据时拦截关窗/刷新
    const onBeforeUnload = (e: BeforeUnloadEvent): void => {
      if (hasUnsavedChanges || Object.keys(pendingValues).length > 0) {
        e.preventDefault()
        e.returnValue = '有未保存的配置更改，确定离开吗？'
      }
    }
    window.addEventListener('beforeunload', onBeforeUnload)

    return () => {
      window.removeEventListener('keydown', onCtrlS)
      window.removeEventListener('beforeunload', onBeforeUnload)
      // 组件卸载时清理所有防抖定时器
      Object.values(debounceTimerRef.current).forEach((timer) => window.clearTimeout(timer))
      debounceTimerRef.current = {}
      // Bug15: 清理 browseJar 的 recheck 定时器
      browseJarTimersRef.current.forEach((id) => window.clearTimeout(id))
      browseJarTimersRef.current = []
    }
  }, [loadFileTree])

  // 手动定位 JAR —— 用户明确要求：「新增一个手动定位 Jar，然后顺着路径去遍历」
  // 链路：selectJarManually → 后端弹 OpenFileDialog → 选 JAR → 推导 WorkingDirectory → 赋值 cfg.Server
  //       → OnServerChanged 自动扫目录 → 前端 loadFileTree 拿配置文件树
  const handleBrowseJar = async (): Promise<void> => {
    try {
      // BugI: 先不清理旧状态，等用户真正选了 JAR 成功后再清理，避免取消时丢原状态
      const result = await selectJarManually()
      if (!result?.success) {
        console.error('手动定位 JAR 失败:', result?.error ?? '用户取消')
        if (result?.error && !result.error.includes('取消')) {
          // Bug18: 失败必须标红（之前 isSaveError 没设导致失败显示绿色）
          setSaveStatusMessage(`定位 JAR 失败: ${result.error}`)
          setIsSaveError(true)
          showToast(`定位 JAR 失败: ${result.error}`, 'error')
        }
        // BugI: 用户取消 → 什么都不做，保留旧状态（原文件树、原配置、原 pending 修改）
        return
      }

      // ✅ 真正选择成功了 → 才清空旧文件的编辑状态，并加载新的服务器/文件树
      // 1) 先 cancel 所有 debounce timers（避免旧文件的防抖跨文件更新）
      const timers = debounceTimerRef.current
      for (const k of Object.keys(timers)) {
        window.clearTimeout(timers[k])
        delete timers[k]
      }
      setPendingValues({})
      setSelectedConfigFile(null)
      setSelectedConfigFileName(null)
      currentFileRef.current = null
      setExpandedDirs(new Set())
      setExpandedGroups(new Set())
      setSaveStatusMessage(null)
      setIsSaveError(false)

      // 后端已赋值 cfg.Server，OnServerChanged 已 fire-and-forget 触发扫描
      if (result.jarPath) {
        setSelectedJarPath(result.jarPath)
        selectedJarPathRef.current = result.jarPath
        selectedServerNameRef.current = result.displayName ?? result.jarPath
      }
      if (result.workingDirectory) {
        setServerWorkingDirectory(result.workingDirectory)
      }

      await loadFileTree()

      // Bug15: 用 ref 存定时器 id，卸载时清理，避免卸载后 setState
      const expectedJar = result.jarPath
      const recheckTimerId = window.setTimeout(async () => {
        try {
          if (!mountedRef.current) return
          if (selectedJarPathRef.current !== expectedJar) return
          const recheck = await getConfigFileTree()
          if (!mountedRef.current) return
          if (recheck.tree && recheck.tree.length > 0) {
            safeSet(setConfigFileTree, recheck.tree)
            setConfigFilesState([])
            if (recheck.hasServerDirectory != null) safeSet(setHasServerDirectory, recheck.hasServerDirectory)
            if (recheck.configFileCountText) safeSet(setConfigFileCountText, recheck.configFileCountText)
            if (recheck.serverWorkingDirectory) safeSet(setServerWorkingDirectory, recheck.serverWorkingDirectory)
          }
        } catch {}
      }, 150)
      // 清理函数：组件卸载时 cancel 这个 recheck timer（通过 ref 维护一个"待清理的 timer 集合"）
      browseJarTimersRef.current.push(recheckTimerId)
    } catch (e) {
      console.error('手动定位 JAR 失败:', e)
    }
  }

  const handleRescan = async (): Promise<void> => {
    try {
      const result = await rescanConfigFiles()
      if (result.success) {
        await loadFileTree()
      } else {
        showToast('重新扫描失败', 'error')
      }
    } catch (e) {
      console.error('重新扫描失败:', e)
      showToast('重新扫描失败', 'error')
    }
  }

  const handleSelectFile = async (path: string): Promise<void> => {
    if (path === selectedConfigFile) return
    // BugJ: 切换文件前：1) 先 cancel 所有老文件的 debounce timers（如果用户没保存就硬切，保留 pendingValues 作为"脏"提示）
    //       2) 同步 currentFileRef 为新路径（防止后续启动的 debounce 快照到旧路径）
    const timers = debounceTimerRef.current
    for (const k of Object.keys(timers)) {
      window.clearTimeout(timers[k])
      delete timers[k]
    }
    try {
      await selectConfigFile(path)
      // BugJ: 清空 pendingValues（切换文件时，老文件的脏值不应该跨文件存在）
      setPendingValues({})
      setSelectedConfigFile(path)
      // BugJ: 同时同步 ref，保证 handleValueChange 的快照准确
      currentFileRef.current = path
      const resp = await loadEntries()
      if (resp) {
        // 默认展开所有分组（对应 WPF IsExpanded="True"）
        setExpandedGroups(new Set(resp.groups.map((g) => g.key)))
      }
    } catch (e) {
      console.error('选择配置文件失败:', e)
    }
  }

  const handleToggleDir = (path: string): void => {
    setExpandedDirs((prev) => {
      const next = new Set(prev)
      if (next.has(path)) next.delete(path)
      else next.add(path)
      return next
    })
  }

  const handleToggleGroup = (key: string): void => {
    setExpandedGroups((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  // BugJ: 防抖启动时快照当前选中的配置文件，触发时如果文件已经切换就直接丢弃
  const handleValueChange = (entry: ConfigEntry, value: string): void => {
    // 本地立即更新（避免输入丢失焦点）
    setPendingValues((prev) => ({ ...prev, [entry.key]: value }))

    // 清除该配置项之前的防抖定时器
    const existingTimer = debounceTimerRef.current[entry.key]
    if (existingTimer) {
      window.clearTimeout(existingTimer)
    }

    // 数值和文本类型添加 300ms 防抖，布尔和枚举立即提交
    const delay = entry.isBoolType || entry.isEnumType ? 0 : 300
    // BugJ: 快照当前选中的配置文件路径（防抖触发时再次校验）
    const fileAtChangeTime = currentFileRef.current

    debounceTimerRef.current[entry.key] = window.setTimeout(() => {
      // BugJ: 防抖触发时，文件已切换 → 丢弃本次提交，避免跨文件乱更新
      if (currentFileRef.current !== fileAtChangeTime) {
        console.log(
          `[ConfigEditor][BugJ] 跳过跨文件提交: key=${entry.key}, oldFile=${fileAtChangeTime}, newFile=${currentFileRef.current}`,
        )
        delete debounceTimerRef.current[entry.key]
        return
      }
      // Bug13: updateConfigValue 失败时 → 前端回滚 pending value，避免假成功
      updateConfigValue({ key: entry.key, value })
        .then((res) => {
          if (!res?.success) {
            // 后端业务层返回 success=false → 回滚 + toast
            if (mountedRef.current) {
              setPendingValues((prev) => {
                const next = { ...prev }
                delete next[entry.key]
                return next
              })
              showToast(`修改「${entry.friendlyDisplayName || entry.key}」失败: ${res?.error || '未知错误'}`, 'error')
            }
          }
        })
        .catch((e) => {
          console.error('更新配置值失败:', e)
          // 网络/桥接抛异常 → 回滚 + toast
          if (mountedRef.current) {
            setPendingValues((prev) => {
              const next = { ...prev }
              delete next[entry.key]
              return next
            })
            showToast(`修改「${entry.friendlyDisplayName || entry.key}」失败，已回滚`, 'error')
          }
        })
      delete debounceTimerRef.current[entry.key]
    }, delay)
  }

  const handleSave = async (): Promise<void> => {
    // BugD: 用户点按钮保存也先 flush debounces，与 Ctrl+S 保持一致（保证保存的=屏幕上看到的）
    try {
      await flushDebouncedUpdates()
    } catch (e) {
      console.error('[handleSave] flush debounces 失败:', e)
      // 继续尝试保存（即使部分 flush 失败，也把后端已经收到的那部分先落盘）
    }
    try {
      const result = await saveConfig()
      if (!mountedRef.current) return
      setSaveStatusMessage(result.message)
      setIsSaveError(!result.success)
      // Bug 修复：之前 setPendingValues({}) 在此处无条件执行，
      // 保存失败时也会清空用户修改 → 数据丢失。移到成功分支内。
      if (result.success) {
        setPendingValues({})
        await loadEntries()
        if (!mountedRef.current) return
        if (result.requiresRestart) {
          setShowRestartConfirm(true)
        } else {
          showToast('配置保存成功', 'success')
        }
      } else {
        if (result.errorType === 'FileLocked') {
          setSaveErrorInfo({
            type: result.errorType,
            detail: result.errorDetail ?? result.message ?? '',
          })
          setShowSaveErrorModal(true)
        } else {
          showToast(result.message ?? '保存失败', 'error')
        }
      }
    } catch (e) {
      console.error('保存配置失败:', e)
      if (mountedRef.current) {
        setSaveStatusMessage('保存失败')
        setIsSaveError(true)
        showToast('保存失败', 'error')
      }
    }
  }

  const handleReset = async (): Promise<void> => {
    try {
      const result = await resetConfig()
      if (result.success) {
        setPendingValues({})
        await loadEntries()
      } else {
        showToast('重置修改失败', 'error')
      }
    } catch (e) {
      console.error('重置修改失败:', e)
      showToast('重置修改失败', 'error')
    }
  }

  const handleUndo = async (): Promise<void> => {
    try {
      const result = await undoConfig()
      if (result.success) {
        setPendingValues({})
        await loadEntries()
      } else {
        showToast('撤销失败', 'error')
      }
    } catch (e) {
      console.error('撤销失败:', e)
      showToast('撤销失败', 'error')
    }
  }

  const handleRedo = async (): Promise<void> => {
    try {
      const result = await redoConfig()
      if (result.success) {
        setPendingValues({})
        await loadEntries()
      } else {
        showToast('重做失败', 'error')
      }
    } catch (e) {
      console.error('重做失败:', e)
      showToast('重做失败', 'error')
    }
  }

  const getDisplayValue = (entry: ConfigEntry): string =>
    entry.key in pendingValues ? pendingValues[entry.key] : entry.value

  const isModifiedLocal = (entry: ConfigEntry): boolean =>
    getDisplayValue(entry) !== entry.originalValue

  const showLoading = isFetchingEntries || isLoading

  return (
    <div className="md-page-enter h-full p-3 flex gap-3">
      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [FS] 左侧：配置文件卡片（服务器选择 + 文件树 + 统计） */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <Reveal
        direction="left"
        delay={0}
        className="md-card md-card-elevated flex flex-col flex-shrink-0 overflow-hidden"
        style={{ width: 280 }}
      >
        {/* 标题栏（主色背景） */}
        <div
          className="flex items-center px-4 py-3"
          style={{
            backgroundColor: 'var(--md-primary-hue-mid)',
            color: 'var(--md-white)',
          }}
        >
          <FaFolderOpen size={20} style={{ marginRight: 8 }} />
          <span style={{ fontSize: 15, fontWeight: 700 }}>配置文件</span>
        </div>

        {/* 服务器选择区 */}
        <div
          className="px-3 py-2.5"
          style={{
            borderBottom: '1px solid var(--md-card-subtle-border)',
            backgroundColor: 'var(--md-card-background)',
          }}
        >
          <div
            style={{
              fontSize: 'var(--md-font-size-sm)',
              fontWeight: 600,
              opacity: 0.6,
              marginBottom: 6,
            }}
          >
            选择服务器
          </div>
          <div className="flex gap-1">
            <button
              className="md-btn md-btn-outlined"
              style={{ height: 32, fontSize: 12, flex: 1, justifyContent: 'center' }}
              onClick={handleBrowseJar}
            >
              <FaFolderOpen size={14} style={{ marginRight: 6 }} />
              手动定位 JAR
            </button>
            <button
              className="md-btn md-btn-outlined md-btn-icon"
              style={{ height: 32, width: 32 }}
              title="重新扫描配置文件"
              onClick={handleRescan}
            >
              <FaArrowsRotate size={14} />
            </button>
          </div>
          {selectedJarPath && (
            <div
              className="truncate mt-1.5"
              style={{ fontSize: 10, opacity: 0.6 }}
              title={selectedJarPath}
            >
              JAR: {selectedJarPath}
            </div>
          )}
          {serverWorkingDirectory && (
            <div
              className="truncate mt-1.5"
              style={{ fontSize: 10, opacity: 0.5 }}
              title={serverWorkingDirectory}
            >
              {serverWorkingDirectory}
            </div>
          )}
        </div>

        {/* 文件树 */}
        <div className="flex-1 overflow-y-auto p-2">
          {configFileTree.length === 0 ? (
            <div
              className="text-center py-8"
              style={{
                color: 'var(--md-body-lighter)',
                fontSize: 'var(--md-font-size-sm)',
              }}
            >
              {hasServerDirectory ? '暂无配置文件' : '请先选择服务器'}
            </div>
          ) : (
            configFileTree.map((node) => (
              <ConfigTreeItem
                key={node.relativePath}
                node={node}
                depth={0}
                selectedFile={selectedConfigFile}
                expandedDirs={expandedDirs}
                onSelectFile={handleSelectFile}
                onToggleDir={handleToggleDir}
              />
            ))
          )}
        </div>

        {/* 文件统计 */}
        <div
          className="px-3 py-2"
          style={{
            borderTop: '1px solid var(--md-card-subtle-border)',
            fontSize: 'var(--md-font-size-sm)',
            color: 'var(--md-body-light)',
            opacity: 0.7,
          }}
        >
          {configFileCountText}
        </div>
      </Reveal>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [EDIT] 右侧：编辑区（操作栏 + 配置项分组列表） */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="flex-1 flex flex-col gap-3 min-w-0">
        {/* 顶部操作栏 */}
        <Reveal direction="up" delay={80} className="md-card md-card-elevated p-4 flex flex-col gap-2">
          <div className="flex items-center justify-between gap-3">
            {/* 左侧：当前文件名 + 副标题 */}
            <div className="min-w-0">
              <div className="flex items-center gap-3">
                <div
                  className="truncate"
                  style={{
                    fontSize: 16,
                    fontWeight: 700,
                    color: 'var(--md-body)',
                  }}
                  title={selectedConfigFile ?? ''}
                >
                  {selectedConfigFileName ?? '未选择文件'}
                </div>
                {modifiedCount > 0 && (
                  <div
                    style={{
                      fontSize: 'var(--md-font-size-sm)',
                      color: 'var(--md-body-light)',
                      flexShrink: 0,
                    }}
                  >
                    已修改 {modifiedCount} 项
                  </div>
                )}
              </div>
              <div
                className="flex items-center mt-1"
                style={{ opacity: 0.6 }}
              >
                <FaPen size={14} style={{ marginRight: 4 }} />
                <span style={{ fontSize: 'var(--md-font-size-sm)' }}>配置编辑器</span>
              </div>
            </div>
            {/* 右侧：操作按钮 */}
            <div className="flex items-center gap-2 flex-shrink-0">
              <button
                className="md-btn md-btn-outlined"
                disabled={!hasUnsavedChanges || isServerRunning}
                title={
                  isServerRunning
                    ? '服务器正在运行，修改需停服后才能撤销'
                    : !hasUnsavedChanges
                    ? '暂无可撤销的修改'
                    : '撤销最近一次编辑'
                }
                onClick={handleUndo}
              >
                <FaRotateLeft size={16} />
                撤销
              </button>
              <button
                className="md-btn md-btn-outlined"
                disabled={isServerRunning}
                title={
                  isServerRunning
                    ? '服务器正在运行，修改需停服后才能重做'
                    : '重做最近一次撤销的操作'
                }
                onClick={handleRedo}
              >
                <FaRotateRight size={16} />
                重做
              </button>
              <button
                className="md-btn md-btn-outlined"
                disabled={!hasUnsavedChanges || isServerRunning}
                title={
                  isServerRunning
                    ? '服务器正在运行，修改需停服后才能重置'
                    : !hasUnsavedChanges
                    ? '暂无可重置的修改'
                    : '重置所有尚未保存的修改'
                }
                onClick={handleReset}
              >
                <FaRotate size={16} />
                重置修改
              </button>
              <button
                className="md-btn md-btn-primary"
                disabled={!hasUnsavedChanges || isServerRunning}
                title={
                  isServerRunning
                    ? '服务器正在运行，请先停服再保存配置'
                    : !hasUnsavedChanges
                    ? '暂无可保存的修改'
                    : '保存配置到文件 (Ctrl+S)'
                }
                onClick={handleSave}
              >
                <FaFloppyDisk size={16} />
                保存配置
              </button>
            </div>
          </div>
          {/* 服务器运行警告横幅 */}
          {isServerRunning && (
            <div
              className="flex items-center"
              style={{
                height: 40,
                paddingLeft: 16,
                paddingRight: 16,
                backgroundColor: 'var(--md-warning-subtle-background)',
                borderRadius: 'var(--md-radius-small)',
                marginBottom: 12,
              }}
            >
              <FaTriangleExclamation
                size={18}
                style={{
                  color: 'var(--md-gauge-yellow)',
                  marginRight: 10,
                  flexShrink: 0,
                }}
              />
              <span
                style={{
                  fontSize: 'var(--md-font-size-base)',
                  color: 'var(--md-body)',
                }}
              >
                服务器正在运行，修改配置不会立即生效，请停止服务器后保存
              </span>
            </div>
          )}
          {/* 保存状态消息 */}
          {saveStatusMessage && (
            <div
              style={{
                fontSize: 'var(--md-font-size-base)',
                color: isSaveError
                  ? 'var(--md-error-text)'
                  : 'var(--md-gauge-green)',
              }}
            >
              {saveStatusMessage}
            </div>
          )}
        </Reveal>

        {/* 配置项列表区 */}
        <div className="flex-1 min-h-0 relative">
          {/* 加载遮罩 */}
          {showLoading && (
            <div
              className="absolute inset-0 flex flex-col items-center justify-center z-10"
              style={{
                backgroundColor: 'var(--md-loading-overlay)',
                borderRadius: 'var(--md-radius)',
              }}
            >
              <FaArrowsRotate
                size={48}
                className="md-spin"
                style={{ color: 'var(--md-primary-hue-mid)' }}
              />
              <div
                className="mt-4 mb-2"
                style={{ fontSize: 14, color: 'var(--md-body)' }}
              >
                正在加载配置...
              </div>
              <div className="md-progress" style={{ width: 200 }}>
                <div
                  className="md-progress-bar"
                  style={{ width: `${loadProgress}%` }}
                />
              </div>
              <div
                className="mt-1"
                style={{
                  fontSize: 'var(--md-font-size-sm)',
                  color: 'var(--md-body-light)',
                  opacity: 0.7,
                }}
              >
                {loadProgress}%
              </div>
            </div>
          )}

          {/* 空状态：尚未选择文件 */}
          {!selectedConfigFile && !showLoading && (
            <div className="h-full flex items-center justify-center">
              <Reveal
                direction="scale"
                delay={120}
                className="md-card md-card-elevated text-center"
                style={{ padding: '40px 48px' }}
              >
                <FaFileLines
                  size={72}
                  className="md-breathe"
                  style={{
                    color: 'var(--md-primary-hue-mid)',
                    opacity: 0.3,
                    margin: '0 auto',
                  }}
                />
                <div
                  className="mt-5 mb-1"
                  style={{
                    fontSize: 18,
                    fontWeight: 600,
                    color: 'var(--md-body)',
                  }}
                >
                  选择左侧的配置文件
                </div>
                <div
                  style={{
                    fontSize: 13,
                    opacity: 0.5,
                    color: 'var(--md-body)',
                  }}
                >
                  开始编辑服务器配置
                </div>
                <div
                  className="inline-flex items-center mt-5 px-3 py-2"
                  style={{
                    backgroundColor: 'var(--md-accent-subtle-border)',
                    borderRadius: 'var(--md-radius-small)',
                    color: 'var(--md-accent-text)',
                    fontSize: 12,
                  }}
                >
                  <FaLightbulb size={16} style={{ marginRight: 8 }} />
                  支持 server.properties / YAML / JSON 格式
                </div>
              </Reveal>
            </div>
          )}

          {/* 配置项分组列表（按分类分组的 Expander） */}
          {/* 修复：loading 时不移除旧条目 DOM，让遮罩盖住即可；避免闪空白/旧条目先消失再出现 */}
          {selectedConfigFile && (
            <div className="h-full overflow-y-auto pr-1">
              {/* ── 顶部：解析失败 Alert（__ERROR__ 条目） ── */}
              {(() => {
                const errEntries: ConfigEntry[] = []
                for (const g of configGroups) {
                  for (const e of g.items) if (e.key === '__ERROR__') errEntries.push(e)
                }
                if (errEntries.length === 0) return null
                return (
                  <div className="mb-2 space-y-1.5">
                    {errEntries.map((err, idx) => (
                      <div
                        key={`__error_${idx}`}
                        className="flex items-start gap-2 rounded-lg p-3 border"
                        style={{
                          borderColor: 'var(--md-warning-subtle-border)',
                          backgroundColor: 'var(--md-warning-subtle-background)',
                        }}
                      >
                        <FaCircleExclamation
                          size={18}
                          style={{ color: 'var(--md-error-text)', marginTop: 2, flexShrink: 0 }}
                        />
                        <div className="flex-1 min-w-0">
                          <div
                            style={{
                              fontSize: 13,
                              fontWeight: 700,
                              color: 'var(--md-error-text)',
                              marginBottom: 4,
                            }}
                          >
                            {err.displayName || '[WARN] 配置文件解析失败'}
                          </div>
                          <div
                            style={{
                              fontSize: 12,
                              color: 'var(--md-body)',
                              whiteSpace: 'pre-wrap',
                              wordBreak: 'break-word',
                              maxHeight: 200,
                              overflowY: 'auto',
                              fontFamily: 'var(--md-font-mono)',
                            }}
                          >
                            {err.errorMessage || err.value || '未知错误'}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )
              })()}

              {configGroups.length === 0 ||
               configGroups.every((g) => g.items.every((e) => e.key === '__ERROR__')) ? (
                <div
                  className="text-center py-8"
                  style={{ color: 'var(--md-body-lighter)' }}
                >
                  该文件无可编辑的配置项
                </div>
              ) : (
                <div className="space-y-1">
                  {/* ── 查找框 ── */}
                  <div className="sticky top-0 z-10 pb-2" style={{ backgroundColor: 'var(--md-background)' }}>
                    <div
                      className="flex items-center gap-2 rounded-lg border px-3 py-1.5"
                      style={{
                        borderColor: isSearchFocused
                          ? 'var(--md-primary-hue-mid)'
                          : 'var(--md-subtle-border)',
                        backgroundColor: 'var(--md-card-background)',
                        transition: 'border-color 150ms var(--md-ease-standard)',
                      }}
                    >
                      <FaMagnifyingGlass
                        size={13}
                        style={{
                          color: isSearchFocused
                            ? 'var(--md-primary-hue-mid)'
                            : 'var(--md-body-lighter)',
                          flexShrink: 0,
                        }}
                      />
                      <input
                        ref={searchInputRef}
                        type="text"
                        className="flex-1 bg-transparent border-none outline-none"
                        style={{
                          fontSize: 'var(--md-font-size-base)',
                          color: 'var(--md-body)',
                          height: 28,
                        }}
                        placeholder="查找配置项（支持中英文、键名、描述）Ctrl+F"
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                        onFocus={() => setIsSearchFocused(true)}
                        onBlur={() => setIsSearchFocused(false)}
                      />
                      {searchQuery && (
                        <>
                          <span
                            style={{
                              fontSize: 11,
                              color: 'var(--md-body-lighter)',
                              flexShrink: 0,
                            }}
                          >
                            {searchMatchCount} 项
                          </span>
                          <button
                            onClick={() => {
                              setSearchQuery('')
                              searchInputRef.current?.focus()
                            }}
                            className="border-none rounded cursor-pointer flex items-center justify-center"
                            style={{
                              width: 20,
                              height: 20,
                              backgroundColor: 'var(--md-subtle-border)',
                              color: 'var(--md-body-light)',
                              flexShrink: 0,
                            }}
                            title="清除"
                          >
                            <FaXmark size={10} />
                          </button>
                        </>
                      )}
                    </div>
                    {searchQuery && searchMatchCount === 0 && (
                      <div
                        className="mt-1.5 text-center"
                        style={{
                          fontSize: 12,
                          color: 'var(--md-body-lighter)',
                          padding: '4px 0',
                        }}
                      >
                        未找到匹配的配置项
                      </div>
                    )}
                  </div>

                  {filteredConfigGroups
                    .filter((g) => g.key !== '__ERROR__')
                    .map((group) => {
                      // 过滤掉本组内的 __ERROR__ 条目（已经在顶部 Alert 渲染）
                      const items = group.items.filter((e) => e.key !== '__ERROR__')
                      if (items.length === 0) return null
                      const isGroupExpanded = expandedGroups.has(group.key) || !!searchQuery.trim()
                      return (
                        <div key={group.key} className="md-expander">
                          {/* 分组标题 */}
                          <div
                            className="md-expander-header"
                            onClick={() => handleToggleGroup(group.key)}
                          >
                            <FaChevronRight
                              size={12}
                              className="md-expander-icon"
                              style={{
                                transform: isGroupExpanded
                                  ? 'rotate(90deg)'
                                  : 'none',
                              }}
                            />
                            <FaFolder
                              size={18}
                              style={{ color: 'var(--md-primary-hue-mid)' }}
                            />
                            <span
                              style={{
                                fontSize: 'var(--md-font-size-md)',
                                fontWeight: 700,
                                color: 'var(--md-primary-hue-mid)',
                              }}
                            >
                              {group.key}
                            </span>
                            <span className="md-badge" style={{ marginLeft: 10 }}>
                              {items.length}
                            </span>
                          </div>
                          {/* 分组内容：配置项卡片列表 */}
                          {isGroupExpanded && (
                            <div className="px-2 py-2 space-y-1.5">
                              {items.map((entry) => {
                                const modified = isModifiedLocal(entry)
                                const displayValue = getDisplayValue(entry)
                              return (
                                <div
                                  key={entry.key}
                                  className="relative rounded-lg p-3 border border-transparent transition-colors bg-[var(--md-card-background)] hover:bg-[var(--md-card-hover)] hover:border-[var(--md-accent-subtle-border)]"
                                  style={{
                                    borderRadius: 'var(--md-radius-small)',
                                  }}
                                >
                                  <div className="flex items-start justify-between gap-3">
                                    {/* 左侧：名称 + 描述 + 键名 */}
                                    <div className="min-w-0 flex-1">
                                      <div className="flex items-center">
                                        <span
                                          className="truncate"
                                          style={{
                                            fontSize: 13,
                                            fontWeight: 600,
                                            color: 'var(--md-body)',
                                          }}
                                          title={entry.friendlyDisplayName}
                                        >
                                          {entry.friendlyDisplayName}
                                        </span>
                                        {entry.requiresRestart && (
                                          <FaRotateLeft
                                            size={14}
                                            style={{
                                              marginLeft: 6,
                                              color: 'var(--md-gauge-yellow)',
                                            }}
                                            title="修改此项需要重启服务器"
                                          />
                                        )}
                                      </div>
                                      {entry.description && (
                                        <div
                                          className="mt-0.5 truncate"
                                          style={{
                                            fontSize: 'var(--md-font-size-sm)',
                                            color: 'var(--md-body-light)',
                                            opacity: 0.7,
                                          }}
                                          title={entry.description}
                                        >
                                          {entry.description}
                                        </div>
                                      )}
                                      <div
                                        className="mt-1 truncate"
                                        style={{
                                          fontSize: 10,
                                          color: 'var(--md-body-light)',
                                          opacity: 0.5,
                                          fontFamily: 'var(--md-font-mono)',
                                        }}
                                        title={entry.key}
                                      >
                                        {entry.key}
                                      </div>
                                    </div>
                                    {/* 右侧：编辑控件 + 错误提示 */}
                                    <div className="flex flex-col items-end flex-shrink-0">
                                      <ConfigEntryEditor
                                        entry={entry}
                                        displayValue={displayValue}
                                        onChange={(v) =>
                                          handleValueChange(entry, v)
                                        }
                                      />
                                      {!entry.isValid && entry.errorMessage && (
                                        <div
                                          className="mt-1.5"
                                          style={{
                                            color: 'var(--md-error-text)',
                                            fontSize: 'var(--md-font-size-sm)',
                                            fontWeight: 500,
                                          }}
                                        >
                                          {entry.errorMessage}
                                        </div>
                                      )}
                                    </div>
                                  </div>
                                  {/* 修改状态指示器（右上角小圆点） */}
                                  {modified && (
                                    <span
                                      className="absolute rounded-full"
                                      style={{
                                        top: 8,
                                        right: 8,
                                        width: 8,
                                        height: 8,
                                        backgroundColor: 'var(--md-gauge-yellow)',
                                      }}
                                      title="已修改，尚未保存"
                                    />
                                  )}
                                </div>
                              )
                            })}
                          </div>
                        )}
                      </div>
                    )
                  })}
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {showSaveErrorModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'var(--md-modal-backdrop)',
            zIndex: 10000,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            animation: 'mdFadeIn 0.2s ease-out',
          }}
          onClick={() => setShowSaveErrorModal(false)}
        >
          <div
            className="md-card"
            style={{
              width: 420,
              padding: 24,
              borderRadius: 'var(--md-radius-large)',
              boxShadow: 'var(--md-shadow-modal)',
              animation: 'mdModalIn 0.2s ease-out',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start gap-4">
              <div
                style={{
                  width: 48,
                  height: 48,
                  borderRadius: '50%',
                  backgroundColor: 'var(--md-error-subtle)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <FaCircleExclamation
                  size={24}
                  style={{ color: 'var(--md-error-text)' }}
                />
              </div>
              <div className="flex-1 min-w-0">
                <div
                  style={{
                    fontSize: 16,
                    fontWeight: 700,
                    color: 'var(--md-body)',
                    marginBottom: 8,
                  }}
                >
                  保存失败 - 文件被占用
                </div>
                <div
                  style={{
                    fontSize: 'var(--md-font-size-base)',
                    color: 'var(--md-body-light)',
                    lineHeight: 1.6,
                  }}
                >
                  {saveErrorInfo?.detail}
                  <br />
                  请关闭正在使用该文件的程序（如服务器进程或文本编辑器）后重试
                </div>
              </div>
            </div>
            <div className="flex justify-end mt-6">
              <button
                className="md-btn md-btn-primary"
                onClick={() => setShowSaveErrorModal(false)}
              >
                我知道了
              </button>
            </div>
          </div>
        </div>
      )}

      {showRestartConfirm && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'var(--md-modal-backdrop)',
            zIndex: 10000,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            animation: 'mdFadeIn 0.2s ease-out',
          }}
          onClick={() => setShowRestartConfirm(false)}
        >
          <div
            className="md-card"
            style={{
              width: 420,
              padding: 24,
              borderRadius: 'var(--md-radius-large)',
              boxShadow: 'var(--md-shadow-modal)',
              animation: 'mdModalIn 0.2s ease-out',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start gap-4">
              <div
                style={{
                  width: 48,
                  height: 48,
                  borderRadius: '50%',
                  backgroundColor: 'var(--md-primary-subtle)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <FaCheck size={24} style={{ color: 'var(--md-primary-hue-mid)' }} />
              </div>
              <div className="flex-1 min-w-0">
                <div
                  style={{
                    fontSize: 16,
                    fontWeight: 700,
                    color: 'var(--md-body)',
                    marginBottom: 8,
                  }}
                >
                  保存成功，是否重启服务器？
                </div>
                <div
                  style={{
                    fontSize: 'var(--md-font-size-base)',
                    color: 'var(--md-body-light)',
                    lineHeight: 1.6,
                  }}
                >
                  部分配置需要重启服务器才能生效，是否现在重启？
                </div>
              </div>
            </div>
            <div className="flex justify-end gap-2 mt-6">
              <button
                className="md-btn md-btn-outlined"
                onClick={() => setShowRestartConfirm(false)}
              >
                稍后重启
              </button>
              <button
                className="md-btn md-btn-primary"
                onClick={async () => {
                  setShowRestartConfirm(false)
                  // Bug16: 之前是假的"开发中"toast，现在真·停服 → 导航到 Dashboard 让用户点启动（启动需要完整的 Dashboard 上下文）
                  try {
                    const stopRes = await getBridge().invoke<{ success: boolean; error?: string; message?: string }>('server:stop')
                    if (!stopRes?.success) {
                      showToast(`停服失败: ${stopRes?.error || stopRes?.message || '未知错误'}`, 'error')
                      return
                    }
                    showToast('服务器已停止，请在 Dashboard 启动服务器以应用新配置', 'info')
                  } catch (e) {
                    showToast(`停服失败: ${e instanceof Error ? e.message : String(e)}`, 'error')
                    return
                  }
                  // 跳转到 Dashboard（那里有启动按钮和完整上下文）
                  navigate('/')
                }}
              >
                <FaPowerOff size={14} />
                立即停服并返回启动
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── BugB: Router 导航脏数据离开确认弹窗 ── */}
      {showNavConfirm.targetPath && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'var(--md-modal-backdrop)',
            zIndex: 10001,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            animation: 'mdFadeIn 0.2s ease-out',
          }}
          onClick={() => setShowNavConfirm({ targetPath: null })}
        >
          <div
            className="md-card"
            style={{
              width: 420,
              padding: 24,
              borderRadius: 'var(--md-radius-large)',
              boxShadow: 'var(--md-shadow-modal)',
              animation: 'mdModalIn 0.2s ease-out',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start gap-4">
              <div
                style={{
                  width: 48,
                  height: 48,
                  borderRadius: '50%',
                  backgroundColor: 'var(--md-warning-subtle)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <FaTriangleExclamation
                  size={24}
                  style={{ color: 'var(--md-gauge-yellow)' }}
                />
              </div>
              <div className="flex-1 min-w-0">
                <div
                  style={{
                    fontSize: 16,
                    fontWeight: 700,
                    color: 'var(--md-body)',
                    marginBottom: 8,
                  }}
                >
                  有未保存的修改
                </div>
                <div
                  style={{
                    fontSize: 'var(--md-font-size-base)',
                    color: 'var(--md-body-light)',
                    lineHeight: 1.6,
                  }}
                >
                  您对配置的修改尚未保存，离开此页面将丢失这些更改。确定要离开吗？
                </div>
              </div>
            </div>
            <div className="flex justify-end gap-2 mt-6">
              <button
                className="md-btn md-btn-outlined"
                onClick={() => {
                  setShowNavConfirm({ targetPath: null })
                  // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                  blocker.reset?.()
                }}
              >
                继续编辑
              </button>
              <button
                className="md-btn md-btn-danger"
                onClick={() => {
                  setShowNavConfirm({ targetPath: null })
                  // BugB: 用户确认离开 → 允许跳转（丢弃 pending 修改）
                  // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
                  blocker.proceed?.()
                }}
              >
                放弃更改并离开
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
