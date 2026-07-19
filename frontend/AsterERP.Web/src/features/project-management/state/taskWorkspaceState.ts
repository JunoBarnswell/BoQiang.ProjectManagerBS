import type { ProjectManagementTaskQuery, ProjectManagementTaskView } from '../../../api/project-management/projectManagement.types';

export type TaskWorkspaceGroupBy = NonNullable<ProjectManagementTaskQuery['groupBy']>;
export type TaskWorkspaceSortBy = NonNullable<ProjectManagementTaskQuery['sortBy']>;
export type TaskWorkspaceSortDirection = NonNullable<ProjectManagementTaskQuery['sortDirection']>;

export interface TaskWorkspaceState {
  assigneeUserId?: string;
  dueFrom?: string;
  dueTo?: string;
  groupBy?: TaskWorkspaceGroupBy;
  includeCompleted: boolean;
  keyword: string;
  milestoneId?: string;
  pageIndex: number;
  pageSize: number;
  selectedTaskId?: string;
  sortBy: TaskWorkspaceSortBy;
  sortDirection: TaskWorkspaceSortDirection;
  status?: string;
  viewKey: ProjectManagementTaskView;
}

const views: readonly ProjectManagementTaskView[] = ['tree', 'list', 'card', 'board', 'gantt', 'calendar'];
const groupByValues: readonly TaskWorkspaceGroupBy[] = ['status', 'priority', 'assignee', 'milestone', 'parent', 'label'];
const sortByValues: readonly TaskWorkspaceSortBy[] = ['tree', 'dueDate', 'priority', 'status', 'updated'];

export function createTaskWorkspaceState(viewKey: ProjectManagementTaskView): TaskWorkspaceState {
  return {
    includeCompleted: true,
    keyword: '',
    pageIndex: 1,
    pageSize: 50,
    sortBy: viewKey === 'gantt' || viewKey === 'calendar' ? 'dueDate' : 'tree',
    sortDirection: 'asc',
    viewKey,
  };
}

export function normalizeTaskWorkspaceState(
  viewKey: ProjectManagementTaskView,
  input: Partial<TaskWorkspaceState>,
): TaskWorkspaceState {
  const fallback = createTaskWorkspaceState(viewKey);
  const sortBy = sortByValues.includes(input.sortBy ?? fallback.sortBy) ? input.sortBy ?? fallback.sortBy : fallback.sortBy;
  const groupBy = groupByValues.includes(input.groupBy as TaskWorkspaceGroupBy) ? input.groupBy : undefined;
  const sortDirection: TaskWorkspaceSortDirection = input.sortDirection === 'desc' ? 'desc' : 'asc';

  return {
    assigneeUserId: normalizeText(input.assigneeUserId),
    dueFrom: normalizeDate(input.dueFrom),
    dueTo: normalizeDate(input.dueTo),
    groupBy,
    includeCompleted: input.includeCompleted !== false,
    keyword: normalizeText(input.keyword) ?? '',
    milestoneId: normalizeText(input.milestoneId),
    pageIndex: normalizeInteger(input.pageIndex, 1, 1, 100000),
    pageSize: normalizeInteger(input.pageSize, 50, 1, 200),
    selectedTaskId: normalizeText(input.selectedTaskId),
    sortBy,
    sortDirection,
    status: normalizeText(input.status),
    viewKey: views.includes(viewKey) ? viewKey : 'tree',
  };
}

export function taskWorkspaceStateToQuery(projectId: string, state: TaskWorkspaceState): ProjectManagementTaskQuery {
  return {
    assigneeUserId: state.assigneeUserId,
    dueFrom: state.dueFrom,
    dueTo: state.dueTo,
    groupBy: state.groupBy,
    includeCompleted: state.includeCompleted,
    keyword: state.keyword || undefined,
    milestoneId: state.milestoneId,
    pageIndex: state.pageIndex,
    pageSize: state.pageSize,
    projectId,
    sortBy: state.sortBy,
    sortDirection: state.sortDirection,
    status: state.status,
    viewKey: state.viewKey,
  };
}

export function taskWorkspaceStateToSavedView(state: TaskWorkspaceState) {
  return {
    dueFrom: state.dueFrom,
    dueTo: state.dueTo,
    groupBy: state.groupBy,
    includeCompleted: state.includeCompleted,
    keyword: state.keyword || undefined,
    milestoneId: state.milestoneId,
    sortBy: state.sortBy,
    sortDirection: state.sortDirection,
    status: state.status,
    version: 1,
    viewKey: state.viewKey,
  };
}

function normalizeDate(value: string | undefined): string | undefined {
  const normalized = normalizeText(value);
  return normalized && /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : undefined;
}

function normalizeInteger(value: number | undefined, fallback: number, minimum: number, maximum: number): number {
  return typeof value === 'number' && Number.isInteger(value) && value >= minimum && value <= maximum ? value : fallback;
}

function normalizeText(value: string | undefined): string | undefined {
  const normalized = value?.trim();
  return normalized || undefined;
}
