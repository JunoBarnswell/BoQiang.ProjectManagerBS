import { useMemo, useState, type DragEvent } from 'react';

import type { ProjectManagementMilestone, ProjectManagementTaskDependency, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { buildTaskScheduleRows, validateScheduleMove, type TaskScheduleRow } from '../task-workspace/taskScheduleProjectionModel';

import {
  addProjectManagementCalendarDays,
  buildProjectManagementCalendarSegments,
  buildProjectManagementCalendarWeeks,
  formatProjectManagementCalendarDate,
  isProjectManagementCalendarDateInMonth,
  shiftProjectManagementCalendarTask,
  toProjectManagementCalendarDate,
  type ProjectManagementCalendarMode,
} from './projectManagementCalendarModel';

interface ProjectManagementTaskCalendarProps {
  dependencies: readonly ProjectManagementTaskDependency[];
  milestones: readonly ProjectManagementMilestone[];
  onChangeTaskSchedule?: (task: ProjectManagementTaskListItem, startDate: string | undefined, dueDate: string | undefined) => void;
  onCreateTask?: (date: string) => void;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  rows: ProjectManagementTaskListItem[];
  schedulePending?: boolean;
  selectedTaskIds: ReadonlySet<string>;
}

const weekDayLabels = ['日', '一', '二', '三', '四', '五', '六'];
const visibleTaskLimit = 3;

export function ProjectManagementTaskCalendar({ dependencies, milestones, onChangeTaskSchedule, onCreateTask, onSelectTask, onToggleTaskSelection, rows, schedulePending, selectedTaskIds }: ProjectManagementTaskCalendarProps) {
  const { hasPermission: canAddTask } = usePermission('project-management:task:add');
  const { hasPermission: canEditTask } = usePermission('project-management:task:edit');
  const scheduleRows = useMemo(() => buildTaskScheduleRows(rows, dependencies), [dependencies, rows]);
  const [mode, setMode] = useState<ProjectManagementCalendarMode>('month');
  const [anchorDate, setAnchorDate] = useState(() => firstScheduleDate(scheduleRows) ?? new Date());
  const [drawerDate, setDrawerDate] = useState<string>();
  const [draggedTask, setDraggedTask] = useState<TaskScheduleRow>();
  const [announcement, setAnnouncement] = useState('日历任务可拖到其他日期调整计划；在移动端可使用每项的日期菜单。');
  const weeks = useMemo(() => buildProjectManagementCalendarWeeks(anchorDate, mode), [anchorDate, mode]);
  const eventsByDate = useMemo(() => buildEventsByDate(scheduleRows), [scheduleRows]);
  const milestonesByDate = useMemo(() => buildMilestonesByDate(milestones), [milestones]);
  const drawerTasks = drawerDate ? eventsByDate.get(drawerDate) ?? [] : [];

  const requestScheduleChange = (task: TaskScheduleRow, targetStartDate: string) => {
    if (!canEditTask || schedulePending || task.isSummary || !onChangeTaskSchedule) return;
    const next = shiftProjectManagementCalendarTask(task, targetStartDate);
    const validationError = validateScheduleMove(task, next.startDate, next.dueDate, rows, dependencies);
    if (validationError) {
      setAnnouncement(`未调整“${task.title}”：${validationError}`);
      return;
    }
    setAnnouncement(`正在调整“${task.title}”到 ${targetStartDate}。`);
    onChangeTaskSchedule(task, next.startDate, next.dueDate);
  };

  const onDragStart = (event: DragEvent<HTMLButtonElement>, task: TaskScheduleRow) => {
    if (!canEditTask || task.isSummary || schedulePending) return;
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', task.id);
    setDraggedTask(task);
    setAnnouncement(`已抓取任务“${task.title}”，拖到目标日期后将保留原持续时间。`);
  };

  const onDayDrop = (event: DragEvent<HTMLElement>, date: string) => {
    event.preventDefault();
    const task = draggedTask;
    setDraggedTask(undefined);
    if (task) requestScheduleChange(task, date);
  };

  return <div aria-label="项目任务日历" className="pm-calendar space-y-3">
    <p className="sr-only" aria-live="polite">{announcement}</p>
    <div className="flex flex-wrap items-center justify-between gap-2">
      <p className="pm-prototype-note">任务条按开始至截止日期连续显示；筛选、删除状态和项目权限均来自当前任务查询。◆ 为里程碑。</p>
      <div className="flex flex-wrap items-center gap-2">
        <button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate((current) => addProjectManagementCalendarDays(current, mode === 'week' ? -7 : -31))} type="button">上一{mode === 'week' ? '周' : '月'}</button>
        <button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate(new Date())} type="button">今天</button>
        <button className="rounded border border-gray-300 px-2 py-1 text-sm" onClick={() => setAnchorDate((current) => addProjectManagementCalendarDays(current, mode === 'week' ? 7 : 31))} type="button">下一{mode === 'week' ? '周' : '月'}</button>
        <div aria-label="日历视图" className="inline-flex overflow-hidden rounded border border-gray-300">
          <button aria-pressed={mode === 'month'} className={mode === 'month' ? 'bg-blue-600 px-2 py-1 text-sm text-white' : 'px-2 py-1 text-sm'} onClick={() => setMode('month')} type="button">月</button>
          <button aria-pressed={mode === 'week'} className={mode === 'week' ? 'bg-blue-600 px-2 py-1 text-sm text-white' : 'px-2 py-1 text-sm'} onClick={() => setMode('week')} type="button">周</button>
        </div>
      </div>
    </div>
    <div className="overflow-x-auto rounded border border-gray-200">
      <div className="min-w-[720px]">
        <div className="grid grid-cols-7 border-b border-gray-200 bg-gray-50 text-center text-xs text-gray-500">{weekDayLabels.map((day) => <div className="p-2" key={day}>周{day}</div>)}</div>
        {weeks.map((week) => {
          const segments = buildProjectManagementCalendarSegments(scheduleRows, week.days);
          const rowCount = Math.max(visibleTaskLimit, ...segments.map((segment) => segment.lane + 1));
          return <div className="relative grid grid-cols-7 border-b border-gray-100" key={week.key} style={{ minHeight: `${112 + rowCount * 25}px` }}>
            {week.days.map((day) => {
              const date = formatProjectManagementCalendarDate(day);
              const dayTasks = eventsByDate.get(date) ?? [];
              const inCurrentMonth = mode === 'week' || isProjectManagementCalendarDateInMonth(day, anchorDate);
              const milestoneItems = milestonesByDate.get(date) ?? [];
              return <section
                aria-label={`${date}，${dayTasks.length} 项任务`}
                className={`min-h-28 border-r border-gray-100 p-2 ${inCurrentMonth ? 'bg-white' : 'bg-gray-50 text-gray-400'}`}
                key={date}
                onDragOver={(event) => { if (draggedTask) event.preventDefault(); }}
                onDrop={(event) => onDayDrop(event, date)}
              >
                <div className="flex items-center justify-between gap-1 text-xs"><button aria-label={`查看 ${date} 的任务`} className="rounded px-1 font-medium hover:bg-slate-100" onClick={() => setDrawerDate(date)} type="button">{day.getDate()}</button>{dayTasks.length > visibleTaskLimit ? <button className="rounded px-1 text-blue-700 hover:bg-blue-50" onClick={() => setDrawerDate(date)} type="button">还有 {dayTasks.length - visibleTaskLimit} 项</button> : <span>{dayTasks.length ? `${dayTasks.length} 项` : ''}</span>}</div>
                {milestoneItems.length ? <button className="mt-1 block max-w-full truncate text-left text-xs text-amber-700" onClick={() => setDrawerDate(date)} title={milestoneItems.map((item) => item.milestoneName).join('、')} type="button">◆ {milestoneItems.map((item) => item.milestoneName).join('、')}</button> : null}
                <div className="absolute inset-x-0 top-9 pointer-events-none" style={{ height: `${rowCount * 25}px` }} />
                {canAddTask ? <PermissionButton className="mt-1 rounded px-1 text-xs text-blue-700 hover:bg-blue-50" code="project-management:task:add" onClick={() => onCreateTask?.(date)} type="button">+ 新建</PermissionButton> : null}
              </section>;
            })}
            <div className="pointer-events-none absolute inset-x-0 top-10 grid grid-cols-7" style={{ gridTemplateRows: `repeat(${rowCount}, 25px)` }}>
              {segments.map((segment) => <CalendarTaskBar
                canEdit={canEditTask}
                dragged={draggedTask?.id === segment.task.id}
                key={`${segment.task.id}-${week.key}`}
                onDragEnd={() => setDraggedTask(undefined)}
                onDragStart={(event) => onDragStart(event, segment.task)}
                onSelect={() => onSelectTask(segment.task.id)}
                pending={schedulePending}
                segment={segment}
              />)}
            </div>
          </div>;
        })}
      </div>
    </div>
    {drawerDate ? <CalendarDayDrawer
      date={drawerDate}
      milestones={milestonesByDate.get(drawerDate) ?? []}
      onClose={() => setDrawerDate(undefined)}
      onCreate={() => onCreateTask?.(drawerDate)}
      onMove={(task, date) => requestScheduleChange(task, date)}
      onSelectTask={onSelectTask}
      onToggleTaskSelection={onToggleTaskSelection}
      canAdd={canAddTask}
      canEdit={canEditTask}
      pending={schedulePending}
      selectedTaskIds={selectedTaskIds}
      tasks={drawerTasks}
    /> : null}
  </div>;
}

