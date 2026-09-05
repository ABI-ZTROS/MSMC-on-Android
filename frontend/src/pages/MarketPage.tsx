import { useState, useEffect, useCallback } from 'react'
import {
  FaMagnifyingGlass,
  FaDownload,
  FaCheck,
  FaStore,
  FaServer,
} from 'react-icons/fa6'
import { searchMarket, getMarketVersions, installPlugin, getInstalledPlugins, getSelectedServer } from '@/utils/bridge'
import type { MarketProject, MarketVersion, InstalledPlugin, InstallResult, ServerInfo } from '@/types/bridge'

export function MarketPage(): JSX.Element {
  const [searchQuery, setSearchQuery] = useState('')
  const [searching, setSearching] = useState(false)
  const [projects, setProjects] = useState<MarketProject[]>([])
  const [selectedProject, setSelectedProject] = useState<MarketProject | null>(null)
  const [versions, setVersions] = useState<MarketVersion[]>([])
  const [installing, setInstalling] = useState(false)
  const [installResult, setInstallResult] = useState<InstallResult | null>(null)
  const [installedPlugins, setInstalledPlugins] = useState<InstalledPlugin[]>([])
  const [statusMsg, setStatusMsg] = useState('')
  const [selectedVersion, setSelectedVersion] = useState<MarketVersion | null>(null)
  const [selectedServer, setSelectedServer] = useState<ServerInfo | null>(null)
  const [loadingServer, setLoadingServer] = useState(true)

  const handleSearch = async () => {
    if (!searchQuery.trim()) {
      setStatusMsg('请输入搜索关键词')
      return
    }
    setSearching(true)
    setStatusMsg('')
    setSelectedProject(null)
    setVersions([])
    setInstallResult(null)
    try {
      const results = await searchMarket(searchQuery, 20)
      setProjects(results)
      if (results.length === 0) {
        setStatusMsg('未找到匹配的插件')
      }
    } catch (e) {
      setStatusMsg(`搜索失败：${(e as Error).message}`)
    } finally {
      setSearching(false)
    }
  }

  const handleSelectProject = async (project: MarketProject) => {
    setSelectedProject(project)
    setVersions([])
    setInstallResult(null)
    try {
      const vers = await getMarketVersions(project.id)
      setVersions(vers)
    } catch (e) {
      setStatusMsg(`获取版本失败：${(e as Error).message}`)
    }
  }

  const handleInstall = async () => {
    if (!selectedProject || !selectedVersion) return
    const serverPath = selectedServer?.workingDirectory ?? ''
    if (!serverPath) {
      setStatusMsg('❌ 未检测到选中的服务器，请先在仪表盘选择一台服务器')
      return
    }
    setInstalling(true)
    setInstallResult(null)
    try {
      const result = await installPlugin(selectedVersion, serverPath)
      setInstallResult(result)
      if (result.success) {
        setStatusMsg(`✅ 插件 ${selectedProject.name} v${selectedVersion.versionNumber} 安装成功`)
        loadInstalledPlugins()
      } else {
        setStatusMsg(`❌ 安装失败：${result.error}`)
      }
    } catch (e) {
      setStatusMsg(`❌ 安装失败：${(e as Error).message}`)
    } finally {
      setInstalling(false)
    }
  }

  const loadInstalledPlugins = useCallback(async () => {
    const serverPath = selectedServer?.workingDirectory ?? ''
    if (!serverPath) {
      setInstalledPlugins([])
      return
    }
    try {
      const plugins = await getInstalledPlugins(serverPath)
      setInstalledPlugins(plugins)
    } catch (e) {
      console.error('加载已安装插件失败:', e)
    }
  }, [selectedServer])

  const loadSelectedServer = useCallback(async () => {
    try {
      setLoadingServer(true)
      const server = await getSelectedServer()
      setSelectedServer(server)
    } catch (e) {
      console.error('获取选中服务器失败:', e)
      setSelectedServer(null)
    } finally {
      setLoadingServer(false)
    }
  }, [])

  useEffect(() => {
    loadSelectedServer()
  }, [loadSelectedServer])

  useEffect(() => {
    if (!loadingServer) {
      loadInstalledPlugins()
    }
  }, [loadInstalledPlugins, loadingServer])

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') handleSearch()
  }

  return (
    <div className="md-page-enter p-4 pb-8 max-w-5xl mx-auto">
      <div className="flex items-center mb-4">
        <FaStore size={32} style={{ color: 'var(--md-accent-text)', marginRight: 12 }} />
        <div>
          <h1 style={{ fontSize: 22, fontWeight: 700, color: 'var(--md-body)' }}>插件市场</h1>
          <p style={{ fontSize: 13, color: 'var(--md-body-light)' }}>
            浏览和安装 Minecraft 服务器插件
          </p>
        </div>
      </div>

      {statusMsg && (
        <div
          style={{
            marginBottom: 12,
            padding: '10px 14px',
            background: statusMsg.startsWith('✅')
              ? 'var(--md-success-subtle-background)'
              : 'var(--md-danger-subtle-background)',
            borderRadius: 'var(--md-radius)',
            fontSize: 13,
          }}
        >
          {statusMsg}
        </div>
      )}

      {/* 当前服务器上下文提示 */}
      <div
        style={{
          marginBottom: 12,
          padding: '10px 14px',
          background: selectedServer
            ? 'var(--md-primary-subtle-background)'
            : 'var(--md-warning-subtle-background, rgba(255, 193, 7, 0.1))',
          borderRadius: 'var(--md-radius)',
          fontSize: 12,
          display: 'flex',
          alignItems: 'center',
          gap: 8,
        }}
      >
        <FaServer size={14} style={{ color: selectedServer ? 'var(--md-primary)' : 'var(--md-warning, #f39c12)' }} />
        {loadingServer ? (
          <span style={{ color: 'var(--md-body-light)' }}>正在加载服务器信息...</span>
        ) : selectedServer ? (
          <span style={{ color: 'var(--md-body)' }}>
            当前服务器：<strong>{selectedServer.displayName}</strong>
            <span style={{ color: 'var(--md-body-light)', marginLeft: 8 }}>
              ({selectedServer.workingDirectory})
            </span>
          </span>
        ) : (
          <span style={{ color: 'var(--md-warning, #f39c12)' }}>
            未检测到选中的服务器。请先在仪表盘选择一台服务器，插件将安装到该服务器的 plugins 目录。
          </span>
        )}
      </div>

      {/* 搜索栏 */}
      <div className="md-card p-4 mb-4">
        <div className="flex gap-2">
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            onKeyPress={handleKeyPress}
            placeholder="搜索插件，如：iris, essentials, vault..."
            style={{
              flex: 1,
              padding: '10px 14px',
              borderRadius: 8,
              border: '1px solid var(--md-subtle-border)',
              background: 'var(--md-card-hover)',
              color: 'var(--md-body)',
              fontSize: 14,
            }}
          />
          <button
            className="md-btn md-btn-primary"
            onClick={handleSearch}
            disabled={searching}
          >
            <FaMagnifyingGlass style={{ marginRight: 6 }} />
            {searching ? '搜索中...' : '搜索'}
          </button>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* 搜索结果 */}
        <div className="md-card md-card-elevated p-5">
          <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}>
            搜索结果 ({projects.length})
          </h2>

          {projects.length === 0 ? (
            <div style={{ textAlign: 'center', padding: 30, color: 'var(--md-body-light)' }}>
              <FaMagnifyingGlass size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
              <div style={{ fontSize: 13 }}>
                {searching ? '正在搜索...' : '输入关键词开始搜索'}
              </div>
            </div>
          ) : (
            <div className="space-y-2 max-h-96 overflow-y-auto">
              {projects.map((project) => (
                <div
                  key={project.id}
                  className="md-card"
                  style={{
                    padding: 12,
                    cursor: 'pointer',
                    borderColor: selectedProject?.id === project.id ? 'var(--md-accent-text)' : 'transparent',
                    borderWidth: 2,
                  }}
                  onClick={() => handleSelectProject(project)}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    {project.iconUrl ? (
                      <img
                        src={project.iconUrl}
                        alt={project.name}
                        style={{ width: 36, height: 36, borderRadius: 6 }}
                      />
                    ) : (
                      <div
                        style={{
                          width: 36,
                          height: 36,
                          borderRadius: 6,
                          background: 'var(--md-primary-subtle-background)',
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                        }}
                      >
                        <FaStore size={16} style={{ color: 'var(--md-accent-text)' }} />
                      </div>
                    )}
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--md-body)' }}>
                        {project.name}
                      </div>
                      {project.description && (
                        <div
                          style={{
                            fontSize: 11,
                            color: 'var(--md-body-light)',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }}
                        >
                          {project.description}
                        </div>
                      )}
                      <div style={{ fontSize: 10, color: 'var(--md-body-lighter)', marginTop: 2 }}>
                        ⬇ {project.downloads?.toLocaleString() ?? 0} · 🔼 {project.followers?.toLocaleString() ?? 0}关注
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* 版本详情 */}
        <div className="md-card md-card-elevated p-5">
          <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}>
            <FaDownload style={{ marginRight: 6 }} />
            版本详情
          </h2>

          {!selectedProject ? (
            <div style={{ textAlign: 'center', padding: 30, color: 'var(--md-body-light)' }}>
              <FaServer size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
              <div style={{ fontSize: 13 }}>选择一个插件查看可用版本</div>
            </div>
          ) : (
            <>
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--md-body)' }}>
                  {selectedProject.name}
                </div>
                {selectedProject.description && (
                  <div style={{ fontSize: 12, color: 'var(--md-body-light)', marginTop: 4 }}>
                    {selectedProject.description}
                  </div>
                )}
              </div>

              <div style={{ marginBottom: 16 }}>
                <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 6 }}>
                  选择版本
                </label>
                <select
                  value={selectedVersion?.id ?? ''}
                  onChange={(e) => {
                    const v = versions.find((v) => v.id === e.target.value)
                    setSelectedVersion(v ?? null)
                  }}
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    borderRadius: 6,
                    border: '1px solid var(--md-subtle-border)',
                    background: 'var(--md-card-hover)',
                    color: 'var(--md-body)',
                  }}
                >
                  <option value="">-- 请选择版本 --</option>
                  {versions.map((v) => (
                    <option key={v.id} value={v.id}>
                      v{v.versionNumber} {v.releasedAt ? `(${new Date(v.releasedAt).toLocaleDateString()})` : ''}
                    </option>
                  ))}
                </select>
              </div>

              {selectedVersion?.changelog && (
                <div
                  style={{
                    marginBottom: 16,
                    padding: 12,
                    borderRadius: 8,
                    backgroundColor: 'var(--md-card-hover)',
                    fontSize: 12,
                    color: 'var(--md-body)',
                    maxHeight: 120,
                    overflowY: 'auto',
                  }}
                >
                  <div style={{ fontSize: 11, color: 'var(--md-body-light)', marginBottom: 6 }}>
                    更新日志：
                  </div>
                  <div style={{ whiteSpace: 'pre-wrap' }}>{selectedVersion.changelog}</div>
                </div>
              )}

              {installResult && (
                <div
                  style={{
                    marginBottom: 12,
                    padding: '10px',
                    borderRadius: 6,
                    backgroundColor: installResult.success
                      ? 'var(--md-success-subtle-background)'
                      : 'var(--md-danger-subtle-background)',
                    fontSize: 12,
                  }}
                >
                  {installResult.success ? (
                    <span style={{ color: 'var(--md-success-text)' }}>
                      <FaCheck style={{ marginRight: 4 }} />
                      安装成功！已备份原文件。
                    </span>
                  ) : (
                    <span style={{ color: 'var(--md-danger-text)' }}>
                      ❌ {installResult.error}
                    </span>
                  )}
                </div>
              )}

              <button
                className="md-btn md-btn-primary"
                onClick={handleInstall}
                disabled={!selectedVersion || installing}
                style={{ width: '100%' }}
              >
                <FaDownload style={{ marginRight: 6 }} />
                {installing ? '安装中...' : `安装 v${selectedVersion?.versionNumber ?? '???'}`}
              </button>
            </>
          )}
        </div>
      </div>

      {/* 已安装插件 */}
      <div className="md-card p-5 mt-4">
        <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}>
          <FaCheck style={{ marginRight: 6 }} />
          已安装插件 ({installedPlugins.length})
        </h2>

        {installedPlugins.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 20, color: 'var(--md-body-light)', fontSize: 12 }}>
            暂无已安装的插件
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-2">
            {installedPlugins.map((plugin) => (
              <div
                key={plugin.id}
                className="md-card"
                style={{ padding: '10px 14px', display: 'flex', alignItems: 'center', gap: 10 }}
              >
                <div
                  style={{
                    width: 32,
                    height: 32,
                    borderRadius: 6,
                    background: 'var(--md-success-subtle-background)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  <FaCheck size={12} style={{ color: 'var(--md-success-text)' }} />
                </div>
                <div>
                  <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--md-body)' }}>
                    {plugin.projectName}
                  </div>
                  <div style={{ fontSize: 11, color: 'var(--md-body-light)' }}>
                    v{plugin.version} · 安装于 {new Date(plugin.installedAt).toLocaleDateString()}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
