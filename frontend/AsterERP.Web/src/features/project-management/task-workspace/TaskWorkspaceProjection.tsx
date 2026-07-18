import { useMemo, useState, type CSSProperties, type DragEvent } from 'react';

import type { ProjectManagementTask } from '../../../api/project-management/projectManagement.types';
import { DataTable } from '../../../shared/table/DataTable';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import type { TaskWorkspaceState } from '../state/taskWorkspaceState';

import type { TaskMoveDropTarget } from './taskMoveIntent';

interface TaskWorkspaceProjectionProps {
  onSelectTask: (taskId: string) => void;
  onMoveTask: (task: ProjectManagementTask, target: TaskMoveDropTarget) => void;
  onToggleTaskSelection: (taskId: string) => void;
  rows: ProjectManagementTask[];
  selectedTaskIds: ReadonlySet<string>;
  state: TaskWorkspaceState;
}

export function TaskWorkspaceProjection({ onMoveTask, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds, state }: TaskWorkspaceProjectionProps) {
  const [draggedTask, setDraggedTask] = useState<ProjectManagementTask>();
  const drag = {
    draggedTaskId: draggedTask?.id,
    onDragEnd: () => setDraggedTask(undefined),
    onDragOver: (event: DragEvent<HTMLElement>) => event.preventDefault(),
    onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTask) => {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', task.id);
      setDraggedTask(task);
    },
    onDrop: (event: DragEvent<HTMLElement>, target: TaskMoveDropTarget) => {
      event.preventDefault();
      event.stopPropagation();
      if (draggedTask) onMoveTask(draggedTask, target);
      setDraggedTask(undefined);
    },
  };
  const rootDropZone = <div aria-label="拖到此处成为顶级任务" className="pm-root-drop-zone" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'root' })}>拖到此处成为顶级任务</div>;

  if (state.viewKey === 'board') return <>{rootDropZone}<TaskBoardProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} /></>;
  if (state.viewKey === 'card') return <>{rootDropZone}<TaskCardProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} /></>;
  if (state.viewKey === 'gantt') return <>{rootDropZone}<TaskGanttProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} /></>;
  if (state.viewKey === 'calendar') return <>{rootDropZone}<TaskCalendarProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} /></>;
  return <>{rootDropZone}<TaskTableProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} state={state} /></>;
}

interface TaskDragHandlers {
  draggedTaskId?: string;
  onDragEnd: () => void;
  onDragOver: (event: DragEvent<HTMLElement>) => void;
  onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTask) => void;
  onDrop: (event: DragEvent<HTMLElement>, target: TaskMoveDropTarget) => void;
}

function TaskTableProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds, state }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds' | 'state'> & { drag: TaskDragHandlers }) {
  const columns = useMemo<DataTableColumn<ProjectManagementTask>[]>(() => [
    { key: 'select', title: '选择', width: '64px', render: (row) => <input aria-label={`选择任务 ${row.title}`} checked={selectedTaskIds.has(row.id)} type="checkbox" onChange={() => onToggleTaskSelection(row.id)} /> },
    { key: 'taskCode', title: '编码', width: '120px', responsivePriority: 100 },
    {
      key: 'title', title: '任务', responsivePriority: 100, render: (row) => (
        <div
          className={`pm-task-tree-title${drag.draggedTaskId === row.id ? ' is-dragging' : ''}`}
          draggable
          onDragEnd={drag.onDragEnd}
          onDragOver={drag.onDragOver}
          onDragStart={(event) => drag.onDragStart(event, row)}
          onDrop={(event) => drag.onDrop(event, { kind: 'before', task: row })}
          style={{ '--pm-task-depth': state.viewKey === 'tree' ? row.depth : 0 } as CSSProperties}
        >
          <span>{row.title}</span>
          <span className="pm-child-drop-zone" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task: row })}>作为子任务</span>
        </div>
      )
    },
    { key: 'status', title: '状态', width: '110px', render: (row) => <StatusBadge status={row.status} /> },
    { key: 'priority', title: '优先级', width: '96px', render: (row) => <PriorityBadge priority={row.priority} /> },
    { key: 'progressPercent', title: '进度', width: '138px', render: (row) => <Progress value={row.progressPercent} /> },
    { key: 'dueDate', title: '截止日期', width: '120px', render: (row) => formatDate(row.dueDate) },
    { key: 'blockedByCount', title: '阻塞', width: '96px', render: (row) => row.blockedByCount ? <StatusBadge status="Blocked" label={`${row.blockedByCount} 项`} /> : '—' },
  ], [drag, onToggleTaskSelection, selectedTaskIds, state.viewKey]);

  return <DataTable columnSettingsKey={`project-management-tasks-${state.viewKey}`} columns={columns} emptyText="暂无任务" rowActions={(row) => <button type="button" onClick={() => onSelectTask(row.id)}>查看</button>} rowKey={(row) => row.id} rows={rows} showColumnSettings />;
}

function TaskCardProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'> & { drag: TaskDragHandlers }) {
  return <div className="pm-task-grid">{rows.map((task) => <TaskCard drag={drag} key={task.id} selected={selectedTaskIds.has(task.id)} task={task} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} />)}</div>;
}

function TaskBoardProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'> & { drag: TaskDragHandlers }) {
  const groups = ['Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled'].map((status) => ({ status, rows: rows.filter((task) => task.status === status) }));
  return <div className="pm-board">{groups.map((group) => <section className="pm-board-column" key={group.status}><header><StatusBadge status={group.status} /><span>{group.rows.length}</span></header><div className="pm-board-column__body">{group.rows.length ? group.rows.map((task) => <TaskCard drag={drag} key={task.id} selected={selectedTaskIds.has(task.id)} task={task} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} />) : <p>暂无任务</p>}</div></section>)}</div>;
}

function TaskGanttProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'> & { drag: TaskDragHandlers }) {
  const datedRows = rows.filter((task) => task.startDate || task.dueDate);
  return <div className="pm-gantt"><p className="pm-prototype-note">当前按真实开始/截止日期呈现计划投影；日期拖动、时间缩放、关键路径和依赖线需要后续排期能力支持。</p>{datedRows.length ? datedRows.map((task) => <div className={`pm-gantt-row${drag.draggedTaskId === task.id ? ' is-dragging' : ''}`} draggable key={task.id} onDragEnd={drag.onDragEnd} onDragOver={drag.onDragOver} onDragStart={(event) => drag.onDragStart(event, task)} onDrop={(event) => drag.onDrop(event, { kind: 'before', task })}><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button type="button" onClick={() => onSelectTask(task.id)}><span>{task.title}</span><span>{formatDate(task.startDate)} — {formatDate(task.dueDate)}</span></button><span className="pm-child-drop-zone" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task })}>作为子任务</span></div>) : <p className="pm-projection-empty">没有包含计划日期的任务</p>}</div>;
}

function TaskCalendarProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'> & { drag: TaskDragHandlers }) {
  const groups = rows.filter((task) => task.dueDate).reduce<Record<string, ProjectManagementTask[]>>((result, task) => {
    const key = task.dueDate?.slice(0, 10) ?? '';
    result[key] = [...(result[key] ?? []), task];
    return result;
  }, {});
  const dates = Object.keys(groups).sort();
  return <div className="pm-calendar"><p className="pm-prototype-note">当前按真实截止日期聚合；月/周网格、日期拖放与快速新建需要后续日历交互能力支持。</p>{dates.length ? dates.map((date) => <section className="pm-calendar-day" key={date}><h3>{date}</h3><div>{groups[date].map((task) => <TaskCard drag={drag} key={task.id} selected={selectedTaskIds.has(task.id)} task={task} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} />)}</div></section>) : <p className="pm-projection-empty">没有包含截止日期的任务</p>}</div>;
}

function TaskCard({ drag, onSelectTask, onToggleTaskSelection, selected, task }: { drag: TaskDragHandlers; onSelectTask: (taskId: string) => void; onToggleTaskSelection: (taskId: string) => void; selected: boolean; task: ProjectManagementTask }) {
  return <article className={`pm-task-card${selected ? ' is-selected' : ''}${drag.draggedTaskId === task.id ? ' is-dragging' : ''}`} draggable onDragEnd={drag.onDragEnd} onDragOver={drag.onDragOver} onDragStart={(event) => drag.onDragStart(event, task)} onDrop={(event) => drag.onDrop(event, { kind: 'before', task })}><input aria-label={`选择任务 ${task.title}`} checked={selected} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><div className="pm-task-card__content"><div className="pm-task-card__meta"><code>{task.taskCode}</code><StatusBadge status={task.status} /></div><button className="pm-task-card__open" type="button" onClick={() => onSelectTask(task.id)}>{task.title}</button><div className="pm-task-card__signals"><PriorityBadge priority={task.priority} /><span>{task.canStart ? '可开始' : task.blockedReason ?? '受阻塞'}</span></div><Progress value={task.progressPercent} /><footer><span>截止：{formatDate(task.dueDate)}</span>{task.blockedByCount ? <span>{task.blockedByCount} 项前置</span> : null}</footer></div><span className="pm-child-drop-zone" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task })}>作为子任务</span></article>;
}

function StatusBadge({ label, status }: { label?: string; status: string }) {
  return <span className={`pm-status-badge pm-status-badge--${toKebabCase(status)}`}>{label ?? statusLabel(status)}</span>;
}

function PriorityBadge({ priority }: { priority: string }) {
  return <span className={`pm-priority-badge pm-priority-badge--${priority.toLowerCase()}`}>{priorityLabel(priority)}</span>;
}

function Progress({ value }: { value: number }) {
  return <div className="pm-progress"><progress max={100} value={value} /><span>{value}%</span></div>;
}

function statusLabel(status: string): string {
  return ({ Todo: '待开始', InProgress: '进行中', Blocked: '受阻塞', Done: '已完成', Cancelled: '已取消' } as Record<string, string>)[status] ?? status;
}

function priorityLabel(priority: string): string {
  return ({ Low: '低', Medium: '中', High: '高', Urgent: '紧急' } as Record<string, string>)[priority] ?? priority;
}

function formatDate(value: string | undefined): string {
  return value ? new Date(value).toLocaleDateString() : '未设置';
}

function toKebabCase(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
}
