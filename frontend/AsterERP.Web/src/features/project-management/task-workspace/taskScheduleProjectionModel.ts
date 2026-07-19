import type { ProjectManagementMilestone, ProjectManagementTaskDependency, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

export interface TaskScheduleRow extends ProjectManagementTaskListItem {
  scheduleStartDate?: string;
  scheduleDueDate?: string;
  isSummary: boolean;
  childTaskCount: number;
  isCritical: boolean;
}

export interface ScheduleWindow {
  start: Date;
  days: Date[];
}

export function buildTaskScheduleRows(rows: readonly ProjectManagementTaskListItem[], dependencies: readonly ProjectManagementTaskDependency[] = []): TaskScheduleRow[] {
  const childrenByParent = new Map<string, ProjectManagementTaskListItem[]>();
  rows.forEach((row) => {
    if (row.parentTaskId) childrenByParent.set(row.parentTaskId, [...(childrenByParent.get(row.parentTaskId) ?? []), row]);
  });
  const criticalIds = deriveCriticalTaskIds(rows, dependencies);
  const descendantCache = new Map<string, ProjectManagementTaskListItem[]>();
  const descendants = (taskId: string, visiting = new Set<string>()): ProjectManagementTaskListItem[] => {
    if (descendantCache.has(taskId)) return descendantCache.get(taskId) ?? [];
    if (visiting.has(taskId)) return [];
    const nextVisiting = new Set(visiting).add(taskId);
    const children = childrenByParent.get(taskId) ?? [];
    const result = [...children, ...children.flatMap((child) => descendants(child.id, nextVisiting))];
    descendantCache.set(taskId, result);
    return result;
  };

  return rows.map((row) => {
    const childRows = descendants(row.id);
    const aggregate = [row, ...childRows];
    const starts = aggregate.map((item) => item.startDate).filter((value): value is string => Boolean(value));
    const dues = aggregate.map((item) => item.dueDate).filter((value): value is string => Boolean(value));
    const isSummary = childRows.length > 0;
    const progress = isSummary
      ? Math.round(childRows.reduce((sum, item) => sum + item.progressPercent, 0) / childRows.length)
      : row.progressPercent;
    return {
      ...row,
      scheduleStartDate: minDate(starts) ?? row.startDate,
      scheduleDueDate: maxDate(dues) ?? row.dueDate,
      progressPercent: progress,
      isSummary,
      childTaskCount: childRows.length,
      isCritical: criticalIds.has(row.id),
    };
  });
}

export function deriveCriticalTaskIds(rows: readonly ProjectManagementTaskListItem[], dependencies: readonly ProjectManagementTaskDependency[]): Set<string> {
  const byId = new Map(rows.map((row) => [row.id, row]));
  const predecessors = new Map<string, string[]>();
  dependencies.forEach((dependency) => {
    if (!byId.has(dependency.predecessorTaskId) || !byId.has(dependency.successorTaskId)) return;
    predecessors.set(dependency.successorTaskId, [...(predecessors.get(dependency.successorTaskId) ?? []), dependency.predecessorTaskId]);
  });
  const memo = new Map<string, { duration: number; path: string[] }>();
  const longestPath = (taskId: string, visiting = new Set<string>()): { duration: number; path: string[] } => {
    const cached = memo.get(taskId);
    if (cached) return cached;
    if (visiting.has(taskId)) return { duration: 0, path: [] };
    const task = byId.get(taskId);
    if (!task) return { duration: 0, path: [] };
    const nextVisiting = new Set(visiting).add(taskId);
    const ownDuration = dateDuration(task.startDate, task.dueDate);
    const candidates = (predecessors.get(taskId) ?? []).map((id) => longestPath(id, nextVisiting));
    const best = candidates.sort((left, right) => right.duration - left.duration)[0] ?? { duration: 0, path: [] };
    const result = { duration: best.duration + ownDuration, path: [...best.path, taskId] };
    memo.set(taskId, result);
    return result;
  };
  const longest = rows.map((row) => longestPath(row.id)).sort((left, right) => right.duration - left.duration)[0];
  return new Set(longest?.path ?? []);
}

export function createScheduleWindow(anchor: Date, dayCount: number): ScheduleWindow {
  const start = startOfDay(anchor);
  return { start, days: Array.from({ length: dayCount }, (_, index) => addCalendarDays(start, index)) };
}

export function getSchedulePlacement(startDate: string | undefined, dueDate: string | undefined, range: ScheduleWindow): { startOffset: number; endOffset: number } | undefined {
  const start = toLocalDate(startDate ?? dueDate);
  const end = toLocalDate(dueDate ?? startDate);
  if (!start || !end) return undefined;
  const startOffset = Math.floor((start.getTime() - range.start.getTime()) / 86_400_000);
  const endOffset = Math.floor((end.getTime() - range.start.getTime()) / 86_400_000);
  if (startOffset > range.days.length - 1 || endOffset < 0) return undefined;
  return { startOffset: Math.max(0, startOffset), endOffset: Math.min(range.days.length - 1, Math.max(startOffset, endOffset)) };
}

export function shiftScheduleDate(value: string | undefined, dayDelta: number): string | undefined {
  const date = toLocalDate(value);
  return date ? formatDateKey(addCalendarDays(date, dayDelta)) : undefined;
}

export function validateScheduleMove(
  task: Pick<ProjectManagementTaskListItem, 'id'>,
  nextStartDate: string | undefined,
  nextDueDate: string | undefined,
  rows: readonly ProjectManagementTaskListItem[],
  dependencies: readonly ProjectManagementTaskDependency[],
  projectStartDate?: string,
  projectDueDate?: string,
): string | undefined {
  const start = toLocalDate(nextStartDate);
  const due = toLocalDate(nextDueDate);
  if (nextStartDate && !start || nextDueDate && !due) return '日期格式无效';
  if (start && due && start > due) return '开始日期不能晚于截止日期';
  const projectStart = toLocalDate(projectStartDate);
  const projectDue = toLocalDate(projectDueDate);
  if (start && projectStart && start < projectStart) return '任务开始日期不能早于项目开始日期';
  if (due && projectDue && due > projectDue) return '任务截止日期不能晚于项目截止日期';
  const byId = new Map(rows.map((row) => [row.id, row]));
  for (const dependency of dependencies) {
    if (dependency.successorTaskId === task.id) {
      const predecessorDue = toLocalDate(byId.get(dependency.predecessorTaskId)?.dueDate);
      if (start && predecessorDue && start < addCalendarDays(predecessorDue, Math.ceil(dependency.lagMinutes / 1440))) return '不能早于前置任务完成时间';
    }
    if (dependency.predecessorTaskId === task.id) {
      const successorStart = toLocalDate(byId.get(dependency.successorTaskId)?.startDate);
      if (due && successorStart && due > addCalendarDays(successorStart, -Math.ceil(dependency.lagMinutes / 1440))) return '不能晚于后置任务开始时间';
    }
  }
  return undefined;
}

export function createCalendarEvents(rows: readonly TaskScheduleRow[]): Record<string, Array<{ task: TaskScheduleRow; kind: 'start' | 'due' }>> {
  const events: Record<string, Array<{ task: TaskScheduleRow; kind: 'start' | 'due' }>> = {};
  rows.forEach((task) => {
    if (task.scheduleStartDate) events[task.scheduleStartDate.slice(0, 10)] = [...(events[task.scheduleStartDate.slice(0, 10)] ?? []), { task, kind: 'start' }];
    if (task.scheduleDueDate && task.scheduleDueDate.slice(0, 10) !== task.scheduleStartDate?.slice(0, 10)) events[task.scheduleDueDate.slice(0, 10)] = [...(events[task.scheduleDueDate.slice(0, 10)] ?? []), { task, kind: 'due' }];
  });
  return events;
}

export function milestonePlacement(milestones: readonly ProjectManagementMilestone[], range: ScheduleWindow): Array<{ milestone: ProjectManagementMilestone; offset: number }> {
  return milestones.flatMap((milestone) => {
    const date = toLocalDate(milestone.dueDate ?? milestone.startDate);
    if (!date) return [];
    const offset = Math.floor((date.getTime() - range.start.getTime()) / 86_400_000);
    return offset >= 0 && offset < range.days.length ? [{ milestone, offset }] : [];
  });
}

function dateDuration(startDate?: string, dueDate?: string): number {
  const start = toLocalDate(startDate ?? dueDate);
  const due = toLocalDate(dueDate ?? startDate);
  return start && due ? Math.max(1, Math.floor((due.getTime() - start.getTime()) / 86_400_000) + 1) : 1;
}

function minDate(values: readonly string[]): string | undefined { return [...values].sort()[0]; }
function maxDate(values: readonly string[]): string | undefined { return [...values].sort().at(-1); }
function toLocalDate(value: string | undefined): Date | undefined { if (!value) return undefined; const date = new Date(`${value.slice(0, 10)}T00:00:00`); return Number.isNaN(date.getTime()) ? undefined : date; }
function startOfDay(value: Date): Date { return new Date(value.getFullYear(), value.getMonth(), value.getDate()); }
function addCalendarDays(value: Date, days: number): Date { const next = new Date(value); next.setDate(next.getDate() + days); return next; }
function formatDateKey(value: Date): string { return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`; }
