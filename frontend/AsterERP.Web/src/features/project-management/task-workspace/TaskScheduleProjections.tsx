import { useVirtualizer } from '@tanstack/react-virtual';
import { useMemo, useRef, useState } from 'react';

import type { ProjectManagementMilestone, ProjectManagementTaskDependency, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import {
  buildTaskScheduleRows,
  createCalendarEvents,
  createScheduleWindow,
  getSchedulePlacement,
  milestonePlacement,
  shiftScheduleDate,
  type TaskScheduleRow,
} from './taskScheduleProjectionModel';

interface TaskScheduleProjectionProps {
  dependencies?: readonly ProjectManagementTaskDependency[];
  milestones?: readonly ProjectManagementMilestone[];
  onChangeTaskSchedule?: (task: ProjectManagementTaskListItem, startDate: string | undefined, dueDate: string | undefined) => void;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  rows: ProjectManagementTaskListItem[];
  selectedTaskIds: ReadonlySet<string>;
}

const dayWidth = 36;
const labelWidth = 240;

export function TaskGanttScheduleProjection({ dependencies = [], milestones = [], onChangeTaskSchedule, onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: TaskScheduleProjectionProps) {
  const scheduleRows = useMemo(() => buildTaskScheduleRows(rows, dependencies), [dependencies, rows]);
  const datedRows = scheduleRows.filter((task) => task.scheduleStartDate || task.scheduleDueDate);
  const firstDate = datedRows.map((task) => task.scheduleStartDate ?? task.scheduleDueDate).filter((value): value is string => Boolean(value)).sort()[0];
  const [rangeDays, setRangeDays] = useState(56);
  const [anchorDate, setAnchorDate] = useState(() => firstDate ? new Date(`${firstDate.slice(0, 10)}T00:00:00`) : new Date());
  const range = useMemo(() => createScheduleWindow(anchorDate, rangeDays), [anchorDate, rangeDays]);
  const scrollRef = useRef<HTMLDivElement>(null);
  const pointerRef = useRef<{ task: TaskScheduleRow; startX: number } | null>(null);
  const virtualizer = useVirtualizer({ count: datedRows.length, getScrollElement: () => scrollRef.current, estimateSize: () => 58, overscan: 8 });
  const virtualItems = virtualizer.getVirtualItems();
  const positions = new Map(virtualItems.map((item) => [datedRows[item.index]?.id, item.start + 28]));
  const contentWidth = labelWidth + range.days.length * dayWidth;
  const markers = milestonePlacement(milestones, range);

  if (!datedRows.length) return <div className="pm-gantt"><p className="pm-projection-empty">没有包含计划日期的任务。请在任务详情填写开始日期或截止日期。</p></div>;
  return <div className="pm-gantt">
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
      <p className="pm-prototype-note">父任务显示后代汇总条；红色条为关键路径，线段为真实依赖，菱形为里程碑。大范围计划按行虚拟渲染。</p>
      <div className="flex flex-wrap gap-2"><button className="rounded border border-gray-300 px-2 py-1 text-xs" onClick={() => setAnchorDate((value) => addDays(value, -rangeDays))} type="button">前移窗口</button><button className="rounded border border-gray-300 px-2 py-1 text-xs" onClick={() => setAnchorDate((value) => addDays(value, rangeDays))} type="button">后移窗口</button>{[28, 56, 84].map((days) => <button aria-pressed={rangeDays === days} className={rangeDays === days ? 'rounded bg-blue-600 px-2 py-1 text-xs text-white' : 'rounded border border-gray-300 px-2 py-1 text-xs'} key={days} onClick={() => setRangeDays(days)} type="button">{days / 7} 周</button>)}</div>
    </div>
    <div className="overflow-auto rounded border border-gray-200" ref={scrollRef} style={{ maxHeight: '68vh' }}>
      <div className="relative" style={{ height: virtualizer.getTotalSize() + 52, minWidth: contentWidth }}>
        <div className="sticky top-0 z-20 grid border-b border-gray-200 bg-gray-50 text-xs text-gray-500" style={{ gridTemplateColumns: `${labelWidth}px repeat(${range.days.length}, ${dayWidth}px)` }}><div className="p-2 font-medium">任务 / {formatDateKey(range.start)}</div>{range.days.map((day) => <div className={day.getDay() === 0 || day.getDay() === 6 ? 'border-l border-gray-100 bg-gray-100 p-1 text-center' : 'border-l border-gray-100 p-1 text-center'} key={formatDateKey(day)}>{day.getDate()}</div>)}</div>
        <svg className="pointer-events-none absolute left-0 top-12 z-10" height={virtualizer.getTotalSize()} width={contentWidth}>{dependencies.flatMap((dependency) => {
          const from = datedRows.find((task) => task.id === dependency.predecessorTaskId);
          const to = datedRows.find((task) => task.id === dependency.successorTaskId);
          const fromY = positions.get(from?.id ?? '');
          const toY = positions.get(to?.id ?? '');
          const fromPlacement = from ? getSchedulePlacement(from.scheduleStartDate, from.scheduleDueDate, range) : undefined;
          const toPlacement = to ? getSchedulePlacement(to.scheduleStartDate, to.scheduleDueDate, range) : undefined;
          return fromY !== undefined && toY !== undefined && fromPlacement && toPlacement
            ? [<line key={dependency.id} stroke="#94a3b8" strokeWidth="1.5" x1={labelWidth + (fromPlacement.endOffset + 1) * dayWidth} x2={labelWidth + toPlacement.startOffset * dayWidth} y1={fromY} y2={toY} />]
            : [];
        })}</svg>
        {virtualItems.map((virtualRow) => {
          const task = datedRows[virtualRow.index];
          if (!task) return null;
          const placement = getSchedulePlacement(task.scheduleStartDate, task.scheduleDueDate, range);
          return <div className="absolute left-0 right-0 grid min-h-14 border-b border-gray-100" data-index={virtualRow.index} key={task.id} ref={virtualizer.measureElement} style={{ gridTemplateColumns: `${labelWidth}px repeat(${range.days.length}, ${dayWidth}px)`, top: virtualRow.start + 52 }}>
            <div className="flex items-center gap-2 p-2"><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="truncate text-left text-sm hover:underline" onClick={() => onSelectTask(task.id)} title={task.title} type="button">{task.isSummary ? '▰ ' : ''}{task.title}<span className="ml-1 text-xs text-slate-500">{task.childTaskCount ? `(${task.childTaskCount} 项)` : ''}</span>{task.isCritical ? <span className="ml-1 text-xs text-red-600">关键路径</span> : null}</button></div>
            {placement ? <button className={task.isSummary ? 'my-3 min-w-0 rounded bg-slate-500 px-2 text-left text-xs text-white' : task.isCritical ? 'my-3 min-w-0 rounded bg-red-600 px-2 text-left text-xs text-white' : 'my-3 min-w-0 rounded bg-blue-600 px-2 text-left text-xs text-white'} disabled={task.isSummary || !onChangeTaskSchedule} onClick={() => onSelectTask(task.id)} onPointerDown={(event) => { if (!task.isSummary) { event.currentTarget.setPointerCapture(event.pointerId); pointerRef.current = { task, startX: event.clientX }; } }} onPointerUp={(event) => { const pointer = pointerRef.current; pointerRef.current = null; if (!pointer || task.isSummary) return; const delta = Math.round((event.clientX - pointer.startX) / dayWidth); if (delta) onChangeTaskSchedule?.(task, shiftScheduleDate(task.startDate, delta), shiftScheduleDate(task.dueDate, delta)); }} style={{ gridColumn: `${placement.startOffset + 2} / ${placement.endOffset + 3}` }} title={`${task.title} · ${formatDate(task.scheduleStartDate)} — ${formatDate(task.scheduleDueDate)}`} type="button">{task.taskCode} · {task.progressPercent}%</button> : null}
          </div>;
        })}
        {markers.map(({ milestone, offset }) => <span className="pointer-events-none absolute top-0 z-30 text-amber-600" key={milestone.id} style={{ left: labelWidth + offset * dayWidth }} title={`${milestone.milestoneName} · ${formatDate(milestone.dueDate ?? milestone.startDate)}`}>◆</span>)}
      </div>
    </div>
  </div>;
}

export function TaskCalendarScheduleProjection({ dependencies = [], onSelectTask, onToggleTaskSelection, rows, selectedTaskIds }: Omit<TaskScheduleProjectionProps, 'milestones' | 'onChangeTaskSchedule'>) {
  const scheduleRows = useMemo(() => buildTaskScheduleRows(rows, dependencies), [dependencies, rows]);
  const events = useMemo(() => createCalendarEvents(scheduleRows), [scheduleRows]);
  const [mode, setMode] = useState<'month' | 'week'>('month');
  const [anchorDate, setAnchorDate] = useState(() => firstScheduleDate(scheduleRows) ?? new Date());
  const calendar = useMemo(() => createScheduleWindow(anchorDate, mode === 'week' ? 7 : 42), [anchorDate, mode]);
  return <div className="pm-calendar">
    <div className="mb-3 flex flex-wrap items-center justify-between gap-2"><p className="pm-prototype-note">日历与甘特共享真实任务日期；月/周视图同时显示开始和截止事件，父任务使用子任务汇总日期。</p><div className="flex items-center gap-2"><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate((value) => movePeriod(value, mode, -1))} type="button">上一{mode === 'month' ? '月' : '周'}</button><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate(firstScheduleDate(scheduleRows) ?? new Date())} type="button">回到有任务日期</button><button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate((value) => movePeriod(value, mode, 1))} type="button">下一{mode === 'month' ? '月' : '周'}</button><select aria-label="日历视图" onChange={(event) => setMode(event.target.value as 'month' | 'week')} value={mode}><option value="month">月视图</option><option value="week">周视图</option></select></div></div>
    <div className="grid grid-cols-7 overflow-hidden rounded border border-gray-200"><div className="col-span-7 grid grid-cols-7 border-b border-gray-200 bg-gray-50 text-center text-xs text-gray-500">{['日', '一', '二', '三', '四', '五', '六'].map((day) => <div className="p-2" key={day}>周{day}</div>)}</div>{calendar.days.map((day) => { const key = formatDateKey(day); const currentPeriod = mode === 'week' || day.getMonth() === anchorDate.getMonth(); const dayEvents = events[key] ?? []; return <section className={`min-h-28 border-b border-r border-gray-100 p-2 ${currentPeriod ? 'bg-white' : 'bg-gray-50 text-gray-400'}`} key={key}><div className="mb-1 flex items-center justify-between text-xs"><span>{day.getDate()}</span>{dayEvents.length ? <span>{dayEvents.length} 项</span> : null}</div><div className="space-y-1">{dayEvents.map(({ task, kind }) => <div className="flex items-center gap-1" key={`${task.id}-${kind}`}><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} type="checkbox" onChange={() => onToggleTaskSelection(task.id)} /><button className="truncate rounded bg-blue-50 px-1 py-0.5 text-left text-xs text-blue-800 hover:bg-blue-100" onClick={() => onSelectTask(task.id)} title={`${task.title} · ${kind === 'start' ? '开始' : '截止'}`} type="button"><span className="mr-1 text-slate-500">{kind === 'start' ? '始' : '止'}</span>{task.title}</button></div>)}</div></section>; })}</div>
  </div>;
}

function firstScheduleDate(rows: readonly TaskScheduleRow[]): Date | undefined {
  const first = rows.map((task) => task.scheduleStartDate ?? task.scheduleDueDate).filter((value): value is string => Boolean(value)).sort()[0];
  return first ? new Date(`${first.slice(0, 10)}T00:00:00`) : undefined;
}

function addDays(value: Date, days: number): Date { const next = new Date(value); next.setDate(next.getDate() + days); return next; }
function movePeriod(value: Date, mode: 'month' | 'week', direction: -1 | 1): Date { if (mode === 'week') return addDays(value, direction * 7); const next = new Date(value); next.setMonth(next.getMonth() + direction); return next; }
function formatDate(value: string | undefined): string { return value ? new Date(value).toLocaleDateString() : '未设置'; }
function formatDateKey(value: Date): string { return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`; }
