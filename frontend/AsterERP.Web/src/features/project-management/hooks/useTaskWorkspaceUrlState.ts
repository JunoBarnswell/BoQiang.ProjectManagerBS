import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';

import type { ProjectManagementTaskView } from '../../../api/project-management/projectManagement.types';
import {
  normalizeTaskWorkspaceState,
  type TaskWorkspaceState,
} from '../state/taskWorkspaceState';

export function useTaskWorkspaceUrlState(viewKey: ProjectManagementTaskView) {
  const [searchParams, setSearchParams] = useSearchParams();
  const state = useMemo(
    () => normalizeTaskWorkspaceState(readViewKey(searchParams.get('view'), viewKey), {
      assigneeUserId: searchParams.get('assignee') ?? undefined,
      dueFrom: searchParams.get('dueFrom') ?? undefined,
      dueTo: searchParams.get('dueTo') ?? undefined,
      groupBy: searchParams.get('groupBy') as TaskWorkspaceState['groupBy'],
      includeCompleted: searchParams.get('completed') !== 'false',
      ganttZoom: parseInteger(searchParams.get('ganttZoom')) as TaskWorkspaceState['ganttZoom'],
      keyword: searchParams.get('q') ?? '',
      labelIds: parseCsv(searchParams.get('labelIds')),
      labelMatchMode: searchParams.get('labelMatchMode') as TaskWorkspaceState['labelMatchMode'],
      milestoneId: searchParams.get('milestoneId') ?? undefined,
      pageIndex: parseInteger(searchParams.get('page')),
      pageSize: parseInteger(searchParams.get('pageSize')),
      selectedTaskId: searchParams.get('taskId') ?? searchParams.get('selectedTaskId') ?? undefined,
      sortBy: searchParams.get('sortBy') as TaskWorkspaceState['sortBy'],
      sortDirection: searchParams.get('sortDirection') as TaskWorkspaceState['sortDirection'],
      status: searchParams.get('status') ?? undefined,
      visibleColumns: parseCsv(searchParams.get('columns')),
    }),
    [searchParams, viewKey],
  );

  const setState = useCallback(
    (next: Partial<TaskWorkspaceState>, options: { replace?: boolean } = {}) => {
      const nextViewKey = next.viewKey ?? state.viewKey;
      const normalized = normalizeTaskWorkspaceState(nextViewKey, { ...state, ...next });
      setSearchParams(createTaskWorkspaceSearchParams(nextViewKey, normalized), { replace: options.replace ?? false });
    },
    [setSearchParams, state, viewKey],
  );

  return { state, setState };
}

export function createTaskWorkspaceSearchParams(viewKey: ProjectManagementTaskView, normalized: TaskWorkspaceState): URLSearchParams {
  const params = new URLSearchParams();
  if (viewKey !== 'tree') params.set('view', viewKey);
  setOptional(params, 'q', normalized.keyword);
  setOptional(params, 'status', normalized.status);
  setOptional(params, 'assignee', normalized.assigneeUserId);
  setOptional(params, 'milestoneId', normalized.milestoneId);
  setOptional(params, 'groupBy', normalized.groupBy);
  setOptional(params, 'dueFrom', normalized.dueFrom);
  setOptional(params, 'dueTo', normalized.dueTo);
  setOptional(params, 'taskId', normalized.selectedTaskId);
  setOptional(params, 'labelIds', normalized.labelIds.join(','));
  if (normalized.labelMatchMode === 'All') params.set('labelMatchMode', 'All');
  if (normalized.visibleColumns.length > 0) params.set('columns', normalized.visibleColumns.join(','));
  if (normalized.ganttZoom !== 56) params.set('ganttZoom', String(normalized.ganttZoom));
  if (!normalized.includeCompleted) params.set('completed', 'false');
  if (normalized.sortBy !== (viewKey === 'gantt' || viewKey === 'calendar' ? 'dueDate' : 'tree')) params.set('sortBy', normalized.sortBy);
  if (normalized.sortDirection !== 'asc') params.set('sortDirection', normalized.sortDirection);
  if (normalized.pageIndex !== 1) params.set('page', String(normalized.pageIndex));
  if (normalized.pageSize !== 50) params.set('pageSize', String(normalized.pageSize));
  return params;
}

export function hasTaskWorkspaceUrlOverrides(search: string): boolean {
  const params = new URLSearchParams(search);
  return ['q', 'status', 'assignee', 'milestoneId', 'groupBy', 'dueFrom', 'dueTo', 'completed', 'sortBy', 'sortDirection', 'labelIds', 'labelMatchMode', 'columns', 'ganttZoom'].some((key) => params.has(key));
}

function parseInteger(value: string | null): number | undefined {
  if (!value) return undefined;
  const parsed = Number(value);
  return Number.isInteger(parsed) ? parsed : undefined;
}

function setOptional(params: URLSearchParams, key: string, value: string | undefined): void {
  if (value) params.set(key, value);
}

function parseCsv(value: string | null): string[] | undefined {
  return value ? value.split(',') : undefined;
}

function readViewKey(value: string | null, fallback: ProjectManagementTaskView): ProjectManagementTaskView {
  return value && ['tree', 'list', 'card', 'board', 'gantt', 'calendar'].includes(value)
    ? value as ProjectManagementTaskView
    : fallback;
}