function CalendarTaskBar({ canEdit, dragged, onDragEnd, onDragStart, onSelect, pending, segment }: { canEdit: boolean; dragged: boolean; onDragEnd: () => void; onDragStart: (event: DragEvent<HTMLButtonElement>) => void; onSelect: () => void; pending?: boolean; segment: ReturnType<typeof buildProjectManagementCalendarSegments>[number] }) {
  const { task } = segment;
  const gridColumn = `${segment.startColumn + 1} / ${segment.endColumn + 2}`;
  return <button
    aria-label={`任务 ${task.title}，${task.scheduleStartDate ?? task.scheduleDueDate} 至 ${task.scheduleDueDate ?? task.scheduleStartDate}`}
    className={`pointer-events-auto mx-1 truncate rounded px-2 text-left text-xs text-white ${task.isSummary ? 'bg-slate-500' : task.isCritical ? 'bg-red-600' : 'bg-blue-600'} ${dragged ? 'opacity-50' : ''}`}
    draggable={canEdit && !task.isSummary && !pending}
    onClick={onSelect}
    onDragEnd={onDragEnd}
    onDragStart={onDragStart}
    style={{ gridColumn, gridRow: segment.lane + 1 }}
    title={`${task.taskCode} · ${task.title}${task.isSummary ? '（汇总任务，不可拖动）' : '（拖动调整日期）'}`}
    type="button"
  >{task.isSummary ? '▰ ' : ''}{task.title}</button>;
}

