import { useCallback, useEffect, useState } from 'react'
import {
  FaGear,
  FaBell,
  FaRotate,
  FaCheck,
  FaShield,
  FaHeart,
  FaGithub,
  FaXmark,
  FaUser,
  FaShieldHalved,
  FaBolt,
  FaMemory,
  FaMoon,
} from 'react-icons/fa6'
import {
  getSettings,
  setPrimaryColor,
  setAccentColor,
  applyTheme,
  saveSettings,
  updateSettings,
  setPreset,
  resetSettings,
  toggleAnimations,
  testNotification,
  getAppInfo,
  getPresets,
  getPrimarySwatches,
  getAccentSwatches,
  getTeamInfo,
  setBackgroundColor,
  setCardColor,
  setTextColor,
  setBorderColor,
  setSuccessColor,
  setWarningColor,
  setErrorColor,
  setGaugeGreenColor,
  setGaugeYellowColor,
  setGaugeRedColor,
} from '@/utils/bridge'
import type {
  SettingsData,
  AppInfo,
  ThemePreset,
  SwatchInfo,
  PresetInfo,
  TeamInfoResponse,
} from '@/types/bridge'
import {
  applySettingsToCss,
  applyPrimaryColor,
  applyAccentColor,
  applyCornerRadius,
  applyAnimationSettings,
  applyBackgroundColor,
  applyCardColor,
  applyTextColor,
  applyBorderColor,
  applySemanticColors,
} from '@/utils/theme'
import { ColorPicker } from '@/components/ui/ColorPicker'
import abiAvatar from '@/assets/avatars/ABI-ZTROS.png'
import yanlanxiangAvatar from '@/assets/avatars/yanlanxiang.jpg'
import mochaAvatar from '@/assets/avatars/MochaCello92377.png'
import catstackAvatar from '@/assets/avatars/CatStack-pixe.png'

const avatarMap: Record<string, string> = {
  'ABI-ZTROS': abiAvatar,
  '烟蓝湘': yanlanxiangAvatar,
  'MochaCello92377': mochaAvatar,
  'CatStack-pixe': catstackAvatar,
}

