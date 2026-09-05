import { useState, useEffect, useCallback } from 'react'
import {
  FaClock,
  FaPlay,
  FaPlus,
  FaTrash,
  FaRotate,
  FaChevronDown,
  FaChevronUp,
  FaCalendar,
  FaBolt,
  FaList,
} from 'react-icons/fa6'
import {
  getScheduledTasks,
  addScheduledTask,
  deleteScheduledTask,
  runScheduledTaskNow,
  getSchedulerHistory,
} from '@/utils/bridge'
// TODO: 启用/禁用任务切换 —— 当前 bridge 仅提供 scheduler.list / add / delete / runNow / history，
//       缺少 scheduler.update 或 scheduler.setEnabled 等修改任务状态的 API。
//       待后端补齐后，应在任务列表项中添加开关控件，调用对应 bridge action 切换 enabled 状态。
import type { ScheduledTask, ExecutionRecord, TriggerType, ActionType } from '@/types/bridge'

export function SchedulerPage(): JSX.Element {
  const [tasks, setTasks] = useState<ScheduledTask[]>([])
  const [history, setHistory] = useState<ExecutionRecord[]>([])
  const [showAddModal, setShowAddModal] = useState(false)
  const [loading, setLoading] = useState(false)
  const [statusMsg, setStatusMsg] = useState('')
  const [expandedTask, setExpandedTask] = useState<string | null>(null)

  // 新任务表单
  const [newTask, setNewTask] = useState({
    name: '',
    triggerType: 'Interval' as TriggerType,
    intervalMinutes: 30,
    cronExpression: '0 9 * * MON-FRI',
    actionType: 'SendNotification' as ActionType,
    actionMessage: '定时任务提醒',
  })

  const loadTasks = useCallback(async () => {
    setLoading(true)
    try {
      const list = await getScheduledTasks()
      setTasks(list)
    } catch (e) {
      setStatusMsg(`加载任务失败：${(e as Error).message}`)
    } finally {
      setLoading(false)
    }
  }, [])

  const loadHistory = useCallback(async () => {
    try {
      const records = await getSchedulerHistory(50)
      setHistory(records)
    } catch (e) {
      console.error('加载历史失败:', e)
    }
  }, [])

  useEffect(() => {
    loadTasks()
    loadHistory()
    const interval = setInterval(() => {
      loadTasks()
      loadHistory()
    }, 30000)
    return () => clearInterval(interval)
  }, [loadTasks, loadHistory])

  const handleAddTask = async () => {
    if (!newTask.name.trim()) {
      setStatusMsg('请输入任务名称')
      return
    }

    const trigger = newTask.triggerType === 'Interval'
      ? { type: 'Interval' as TriggerType, interval: `PT${newTask.intervalMinutes}M` }
      : newTask.triggerType === 'Cron'
        ? { type: 'Cron' as TriggerType, cronExpression: newTask.cronExpression }
        : { type: 'OneTime' as TriggerType, oneTimeAt: new Date(Date.now() + 3600000).toISOString() }

    const action = {
      type: newTask.actionType,
      commandOrPath: newTask.actionMessage,
    }

    try {
      await addScheduledTask({
        id: '',
        name: newTask.name,
        enabled: true,
        trigger,
        action,
        maxConsecutiveFailures: 3,
        consecutiveFailures: 0,
        totalRunCount: 0,
      } as ScheduledTask)

      setStatusMsg(`✅ 任务「${newTask.name}」已添加`)
      setShowAddModal(false)
      setNewTask({
        name: '',
        triggerType: 'Interval',
        intervalMinutes: 30,
        cronExpression: '0 9 * * MON-FRI',
        actionType: 'SendNotification',
        actionMessage: '定时任务提醒',
      })
      loadTasks()
    } catch (e) {
      setStatusMsg(`❌ 添加任务失败：${(e as Error).message}`)
    }
  }

  const handleDelete = async (id: string) => {
    if (!window.confirm('确定删除此任务？')) return
    try {
      await deleteScheduledTask(id)
      setStatusMsg('✅ 任务已删除')
      loadTasks()
    } catch (e) {
      setStatusMsg(`❌ 删除失败：${(e as Error).message}`)
    }
  }

  const handleRunNow = async (id: string) => {
    try {
      await runScheduledTaskNow(id)
      setStatusMsg('✅ 任务已触发执行')
      setTimeout(() => {
        loadTasks()
        loadHistory()
      }, 1000)
    } catch (e) {
      setStatusMsg(`❌ 执行失败：${(e as Error).message}`)
    }
  }

  const getTriggerDisplay = (task: ScheduledTask): string => {
    switch (task.trigger.type) {
      case 'Interval':
        const match = task.trigger.interval?.match(/PT(\d+)M/)
        return match ? `每 ${match[1]} 分钟` : task.trigger.interval ?? '未知间隔'
      case 'Cron':
        return `Cron: ${task.trigger.cronExpression}`
      case 'OneTime':
        return `一次性：${task.trigger.oneTimeAt ? new Date(task.trigger.oneTimeAt).toLocaleString() : '未知时间'}`
      default:
        return '未知触发'
    }
  }

  const getActionDisplay = (task: ScheduledTask): string => {
    switch (task.action.type) {
      case 'SendNotification':
        return `📢 通知：${task.action.commandOrPath ?? '无消息'}`
      case 'RunCommand':
        return `⚡ 命令：${task.action.commandOrPath ?? '无命令'}`
      case 'Backup':
        return '💾 备份'
      default:
        return '未知动作'
    }
  }

  const getStatusColor = (status?: string): string => {
    switch (status) {
      case 'Completed': return 'var(--md-success-text)'
      case 'Failed': return 'var(--md-danger-text)'
      case 'Running': return 'var(--md-warning-text)'
      default: return 'var(--md-body-light)'
    }
  }

  return (
    <div className="md-page-enter p-4 pb-8 max-w-4xl mx-auto">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center">
          <FaClock size={32} style={{ color: 'var(--md-accent-text)', marginRight: 12 }} />
          <div>
            <h1 style={{ fontSize: 22, fontWeight: 700, color: 'var(--md-body)' }}>计划任务</h1>
            <p style={{ fontSize: 13, color: 'var(--md-body-light)' }}>
              定时执行通知、命令或备份操作
            </p>
          </div>
        </div>
        <button
          className="md-btn md-btn-primary"
          onClick={() => setShowAddModal(true)}
        >
          <FaPlus size={14} style={{ marginRight: 6 }} />
          新建任务
        </button>
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

      {/* 任务列表 */}
      <div className="md-card md-card-elevated p-5 mb-4">
        <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}>
          <FaList style={{ marginRight: 6 }} />
          任务列表 ({tasks.length})
        </h2>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 40, color: 'var(--md-body-light)' }}>
            加载中...
          </div>
        ) : tasks.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 40, color: 'var(--md-body-light)' }}>
            <FaCalendar size={40} style={{ opacity: 0.3, marginBottom: 12 }} />
            <div>暂无计划任务</div>
            <div style={{ fontSize: 12, marginTop: 4 }}>点击"新建任务"添加第一个定时任务</div>
          </div>
        ) : (
          <div className="space-y-2">
            {tasks.map((task) => (
              <div
                key={task.id}
                className="md-card"
                style={{ padding: 12 }}
              >
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    cursor: 'pointer',
                  }}
                  onClick={() => setExpandedTask(expandedTask === task.id ? null : task.id)}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                    <div
                      style={{
                        width: 32,
                        height: 32,
                        borderRadius: 8,
                        background: task.enabled ? 'var(--md-primary-subtle-background)' : 'var(--md-subtle-background)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        opacity: task.enabled ? 1 : 0.5,
                      }}
                    >
                      <FaClock size={14} style={{ color: 'var(--md-accent-text)' }} />
                    </div>
                    <div>
                      <div
                        style={{
                          fontSize: 14,
                          fontWeight: 600,
                          color: 'var(--md-body)',
                          opacity: task.enabled ? 1 : 0.5,
                        }}
                      >
                        {task.name}
                      </div>
                      <div style={{ fontSize: 11, color: 'var(--md-body-light)' }}>
                        {getTriggerDisplay(task)} · {getActionDisplay(task)}
                      </div>
                    </div>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    {/* TODO: 启用/禁用开关 —— 待后端提供 scheduler.update API 后替换为可交互的 toggle 控件 */}
                    <span
                      style={{
                        fontSize: 10,
                        padding: '2px 8px',
                        borderRadius: 10,
                        backgroundColor: task.enabled
                          ? 'var(--md-success-subtle-background)'
                          : 'var(--md-subtle-background)',
                        color: task.enabled ? 'var(--md-success-text)' : 'var(--md-body-light)',
                        cursor: 'default',
                        opacity: 0.85,
                      }}
                      title="任务启用状态（切换功能即将开放）"
                    >
                      {task.enabled ? '启用' : '禁用'}
                    </span>
                    {task.lastStatus && (
                      <span
                        style={{
                          fontSize: 10,
                          padding: '2px 8px',
                          borderRadius: 10,
                          backgroundColor: 'var(--md-card-hover)',
                          color: getStatusColor(task.lastStatus),
                        }}
                      >
                        {task.lastStatus}
                      </span>
                    )}
                    {expandedTask === task.id ? (
                      <FaChevronUp size={12} style={{ color: 'var(--md-body-light)' }} />
                    ) : (
                      <FaChevronDown size={12} style={{ color: 'var(--md-body-light)' }} />
                    )}
                  </div>
                </div>

                {expandedTask === task.id && (
                  <div
                    style={{
                      marginTop: 12,
                      paddingTop: 12,
                      borderTop: '1px solid var(--md-card-subtle-border)',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                    }}
                  >
                    <button
                      className="md-btn md-btn-outlined"
                      style={{ fontSize: 12 }}
                      onClick={() => handleRunNow(task.id)}
                    >
                      <FaPlay size={10} style={{ marginRight: 4 }} />
                      立即执行
                    </button>
                    <button
                      className="md-btn md-btn-danger"
                      style={{ fontSize: 12 }}
                      onClick={() => handleDelete(task.id)}
                    >
                      <FaTrash size={10} style={{ marginRight: 4 }} />
                      删除
                    </button>
                    <div style={{ marginLeft: 'auto', fontSize: 11, color: 'var(--md-body-light)' }}>
                      下次运行：{task.nextRunTime ? new Date(task.nextRunTime).toLocaleString() : 'N/A'}
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* 执行历史 */}
      <div className="md-card p-5">
        <h2 className="md-section-title" style={{ color: 'var(--md-accent-text)', margin: '0 0 12px 0' }}>
          <FaRotate style={{ marginRight: 6 }} />
          执行历史 ({history.length})
        </h2>

        {history.length === 0 ? (
          <div style={{ textAlign: 'center', padding: 20, color: 'var(--md-body-light)', fontSize: 12 }}>
            暂无执行记录
          </div>
        ) : (
          <div className="space-y-1 max-h-64 overflow-y-auto">
            {history.map((record, idx) => (
              <div
                key={idx}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 8,
                  padding: '6px 10px',
                  borderRadius: 6,
                  backgroundColor: 'var(--md-card-hover)',
                  fontSize: 12,
                }}
              >
                <FaBolt size={10} style={{ color: getStatusColor(record.status) }} />
                <span style={{ color: 'var(--md-body)', fontWeight: 500 }}>{record.taskName}</span>
                <span
                  style={{
                    fontSize: 10,
                    padding: '1px 6px',
                    borderRadius: 8,
                    backgroundColor: 'var(--md-card-background)',
                    color: getStatusColor(record.status),
                  }}
                >
                  {record.status}
                </span>
                <span style={{ color: 'var(--md-body-light)', marginLeft: 'auto' }}>
                  {new Date(record.startedAt).toLocaleString()}
                </span>
                {record.errorMessage && (
                  <span style={{ color: 'var(--md-danger-text)', fontSize: 10 }}>
                    {record.errorMessage}
                  </span>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* 新建任务弹窗 */}
      {showAddModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0,0,0,0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
          }}
          onClick={() => setShowAddModal(false)}
        >
          <div
            className="md-card"
            style={{ width: 400, padding: 24 }}
            onClick={(e) => e.stopPropagation()}
          >
            <h3 style={{ fontSize: 16, fontWeight: 600, color: 'var(--md-body)', marginBottom: 16 }}>
              <FaPlus style={{ marginRight: 6 }} />
              新建计划任务
            </h3>

            <div style={{ marginBottom: 12 }}>
              <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 4 }}>
                任务名称
              </label>
              <input
                type="text"
                className="md-input"
                value={newTask.name}
                onChange={(e) => setNewTask({ ...newTask, name: e.target.value })}
                placeholder="如：每 30 分钟发送一次状态通知"
                style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--md-subtle-border)', background: 'var(--md-card-hover)', color: 'var(--md-body)' }}
              />
            </div>

            <div style={{ marginBottom: 12 }}>
              <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 4 }}>
                触发方式
              </label>
              <select
                className="md-select"
                value={newTask.triggerType}
                onChange={(e) => setNewTask({ ...newTask, triggerType: e.target.value as TriggerType })}
                style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--md-subtle-border)', background: 'var(--md-card-hover)', color: 'var(--md-body)' }}
              >
                <option value="Interval">间隔触发</option>
                <option value="Cron">Cron 表达式</option>
                <option value="OneTime">一次性</option>
              </select>
            </div>

            {newTask.triggerType === 'Interval' && (
              <div style={{ marginBottom: 12 }}>
                <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 4 }}>
                  间隔（分钟）
                </label>
                <input
                  type="number"
                  min={1}
                  value={newTask.intervalMinutes}
                  onChange={(e) => setNewTask({ ...newTask, intervalMinutes: Number(e.target.value) })}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--md-subtle-border)', background: 'var(--md-card-hover)', color: 'var(--md-body)' }}
                />
              </div>
            )}

            {newTask.triggerType === 'Cron' && (
              <div style={{ marginBottom: 12 }}>
                <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 4 }}>
                  Cron 表达式
                </label>
                <input
                  type="text"
                  value={newTask.cronExpression}
                  onChange={(e) => setNewTask({ ...newTask, cronExpression: e.target.value })}
                  placeholder="0 9 * * MON-FRI"
                  style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--md-subtle-border)', background: 'var(--md-card-hover)', color: 'var(--md-body)' }}
                />
              </div>
            )}

            <div style={{ marginBottom: 12 }}>
              <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 4 }}>
                动作类型
              </label>
              <select
                className="md-select"
                value={newTask.actionType}
                onChange={(e) => setNewTask({ ...newTask, actionType: e.target.value as ActionType })}
                style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--md-subtle-border)', background: 'var(--md-card-hover)', color: 'var(--md-body)' }}
              >
                <option value="SendNotification">发送通知</option>
                <option value="RunCommand">执行命令</option>
                <option value="Backup">执行备份</option>
              </select>
            </div>

            <div style={{ marginBottom: 16 }}>
              <label style={{ fontSize: 12, color: 'var(--md-body-light)', display: 'block', marginBottom: 4 }}>
                消息/命令内容
              </label>
              <input
                type="text"
                value={newTask.actionMessage}
                onChange={(e) => setNewTask({ ...newTask, actionMessage: e.target.value })}
                placeholder={newTask.actionType === 'SendNotification' ? '通知消息内容' : '要执行的命令'}
                style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--md-subtle-border)', background: 'var(--md-card-hover)', color: 'var(--md-body)' }}
              />
            </div>

            <div style={{ display: 'flex', gap: 8 }}>
              <button
                className="md-btn md-btn-outlined"
                style={{ flex: 1 }}
                onClick={() => setShowAddModal(false)}
              >
                取消
              </button>
              <button
                className="md-btn md-btn-primary"
                style={{ flex: 1 }}
                onClick={handleAddTask}
              >
                创建
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