function CalendarDayDrawer({ canAdd, canEdit, date, milestones, onClose, onCreate, onMove, onSelectTask, onToggleTaskSelection, pending, selectedTaskIds, tasks }: { canAdd: boolean; canEdit: boolean; date: string; milestones: ProjectManagementMilestone[]; onClose: () => void; onCreate: () => void; onMove: (task: TaskScheduleRow, date: string) => void; onSelectTask: (taskId: string) => void; onToggleTaskSelection: (taskId: string) => void; pending?: boolean; selectedTaskIds: ReadonlySet<string>; tasks: TaskScheduleRow[] }) {
  return <aside aria-label={`${date} 任务抽屉`} className="fixed inset-y-0 right-0 z-50 w-full max-w-md overflow-y-auto border-l border-gray-200 bg-white p-5 shadow-2xl" role="dialog">
    <div className="mb-4 flex items-center justify-between gap-3"><div><h3 className="text-lg font-semibold">{date}</h3><p className="text-sm text-slate-500">{tasks.length} 项任务，显示当前筛选结果。</p></div><button aria-label="关闭当日任务抽屉" className="rounded border border-gray-300 px-2 py-1" onClick={onClose} type="button">关闭</button></div>
    {milestones.length ? <div className="mb-4 rounded bg-amber-50 p-3 text-sm text-amber-800"><strong>里程碑</strong><ul className="mt-1 list-disc pl-5">{milestones.map((item) => <li key={item.id}>{item.milestoneName}</li>)}</ul></div> : null}
    <div className="space-y-3">{tasks.map((task) => <article className="rounded border border-gray-200 p-3" key={task.id}>
      <div className="flex items-start gap-2"><input aria-label={`选择任务 ${task.title}`} checked={selectedTaskIds.has(task.id)} onChange={() => onToggleTaskSelection(task.id)} type="checkbox" /><button className="min-w-0 flex-1 text-left font-medium hover:underline" onClick={() => onSelectTask(task.id)} type="button">{task.title}<span className="ml-1 text-xs text-slate-500">{task.taskCode}</span></button></div>
      <p className="mt-1 text-xs text-slate-500">{task.scheduleStartDate ?? task.scheduleDueDate} — {task.scheduleDueDate ?? task.scheduleStartDate}</p>
      {!task.isSummary ? <label className="mt-2 flex items-center gap-2 text-xs text-slate-600">移动到日期<input aria-label={`移动任务 ${task.title} 到日期`} className="rounded border border-gray-300 p-1" defaultValue={task.startDate ?? task.dueDate} disabled={!canEdit || pending} onChange={(event) => { if (event.target.value) onMove(task, event.target.value); }} type="date" /></label> : <p className="mt-2 text-xs text-slate-500">汇总任务由子任务日期决定。</p>}
    </article>)}</div>
    {!tasks.length ? <p className="rounded border border-dashed border-gray-300 p-4 text-sm text-slate-500">当天没有任务。</p> : null}
    {canAdd ? <PermissionButton className="mt-4 rounded bg-blue-600 px-3 py-2 text-sm text-white" code="project-management:task:add" onClick={onCreate} type="button">在 {date} 新建任务</PermissionButton> : null}
  </aside>;
}

