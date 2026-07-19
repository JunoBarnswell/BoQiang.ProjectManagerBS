import { useEffect, useRef, type ReactNode } from 'react';

import type {
  ProjectManagementTaskDependency,
  ProjectManagementTaskDetail,
  ProjectManagementTaskListItem,
  ProjectManagementTaskVersionConflictResponse,
} from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';

import { taskDetailSections, type TaskDetailSection } from './taskDetailDrawerModel';

interface TaskDetailDrawerProps {
  activeSection: TaskDetailSection;
  children: ReactNode;
  conflict?: ProjectManagementTaskVersionConflictResponse | null;
  conflictPending?: boolean;
  creating: boolean;
  errorMessage?: string;
  loading?: boolean;
  onClose: () => void;
  onKeepLocal: () => void;
  onOverwrite: () => void;
  onReload: () => void;
  onSectionChange: (section: TaskDetailSection) => void;
  open: boolean;
  selectedTask?: ProjectManagementTaskDetail;
  sectionContent?: ReactNode;
}

export function TaskDetailDrawer({
  activeSection,
  children,
  conflict,
  conflictPending = false,
  creating,
  errorMessage,
  loading = false,
  onClose,
  onKeepLocal,
  onOverwrite,
  onReload,
  onSectionChange,
  open,
  selectedTask,
  sectionContent,
}: TaskDetailDrawerProps) {
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const previousFocusRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;
    previousFocusRef.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    closeButtonRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onClose();
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('keydown', onKeyDown);
      previousFocusRef.current?.focus();
    };
  }, [onClose, open]);

  if (!open) return null;

  return <div className="pm-task-drawer__backdrop" role="presentation" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <aside aria-label={creating ? '新建任务' : `任务详情 ${selectedTask?.taskCode ?? ''}`} aria-modal="true" className="pm-task-drawer" role="dialog">
      <header className="pm-task-drawer__header">
        <div>
          <h2>{creating ? '新建任务' : selectedTask ? `${selectedTask.taskCode} · ${selectedTask.title}` : '任务详情'}</h2>
          <p>{creating ? '填写基本信息后保存任务' : `版本 ${selectedTask?.versionNo ?? '-'} · 详情数据按分区加载`}</p>
        </div>
        <button ref={closeButtonRef} aria-label="关闭任务详情" className="pm-task-drawer__close" type="button" onClick={onClose}>×</button>
      </header>
      {!creating ? <nav aria-label="任务详情分区" className="pm-task-drawer__tabs" role="tablist">
        {taskDetailSections.map((section) => <button
          aria-controls={`task-detail-panel-${section.key}`}
          aria-selected={activeSection === section.key}
          className={activeSection === section.key ? 'is-active' : ''}
          id={`task-detail-tab-${section.key}`}
          key={section.key}
          role="tab"
          tabIndex={activeSection === section.key ? 0 : -1}
          type="button"
          onClick={() => onSectionChange(section.key)}
        >{section.label}</button>)}
      </nav> : null}
      <div className="pm-task-drawer__body">
        {errorMessage ? <div className="pm-task-drawer__error" role="alert">{errorMessage}<button type="button" onClick={onReload}>重新加载</button></div> : null}
        {loading ? <div className="pm-task-drawer__loading" role="status">正在加载任务详情…</div> : null}
        {conflict ? <TaskConflictPanel conflict={conflict} pending={conflictPending} onKeepLocal={onKeepLocal} onOverwrite={onOverwrite} onReload={onReload} /> : null}
        <section aria-labelledby={`task-detail-tab-${activeSection}`} id={`task-detail-panel-${activeSection}`} role="tabpanel">
          {sectionContent ?? children}
        </section>
      </div>
    </aside>
  </div>;
}

function TaskConflictPanel({ conflict, onKeepLocal, onOverwrite, onReload, pending }: { conflict: ProjectManagementTaskVersionConflictResponse; onKeepLocal: () => void; onOverwrite: () => void; onReload: () => void; pending: boolean }) {
  return <section aria-label="任务并发冲突" className="pm-task-conflict" role="alert">
    <div><strong>任务已被其他用户修改</strong><p>服务器值、本地值和发生差异的字段均已保留；保存失败没有丢失当前输入。</p></div>
    {conflict.fieldConflicts.length ? <div className="pm-task-conflict__fields">{conflict.fieldConflicts.map((field) => <div className="pm-task-conflict__field" key={field.field}><strong>{field.displayName}</strong><span>服务器：{formatConflictValue(field.serverValue)}</span><span>本地：{formatConflictValue(field.localValue)}</span></div>)}</div> : <p>版本已变化，但本次提交字段没有可展示的值差异。</p>}
    <div className="pm-task-conflict__actions"><button type="button" onClick={onReload}>重新加载服务器值</button><button type="button" onClick={onKeepLocal}>保留本地内容</button><PermissionButton code="project-management:task:edit" disabled={pending} onClick={onOverwrite}>{pending ? '覆盖保存中…' : '确认覆盖服务器值'}</PermissionButton></div>
  </section>;
}

export function TaskDetailChildrenSection({ error, loading, onRetry, onSelect, tasks }: { error: boolean; loading: boolean; onRetry: () => void; onSelect: (taskId: string) => void; tasks: ProjectManagementTaskListItem[] }) {
  if (loading) return <p className="pm-task-drawer__empty">正在加载子任务…</p>;
  if (error) return <p className="pm-task-drawer__error">子任务加载失败。<button type="button" onClick={onRetry}>重试</button></p>;
  if (!tasks.length) return <p className="pm-task-drawer__empty">暂无子任务。</p>;
  return <div className="pm-task-drawer__list">{tasks.map((task) => <button className="pm-task-drawer__list-item" key={task.id} type="button" onClick={() => onSelect(task.id)}><span><code>{task.taskCode}</code> {task.title}</span><span>{task.status} · {task.progressPercent}%</span></button>)}</div>;
}

export function TaskDetailDependenciesSection({ dependencies, error, labels, loading, onRetry }: { dependencies: ProjectManagementTaskDependency[]; error: boolean; labels: Readonly<Record<string, string>>; loading: boolean; onRetry: () => void }) {
  if (loading) return <p className="pm-task-drawer__empty">正在加载依赖…</p>;
  if (error) return <p className="pm-task-drawer__error">依赖加载失败。<button type="button" onClick={onRetry}>重试</button></p>;
  if (!dependencies.length) return <p className="pm-task-drawer__empty">暂无依赖。</p>;
  return <div className="pm-task-drawer__list">{dependencies.map((dependency) => <div className="pm-task-drawer__list-item" key={dependency.id}><span>{labels[dependency.predecessorTaskId] ?? dependency.predecessorTaskId} → {labels[dependency.successorTaskId] ?? dependency.successorTaskId}</span><span>{dependency.dependencyType} · {dependency.lagMinutes} 分钟</span></div>)}</div>;
}

function formatConflictValue(value: unknown): string {
  if (value === undefined || value === null || value === '') return '—';
  if (typeof value === 'object') return JSON.stringify(value);
  return String(value);
}
