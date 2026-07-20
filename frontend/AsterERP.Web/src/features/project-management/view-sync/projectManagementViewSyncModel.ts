import type { ProjectManagementTaskView } from '../../../api/project-management/projectManagement.types';

export interface ProjectManagementViewSyncState {
  assigneeUserId?: string;
  dueFrom?: string;
  dueTo?: string;
  groupBy?: 'status' | 'priority' | 'assignee' | 'milestone' | 'parent' | 'label';
  includeCompleted: boolean;
  keyword: string;
  milestoneId?: string;
  pageIndex: number;
  pageSize: number;
  selectedTaskId?: string;
  sortBy: 'tree' | 'dueDate' | 'priority' | 'status' | 'updated';
  sortDirection: 'asc' | 'desc';
  status?: string;
  viewKey: ProjectManagementTaskView;
}

export interface ProjectManagementViewSyncInvalidation {
  aggregateId: string;
  aggregateType: 'Project' | 'ProjectMember' | 'Task' | 'TaskAttachment' | 'TaskComment' | 'TaskReminder';
  changedFields?: string[];
  eventType: string;
  eventId?: string;
  patch?: Record<string, unknown>;
  projectId: string;
  version: number;
}

export type ProjectManagementViewSyncInvalidationTarget = 'overview' | 'task-attachments' | 'task-comments' | 'task-reminders' | 'tasks';

const validViews: readonly ProjectManagementTaskView[] = ['tree', 'list', 'card', 'board', 'gantt', 'calendar'];
const validGroupBy: readonly NonNullable<ProjectManagementViewSyncState['groupBy']>[] = ['status', 'priority', 'assignee', 'milestone', 'parent', 'label'];
const validSortBy: readonly ProjectManagementViewSyncState['sortBy'][] = ['tree', 'dueDate', 'priority', 'status', 'updated'];

export function preserveProjectManagementViewSyncState(state: ProjectManagementViewSyncState, targetView: ProjectManagementTaskView): ProjectManagementViewSyncState {
  return { ...state, viewKey: targetView };
}

export function serializeProjectManagementViewSyncState(state: ProjectManagementViewSyncState): URLSearchParams {
  const params = new URLSearchParams();
  setOptional(params, 'q', state.keyword);
  setOptional(params, 'status', state.status);
  setOptional(params, 'assignee', state.assigneeUserId);
  setOptional(params, 'milestoneId', state.milestoneId);
  setOptional(params, 'groupBy', state.groupBy);
  setOptional(params, 'dueFrom', state.dueFrom);
  setOptional(params, 'dueTo', state.dueTo);
  setOptional(params, 'taskId', state.selectedTaskId);
  if (!state.includeCompleted) params.set('completed', 'false');
  if (state.sortBy !== defaultSortBy(state.viewKey)) params.set('sortBy', state.sortBy);
  if (state.sortDirection !== 'asc') params.set('sortDirection', state.sortDirection);
  if (state.pageIndex !== 1) params.set('page', String(state.pageIndex));
  if (state.pageSize !== 50) params.set('pageSize', String(state.pageSize));
  return params;
}

export function parseProjectManagementViewSyncState(viewKey: ProjectManagementTaskView, params: URLSearchParams): ProjectManagementViewSyncState {
  const normalizedView = validViews.includes(viewKey) ? viewKey : 'tree';
  const groupBy = params.get('groupBy');
  const sortBy = params.get('sortBy') as ProjectManagementViewSyncState['sortBy'];
  return {
    assigneeUserId: normalizeText(params.get('assignee')),
    dueFrom: normalizeDate(params.get('dueFrom')),
    dueTo: normalizeDate(params.get('dueTo')),
    groupBy: groupBy && validGroupBy.includes(groupBy as NonNullable<ProjectManagementViewSyncState['groupBy']>)
      ? groupBy as NonNullable<ProjectManagementViewSyncState['groupBy']>
      : undefined,
    includeCompleted: params.get('completed') !== 'false',
    keyword: normalizeText(params.get('q')) ?? '',
    milestoneId: normalizeText(params.get('milestoneId')),
    pageIndex: normalizeInteger(params.get('page'), 1, 1, 100_000),
    pageSize: normalizeInteger(params.get('pageSize'), 50, 1, 200),
    selectedTaskId: normalizeText(params.get('taskId') ?? params.get('selectedTaskId')),
    sortBy: validSortBy.includes(sortBy) ? sortBy : defaultSortBy(normalizedView),
    sortDirection: params.get('sortDirection') === 'desc' ? 'desc' : 'asc',
    status: normalizeText(params.get('status')),
    viewKey: normalizedView,
  };
}

export function getProjectManagementViewSyncInvalidationTargets(event: ProjectManagementViewSyncInvalidation): readonly ProjectManagementViewSyncInvalidationTarget[] {
  switch (event.aggregateType) {
    case 'Task':
    case 'Project':
    case 'ProjectMember':
      return ['tasks', 'overview'];
    case 'TaskAttachment':
      return ['task-attachments'];
    case 'TaskComment':
      return ['task-comments'];
    case 'TaskReminder':
      return ['task-reminders'];
  }
}

export function shouldClearProjectManagementViewSyncSelection(event: ProjectManagementViewSyncInvalidation, selectedTaskId?: string): boolean {
  if (event.aggregateType !== 'Task' || !selectedTaskId || event.aggregateId !== selectedTaskId) return false;
  return /delete|remove|archive|purge|access.?revok/i.test(event.eventType);
}

export function isProjectManagementViewSyncInvalidation(value: unknown): value is ProjectManagementViewSyncInvalidation {
  if (!value || typeof value !== 'object') return false;
  const event = value as Record<string, unknown>;
  return typeof event.aggregateId === 'string'
    && typeof event.aggregateType === 'string'
    && typeof event.eventType === 'string'
    && typeof event.projectId === 'string'
    && typeof event.version === 'number'
    && Number.isFinite(event.version)
    && (event.changedFields === undefined || (Array.isArray(event.changedFields) && event.changedFields.every(item => typeof item === 'string')))
    && (event.patch === undefined || (typeof event.patch === 'object' && event.patch !== null && !Array.isArray(event.patch)))
    && ['Project', 'ProjectMember', 'Task', 'TaskAttachment', 'TaskComment', 'TaskReminder'].includes(event.aggregateType);
}

function defaultSortBy(viewKey: ProjectManagementTaskView): ProjectManagementViewSyncState['sortBy'] {
  return viewKey === 'gantt' || viewKey === 'calendar' ? 'dueDate' : 'tree';
}

function setOptional(params: URLSearchParams, key: string, value: string | undefined): void {
  if (value) params.set(key, value);
}

function normalizeText(value: string | null): string | undefined {
  const normalized = value?.trim();
  return normalized || undefined;
}

function normalizeDate(value: string | null): string | undefined {
  const normalized = normalizeText(value);
  return normalized && /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : undefined;
}

function normalizeInteger(value: string | null, fallback: number, minimum: number, maximum: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum ? parsed : fallback;
}