function buildEventsByDate(rows: readonly TaskScheduleRow[]): Map<string, TaskScheduleRow[]> {
  const result = new Map<string, TaskScheduleRow[]>();
  rows.forEach((task) => {
    const start = toProjectManagementCalendarDate(task.scheduleStartDate ?? task.scheduleDueDate);
    const due = toProjectManagementCalendarDate(task.scheduleDueDate ?? task.scheduleStartDate);
    if (!start || !due) return;
    for (let date = start; date <= due; date = addProjectManagementCalendarDays(date, 1)) {
      const key = formatProjectManagementCalendarDate(date);
      result.set(key, [...(result.get(key) ?? []), task]);
    }
  });
  return result;
}

function buildMilestonesByDate(milestones: readonly ProjectManagementMilestone[]): Map<string, ProjectManagementMilestone[]> {
  const result = new Map<string, ProjectManagementMilestone[]>();
  milestones.forEach((milestone) => {
    const date = milestone.dueDate ?? milestone.startDate;
    if (!date) return;
    const key = date.slice(0, 10);
    result.set(key, [...(result.get(key) ?? []), milestone]);
  });
  return result;
}

function firstScheduleDate(rows: readonly TaskScheduleRow[]): Date | undefined {
  const first = rows.map((task) => task.scheduleStartDate ?? task.scheduleDueDate).filter((value): value is string => Boolean(value)).sort()[0];
  return toProjectManagementCalendarDate(first);
}
