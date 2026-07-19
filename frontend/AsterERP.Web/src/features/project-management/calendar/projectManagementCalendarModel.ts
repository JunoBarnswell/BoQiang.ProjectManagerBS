import type { TaskScheduleRow } from '../task-workspace/taskScheduleProjectionModel';

export type ProjectManagementCalendarMode = 'month' | 'week';

export interface ProjectManagementCalendarWeek {
  days: Date[];
  key: string;
}

export interface ProjectManagementCalendarTaskSegment {
  endColumn: number;
  lane: number;
  startColumn: number;
  task: TaskScheduleRow;
}

export function buildProjectManagementCalendarWeeks(anchorDate: Date, mode: ProjectManagementCalendarMode): ProjectManagementCalendarWeek[] {
  const visibleStart = startOfWeek(mode === 'month' ? startOfMonth(anchorDate) : anchorDate);
  const dayCount = mode === 'month' ? 42 : 7;
  const days = Array.from({ length: dayCount }, (_, index) => addDays(visibleStart, index));
  return Array.from({ length: dayCount / 7 }, (_, index) => {
    const weekDays = days.slice(index * 7, index * 7 + 7);
    return { days: weekDays, key: formatDateKey(weekDays[0] ?? visibleStart) };
  });
}

export function buildProjectManagementCalendarSegments(rows: readonly TaskScheduleRow[], weekDays: readonly Date[]): ProjectManagementCalendarTaskSegment[] {
  const firstDay = weekDays[0];
  const lastDay = weekDays.at(-1);
  if (!firstDay || !lastDay) return [];

  const candidates = rows
    .flatMap((task) => {
      const start = toLocalDate(task.scheduleStartDate ?? task.scheduleDueDate);
      const due = toLocalDate(task.scheduleDueDate ?? task.scheduleStartDate);
      if (!start || !due || due < firstDay || start > lastDay) return [];
      const startColumn = Math.max(0, calendarDayDelta(firstDay, start));
      const endColumn = Math.min(6, Math.max(startColumn, calendarDayDelta(firstDay, due)));
      return [{ endColumn, startColumn, task }];
    })
    .sort((left, right) => left.startColumn - right.startColumn || right.endColumn - left.endColumn || left.task.title.localeCompare(right.task.title));

  const laneEnds: number[] = [];
  return candidates.map((candidate) => {
    const lane = laneEnds.findIndex((lastEnd) => lastEnd < candidate.startColumn);
    const assignedLane = lane < 0 ? laneEnds.length : lane;
    laneEnds[assignedLane] = candidate.endColumn;
    return { ...candidate, lane: assignedLane };
  });
}

export function shiftProjectManagementCalendarTask(task: Pick<TaskScheduleRow, 'dueDate' | 'startDate'>, targetStartDate: string): { dueDate: string | undefined; startDate: string | undefined } {
  const currentStart = toLocalDate(task.startDate ?? task.dueDate);
  const currentDue = toLocalDate(task.dueDate ?? task.startDate);
  const targetStart = toLocalDate(targetStartDate);
  if (!currentStart || !currentDue || !targetStart) return { dueDate: task.dueDate, startDate: task.startDate };
  const delta = calendarDayDelta(currentStart, targetStart);
  return {
    dueDate: task.dueDate ? formatDateKey(addDays(currentDue, delta)) : undefined,
    startDate: task.startDate ? formatDateKey(targetStart) : undefined,
  };
}

export function addProjectManagementCalendarDays(value: Date, days: number): Date {
  return addDays(value, days);
}

export function formatProjectManagementCalendarDate(value: Date): string {
  return formatDateKey(value);
}

export function isProjectManagementCalendarDateInMonth(value: Date, anchorDate: Date): boolean {
  return value.getFullYear() === anchorDate.getFullYear() && value.getMonth() === anchorDate.getMonth();
}

export function toProjectManagementCalendarDate(value: string | undefined): Date | undefined {
  return toLocalDate(value);
}

function startOfMonth(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), 1);
}

function startOfWeek(value: Date): Date {
  return addDays(new Date(value.getFullYear(), value.getMonth(), value.getDate()), -value.getDay());
}

function calendarDayDelta(from: Date, to: Date): number {
  return Math.round((startOfDay(to).getTime() - startOfDay(from).getTime()) / 86_400_000);
}

function addDays(value: Date, days: number): Date {
  const next = new Date(value);
  next.setDate(next.getDate() + days);
  return next;
}

function startOfDay(value: Date): Date {
  return new Date(value.getFullYear(), value.getMonth(), value.getDate());
}

function toLocalDate(value: string | undefined): Date | undefined {
  if (!value) return undefined;
  const date = new Date(`${value.slice(0, 10)}T00:00:00`);
  return Number.isNaN(date.getTime()) ? undefined : date;
}

function formatDateKey(value: Date): string {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
}
