import { useVirtualizer } from '@tanstack/react-virtual';
import { useEffect, useMemo, useRef, useState, type DragEvent } from 'react';

import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { useAuthStore } from '../../../core/state';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { useProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';

import { groupTaskCards, getTaskCardRisks, type TaskCardGroupBy } from './taskCardProjectionModel';
import { moveTaskGroup, orderTaskGroups, readTaskGroupPreference, taskGroupPreferenceKey, toggleTaskGroup, writeTaskGroupPreference, type TaskGroupPreference } from './taskGroupPreferenceModel';
import type { TaskGroupDropTarget } from './taskMoveIntent';

export interface TaskCardDragHandlers {
  draggedTaskId?: string;
  keyboardHintId?: string;
  keyboardTaskId?: string;
  onDragEnd: () => void;
  onDragOver: (event: DragEvent<HTMLElement>) => void;
  onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTaskListItem) => void;
  onDrop: (event: DragEvent<HTMLElement>, target: { kind: 'before' | 'child'; task: ProjectManagementTaskListItem } | { kind: 'root' } | { kind: 'status'; status: string } | TaskGroupDropTarget) => void;
  onKeyboardCancel?: () => void;
  onKeyboardMove?: (task: ProjectManagementTaskListItem, direction: 'next' | 'previous') => void;
  onKeyboardStart?: (task: ProjectManagementTaskListItem) => void;
}

interface TaskCardActions {
  onAddChildTask?: (task: ProjectManagementTaskListItem) => void;
  onCompleteTask?: (task: ProjectManagementTaskListItem) => void;
  onDeleteTask?: (task: ProjectManagementTaskListItem) => void;
  pendingTaskId?: string;
}

interface TaskCardProjectionProps extends TaskCardActions {
  drag?: TaskCardDragHandlers;
  milestoneLabels?: Readonly<Record<string, string>>;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  projectId: string;
  participantLabels?: Readonly<Record<string, string>>;
  rows: ProjectManagementTaskListItem[];
  selectedTaskIds: ReadonlySet<string>;
  groupBy?: TaskCardGroupBy;
}

export function TaskCardProjection({ drag, groupBy, milestoneLabels, onAddChildTask, onCompleteTask, onDeleteTask, onSelectTask, onToggleTaskSelection, participantLabels, pendingTaskId, projectId, rows, selectedTaskIds }: TaskCardProjectionProps) {
  const scope = useProjectManagementWorkspaceScope();
  const userId = useAuthStore((current) => current.user?.userId ?? '');
  const preferenceKey = useMemo(() => groupBy && userId && scope.tenantId && scope.appCode && projectId
    ? taskGroupPreferenceKey(userId, scope.tenantId, scope.appCode, projectId, 'card', groupBy)
    : '', [groupBy, projectId, scope.appCode, scope.tenantId, userId]);
  const [preference, setPreference] = useState<TaskGroupPreference>(() => readTaskGroupPreference(''));
  useEffect(() => setPreference(readTaskGroupPreference(preferenceKey)), [preferenceKey]);
  useEffect(() => {
    if (preferenceKey) writeTaskGroupPreference(preferenceKey, preference);
  }, [preference, preferenceKey]);
  const groups = useMemo(() => orderTaskGroups(groupTaskCards(rows, groupBy), preference), [groupBy, preference, rows]);
  const virtualRows = useMemo(() => groups.flatMap((group) => {
    const chunks: Array<{ kind: 'cards'; groupKey: string; rows: ProjectManagementTaskListItem[] } | { kind: 'header'; groupKey: string; label: string; count: number; collapsed: boolean }> = [{ kind: 'header', groupKey: group.key, label: group.label, count: group.rows.length, collapsed: preference.collapsedKeys.includes(group.key) }];
    if (preference.collapsedKeys.includes(group.key)) return chunks;
    for (let index = 0; index < group.rows.length; index += 3) chunks.push({ kind: 'cards', groupKey: group.key, rows: group.rows.slice(index, index + 3) });
    return chunks;
  }), [groups, preference.collapsedKeys]);
  const scrollRef = useRef<HTMLDivElement>(null);
  const virtualizer = useVirtualizer({
    count: virtualRows.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: (index) => virtualRows[index]?.kind === 'header' ? 44 : 232,
    overscan: 4,
  });

  if (!rows.length) return <div className="pm-projection-empty" role="status">当前筛选没有匹配的任务卡片。</div>;

  return <div aria-label="任务卡片列表" className="pm-card-virtual-list" ref={scrollRef} role="list">
    <div style={{ height: virtualizer.getTotalSize(), position: 'relative' }}>
      {virtualizer.getVirtualItems().map((virtualRow) => {
        const item = virtualRows[virtualRow.index];
        if (!item) return null;
        return <div data-index={virtualRow.index} key={`${item.groupKey}-${virtualRow.index}`} ref={virtualizer.measureElement} role="presentation" style={{ position: 'absolute', left: 0, right: 0, top: 0, transform: `translateY(${virtualRow.start}px)` }}>
          {item.kind === 'header'
            ? <div className="pm-task-card-group-heading" onDragOver={drag?.onDragOver} onDrop={groupBy && groupBy !== 'status' && groupBy !== 'priority' ? (event) => drag?.onDrop(event, { kind: 'group', groupBy, groupValue: item.groupKey }) : undefined}>
              <button aria-expanded={!item.collapsed} aria-label={`${item.collapsed ? '展开' : '折叠'}分组 ${item.label}`} className="pm-task-card-group-heading__toggle" onClick={() => setPreference((current) => toggleTaskGroup(current, item.groupKey))} type="button">{item.collapsed ? '▸' : '▾'}</button>
              <strong>{item.label}</strong><span aria-label={`${item.count} 项${groupBy === 'label' ? '，多标签任务可能在多个分组展示' : ''}`}>{item.count} 项</span>
              {groupBy ? <span className="pm-task-card-group-heading__actions"><button aria-label={`上移分组 ${item.label}`} onClick={() => setPreference((current) => moveTaskGroup(current, item.groupKey, groups[Math.max(0, groups.findIndex((group) => group.key === item.groupKey) - 1)]?.key))} type="button">↑</button><button aria-label={`下移分组 ${item.label}`} onClick={() => setPreference((current) => moveTaskGroup(current, item.groupKey, groups[groups.findIndex((group) => group.key === item.groupKey) + 2]?.key))} type="button">↓</button></span> : null}
            </div>
            : <div className="pm-task-grid pm-task-grid--virtual">{item.rows.map((task) => <TaskCard
              drag={drag}
              key={task.id}
              milestoneLabels={milestoneLabels}
              onAddChildTask={onAddChildTask}
              onCompleteTask={onCompleteTask}
              onDeleteTask={onDeleteTask}
              onSelectTask={onSelectTask}
              onToggleTaskSelection={onToggleTaskSelection}
              participantLabels={participantLabels}
              pending={pendingTaskId === task.id}
              selected={selectedTaskIds.has(task.id)}
              task={task}
            />)}</div>}
        </div>;
      })}
    </div>
  </div>;
}

