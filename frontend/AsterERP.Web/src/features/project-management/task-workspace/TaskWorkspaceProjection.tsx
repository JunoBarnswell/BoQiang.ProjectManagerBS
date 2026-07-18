import { useMemo, useState, type CSSProperties, type DragEvent } from 'react';

import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { DataTable } from '../../../shared/table/DataTable';
import type { DataTableColumn } from '../../../shared/table/tableTypes';
import type { TaskWorkspaceState } from '../state/taskWorkspaceState';

import type { TaskMoveDropTarget } from './taskMoveIntent';

interface TaskWorkspaceProjectionProps {
  onSelectTask: (taskId: string) => void;
  onMoveTask: (task: ProjectManagementTaskListItem, target: TaskMoveDropTarget) => void;
  onToggleTaskSelection: (taskId: string) => void;
  rows: ProjectManagementTaskListItem[];
  selectedTaskIds: ReadonlySet<string>;
  state: TaskWorkspaceState;
}

export function TaskWorkspaceProjection({ onMoveTask, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds, state }: TaskWorkspaceProjectionProps) {
  const [draggedTask, setDraggedTask] = useState<ProjectManagementTaskListItem>();
  const drag = {
    draggedTaskId: draggedTask?.id,
    onDragEnd: () => setDraggedTask(undefined),
    onDragOver: (event: DragEvent<HTMLElement>) => event.preventDefault(),
    onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTaskListItem) => {
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
  if (state.viewKey === 'gantt') return <TaskGanttProjection rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} />;
  if (state.viewKey === 'calendar') return <TaskCalendarProjection rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} />;
  return <>{rootDropZone}<TaskTableProjection drag={drag} rows={rows} onSelectTask={onSelectTask} onToggleTaskSelection={onToggleTaskSelection} selectedTaskIds={selectedTaskIds} state={state} /></>;
}

interface TaskDragHandlers {
  draggedTaskId?: string;
  onDragEnd: () => void;
  onDragOver: (event: DragEvent<HTMLElement>) => void;
  onDragStart: (event: DragEvent<HTMLElement>, task: ProjectManagementTaskListItem) => void;
  onDrop: (event: DragEvent<HTMLElement>, target: TaskMoveDropTarget) => void;
}

