import { useVirtualizer } from '@tanstack/react-virtual';
import { useMemo, useRef, type DragEvent } from 'react';

import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { groupTaskCards, getTaskCardRisks, type TaskCardGroupBy } from './taskCardProjectionModel';

export interface TaskCardDragHandlers {
  draggedTaskId?: string;
  onDragEnd: () => void;
  onDragOver: (event: DragEvent<HTMLElement>) => void;
  onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTaskListItem) => void;
  onDrop: (event: DragEvent<HTMLElement>, target: { kind: 'before' | 'child'; task: ProjectManagementTaskListItem } | { kind: 'root' } | { kind: 'status'; status: string }) => void;
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
  participantLabels?: Readonly<Record<string, string>>;
  rows: ProjectManagementTaskListItem[];
  selectedTaskIds: ReadonlySet<string>;
  groupBy?: TaskCardGroupBy;
}

export function TaskCardProjection({ drag, groupBy, milestoneLabels, onAddChildTask, onCompleteTask, onDeleteTask, onSelectTask, onToggleTaskSelection, participantLabels, pendingTaskId, rows, selectedTaskIds }: TaskCardProjectionProps) {
  const groups = useMemo(() => groupTaskCards(rows, groupBy), [groupBy, rows]);
  const virtualRows = useMemo(() => groups.flatMap((group) => {
    const chunks: Array<{ kind: 'cards'; groupKey: string; rows: ProjectManagementTaskListItem[] } | { kind: 'header'; groupKey: string; label: string; count: number }> = [{ kind: 'header', groupKey: group.key, label: group.label, count: group.rows.length }];
    for (let index = 0; index < group.rows.length; index += 3) chunks.push({ kind: 'cards', groupKey: group.key, rows: group.rows.slice(index, index + 3) });
    return chunks;
  }), [groups]);
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
            ? <div aria-label={`${item.label}，${item.count} 项`} className="pm-task-card-group-heading"><strong>{item.label}</strong><span>{item.count} 项</span></div>
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

export function TaskCard({ drag, milestoneLabels, onAddChildTask, onCompleteTask, onDeleteTask, onSelectTask, onToggleTaskSelection, participantLabels, pending = false, selected, task }: {
  drag?: TaskCardDragHandlers;
  milestoneLabels?: Readonly<Record<string, string>>;
  onAddChildTask?: (task: ProjectManagementTaskListItem) => void;
  onCompleteTask?: (task: ProjectManagementTaskListItem) => void;
  onDeleteTask?: (task: ProjectManagementTaskListItem) => void;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  participantLabels?: Readonly<Record<string, string>>;
  pending?: boolean;
  selected: boolean;
  task: ProjectManagementTaskListItem;
}) {
  const risks = getTaskCardRisks(task);
  const participantIds = task.participantUserIds ?? [];
  const riskClass = risks.length ? risks.join(' ') : 'normal';
  return <article aria-label={`任务 ${task.title}`} className={`pm-task-card pm-task-card--risk-${riskClass}${selected ? ' is-selected' : ''}${drag?.draggedTaskId === task.id ? ' is-dragging' : ''}`} data-risk={riskClass} draggable={drag ? true : undefined} onDragEnd={drag?.onDragEnd} onDragOver={drag?.onDragOver} onDragStart={drag ? (event) => drag.onDragStart(event, task) : undefined} onDrop={drag ? (event) => drag.onDrop(event, { kind: 'before', task }) : undefined} role="listitem">
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
