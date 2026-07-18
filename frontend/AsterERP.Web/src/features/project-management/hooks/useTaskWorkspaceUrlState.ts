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
    () => normalizeTaskWorkspaceState(viewKey, {
      assigneeUserId: searchParams.get('assignee') ?? undefined,
      dueFrom: searchParams.get('dueFrom') ?? undefined,
      dueTo: searchParams.get('dueTo') ?? undefined,
      groupBy: searchParams.get('groupBy') as TaskWorkspaceState['groupBy'],
      includeCompleted: searchParams.get('completed') !== 'false',
      keyword: searchParams.get('q') ?? '',
      milestoneId: searchParams.get('milestoneId') ?? undefined,
      pageIndex: parseInteger(searchParams.get('page')),
      pageSize: parseInteger(searchParams.get('pageSize')),
      selectedTaskId: searchParams.get('taskId') ?? searchParams.get('selectedTaskId') ?? undefined,
      sortBy: searchParams.get('sortBy') as TaskWorkspaceState['sortBy'],
      sortDirection: searchParams.get('sortDirection') as TaskWorkspaceState['sortDirection'],
      status: searchParams.get('status') ?? undefined,
    }),
    [searchParams, viewKey],
  );

  const setState = useCallback(
    (next: Partial<TaskWorkspaceState>, options: { replace?: boolean } = {}) => {
      const normalized = normalizeTaskWorkspaceState(viewKey, { ...state, ...next });
      const params = new URLSearchParams();
      setOptional(params, 'q', normalized.keyword);
      setOptional(params, 'status', normalized.status);
      setOptional(params, 'assignee', normalized.assigneeUserId);
      setOptional(params, 'milestoneId', normalized.milestoneId);
      setOptional(params, 'groupBy', normalized.groupBy);
      setOptional(params, 'dueFrom', normalized.dueFrom);
      setOptional(params, 'dueTo', normalized.dueTo);
      setOptional(params, 'taskId', normalized.selectedTaskId);
      if (!normalized.includeCompleted) params.set('completed', 'false');
      if (normalized.sortBy !== (viewKey === 'gantt' || viewKey === 'calendar' ? 'dueDate' : 'tree')) params.set('sortBy', normalized.sortBy);
      if (normalized.sortDirection !== 'asc') params.set('sortDirection', normalized.sortDirection);
      if (normalized.pageIndex !== 1) params.set('page', String(normalized.pageIndex));
      if (normalized.pageSize !== 50) params.set('pageSize', String(normalized.pageSize));
      setSearchParams(params, { replace: options.replace ?? false });
    },
    [setSearchParams, state, viewKey],
  );

  return { state, setState };
}

function parseInteger(value: string | null): number | undefined {
  if (!value) return undefined;
  const parsed = Number(value);
  return Number.isInteger(parsed) ? parsed : undefined;
}

function setOptional(params: URLSearchParams, key: string, value: string | undefined): void {
  if (value) params.set(key, value);
}