export function TaskCard({ drag, milestoneLabels, onAddChildTask, onChangeTaskStatus, onCompleteTask, onDeleteTask, onSelectTask, onToggleTaskSelection, participantLabels, pending = false, selected, statusOptions, task }: {
  drag?: TaskCardDragHandlers;
  milestoneLabels?: Readonly<Record<string, string>>;
  onAddChildTask?: (task: ProjectManagementTaskListItem) => void;
  onChangeTaskStatus?: (task: ProjectManagementTaskListItem, status: string) => void;
  onCompleteTask?: (task: ProjectManagementTaskListItem) => void;
  onDeleteTask?: (task: ProjectManagementTaskListItem) => void;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  participantLabels?: Readonly<Record<string, string>>;
  pending?: boolean;
  selected: boolean;
  statusOptions?: readonly string[];
  task: ProjectManagementTaskListItem;
}) {
  const risks = getTaskCardRisks(task);
  const participantIds = task.participantUserIds ?? [];
  const riskClass = risks.length ? risks.join(' ') : 'normal';
  return <article aria-describedby={drag?.keyboardHintId} aria-grabbed={drag?.keyboardTaskId === task.id || undefined} aria-label={`任务 ${task.title}`} className={`pm-task-card pm-task-card--risk-${riskClass}${selected ? ' is-selected' : ''}${drag?.draggedTaskId === task.id ? ' is-dragging' : ''}`} data-project-task-id={task.id} data-risk={riskClass} draggable={drag ? true : undefined} onDragEnd={drag?.onDragEnd} onDragOver={drag?.onDragOver} onDragStart={drag ? (event) => drag.onDragStart(event, task) : undefined} onDrop={drag ? (event) => drag.onDrop(event, { kind: 'before', task }) : undefined} onKeyDown={(event) => {
    const target = event.target as HTMLElement;
    if (['BUTTON', 'INPUT', 'SELECT', 'TEXTAREA', 'A'].includes(target.tagName)) return;
    if (event.key === 'Escape' && drag?.keyboardTaskId === task.id) {
      event.preventDefault();
      drag.onKeyboardCancel?.();
    } else if ((event.key === ' ' || event.key === 'Enter') && !drag?.keyboardTaskId) {
      event.preventDefault();
      drag?.onKeyboardStart?.(task);
    } else if (drag?.keyboardTaskId === task.id && event.key === 'ArrowRight') {
      event.preventDefault();
      drag.onKeyboardMove?.(task, 'next');
    } else if (drag?.keyboardTaskId === task.id && event.key === 'ArrowLeft') {
      event.preventDefault();
      drag.onKeyboardMove?.(task, 'previous');
    }
  }} role="listitem" tabIndex={0}>
    <div className="pm-task-card__content">
      <div className="pm-task-card__meta"><input aria-label={`选择任务 ${task.title}`} checked={selected} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><code>{task.taskCode}</code><span className="pm-task-card__risk-list">{risks.map((risk) => <span className={`pm-risk-chip pm-risk-chip--${risk}`} key={risk}>{riskLabel(risk)}</span>)}</span></div>
      <button className="pm-task-card__open" onClick={() => onSelectTask(task.id)} type="button">{task.title}</button>
      <p className="pm-task-card__summary">{task.summary || '暂无摘要'}</p>
      <div className="pm-task-card__signals"><StatusBadge status={task.status} /><PriorityBadge priority={task.priority} /><span>{task.progressPercent}%</span><span>{task.canStart ? '可开始' : task.blockedReason ?? '受阻塞'}</span></div>
      <div className="pm-progress"><progress max={100} value={task.progressPercent} /><span>{task.progressPercent}%</span></div>
      <div className="pm-task-card__details"><span>负责人：{task.assigneeUserId ? participantLabels?.[task.assigneeUserId] ?? task.assigneeUserId : '未分配'}</span><span>参与人：{participantIds.length ? participantIds.map((id) => participantLabels?.[id] ?? id).join('、') : '无'}</span><span>里程碑：{task.milestoneId ? milestoneLabels?.[task.milestoneId] ?? task.milestoneId : '未设置'}</span><span>计划：{formatDate(task.startDate)} → {formatDate(task.dueDate)}</span></div>
      <div className="pm-task-card__labels">{task.labels?.length ? task.labels.map((label) => <span key={label.id} style={{ borderColor: label.color, color: label.color }}>{label.labelName}</span>) : <span>无标签</span>}</div>
      {task.blockedReason ? <p className="pm-task-card__blocked">阻塞：{task.blockedReason}</p> : null}
      <div className="pm-task-card__actions">
        {onChangeTaskStatus && statusOptions?.length ? <label>
          <span className="sr-only">触屏操作：将任务移动到状态列</span>
          <select aria-label={`移动任务 ${task.title} 到状态列`} disabled={pending} onChange={(event) => { if (event.target.value) onChangeTaskStatus(task, event.target.value); event.currentTarget.value = ''; }} defaultValue="">
            <option value="">移动到列…</option>
            {statusOptions.filter((status) => status !== task.status).map((status) => <option key={status} value={status}>{statusLabel(status)}</option>)}
          </select>
        </label> : null}
        <PermissionButton code="project-management:task:edit" disabled={pending} onClick={() => onSelectTask(task.id)}>快速编辑</PermissionButton>
        {onCompleteTask ? <PermissionButton code="project-management:task:edit" disabled={pending || task.status === 'Done' || task.status === 'Cancelled'} onClick={() => onCompleteTask(task)}>完成</PermissionButton> : null}
        {onAddChildTask ? <PermissionButton code="project-management:task:add" disabled={pending} onClick={() => onAddChildTask(task)}>新增子任务</PermissionButton> : null}
        {onDeleteTask ? <PermissionButton code="project-management:task:delete" disabled={pending} onClick={() => onDeleteTask(task)}>删除</PermissionButton> : null}
      </div>
    </div>
    {drag ? <span className="pm-child-drop-zone" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task })}>作为子任务</span> : null}
  </article>;
}

function StatusBadge({ status }: { status: string }) {
  return <span className={`pm-status-badge pm-status-badge--${toKebabCase(status)}`}>{statusLabel(status)}</span>;
}

function PriorityBadge({ priority }: { priority: string }) {
  return <span className={`pm-priority-badge pm-priority-badge--${priority.toLowerCase()}`}>{priorityLabel(priority)}</span>;
}

function statusLabel(status: string): string {
  return ({ Todo: '待开始', InProgress: '进行中', Blocked: '受阻塞', Done: '已完成', Cancelled: '已取消' } as Record<string, string>)[status] ?? status;
}

function priorityLabel(priority: string): string {
  return ({ Low: '低', Medium: '中', High: '高', Urgent: '紧急' } as Record<string, string>)[priority] ?? priority;
}

function riskLabel(risk: string): string {
  return ({ overdue: '逾期', urgent: '紧急', blocked: '阻塞', done: '完成' } as Record<string, string>)[risk] ?? risk;
}

function formatDate(value: string | undefined): string {
  return value ? new Date(value).toLocaleDateString() : '未设置';
}

function toKebabCase(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
}