function TaskTableProjection({ drag, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds, state }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds' | 'state'> & { drag: TaskDragHandlers }) {
  const columns = useMemo<DataTableColumn<ProjectManagementTaskListItem>[]>(() => [
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

function TaskGanttProjection({ onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'>) {
  const datedRows = rows.filter((task) => task.startDate || task.dueDate);
  const [rangeDays, setRangeDays] = useState(56);
  const range = useMemo(() => createGanttRange(datedRows, rangeDays), [datedRows, rangeDays]);

  return <div className="pm-gantt">
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
      <p className="pm-prototype-note">基于真实开始/截止日期的只读计划时间轴。日期调整仍须从任务详情保存，避免将层级拖动误当作排期。</p>
      <div aria-label="甘特时间轴密度" className="flex gap-2">
        {[28, 56, 84].map((days) => <button aria-pressed={rangeDays === days} className={rangeDays === days ? 'rounded bg-blue-600 px-2 py-1 text-xs text-white' : 'rounded border border-gray-300 px-2 py-1 text-xs'} key={days} onClick={() => setRangeDays(days)} type="button">{days / 7} 周</button>)}
      </div>
    </div>
    {!datedRows.length ? <p className="pm-projection-empty">没有包含计划日期的任务。请在任务详情填写开始日期或截止日期。</p> : <div className="overflow-x-auto rounded border border-gray-200"><div className="min-w-[920px]">
      <div className="grid border-b border-gray-200 bg-gray-50 text-xs text-gray-500" style={{ gridTemplateColumns: `minmax(220px, 1fr) repeat(${range.days.length}, minmax(20px, 1fr))` }}><div className="p-2 font-medium">任务 / {formatDateOnly(range.start)} 起</div>{range.days.map((day) => <div className={day.getDay() === 0 || day.getDay() === 6 ? 'border-l border-gray-100 bg-gray-100 p-1 text-center' : 'border-l border-gray-100 p-1 text-center'} key={toDateKey(day)}>{day.getDate()}</div>)}</div>
      {datedRows.map((task) => {
        const placement = getGanttPlacement(task, range);
        return <div className="grid min-h-14 border-b border-gray-100 last:border-b-0" key={task.id} style={{ gridTemplateColumns: `minmax(220px, 1fr) repeat(${range.days.length}, minmax(20px, 1fr))` }}>
          <div className="flex items-center gap-2 p-2"><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="truncate text-left text-sm hover:underline" onClick={() => onSelectTask(task.id)} title={task.title} type="button">{task.title}</button></div>
          {placement ? <button className="my-3 min-w-0 rounded bg-blue-600 px-2 text-left text-xs text-white hover:bg-blue-700" onClick={() => onSelectTask(task.id)} style={{ gridColumn: `${placement.startColumn} / span ${placement.span}` }} title={`${task.title} · ${formatDate(task.startDate)} — ${formatDate(task.dueDate)}`} type="button">{task.taskCode}</button> : null}
        </div>;
      })}
    </div></div>}
  </div>;
}

function TaskCalendarProjection({ onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Pick<TaskWorkspaceProjectionProps, 'onSelectTask' | 'onToggleTaskSelection' | 'rows' | 'selectedTaskIds'>) {
  const [mode, setMode] = useState<'month' | 'week'>('month');
  const [anchorDate, setAnchorDate] = useState(() => firstDueDate(rows) ?? new Date());
  const groups = rows.filter((task) => task.dueDate).reduce<Record<string, ProjectManagementTaskListItem[]>>((result, task) => {
    const key = task.dueDate?.slice(0, 10) ?? '';
    result[key] = [...(result[key] ?? []), task];
    return result;
  }, {});
  const calendar = useMemo(() => createCalendarRange(anchorDate, mode), [anchorDate, mode]);

  return <div className="pm-calendar">
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
      <p className="pm-prototype-note">按真实截止日期呈现的只读{mode === 'month' ? '月' : '周'}视图；日期调整请从任务详情保存，日历中不支持拖放排期。</p>
      <div className="flex items-center gap-2"><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate((value) => moveCalendarPeriod(value, mode, -1))} type="button">上一{mode === 'month' ? '月' : '周'}</button><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate(firstDueDate(rows) ?? new Date())} type="button">回到有任务日期</button><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate((value) => moveCalendarPeriod(value, mode, 1))} type="button">下一{mode === 'month' ? '月' : '周'}</button><select aria-label="日历视图" onChange={(event) => setMode(event.target.value as 'month' | 'week')} value={mode}><option value="month">月视图</option><option value="week">周视图</option></select></div>
    </div>
    <div className="grid grid-cols-7 overflow-hidden rounded border border-gray-200"><div className="col-span-7 grid grid-cols-7 border-b border-gray-200 bg-gray-50 text-center text-xs text-gray-500">{['日', '一', '二', '三', '四', '五', '六'].map((day) => <div className="p-2" key={day}>周{day}</div>)}</div>{calendar.days.map((day) => { const key = toDateKey(day); const isCurrentPeriod = mode === 'week' || day.getMonth() === anchorDate.getMonth(); const tasks = groups[key] ?? []; return <section className={`min-h-28 border-b border-r border-gray-100 p-2 ${isCurrentPeriod ? 'bg-white' : 'bg-gray-50 text-gray-400'}`} key={key}><div className="mb-1 flex items-center justify-between text-xs"><span>{day.getDate()}</span>{tasks.length ? <span>{tasks.length} 项</span> : null}</div><div className="space-y-1">{tasks.map((task) => <div className="flex items-center gap-1" key={task.id}><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="truncate rounded bg-blue-50 px-1 py-0.5 text-left text-xs text-blue-800 hover:bg-blue-100" onClick={() => onSelectTask(task.id)} title={task.title} type="button">{task.title}</button></div>)}</div></section>; })}</div>
  </div>;
}

function TaskCard({ drag, onSelectTask, onToggleTaskSelection, selected, task }: { drag?: TaskDragHandlers; onSelectTask: (taskId: string) => void; onToggleTaskSelection: (taskId: string) => void; selected: boolean; task: ProjectManagementTaskListItem }) {
  return <article className={`pm-task-card${selected ? ' is-selected' : ''}${drag?.draggedTaskId === task.id ? ' is-dragging' : ''}`} draggable={drag ? true : undefined} onDragEnd={drag?.onDragEnd} onDragOver={drag?.onDragOver} onDragStart={drag ? (event) => drag.onDragStart(event, task) : undefined} onDrop={drag ? (event) => drag.onDrop(event, { kind: 'before', task }) : undefined}><input aria-label={`选择任务 ${task.title}`} checked={selected} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><div className="pm-task-card__content"><div className="pm-task-card__meta"><code>{task.taskCode}</code><StatusBadge status={task.status} /></div><button className="pm-task-card__open" type="button" onClick={() => onSelectTask(task.id)}>{task.title}</button><div className="pm-task-card__signals"><PriorityBadge priority={task.priority} /><span>{task.canStart ? '可开始' : task.blockedReason ?? '受阻塞'}</span></div><Progress value={task.progressPercent} /><footer><span>截止：{formatDate(task.dueDate)}</span>{task.blockedByCount ? <span>{task.blockedByCount} 项前置</span> : null}</footer></div>{drag ? <span className="pm-child-drop-zone" onDragOver={drag.onDragOver} onDrop={(event) => drag.onDrop(event, { kind: 'child', task })}>作为子任务</span> : null}</article>;
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

function createGanttRange(rows: ProjectManagementTaskListItem[], dayCount: number) {
  const dates = rows.flatMap((task) => [task.startDate, task.dueDate]).flatMap((value) => value ? [toLocalDate(value)] : []).filter((value): value is Date => Boolean(value));
  const first = dates.length ? new Date(Math.min(...dates.map((value) => value.getTime()))) : new Date();
  const start = startOfDay(first);
  return { start, days: Array.from({ length: dayCount }, (_, index) => addCalendarDays(start, index)) };
}

function getGanttPlacement(task: ProjectManagementTaskListItem, range: ReturnType<typeof createGanttRange>) {
  const start = toLocalDate(task.startDate ?? task.dueDate);
  const end = toLocalDate(task.dueDate ?? task.startDate);
  if (!start || !end) return undefined;
  const startOffset = Math.floor((start.getTime() - range.start.getTime()) / 86_400_000);
  const endOffset = Math.floor((end.getTime() - range.start.getTime()) / 86_400_000);
  const visibleStart = Math.max(0, startOffset);
  const visibleEnd = Math.min(range.days.length - 1, Math.max(visibleStart, endOffset));
  if (visibleStart > range.days.length - 1 || visibleEnd < 0) return undefined;
  return { span: Math.max(1, visibleEnd - visibleStart + 1), startColumn: visibleStart + 2 };
}

function createCalendarRange(anchor: Date, mode: 'month' | 'week') {
  const normalized = startOfDay(anchor);
  const start = mode === 'week'
    ? addCalendarDays(normalized, -normalized.getDay())
    : addCalendarDays(new Date(normalized.getFullYear(), normalized.getMonth(), 1), -new Date(normalized.getFullYear(), normalized.getMonth(), 1).getDay());
  return { days: Array.from({ length: mode === 'week' ? 7 : 42 }, (_, index) => addCalendarDays(start, index)) };
}

function firstDueDate(rows: ProjectManagementTaskListItem[]): Date | undefined {
  const values = rows.flatMap((task) => task.dueDate ? [toLocalDate(task.dueDate)] : []).filter((value): value is Date => Boolean(value));
  return values.length ? new Date(Math.min(...values.map((value) => value.getTime()))) : undefined;
}

function toLocalDate(value: string | undefined): Date | undefined {
  if (!value) return undefined;
  const date = new Date(`${value.slice(0, 10)}T00:00:00`);
  return Number.isNaN(date.getTime()) ? undefined : date;
}

function startOfDay(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate());
}

function addCalendarDays(value: Date, days: number): Date {
  const next = new Date(value);
  next.setDate(next.getDate() + days);
  return next;
}

function moveCalendarPeriod(value: Date, mode: 'month' | 'week', direction: -1 | 1): Date {
  if (mode === 'week') return addCalendarDays(value, direction * 7);
  const next = new Date(value);
  next.setMonth(next.getMonth() + direction);
  return next;
}

function toDateKey(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function formatDateOnly(value: Date): string {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
}

function toKebabCase(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
}