// ─────────────────────────────────────────────────────────────────────
// 设置页主组件
// ─────────────────────────────────────────────────────────────────────
export function SettingsPage(): JSX.Element {
  const [settings, setSettings] = useState<SettingsData | null>(null)
  const [appInfo, setAppInfo] = useState<AppInfo | null>(null)
  const [statusMessage, setStatusMessage] = useState('')

  // 色板和预设数据
  const [primarySwatches, setPrimarySwatches] = useState<SwatchInfo[]>([])
  const [accentSwatches, setAccentSwatches] = useState<SwatchInfo[]>([])
  const [presetOptions, setPresetOptions] = useState<PresetInfo[]>([])
  const [swatchesLoading, setSwatchesLoading] = useState(true)

  // 团队信息
  const [teamInfo, setTeamInfo] = useState<TeamInfoResponse | null>(null)
  const [teamLoading, setTeamLoading] = useState(true)

  // 以下设置项桥接 API 暂未提供独立 setter，使用本地状态承载（初始值来自 getSettings）
  // 同时使用 localStorage 做持久化，避免页面刷新后丢失
  const [cornerRadius, setCornerRadius] = useState(() => {
    const saved = localStorage.getItem('msmc_cornerRadius')
    return saved ? Number(saved) : 0
  })
  const [animationDuration, setAnimationDuration] = useState(() => {
    const saved = localStorage.getItem('msmc_animationDuration')
    return saved ? Number(saved) : 200
  })
  const [enableWindowsNotifications, setEnableWindowsNotifications] = useState(() => {
    const saved = localStorage.getItem('msmc_enableWindowsNotifications')
    return saved ? saved === 'true' : false
  })
  const [preferJavaw, setPreferJavaw] = useState(() => {
    const saved = localStorage.getItem('msmc_preferJavaw')
    return saved ? saved === 'true' : false
  })

  // ─── 进程监管策略（崩溃重启/防睡眠/优先级/内存上限） ───
  // 后端 AppConfig.Supervisor 已扩展，前端走 localStorage 持久化 + 保存时 updateSettings 回传
  const DEFAULT_SUPERVISOR: SettingsData['supervisor'] = {
    enableCrashRestart: true,
    maxRestartAttemptsPerHour: 10,
    restartCooldownSeconds: 30,
    preventSystemSleepWhenRunning: true,
    processPriority: 'Normal',
    maxProcessMemoryBytes: 0,
    maxTotalRestartAttempts: -1,
  }

  const [supervisor, setSupervisor] = useState<SettingsData['supervisor']>(() => {
    try {
      const saved = localStorage.getItem('msmc_supervisor')
      if (saved) return { ...DEFAULT_SUPERVISOR, ...(JSON.parse(saved) as Partial<SettingsData['supervisor']>) }
    } catch { /* ignore */ }
    return DEFAULT_SUPERVISOR
  })

  const patchSupervisor = (patch: Partial<SettingsData['supervisor']>): void => {
    setSupervisor((prev) => {
      const next = { ...prev, ...patch }
      try { localStorage.setItem('msmc_supervisor', JSON.stringify(next)) } catch { /* ignore */ }
      return next
    })
  }

  const processPriorityOptions: Array<{ value: SettingsData['supervisor']['processPriority']; label: string; hint: string }> = [
    { value: 'Idle', label: '最低 (Idle)', hint: '只在系统完全空闲时运行，几乎不影响前台任务' },
    { value: 'BelowNormal', label: '低于标准 (BelowNormal)', hint: '轻度后台任务，推荐不影响游戏体验时使用' },
    { value: 'Normal', label: '标准 (Normal)', hint: '默认均衡调度，推荐绝大部分服主' },
    { value: 'AboveNormal', label: '高于标准 (AboveNormal)', hint: '大服/多人在线服，抢占更多 CPU 时间片' },
    { value: 'High', label: '高 (High)', hint: '竞技服或高 TPS 要求，可能轻微影响鼠标键盘响应' },
    { value: 'RealTime', label: '实时 (RealTime)', hint: '不推荐！抢占鼠标键盘/音频驱动，极端场景才用' },
  ]

  const loadSettings = useCallback(async (): Promise<void> => {
    try {
      const resp = await getSettings()
      setSettings(resp)
      setCornerRadius(resp.cornerRadius)
      setAnimationDuration(resp.animationDuration)
      setEnableWindowsNotifications(resp.enableWindowsNotifications)
      setPreferJavaw(resp.preferJavaw)
      setStatusMessage(resp.statusMessage)
      applySettingsToCss(resp)
      // 如果后端有带 supervisor 字段 → 覆盖 localStorage 默认值（确保后端 C# AppConfig.Supervisor 是真·源）
      if (resp.supervisor) patchSupervisor(resp.supervisor)
    } catch (e) {
      console.error('获取设置失败:', e)
    }
  }, [])

  const loadSwatchesAndPresets = useCallback(async (): Promise<void> => {
    try {
      setSwatchesLoading(true)
      const [presetsResp, primaryResp, accentResp] = await Promise.all([
        getPresets(),
        getPrimarySwatches(),
        getAccentSwatches(),
      ])
      setPresetOptions(presetsResp.presets)
      setPrimarySwatches(primaryResp.swatches)
      setAccentSwatches(accentResp.swatches)
    } catch (e) {
      console.error('获取色板和预设失败:', e)
    } finally {
      setSwatchesLoading(false)
    }
  }, [])

  const loadTeamInfo = useCallback(async (): Promise<void> => {
    try {
      setTeamLoading(true)
      const resp = await getTeamInfo()
      setTeamInfo(resp)
    } catch (e) {
      console.error('获取团队信息失败:', e)
    } finally {
      setTeamLoading(false)
    }
  }, [])

  useEffect(() => {
    loadSettings()
    loadSwatchesAndPresets()
    loadTeamInfo()
    getAppInfo()
      .then((info) => setAppInfo(info))
      .catch((e) => console.error('获取应用信息失败:', e))
  }, [
    loadSettings,
    loadSwatchesAndPresets,
    loadTeamInfo,
  ])

  // ─── 颜色设置 ───
  const handlePrimaryPreview = (hex: string): void => {
    applyPrimaryColor(hex)
  }

  const handleAccentPreview = (hex: string): void => {
    applyAccentColor(hex)
  }

  const handleSetPrimary = async (hex: string): Promise<void> => {
    try {
      await setPrimaryColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置主色失败:', e)
    }
  }

  const handleSetAccent = async (hex: string): Promise<void> => {
    try {
      await setAccentColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置强调色失败:', e)
    }
  }

  // ─── 背景色 ───
  const handleBackgroundPreview = (hex: string): void => {
    applyBackgroundColor(hex)
  }

  const handleSetBackground = async (hex: string): Promise<void> => {
    try {
      await setBackgroundColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置背景色失败:', e)
    }
  }

  // ─── 卡片色 ───
  const handleCardPreview = (hex: string): void => {
    applyCardColor(hex)
  }

  const handleSetCard = async (hex: string): Promise<void> => {
    try {
      await setCardColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置卡片色失败:', e)
    }
  }

  // ─── 文字色 ───
  const handleTextPreview = (hex: string): void => {
    applyTextColor(hex)
  }

  const handleSetText = async (hex: string): Promise<void> => {
    try {
      await setTextColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置文字色失败:', e)
    }
  }

  // ─── 边框色 ───
  const handleBorderPreview = (hex: string): void => {
    applyBorderColor(hex)
  }

  const handleSetBorder = async (hex: string): Promise<void> => {
    try {
      await setBorderColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置边框色失败:', e)
    }
  }

  // ─── 成功色 ───
  const handleSuccessPreview = (hex: string): void => {
    applySemanticColors({ success: hex, warning: warningColorHex, error: errorColorHex, gaugeGreen: gaugeGreenColorHex, gaugeYellow: gaugeYellowColorHex, gaugeRed: gaugeRedColorHex })
  }

  const handleSetSuccess = async (hex: string): Promise<void> => {
    try {
      await setSuccessColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置成功色失败:', e)
    }
  }

  // ─── 警告色 ───
  const handleWarningPreview = (hex: string): void => {
    applySemanticColors({ success: successColorHex, warning: hex, error: errorColorHex, gaugeGreen: gaugeGreenColorHex, gaugeYellow: gaugeYellowColorHex, gaugeRed: gaugeRedColorHex })
  }

  const handleSetWarning = async (hex: string): Promise<void> => {
    try {
      await setWarningColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置警告色失败:', e)
    }
  }

  // ─── 错误色 ───
  const handleErrorPreview = (hex: string): void => {
    applySemanticColors({ success: successColorHex, warning: warningColorHex, error: hex, gaugeGreen: gaugeGreenColorHex, gaugeYellow: gaugeYellowColorHex, gaugeRed: gaugeRedColorHex })
  }

  const handleSetError = async (hex: string): Promise<void> => {
    try {
      await setErrorColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置错误色失败:', e)
    }
  }

  // ─── 仪表盘绿 ───
  const handleGaugeGreenPreview = (hex: string): void => {
    applySemanticColors({ success: successColorHex, warning: warningColorHex, error: errorColorHex, gaugeGreen: hex, gaugeYellow: gaugeYellowColorHex, gaugeRed: gaugeRedColorHex })
  }

  const handleSetGaugeGreen = async (hex: string): Promise<void> => {
    try {
      await setGaugeGreenColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置仪表盘绿失败:', e)
    }
  }

  // ─── 仪表盘黄 ───
  const handleGaugeYellowPreview = (hex: string): void => {
    applySemanticColors({ success: successColorHex, warning: warningColorHex, error: errorColorHex, gaugeGreen: gaugeGreenColorHex, gaugeYellow: hex, gaugeRed: gaugeRedColorHex })
  }

  const handleSetGaugeYellow = async (hex: string): Promise<void> => {
    try {
      await setGaugeYellowColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置仪表盘黄失败:', e)
    }
  }

  // ─── 仪表盘红 ───
  const handleGaugeRedPreview = (hex: string): void => {
    applySemanticColors({ success: successColorHex, warning: warningColorHex, error: errorColorHex, gaugeGreen: gaugeGreenColorHex, gaugeYellow: gaugeYellowColorHex, gaugeRed: hex })
  }

  const handleSetGaugeRed = async (hex: string): Promise<void> => {
    try {
      await setGaugeRedColor(hex)
      await loadSettings()
    } catch (e) {
      console.error('设置仪表盘红失败:', e)
    }
  }

  const handleSetPreset = async (preset: ThemePreset): Promise<void> => {
    try {
      const result = await setPreset(preset)
      if (result.success) {
        await loadSettings()
      } else {
        setStatusMessage('应用预设失败')
      }
    } catch (e) {
      console.error('应用预设失败:', e)
      setStatusMessage('应用预设失败')
    }
  }

  // ─── 动画设置 ───
  const handleToggleAnimations = async (): Promise<void> => {
    try {
      const result = await toggleAnimations()
      if (result.success) {
        await loadSettings()
      }
    } catch (e) {
      console.error('切换动画失败:', e)
    }
  }

  // ─── 通知测试 ───
  const handleTestNotification = async (): Promise<void> => {
    try {
      const result = await testNotification()
      if (result.success) {
        setStatusMessage('✅ 测试通知已发送，请检查通知通道')
      } else {
        setStatusMessage('❌ 发送测试通知失败')
      }
    } catch (e) {
      console.error('发送测试通知失败:', e)
      setStatusMessage('❌ 发送测试通知失败')
    }
  }

  // ─── 底部操作栏 ───
  const handleApplyTheme = async (): Promise<void> => {
    try {
      const updateResult = await updateSettings({
        cornerRadius,
        animationDuration,
        enableAnimations: settings?.enableAnimations ?? true,
        enableWindowsNotifications,
        preferJavaw,
        supervisor,
      } as any)
      if (!updateResult?.success) {
        setStatusMessage(`应用设置失败: ${updateResult?.error || '未知错误'}`)
        return
      }

      const result = await applyTheme()
      setStatusMessage(result.success ? '主题已应用' : '主题应用失败')
      await loadSettings()
    } catch (e) {
      console.error('应用主题失败:', e)
      setStatusMessage('应用主题失败')
    }
  }

  const handleSave = async (): Promise<void> => {
    try {
      const updateResult = await updateSettings({
        cornerRadius,
        animationDuration,
        enableAnimations: settings?.enableAnimations ?? true,
        enableWindowsNotifications,
        preferJavaw,
        supervisor,
      } as any)
      if (!updateResult?.success) {
        setStatusMessage(`应用设置失败: ${updateResult?.error || '未知错误'}`)
        return
      }

      const result = await saveSettings()
      setStatusMessage(result.success ? '设置已保存' : '保存设置失败')
      await loadSettings()
    } catch (e) {
      console.error('保存设置失败:', e)
      setStatusMessage('保存设置失败')
    }
  }

  const handleReset = async (): Promise<void> => {
    try {
      const result = await resetSettings()
      // Bug 修复：之前不清理 localStorage，重置后刷新页面旧值复活。
      // 清理所有 msmc_* 前缀的 localStorage 键，确保重置彻底生效。
      if (result.success) {
        const keysToRemove = [
          'msmc_cornerRadius',
          'msmc_animationDuration',
          'msmc_enableWindowsNotifications',
          'msmc_preferJavaw',
          'msmc_supervisor',
        ]
        keysToRemove.forEach((k) => {
          try { localStorage.removeItem(k) } catch { /* ignore */ }
        })
        setCornerRadius(0)
        setAnimationDuration(200)
        setEnableWindowsNotifications(false)
        setPreferJavaw(false)
        setSupervisor(DEFAULT_SUPERVISOR)
      }
      setStatusMessage(result.success ? '已重置为默认设置' : '重置失败')
      await loadSettings()
    } catch (e) {
      console.error('重置设置失败:', e)
      setStatusMessage('重置设置失败')
    }
  }

  const enableAnimations = settings?.enableAnimations ?? true
  const primaryColorHex = settings?.primaryColorHex ?? '#3B82F6'
  const accentColorHex = settings?.accentColorHex ?? '#FB7185'

  // 新增 10 个颜色默认值（依赖后端 settings 扩展字段）
  const backgroundColorHex = settings?.backgroundColorHex ?? '#020617'
  const cardColorHex = settings?.cardColorHex ?? '#0F172A'
  const textColorHex = settings?.textColorHex ?? '#E2E8F0'
  const borderColorHex = settings?.borderColorHex ?? '#334155'
  const successColorHex = settings?.successColorHex ?? '#4CAF50'
  const warningColorHex = settings?.warningColorHex ?? '#FFC107'
  const errorColorHex = settings?.errorColorHex ?? '#E53935'
  const gaugeGreenColorHex = settings?.gaugeGreenColorHex ?? '#4CAF50'
  const gaugeYellowColorHex = settings?.gaugeYellowColorHex ?? '#FFC107'
  const gaugeRedColorHex = settings?.gaugeRedColorHex ?? '#F4364C'

  return (
    <div className="md-page-enter p-4 pb-8 max-w-4xl mx-auto">
      {/* ═══ 标题 ═══ */}
      <div className="flex items-center mb-4">
        <FaGear
          size={32}
          style={{ color: 'var(--md-accent-text)', marginRight: 12 }}
        />
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, color: 'var(--md-body)' }}>
            外观设置
          </h1>
          <p
            style={{
              fontSize: 13,
              color: 'var(--md-body-light)',
            }}
          >
            自定义颜色、圆角和动画效果
          </p>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [THEME] 外观设置卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '0ms' }}>
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
        >
          颜色方案
        </h2>

        <div className="grid grid-cols-2 gap-4">
          <ColorPicker
            label="主色调"
            value={primaryColorHex}
            onChange={handlePrimaryPreview}
            onChangeEnd={handleSetPrimary}
            presets={primarySwatches.map((s) => s.color)}
          />
          <ColorPicker
            label="强调色"
            value={accentColorHex}
            onChange={handleAccentPreview}
            onChangeEnd={handleSetAccent}
            presets={accentSwatches.map((s) => s.color)}
          />
          <ColorPicker
            label="背景色"
            value={backgroundColorHex}
            onChange={handleBackgroundPreview}
            onChangeEnd={handleSetBackground}
          />
          <ColorPicker
            label="卡片色"
            value={cardColorHex}
            onChange={handleCardPreview}
            onChangeEnd={handleSetCard}
          />
          <ColorPicker
            label="文字色"
            value={textColorHex}
            onChange={handleTextPreview}
            onChangeEnd={handleSetText}
          />
          <ColorPicker
            label="边框色"
            value={borderColorHex}
            onChange={handleBorderPreview}
            onChangeEnd={handleSetBorder}
          />
        </div>

        {/* 语义与仪表盘色 */}
        <div
          style={{
            marginTop: 16,
            paddingTop: 16,
            borderTop: '1px solid var(--md-card-subtle-border)',
          }}
        >
          <h2
            className="md-section-title"
            style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
          >
            语义与仪表盘色
          </h2>
          <div className="grid grid-cols-2 gap-4">
            <ColorPicker
              label="成功色"
              value={successColorHex}
              onChange={handleSuccessPreview}
              onChangeEnd={handleSetSuccess}
            />
            <ColorPicker
              label="警告色"
              value={warningColorHex}
              onChange={handleWarningPreview}
              onChangeEnd={handleSetWarning}
            />
            <ColorPicker
              label="错误色"
              value={errorColorHex}
              onChange={handleErrorPreview}
              onChangeEnd={handleSetError}
            />
            <ColorPicker
              label="仪表盘绿"
              value={gaugeGreenColorHex}
              onChange={handleGaugeGreenPreview}
              onChangeEnd={handleSetGaugeGreen}
            />
            <ColorPicker
              label="仪表盘黄"
              value={gaugeYellowColorHex}
              onChange={handleGaugeYellowPreview}
              onChangeEnd={handleSetGaugeYellow}
            />
            <ColorPicker
              label="仪表盘红"
              value={gaugeRedColorHex}
              onChange={handleGaugeRedPreview}
              onChangeEnd={handleSetGaugeRed}
            />
          </div>
        </div>

        {/* 快速预设方案 */}
        <div style={{ marginTop: 16 }}>
          <div
            style={{
              fontSize: 13,
              color: 'var(--md-body)',
              margin: '8px 0 4px 0',
            }}
          >
            快速方案
          </div>
          <div className="flex flex-wrap" style={{ gap: 8 }}>
            {swatchesLoading ? (
              Array.from({ length: 5 }).map((_, i) => (
                <div
                  key={i}
                  className="md-btn md-btn-outlined md-skeleton"
                  style={{
                    backgroundColor: 'var(--md-card-hover)',
                    borderColor: 'transparent',
                    opacity: 0.6,
                  }}
                >
                  <span
                    style={{
                      width: 20,
                      height: 20,
                      borderRadius: 4,
                      backgroundColor: 'var(--md-subtle-border)',
                    }}
                  />
                  <span
                    style={{
                      width: 20,
                      height: 20,
                      borderRadius: 4,
                      backgroundColor: 'var(--md-subtle-border)',
                      marginLeft: 4,
                    }}
                  />
                  <span
                    style={{
                      marginLeft: 8,
                      width: 60,
                      height: 14,
                      backgroundColor: 'var(--md-subtle-border)',
                      borderRadius: 2,
                    }}
                  />
                </div>
              ))
            ) : (
              presetOptions.map((p) => (
                <button
                  key={p.key}
                  className="md-btn md-btn-outlined"
                  onClick={() => handleSetPreset(p.key)}
                >
                  <span
                    style={{
                      width: 20,
                      height: 20,
                      borderRadius: 4,
                      backgroundColor: p.primary,
                    }}
                  />
                  <span
                    style={{
                      width: 20,
                      height: 20,
                      borderRadius: 4,
                      backgroundColor: p.accent,
                      marginLeft: 4,
                    }}
                  />
                  <span style={{ marginLeft: 8 }}>{p.label}</span>
                </button>
              ))
            )}
          </div>
        </div>

        {/* 圆角设置 */}
        <div
          style={{
            marginTop: 16,
            paddingTop: 16,
            borderTop: '1px solid var(--md-card-subtle-border)',
          }}
        >
          <h2
            className="md-section-title"
            style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
          >
            圆角设置
          </h2>
          <div
            style={{
              fontSize: 13,
              color: 'var(--md-body)',
              margin: '8px 0 4px 0',
            }}
          >
            控件圆角半径
          </div>
          <input
            type="range"
            min={0}
            max={24}
            step={2}
            value={cornerRadius}
            onChange={(e) => {
              const val = Number(e.target.value)
              setCornerRadius(val)
              applyCornerRadius(val)
              localStorage.setItem('msmc_cornerRadius', String(val))
            }}
            style={{ width: 400, margin: '8px 0' }}
          />
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
              marginBottom: 8,
            }}
          >
            当前: {cornerRadius}px
          </div>
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
            }}
          >
            控制按钮、卡片、输入框等元素的圆角大小
          </div>

          {/* 圆角预览 */}
          <div style={{ marginTop: 12 }}>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
                marginBottom: 6,
              }}
            >
              预览
            </div>
            <div className="flex">
              <div
                style={{
                  width: 60,
                  height: 36,
                  backgroundColor: 'var(--md-card-background)',
                  border: '1px solid var(--md-accent-text)',
                  borderRadius: cornerRadius,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 11,
                  color: 'var(--md-body)',
                }}
              >
                按钮
              </div>
              <div
                style={{
                  width: 80,
                  height: 36,
                  backgroundColor: 'var(--md-card-background)',
                  border: '1px solid var(--md-subtle-border)',
                  borderRadius: cornerRadius,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 11,
                  color: 'var(--md-body)',
                  marginLeft: 12,
                }}
              >
                卡片
              </div>
              <div
                style={{
                  width: 100,
                  height: 36,
                  backgroundColor: 'var(--md-card-hover)',
                  borderRadius: cornerRadius,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 11,
                  color: 'var(--md-body)',
                  marginLeft: 12,
                }}
              >
                输入框
              </div>
            </div>
          </div>
        </div>

        {/* 动画设置 */}
        <div
          style={{
            marginTop: 16,
            paddingTop: 16,
            borderTop: '1px solid var(--md-card-subtle-border)',
          }}
        >
          <h2
            className="md-section-title"
            style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
          >
            动画设置
          </h2>
          <div className="flex items-center" style={{ marginTop: 8 }}>
            <label className="md-toggle">
              <input
                type="checkbox"
                checked={enableAnimations}
                onChange={handleToggleAnimations}
              />
              <span className="md-toggle-slider" />
            </label>
            <div style={{ marginLeft: 12 }}>
              <div
                style={{
                  color: 'var(--md-body)',
                  fontSize: 13,
                }}
              >
                启用动画效果
              </div>
              <div
                style={{
                  fontSize: 11,
                  color: 'var(--md-body-light)',
                }}
              >
                页面切换、按钮悬停等动效
              </div>
            </div>
          </div>

          <div
            style={{
              fontSize: 13,
              color: 'var(--md-body)',
              margin: '12px 0 4px 0',
            }}
          >
            动画速度
          </div>
          <input
            type="range"
            min={50}
            max={1000}
            step={50}
            value={animationDuration}
            onChange={(e) => {
              const val = Number(e.target.value)
              setAnimationDuration(val)
              applyAnimationSettings(val, enableAnimations)
              localStorage.setItem('msmc_animationDuration', String(val))
            }}
            disabled={!enableAnimations}
            style={{ width: 400, margin: '8px 0' }}
          />
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
              marginBottom: 8,
            }}
          >
            当前: {animationDuration}ms
          </div>
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
            }}
          >
            控制页面切换、按钮悬停等动画的持续时间
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [TOAST] 服务器设置卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '80ms' }}>
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
        >
          服务器设置
        </h2>

        {/* Windows 通知 */}
        <div className="flex items-center" style={{ marginTop: 8 }}>
          <label className="md-toggle">
            <input
              type="checkbox"
              checked={enableWindowsNotifications}
              onChange={(e) => {
                const val = e.target.checked
                setEnableWindowsNotifications(val)
                localStorage.setItem('msmc_enableWindowsNotifications', String(val))
              }}
            />
            <span className="md-toggle-slider" />
          </label>
          <div style={{ marginLeft: 12 }}>
            <div
              style={{
                color: 'var(--md-body)',
                fontSize: 13,
              }}
            >
              Windows 通知中心
            </div>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
              }}
            >
              重要信息通过系统通知弹出
            </div>
          </div>
        </div>

        {/* 优先使用 javaw */}
        <div className="flex items-center" style={{ marginTop: 16 }}>
          <label className="md-toggle">
            <input
              type="checkbox"
              checked={preferJavaw}
              onChange={(e) => {
                const val = e.target.checked
                setPreferJavaw(val)
                localStorage.setItem('msmc_preferJavaw', String(val))
              }}
            />
            <span className="md-toggle-slider" />
          </label>
          <div style={{ marginLeft: 12 }}>
            <div
              style={{
                color: 'var(--md-body)',
                fontSize: 13,
              }}
            >
              优先使用 javaw.exe
            </div>
            <div
              style={{
                fontSize: 11,
                color: 'var(--md-body-light)',
              }}
            >
              无控制台窗口启动（不推荐，服务器日志将不可见）
            </div>
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [SUPERVISOR] 进程监管策略卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item" style={{ animationDelay: '140ms' }}>
        <div className="flex items-center mb-2">
          <FaShieldHalved size={18} style={{ color: 'var(--md-accent-text)', marginRight: 8 }} />
          <h2
            className="md-section-title"
            style={{ color: 'var(--md-accent-text)', margin: 0, lineHeight: 1.2 }}
          >
            进程监管策略
          </h2>
        </div>
        <div
          style={{
            fontSize: 12,
            color: 'var(--md-body-light)',
            margin: '4px 0 16px 26px',
            lineHeight: 1.55,
          }}
        >
          基于 Win32 Job Object 实现：崩溃自动重启、防止系统睡眠、设置进程优先级/内存硬上限。关闭
          MSMC 时所有服务器进程会被一并终止，不会像老版本出现"幽灵 Java 进程"。
        </div>

        {/* ✅ 1. 崩溃自动重启开关 */}
        <div className="md-field">
          <label className="md-switch md-switch-lg">
            <input
              type="checkbox"
              checked={supervisor.enableCrashRestart}
              onChange={(e) => patchSupervisor({ enableCrashRestart: e.target.checked })}
            />
            <span className="md-slider md-slider-lg" />
            <div className="md-switch-label">
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <FaRotate size={13} style={{ color: 'var(--md-primary)' }} />
                <span style={{ fontSize: 13, color: 'var(--md-body)', fontWeight: 500 }}>
                  崩溃自动重启
                </span>
              </div>
              <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginTop: 2 }}>
                服务器意外崩溃后按冷却时间自动拉起，直到达到次数上限后停止
              </div>
            </div>
          </label>
        </div>

        {/* ✅ 2. 每小时最大重启次数 */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaBolt size={12} style={{ marginRight: 6, color: 'var(--md-warning)' }} />
          每小时最大重启次数
        </div>
        <div className="flex items-center gap-3">
          <input
            type="range"
            min={0}
            max={120}
            step={1}
            value={supervisor.maxRestartAttemptsPerHour}
            onChange={(e) => patchSupervisor({ maxRestartAttemptsPerHour: Number(e.target.value) })}
            className="md-range"
            style={{ flex: 1 }}
            disabled={!supervisor.enableCrashRestart}
          />
          <div style={{ minWidth: 56, textAlign: 'right', fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {supervisor.maxRestartAttemptsPerHour === 0 ? '不限' : supervisor.maxRestartAttemptsPerHour + ' 次/时'}
          </div>
          </div>
        </div>

        {/* ✅ 3. 重启冷却时间（秒） */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaMemory size={12} style={{ marginRight: 6, color: 'var(--md-info)' }} />
          重启冷却时间
        </div>
        <div className="flex items-center gap-3">
          <input
            type="range"
            min={0}
            max={600}
            step={1}
            value={supervisor.restartCooldownSeconds}
            onChange={(e) => patchSupervisor({ restartCooldownSeconds: Number(e.target.value) })}
            className="md-range"
            style={{ flex: 1 }}
            disabled={!supervisor.enableCrashRestart}
          />
          <div style={{ minWidth: 72, textAlign: 'right', fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {supervisor.restartCooldownSeconds} 秒
          </div>
          </div>
        </div>

        {/* ✅ 4. 防睡眠开关 */}
        <div className="md-field">
          <label className="md-switch md-switch-lg">
            <input
              type="checkbox"
              checked={supervisor.preventSystemSleepWhenRunning}
              onChange={(e) => patchSupervisor({ preventSystemSleepWhenRunning: e.target.checked })}
            />
            <span className="md-slider md-slider-lg" />
            <div className="md-switch-label">
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <FaMoon size={13} style={{ color: 'var(--md-accent)' }} />
                <span style={{ fontSize: 13, color: 'var(--md-body)', fontWeight: 500 }}>
                  服务器运行时阻止系统睡眠
                </span>
              </div>
              <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginTop: 2 }}>
                只要有一台监管的服务器在运行，Windows 就不会进入 Modern Standby / S3 睡眠
              </div>
            </div>
          </label>
        </div>

        {/* ✅ 5. 进程优先级下拉 */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaBolt size={12} style={{ marginRight: 6, color: 'var(--md-primary)' }} />
          进程优先级 (Process Priority)
        </div>
        <select
          className="md-select"
          value={supervisor.processPriority}
          onChange={(e) => patchSupervisor({ processPriority: e.target.value as SettingsData['supervisor']['processPriority'] })}
          style={{ maxWidth: 480 }}
        >
          {processPriorityOptions.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginTop: 6, lineHeight: 1.6 }}>
          {processPriorityOptions.find((o) => o.value === supervisor.processPriority)?.hint}
        </div>
        </div>

        {/* ✅ 6. 进程内存硬上限（GB） */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaMemory size={12} style={{ marginRight: 6, color: 'var(--md-danger)' }} />
          进程内存硬上限 (Job Object 级别，超出内核直接 Kill)
        </div>
        <div className="flex items-center gap-3">
          <input
            type="range"
            min={0}
            max={128}
            step={1}
            value={Math.round(supervisor.maxProcessMemoryBytes / (1024 ** 3))}
            onChange={(e) => {
              const gb = Number(e.target.value)
              patchSupervisor({ maxProcessMemoryBytes: gb === 0 ? 0 : gb * (1024 ** 3) })
            }}
            className="md-range"
            style={{ flex: 1 }}
          />
          <div style={{ minWidth: 72, textAlign: 'right', fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {supervisor.maxProcessMemoryBytes === 0
              ? '不限'
              : `${Math.round(supervisor.maxProcessMemoryBytes / (1024 ** 3))} GB`}
          </div>
          </div>
        <div style={{ fontSize: 11, color: 'var(--md-body-lighter)', marginTop: 4, lineHeight: 1.6 }}>
          在 JVM <code>-Xmx</code> 之外再套一层 OS 级别硬上限，防止内存泄漏直接打爆整机（推荐设置为略大于 -Xmx 2-4GB）
        </div>
        </div>

        {/* ✅ 7. 总重启次数上限 */}
        <div className="md-field md-stacked">
          <div className="md-label">
          <FaShield size={12} style={{ marginRight: 6, color: 'var(--md-success)' }} />
          总重启次数上限（防止一次性地图损坏导致无限重启）
        </div>
        <div className="flex items-center gap-3">
          <input
            type="range"
            min={-1}
            max={1000}
            step={1}
            value={supervisor.maxTotalRestartAttempts}
            onChange={(e) => patchSupervisor({ maxTotalRestartAttempts: Number(e.target.value) })}
            className="md-range"
            style={{ flex: 1 }}
            disabled={!supervisor.enableCrashRestart}
          />
          <div style={{ minWidth: 96, textAlign: 'right', fontSize: 14, fontWeight: 600, color: 'var(--md-body)' }}>
            {supervisor.maxTotalRestartAttempts === -1
              ? '不限次数'
              : supervisor.maxTotalRestartAttempts === 0
                ? '永不重启'
                : `${supervisor.maxTotalRestartAttempts} 次`}
          </div>
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [INFO] 关于卡片 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="md-card p-5 mb-4">
        <h2
          className="md-section-title"
          style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}
        >
          关于 MSMC
        </h2>

        {/* 应用信息 */}
        <div
          className="flex flex-col items-center"
          style={{ margin: '4px 0 16px 0' }}
        >
          <div
            style={{
              width: 64,
              height: 64,
              borderRadius: 'var(--md-radius-large)',
              backgroundColor: 'var(--md-primary-subtle-background)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <FaShield
              size={40}
              style={{ color: 'var(--md-accent-text)' }}
            />
          </div>
          <div
            style={{
              fontSize: 20,
              fontWeight: 700,
              color: 'var(--md-body)',
              marginTop: 10,
            }}
          >
            {appInfo?.name ?? 'MSMC'}
          </div>
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
            }}
          >
            {appInfo?.fullName ?? 'Minecraft Server Management Console'}
          </div>
          <div
            style={{
              fontSize: 11,
              color: 'var(--md-body-light)',
              opacity: 0.7,
              marginTop: 4,
            }}
          >
            v{appInfo?.version ?? '0.1.0'}
          </div>
        </div>

        {/* 开发团队标题 */}
        <div
          style={{
            borderTop: '1px solid var(--md-card-subtle-border)',
            paddingTop: 16,
            marginBottom: 12,
          }}
        >
          <h3
            style={{
              fontSize: 15,
              fontWeight: 600,
              color: 'var(--md-body)',
              margin: 0,
              textAlign: 'center',
            }}
          >
            开发团队
          </h3>
        </div>

        {teamLoading ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              {Array.from({ length: 2 }).map((_, i) => (
                <div
                  key={i}
                  style={{
                    padding: 12,
                    borderRadius: 'var(--md-radius)',
                  }}
                  className="md-skeleton"
                >
                  <div className="flex items-center">
                    <div
                      style={{
                        width: 48,
                        height: 48,
                        borderRadius: '50%',
                        backgroundColor: 'var(--md-subtle-border)',
                        flexShrink: 0,
                      }}
                    />
                    <div style={{ marginLeft: 10, flex: 1 }}>
                      <div
                        style={{
                          width: '60%',
                          height: 14,
                          backgroundColor: 'var(--md-subtle-border)',
                          borderRadius: 2,
                          marginBottom: 6,
                        }}
                      />
                      <div
                        style={{
                          width: '80%',
                          height: 12,
                          backgroundColor: 'var(--md-subtle-border)',
                          borderRadius: 2,
                        }}
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>
            <div
              style={{
                padding: 16,
                borderRadius: 'var(--md-radius)',
              }}
              className="md-skeleton"
            >
              <div className="flex items-center justify-center">
                <div
                  style={{
                    width: 56,
                    height: 56,
                    borderRadius: '50%',
                    backgroundColor: 'var(--md-subtle-border)',
                  }}
                />
              </div>
              <div
                style={{
                  width: '40%',
                  height: 14,
                  backgroundColor: 'var(--md-subtle-border)',
                  borderRadius: 2,
                  margin: '10px auto 6px auto',
                }}
              />
              <div
                style={{
                  width: '60%',
                  height: 12,
                  backgroundColor: 'var(--md-subtle-border)',
                  borderRadius: 2,
                  margin: '0 auto',
                }}
              />
            </div>
          </div>
        ) : (
          <>
            {/* 主开发者 + 特别感谢 两列布局 */}
            <div className="grid grid-cols-2 gap-4" style={{ marginBottom: 16 }}>
              {/* 主开发者 */}
              <div>
                <div
                  style={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: 'var(--md-body-light)',
                    marginBottom: 8,
                    textAlign: 'center',
                  }}
                >
                  主开发者
                </div>
                <div className="space-y-2">
                  {teamInfo?.primaryDevelopers.map((member, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: 12,
                        backgroundColor: 'var(--md-card-hover)',
                        borderRadius: 'var(--md-radius)',
                        display: 'flex',
                        alignItems: 'center',
                      }}
                    >
                      <div
                        style={{
                          width: 48,
                          height: 48,
                          borderRadius: '50%',
                          backgroundColor: 'var(--md-primary-subtle-background)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          flexShrink: 0,
                          overflow: 'hidden',
                        }}
                      >
                        {member.avatar || avatarMap[member.name] ? (
                          <img
                            src={member.avatar || avatarMap[member.name]}
                            alt={member.name}
                            style={{
                              width: '100%',
                              height: '100%',
                              objectFit: 'cover',
                              borderRadius: 'inherit',
                            }}
                          />
                        ) : (
                          <FaUser
                            size={24}
                            style={{ color: 'var(--md-accent-text)' }}
                          />
                        )}
                      </div>
                      <div style={{ marginLeft: 10, flex: 1, minWidth: 0 }}>
                        <div
                          style={{
                            fontWeight: 600,
                            color: 'var(--md-body)',
                            fontSize: 13,
                            display: 'flex',
                            alignItems: 'center',
                            gap: 6,
                          }}
                        >
                          {member.name}
                          {member.hasHeartIcon && (
                            <FaHeart
                              size={12}
                              style={{ color: 'var(--md-accent-text)' }}
                            />
                          )}
                          {member.hasCrossIcon && (
                            <FaXmark
                              size={14}
                              style={{ color: 'var(--md-body-light)' }}
                            />
                          )}
                        </div>
                        <div
                          style={{
                            fontSize: 11,
                            color: 'var(--md-body-light)',
                            marginTop: 2,
                          }}
                        >
                          {member.role}
                        </div>
                        {member.github && (
                          <a
                            href={`https://github.com/${member.github}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{
                              fontSize: 11,
                              color: 'var(--md-accent-text)',
                              marginTop: 2,
                              display: 'flex',
                              alignItems: 'center',
                              gap: 4,
                              textDecoration: 'none',
                            }}
                          >
                            <FaGithub size={12} />
                            @{member.github}
                          </a>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              {/* 中间爱心 + 特别感谢 */}
              <div style={{ position: 'relative' }}>
                <div
                  style={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: 'var(--md-body-light)',
                    marginBottom: 8,
                    textAlign: 'center',
                  }}
                >
                  特别感谢
                </div>
                <div className="space-y-2">
                  {teamInfo?.specialThanks.map((member, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: 12,
                        backgroundColor: 'var(--md-card-hover)',
                        borderRadius: 'var(--md-radius)',
                        display: 'flex',
                        alignItems: 'center',
                      }}
                    >
                      <div
                        style={{
                          width: 48,
                          height: 48,
                          borderRadius: '50%',
                          backgroundColor: 'var(--md-primary-subtle-background)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          flexShrink: 0,
                          overflow: 'hidden',
                        }}
                      >
                        {member.avatar || avatarMap[member.name] ? (
                          <img
                            src={member.avatar || avatarMap[member.name]}
                            alt={member.name}
                            style={{
                              width: '100%',
                              height: '100%',
                              objectFit: 'cover',
                              borderRadius: 'inherit',
                            }}
                          />
                        ) : (
                          <FaUser
                            size={24}
                            style={{ color: 'var(--md-accent-text)' }}
                          />
                        )}
                      </div>
                      <div style={{ marginLeft: 10, flex: 1, minWidth: 0 }}>
                        <div
                          style={{
                            fontWeight: 600,
                            color: 'var(--md-body)',
                            fontSize: 13,
                            display: 'flex',
                            alignItems: 'center',
                            gap: 6,
                          }}
                        >
                          {member.name}
                          {member.hasHeartIcon && (
                            <FaHeart
                              size={12}
                              style={{ color: 'var(--md-accent-text)' }}
                            />
                          )}
                          {member.hasCrossIcon && (
                            <FaXmark
                              size={14}
                              style={{ color: 'var(--md-body-light)' }}
                            />
                          )}
                        </div>
                        <div
                          style={{
                            fontSize: 11,
                            color: 'var(--md-body-light)',
                            marginTop: 2,
                          }}
                        >
                          {member.role}
                        </div>
                        {member.github && (
                          <a
                            href={`https://github.com/${member.github}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{
                              fontSize: 11,
                              color: 'var(--md-accent-text)',
                              marginTop: 2,
                              display: 'flex',
                              alignItems: 'center',
                              gap: 4,
                              textDecoration: 'none',
                            }}
                          >
                            <FaGithub size={12} />
                            @{member.github}
                          </a>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* 中间爱心装饰 */}
            <div
              style={{
                display: 'flex',
                justifyContent: 'center',
                margin: '-28px 0 12px 0',
                position: 'relative',
                zIndex: 1,
              }}
            >
              <div
                style={{
                  width: 36,
                  height: 36,
                  borderRadius: '50%',
                  backgroundColor: 'var(--md-card-background)',
                  border: '2px solid var(--md-card-subtle-border)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                <FaHeart
                  size={18}
                  style={{ color: 'var(--md-accent-text)' }}
                />
              </div>
            </div>

            {/* 纪念卡片 */}
            {teamInfo?.memorial && teamInfo.memorial.length > 0 && (
              <div
                style={{
                  marginBottom: 16,
                  padding: 20,
                  background: 'linear-gradient(135deg, var(--md-memorial-gold-bg-start) 0%, var(--md-memorial-gold-bg-end) 100%)',
                  border: '2px solid var(--md-memorial-gold)',
                  borderRadius: 'var(--md-radius)',
                  position: 'relative',
                  overflow: 'hidden',
                }}
              >
                <div
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    background: 'radial-gradient(circle at 50% 0%, var(--md-memorial-gold-glow) 0%, transparent 60%)',
                    pointerEvents: 'none',
                  }}
                />
                {teamInfo.memorial.map((member, idx) => (
                  <div
                    key={idx}
                    className="flex flex-col items-center"
                    style={{ position: 'relative', zIndex: 1 }}
                  >
                    <div
                      style={{
                        width: 64,
                        height: 64,
                        borderRadius: '50%',
                        backgroundColor: 'var(--md-memorial-gold-bg-soft)',
                        border: '2px solid var(--md-memorial-gold)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      <FaUser
                        size={32}
                        style={{ color: 'var(--md-memorial-gold)' }}
                      />
                    </div>
                    <div
                      style={{
                        fontSize: 16,
                        fontWeight: 700,
                        color: 'var(--md-memorial-gold)',
                        marginTop: 10,
                        display: 'flex',
                        alignItems: 'center',
                        gap: 8,
                      }}
                    >
                      {member.name}
                      <FaHeart
                        size={14}
                        style={{ color: 'var(--md-accent-text)' }}
                      />
                    </div>
                    <div
                      style={{
                        fontSize: 12,
                        color: 'var(--md-memorial-gold-soft)',
                        marginTop: 4,
                      }}
                    >
                      {member.role}
                    </div>
                    {member.description && (
                      <div
                        style={{
                          fontSize: 11,
                          color: 'var(--md-memorial-gold-muted)',
                          marginTop: 8,
                          textAlign: 'center',
                          fontStyle: 'italic',
                        }}
                      >
                        {member.description}
                      </div>
                    )}
                    {member.github && (
                      <a
                        href={`https://github.com/${member.github}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        style={{
                          fontSize: 11,
                          color: 'var(--md-memorial-gold)',
                          marginTop: 6,
                          display: 'flex',
                          alignItems: 'center',
                          gap: 4,
                          textDecoration: 'none',
                        }}
                      >
                        <FaGithub size={12} />
                        @{member.github}
                      </a>
                    )}
                  </div>
                ))}
              </div>
            )}

            {/* 贡献者 */}
            {teamInfo?.contributors && teamInfo.contributors.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <div
                  style={{
                    fontSize: 12,
                    fontWeight: 600,
                    color: 'var(--md-body-light)',
                    marginBottom: 8,
                    textAlign: 'center',
                  }}
                >
                  贡献者
                </div>
                <div className="grid grid-cols-2 gap-2">
                  {teamInfo.contributors.map((member, idx) => (
                    <div
                      key={idx}
                      style={{
                        padding: 10,
                        backgroundColor: 'var(--md-card-hover)',
                        borderRadius: 'var(--md-radius)',
                        display: 'flex',
                        alignItems: 'center',
                      }}
                    >
                      <div
                        style={{
                          width: 40,
                          height: 40,
                          borderRadius: '50%',
                          backgroundColor: 'var(--md-primary-subtle-background)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          flexShrink: 0,
                          overflow: 'hidden',
                        }}
                      >
                        {member.avatar || avatarMap[member.name] ? (
                          <img
                            src={member.avatar || avatarMap[member.name]}
                            alt={member.name}
                            style={{
                              width: '100%',
                              height: '100%',
                              objectFit: 'cover',
                              borderRadius: 'inherit',
                            }}
                          />
                        ) : (
                          <FaUser
                            size={20}
                            style={{ color: 'var(--md-accent-text)' }}
                          />
                        )}
                      </div>
                      <div style={{ marginLeft: 8, flex: 1, minWidth: 0 }}>
                        <div
                          style={{
                            fontWeight: 600,
                            color: 'var(--md-body)',
                            fontSize: 12,
                          }}
                        >
                          {member.name}
                        </div>
                        {member.github && (
                          <a
                            href={`https://github.com/${member.github}`}
                            target="_blank"
                            rel="noopener noreferrer"
                            style={{
                              fontSize: 10,
                              color: 'var(--md-accent-text)',
                              marginTop: 1,
                              display: 'flex',
                              alignItems: 'center',
                              gap: 3,
                              textDecoration: 'none',
                            }}
                          >
                            <FaGithub size={10} />
                            @{member.github}
                          </a>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}

        {/* 测试通知按钮 */}
        <div
          style={{
            borderTop: '1px solid var(--md-card-subtle-border)',
            paddingTop: 16,
          }}
        >
          <button
            className="md-btn md-btn-outlined"
            style={{ width: '100%' }}
            onClick={handleTestNotification}
          >
            <FaBell size={16} />
            <span style={{ marginLeft: 8 }}>发送测试通知</span>
          </button>
          <div
            style={{
              fontSize: 12,
              color: 'var(--md-body-light)',
              marginTop: 8,
              textAlign: 'center',
            }}
          >
            点击测试按钮可以验证通知功能是否正常工作
          </div>
        </div>
      </div>

      {/* ═══════════════════════════════════════════════════════════ */}
      {/* [LOG] 底部操作栏 */}
      {/* ═══════════════════════════════════════════════════════════ */}
      <div className="flex" style={{ gap: 8, marginTop: 16 }}>
        <button
          className="md-btn md-btn-outlined"
          onClick={handleReset}
        >
          <FaRotate size={16} />
          <span style={{ marginLeft: 8 }}>重置为默认</span>
        </button>
        <button
          className="md-btn md-btn-primary"
          onClick={handleApplyTheme}
        >
          <FaCheck size={16} />
          <span style={{ marginLeft: 8 }}>应用主题</span>
        </button>
        <button
          className="md-btn md-btn-primary"
          onClick={handleSave}
        >
          <FaCheck size={16} />
          <span style={{ marginLeft: 8 }}>保存设置</span>
        </button>
      </div>

      {/* 状态信息 */}
      {statusMessage && (
        <div
          style={{
            fontSize: 12,
            color: 'var(--md-accent-text)',
            marginTop: 16,
          }}
        >
          {statusMessage}
        </div>
      )}
    </div>
  )
}
