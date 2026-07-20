import type { QueryClient, QueryKey } from '@tanstack/react-query';

import type { ProjectManagementTaskDetail, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { isHttpError } from '../../../core/http/httpError';

import { readProjectManagementTaskConflict } from './projectManagementTaskDetailModel';

type TaskListEnvelope = { data: { total: number; items: ProjectManagementTaskListItem[] } };

export type TaskStatusPatch = Partial<Pick<ProjectManagementTaskListItem, 'status' | 'progressPercent' | 'versionNo'>>;

export function mergeTaskStatusFields(
  items: ProjectManagementTaskListItem[],
  taskId: string,
  patch: TaskStatusPatch,
): ProjectManagementTaskListItem[] {
  return items.map((item) => (item.id === taskId ? { ...item, ...patch } : item));
}

export function patchTaskListCaches(
  queryClient: Pick<QueryClient, 'setQueriesData'>,
  queryKey: QueryKey,
  taskId: string,
  patch: TaskStatusPatch,
): void {
  queryClient.setQueriesData<TaskListEnvelope>({ queryKey }, (current) =>
    current?.data?.items
      ? {
          ...current,
          data: {
            ...current.data,
            items: mergeTaskStatusFields(current.data.items, taskId, patch),
          },
        }
      : current,
  );
}

export function patchTaskListCachesFromDetail(
  queryClient: Pick<QueryClient, 'setQueriesData'>,
  queryKey: QueryKey,
  detail: Pick<ProjectManagementTaskDetail, 'id' | 'status' | 'progressPercent' | 'versionNo'>,
): void {
  patchTaskListCaches(queryClient, queryKey, detail.id, {
    status: detail.status,
    progressPercent: detail.progressPercent,
    versionNo: detail.versionNo,
  });
}

export function isTaskStatusRevisionConflict(error: unknown): boolean {
  return Boolean(readProjectManagementTaskConflict(error)) || (isHttpError(error) && error.status === 409);
}
