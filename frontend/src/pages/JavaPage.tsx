import { useCallback, useEffect, useState } from 'react'
import {
  FaMugHot,
  FaRotate,
  FaStar,
  FaTrashCan,
  FaFolderOpen,
  FaPlus,
} from 'react-icons/fa6'
import {
  getJavaList,
  rescanJava,
  addJavaPath,
  removeJavaPath,
  setDefaultJava,
  browseJavaPath,
} from '@/utils/bridge'
import type {
  JavaInstallationInfo,
  JavaListResponse,
} from '@/types/bridge'

export function JavaPage(): JSX.Element {
  const [javaList, setJavaList] = useState<JavaInstallationInfo[]>([])
  const [isScanningJava, setIsScanningJava] = useState(false)
  const [statusMessage, setStatusMessage] = useState<{ text: string; type: 'info' | 'error' } | null>(null)
  const [newJavaPath, setNewJavaPath] = useState('')
  const [javaOpInProgress, setJavaOpInProgress] = useState(false)

  const showStatus = useCallback((text: string, type: 'info' | 'error' = 'info'): void => {
    setStatusMessage({ text, type })
    window.setTimeout(() => {
      setStatusMessage((cur) => (cur && cur.text === text ? null : cur))
    }, 3500)
  }, [])

  const loadJavaList = useCallback(async (): Promise<void> => {
    try {
      const resp: JavaListResponse = await getJavaList()
      if ((resp as any).success === false) {
        showStatus((resp as any).error ?? '获取 Java 列表失败', 'error')
        return
      }
      setJavaList(resp.javas)
      setIsScanningJava(resp.isScanning)
    } catch (e) {
      console.error('获取 Java 列表失败:', e)
      showStatus(e instanceof Error ? e.message : String(e), 'error')
    }
  }, [showStatus])

  useEffect(() => {
    loadJavaList()
  }, [loadJavaList])

  const handleRescanJava = async (): Promise<void> => {
    try {
      setIsScanningJava(true)
      const result = await rescanJava()
      if (result.success) {
        await loadJavaList()
        showStatus('正在后台扫描 Java 安装...')
      } else {
        showStatus('重新扫描 Java 失败', 'error')
      }
    } catch (e) {
      console.error('重新扫描 Java 失败:', e)
      showStatus(e instanceof Error ? e.message : '重新扫描 Java 失败', 'error')
    }
  }

  const handleBrowseJavaPath = async (): Promise<void> => {
    try {
      setJavaOpInProgress(true)
      const result = await browseJavaPath()
      if (result.success && result.path) {
        setNewJavaPath(result.path)
      } else if (!result.success) {
        showStatus(result.error ?? '未选择有效的 Java 路径', 'error')
      }
    } catch (e) {
      console.error('浏览 Java 路径失败:', e)
      showStatus(e instanceof Error ? e.message : '浏览 Java 路径失败', 'error')
    } finally {
      setJavaOpInProgress(false)
    }
  }

  const handleAddJavaPath = async (): Promise<void> => {
    const path = newJavaPath.trim()
    if (!path) {
      showStatus('请输入或选择 Java 路径', 'error')
      return
    }
    try {
      setJavaOpInProgress(true)
      const result = await addJavaPath(path)
      if (result.success) {
        setNewJavaPath('')
        showStatus(result.statusMessage || '已添加 Java 路径')
        await loadJavaList()
      } else {
        showStatus(result.error || result.statusMessage || '添加 Java 路径失败', 'error')
      }
    } catch (e) {
      console.error('添加 Java 路径失败:', e)
      showStatus(e instanceof Error ? e.message : '添加 Java 路径失败', 'error')
    } finally {
      setJavaOpInProgress(false)
    }
  }

  const handleSetDefaultJava = async (java: JavaInstallationInfo): Promise<void> => {
    try {
      setJavaOpInProgress(true)
      const result = await setDefaultJava(java.javaPath)
      if (result.success) {
        showStatus(result.statusMessage || '已设为默认 Java')
        await loadJavaList()
      } else {
        showStatus(result.error || '设为默认 Java 失败', 'error')
      }
    } catch (e) {
      console.error('设为默认 Java 失败:', e)
      showStatus(e instanceof Error ? e.message : '设为默认 Java 失败', 'error')
    } finally {
      setJavaOpInProgress(false)
    }
  }

  const handleRemoveJavaPath = async (java: JavaInstallationInfo): Promise<void> => {
    if (!java.isCustom) {
      showStatus('只能移除自定义添加的 Java 路径', 'error')
      return
    }
    try {
      setJavaOpInProgress(true)
      const result = await removeJavaPath(java.javaPath)
      if (result.success) {
        showStatus(result.statusMessage || '已移除 Java 路径')
        await loadJavaList()
      } else {
        showStatus(result.error || '移除 Java 路径失败', 'error')
      }
    } catch (e) {
      console.error('移除 Java 路径失败:', e)
      showStatus(e instanceof Error ? e.message : '移除 Java 路径失败', 'error')
    } finally {
      setJavaOpInProgress(false)
    }
  }

  return (
    <div className="md-page-enter p-4 pb-8 max-w-4xl mx-auto">
      <div className="flex items-center mb-4">
        <FaMugHot size={18} style={{ marginRight: 8, color: 'var(--md-accent-text)' }} />
        <h1 className="text-lg font-bold text-[var(--md-body)]">Java 管理</h1>
      </div>

      {statusMessage && (
        <div
          style={{
            fontSize: 12,
            padding: '8px 14px',
            marginBottom: 12,
            borderRadius: 6,
            borderLeft: `3px solid ${statusMessage.type === 'error' ? 'var(--md-danger)' : 'var(--md-info)'}`,
            background: 'var(--md-card-bg)',
            color: statusMessage.type === 'error' ? 'var(--md-danger)' : 'var(--md-body)',
          }}
        >
          {statusMessage.text}
        </div>
      )}

      <div className="md-card md-card-elevated p-5 mb-4 md-stagger-item">
        <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 4px 0' }}>
          Java 运行环境
        </h2>
        <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginBottom: 12 }}>
          管理系统中的 Java 安装，设置默认版本和启动方式
        </div>

        <div className="flex items-center justify-between" style={{ margin: '8px 0' }}>
          <div style={{ fontSize: 13, color: 'var(--md-body)', margin: '8px 0 4px 0' }}>
            已检测到的 Java 版本
          </div>
          <button
            className="md-btn md-btn-outlined"
            disabled={isScanningJava}
            onClick={handleRescanJava}
          >
            <FaRotate size={14} className={isScanningJava ? 'md-spin' : ''} />
            <span style={{ marginLeft: 6 }}>重新扫描</span>
          </button>
        </div>

        <div
          style={{
            backgroundColor: 'var(--md-card-hover)',
            borderRadius: 'var(--md-radius)',
            padding: 8,
            maxHeight: 300,
            overflowY: 'auto',
          }}
        >
          {javaList.length === 0 ? (
            <div style={{ textAlign: 'center', padding: 24, color: 'var(--md-body-lighter)', fontSize: 13 }}>
              {isScanningJava ? '正在扫描...' : '未检测到 Java 安装'}
            </div>
          ) : (
            <div className="space-y-1.5">
              {javaList.map((java) => (
                <div
                  key={java.javaPath}
                  className="flex items-center"
                  style={{
                    padding: 10,
                    borderRadius: 'var(--md-radius-small)',
                    backgroundColor: 'var(--md-card-background)',
                    border: '1px solid var(--md-card-subtle-border)',
                  }}
                >
                  <div
                    style={{
                      width: 36,
                      height: 36,
                      backgroundColor: 'var(--md-primary-subtle-background)',
                      borderRadius: 'var(--md-radius-small)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      flexShrink: 0,
                    }}
                  >
                    <FaMugHot size={20} style={{ color: 'var(--md-accent-text)' }} />
                  </div>
                  <div style={{ marginLeft: 10, flex: 1, minWidth: 0 }}>
                    <div style={{ fontWeight: 600, color: 'var(--md-body)', fontSize: 13 }}>
                      {java.versionDisplay || java.versionString || '未知版本'}
                    </div>
                    <div
                      className="truncate"
                      style={{ fontSize: 11, color: 'var(--md-body-light)', marginTop: 2 }}
                      title={java.javaPath}
                    >
                      {java.javaPath}
                    </div>
                    <div className="flex" style={{ marginTop: 2, gap: 4 }}>
                      {java.isDefault && (
                        <span style={{
                          backgroundColor: 'var(--md-accent-text)',
                          borderRadius: 4,
                          padding: '2px 6px',
                          fontSize: 10,
                          fontWeight: 700,
                          color: 'var(--md-card-background)',
                        }}>
                          默认
                        </span>
                      )}
                      {java.isCustom && (
                        <span style={{
                          backgroundColor: 'var(--md-primary-hue-mid)',
                          borderRadius: 4,
                          padding: '2px 6px',
                          fontSize: 10,
                          fontWeight: 700,
                          color: 'var(--md-white)',
                        }}>
                          自定义
                        </span>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center" style={{ gap: 4, flexShrink: 0, marginLeft: 8 }}>
                    {!java.isDefault && (
                      <button
                        className="md-btn md-btn-outlined"
                        disabled={javaOpInProgress || isScanningJava}
                        onClick={() => handleSetDefaultJava(java)}
                        title="设为默认 Java"
                        style={{ padding: '4px 8px', fontSize: 11 }}
                      >
                        <FaStar size={11} />
                        <span style={{ marginLeft: 4 }}>设为默认</span>
                      </button>
                    )}
                    {java.isCustom && (
                      <button
                        className="md-btn md-btn-outlined"
                        disabled={javaOpInProgress || isScanningJava}
                        onClick={() => handleRemoveJavaPath(java)}
                        title="移除自定义 Java 路径"
                        style={{ padding: '4px 8px', fontSize: 11, color: 'var(--md-accent-text)' }}
                      >
                        <FaTrashCan size={11} />
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div style={{ marginTop: 12 }}>
          <div style={{ fontSize: 13, color: 'var(--md-body)', margin: '8px 0 4px 0' }}>
            自选 Java 路径
          </div>
          <div style={{ fontSize: 11, color: 'var(--md-body-light)', marginBottom: 8 }}>
            手动指定本机上未自动检测到的 Java 安装目录（支持 Java 和 JDK）
          </div>
          <div className="flex items-center" style={{ gap: 8 }}>
            <input
              type="text"
              value={newJavaPath}
              onChange={(e) => setNewJavaPath(e.target.value)}
              placeholder="例如：C:\Program Files\Java\jdk-21"
              disabled={javaOpInProgress || isScanningJava}
              style={{
                flex: 1,
                minWidth: 0,
                padding: '8px 10px',
                borderRadius: 'var(--md-radius-small)',
                border: '1px solid var(--md-card-subtle-border)',
                backgroundColor: 'var(--md-card-background)',
                color: 'var(--md-body)',
                fontSize: 12,
              }}
            />
            <button
              className="md-btn md-btn-outlined"
              onClick={handleBrowseJavaPath}
              disabled={javaOpInProgress || isScanningJava}
              title="浏览选择 Java 安装目录"
            >
              <FaFolderOpen size={14} />
              <span style={{ marginLeft: 6 }}>浏览</span>
            </button>
            <button
              className="md-btn md-btn-filled"
              onClick={handleAddJavaPath}
              disabled={javaOpInProgress || isScanningJava || !newJavaPath.trim()}
              title="添加自定义 Java 路径"
            >
              <FaPlus size={14} />
              <span style={{ marginLeft: 6 }}>添加</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
