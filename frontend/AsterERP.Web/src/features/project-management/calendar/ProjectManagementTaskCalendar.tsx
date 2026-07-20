import { useMemo, useState, type DragEvent } from 'react';

import type { ProjectManagementMilestone, ProjectManagementTaskDependency, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { usePermission } from '../../../core/auth/usePermission';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { buildTaskScheduleRows, validateScheduleMove, type TaskScheduleRow } from '../state/projectManagementScheduleModel';

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
const visibleTaskLimit = 2;

export function ProjectManagementTaskCalendar({
  dependencies,
  milestones,
  onChangeTaskSchedule,
  onCreateTask,
  onSelectTask,
  onToggleTaskSelection,
  rows,
  schedulePending,
  selectedTaskIds,
}: ProjectManagementTaskCalendarProps) {
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
  const todayKey = formatProjectManagementCalendarDate(new Date());
  const titleLabel = mode === 'week'
    ? `${formatProjectManagementCalendarDate(weeks[0]?.days[0] ?? anchorDate)} — ${formatProjectManagementCalendarDate(weeks[0]?.days[6] ?? anchorDate)}`
    : `${anchorDate.getFullYear()}年${anchorDate.getMonth() + 1}月`;

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

  return (
    <div aria-label="项目任务日历" className="pm-cal">
      <p className="sr-only" aria-live="polite">{announcement}</p>

      <div className="pm-cal__toolbar">
        <div className="pm-cal__nav">
          <button
            className="pm-workbench-command"
            onClick={() => setAnchorDate((current) => addProjectManagementCalendarDays(current, mode === 'week' ? -7 : -31))}
            type="button"
          >
            上一{mode === 'week' ? '周' : '月'}
          </button>
          <button className="pm-workbench-command" onClick={() => setAnchorDate(new Date())} type="button">今天</button>
          <button
            className="pm-workbench-command"
            onClick={() => setAnchorDate((current) => addProjectManagementCalendarDays(current, mode === 'week' ? 7 : 31))}
            type="button"
          >
            下一{mode === 'week' ? '周' : '月'}
          </button>
          <strong className="pm-cal__title">{titleLabel}</strong>
        </div>
        <div aria-label="日历视图" className="pm-cal__mode">
          <button aria-pressed={mode === 'month'} className={mode === 'month' ? 'is-active' : undefined} onClick={() => setMode('month')} type="button">月</button>
          <button aria-pressed={mode === 'week'} className={mode === 'week' ? 'is-active' : undefined} onClick={() => setMode('week')} type="button">周</button>
        </div>
      </div>

      <div className="pm-cal__board">
        <div className="pm-cal__weekdays">
          {weekDayLabels.map((day) => <div key={day}>周{day}</div>)}
        </div>
        <div className="pm-cal__weeks" style={{ gridTemplateRows: `repeat(${Math.max(weeks.length, 1)}, minmax(0, 1fr))` }}>
          {weeks.map((week) => {
            const segments = buildProjectManagementCalendarSegments(scheduleRows, week.days);
            const laneCount = Math.max(1, ...segments.map((segment) => segment.lane + 1));
            const visibleLanes = Math.min(laneCount, visibleTaskLimit);
            return (
              <div className="pm-cal__week" key={week.key}>
                {week.days.map((day) => {
                  const date = formatProjectManagementCalendarDate(day);
                  const dayTasks = eventsByDate.get(date) ?? [];
                  const inCurrentMonth = mode === 'week' || isProjectManagementCalendarDateInMonth(day, anchorDate);
                  const milestoneItems = milestonesByDate.get(date) ?? [];
                  const overflow = Math.max(0, dayTasks.length - visibleTaskLimit);
                  const isToday = date === todayKey;
                  return (
                    <section
                      aria-label={`${date}，${dayTasks.length} 项任务`}
                      className={[
                        'pm-cal__day',
                        inCurrentMonth ? '' : 'is-outside',
                        isToday ? 'is-today' : '',
                        draggedTask ? 'is-droppable' : '',
                      ].filter(Boolean).join(' ')}
                      key={date}
                      onDragOver={(event) => { if (draggedTask) event.preventDefault(); }}
                      onDrop={(event) => onDayDrop(event, date)}
                    >
                      <div className="pm-cal__day-head">
                        <button
                          aria-label={`查看 ${date} 的任务`}
                          className={`pm-cal__day-num${isToday ? ' is-today' : ''}`}
                          onClick={() => setDrawerDate(date)}
                          type="button"
                        >
                          {day.getDate()}
                        </button>
                        {canAddTask ? (
                          <PermissionButton
                            aria-label={`${date} 新建任务`}
                            className="pm-cal__day-add"
                            code="project-management:task:add"
                            onClick={() => onCreateTask?.(date)}
                            type="button"
                          >
                            +
                          </PermissionButton>
                        ) : null}
                      </div>
                      {milestoneItems.length ? (
                        <button
                          className="pm-cal__milestone"
                          onClick={() => setDrawerDate(date)}
                          title={milestoneItems.map((item) => item.milestoneName).join('、')}
                          type="button"
                        >
                          ◆ {milestoneItems[0]?.milestoneName}
                          {milestoneItems.length > 1 ? ` +${milestoneItems.length - 1}` : ''}
                        </button>
                      ) : null}
                      {overflow > 0 ? (
                        <button className="pm-cal__more" onClick={() => setDrawerDate(date)} type="button">
                          +{overflow}
                        </button>
                      ) : null}
                    </section>
                  );
                })}
                <div
                  className="pm-cal__bars"
                  style={{ gridTemplateRows: `repeat(${visibleLanes}, 18px)` }}
                >
                  {segments
                    .filter((segment) => segment.lane < visibleTaskLimit)
                    .map((segment) => (
                      <CalendarTaskBar
                        canEdit={canEditTask}
                        dragged={draggedTask?.id === segment.task.id}
                        key={`${segment.task.id}-${week.key}`}
                        onDragEnd={() => setDraggedTask(undefined)}
                        onDragStart={(event) => onDragStart(event, segment.task)}
                        onSelect={() => onSelectTask(segment.task.id)}
                        pending={schedulePending}
                        segment={segment}
                      />
                    ))}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {drawerDate ? (
        <CalendarDayDrawer
          canAdd={canAddTask}
          canEdit={canEditTask}
          date={drawerDate}
          milestones={milestonesByDate.get(drawerDate) ?? []}
          onClose={() => setDrawerDate(undefined)}
          onCreate={() => onCreateTask?.(drawerDate)}
          onMove={(task, date) => requestScheduleChange(task, date)}
          onSelectTask={onSelectTask}
          onToggleTaskSelection={onToggleTaskSelection}
          pending={schedulePending}
          selectedTaskIds={selectedTaskIds}
          tasks={drawerTasks}
        />
      ) : null}
    </div>
  );
}

function CalendarTaskBar({
  canEdit,
  dragged,
  onDragEnd,
  onDragStart,
  onSelect,
  pending,
  segment,
}: {
  canEdit: boolean;
  dragged: boolean;
  onDragEnd: () => void;
  onDragStart: (event: DragEvent<HTMLButtonElement>) => void;
  onSelect: () => void;
  pending?: boolean;
  segment: ReturnType<typeof buildProjectManagementCalendarSegments>[number];
}) {
  const { task } = segment;
  const gridColumn = `${segment.startColumn + 1} / ${segment.endColumn + 2}`;
  const tone = task.isSummary ? 'is-summary' : task.isCritical ? 'is-critical' : '';
  return (
    <button
      aria-label={`任务 ${task.title}，${task.scheduleStartDate ?? task.scheduleDueDate} 至 ${task.scheduleDueDate ?? task.scheduleStartDate}`}
      className={`pm-cal__bar ${tone}${dragged ? ' is-dragging' : ''}`}
      draggable={canEdit && !task.isSummary && !pending}
      onClick={onSelect}
      onDragEnd={onDragEnd}
      onDragStart={onDragStart}
      style={{ gridColumn, gridRow: segment.lane + 1 }}
      title={`${task.taskCode} · ${task.title}${task.isSummary ? '（汇总任务，不可拖动）' : '（拖动调整日期）'}`}
      type="button"
    >
      {task.isSummary ? '▰ ' : ''}{task.title}
    </button>
  );
}

function CalendarDayDrawer({
  canAdd,
  canEdit,
  date,
  milestones,
  onClose,
  onCreate,
  onMove,
  onSelectTask,
  onToggleTaskSelection,
  pending,
  selectedTaskIds,
  tasks,
}: {
  canAdd: boolean;
  canEdit: boolean;
  date: string;
  milestones: ProjectManagementMilestone[];
  onClose: () => void;
  onCreate: () => void;
  onMove: (task: TaskScheduleRow, date: string) => void;
  onSelectTask: (taskId: string) => void;
  onToggleTaskSelection: (taskId: string) => void;
  pending?: boolean;
  selectedTaskIds: ReadonlySet<string>;
  tasks: TaskScheduleRow[];
}) {
  return (
    <aside aria-label={`${date} 任务抽屉`} className="pm-cal__drawer" role="dialog">
      <div className="pm-cal__drawer-head">
        <div>
          <h3>{date}</h3>
          <p>{tasks.length} 项任务</p>
        </div>
        <button className="pm-workbench-command" onClick={onClose} type="button">关闭</button>
      </div>
      {milestones.length ? (
        <div className="pm-cal__drawer-milestones">
          <strong>里程碑</strong>
          <ul>{milestones.map((item) => <li key={item.id}>{item.milestoneName}</li>)}</ul>
        </div>
      ) : null}
      <div className="pm-cal__drawer-list">
        {tasks.map((task) => (
          <article className="pm-cal__drawer-item" key={task.id}>
            <div className="pm-cal__drawer-item-row">
              <input
                aria-label={`选择任务 ${task.title}`}
                checked={selectedTaskIds.has(task.id)}
                onChange={() => onToggleTaskSelection(task.id)}
                type="checkbox"
              />
              <button onClick={() => onSelectTask(task.id)} type="button">
                {task.title}
                <span>{task.taskCode}</span>
              </button>
            </div>
            <p>{task.scheduleStartDate ?? task.scheduleDueDate} — {task.scheduleDueDate ?? task.scheduleStartDate}</p>
            {!task.isSummary ? (
              <label>
                移动到
                <input
                  aria-label={`移动任务 ${task.title} 到日期`}
                  defaultValue={task.startDate ?? task.dueDate}
                  disabled={!canEdit || pending}
                  onChange={(event) => { if (event.target.value) onMove(task, event.target.value); }}
                  type="date"
                />
              </label>
            ) : (
              <p className="pm-cal__drawer-hint">汇总任务由子任务日期决定。</p>
            )}
          </article>
        ))}
      </div>
      {!tasks.length ? <p className="pm-cal__drawer-empty">当天没有任务。</p> : null}
      {canAdd ? (
        <PermissionButton className="pm-primary-button" code="project-management:task:add" onClick={onCreate} type="button">
          在 {date} 新建
        </PermissionButton>
      ) : null}
    </aside>
  );
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
  const first = rows
    .map((task) => task.scheduleStartDate ?? task.scheduleDueDate)
    .filter((value): value is string => Boolean(value))
    .sort()[0];
  return toProjectManagementCalendarDate(first);
}
