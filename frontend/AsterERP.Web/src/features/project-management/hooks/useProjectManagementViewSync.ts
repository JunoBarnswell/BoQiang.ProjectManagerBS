import type { QueryClient } from '@tanstack/react-query';

import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { queryKeys } from '../../../core/query/queryKeys';
import type { ProjectManagementTaskDetail, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import {
  getProjectManagementViewSyncInvalidationTargets,
  type ProjectManagementViewSyncInvalidation,
  type ProjectManagementViewSyncInvalidationTarget,
} from '../view-sync/projectManagementViewSyncModel';

export interface ProjectManagementViewSyncContext {
  projectId: string;
  scope: ProjectManagementWorkspaceScope;
}

type ProjectManagementTaskPage = { total: number; items: ProjectManagementTaskListItem[] };

const structuralTaskFields = new Set([
  'assigneeUserId', 'dueDate', 'isDeleted', 'milestoneId', 'parentTaskId', 'projectId', 'requirementSource',
  'requirementType', 'riskLevel', 'sortOrder', 'startDate', 'status', 'taskCode', 'workItemType',
]);

/**
 * Applies a server Patch to already loaded task/detail caches. Query invalidation remains the fallback for
 * structural/filter membership changes; field-only edits stay in place and do not reset the editor or scroll.
 */
export function applyProjectManagementTaskPatch(
  queryClient: Pick<QueryClient, 'setQueryData' | 'setQueriesData'>,
  context: ProjectManagementViewSyncContext,
  event: ProjectManagementViewSyncInvalidation,
): boolean {
  if (!context.scope.isAvailable || event.projectId !== context.projectId || event.aggregateType !== 'Task' || !event.patch || typeof queryClient.setQueryData !== 'function' || typeof queryClient.setQueriesData !== 'function') return false;
  const patch = event.patch;
  queryClient.setQueryData<ApiEnvelope<ProjectManagementTaskDetail>>(
    queryKeys.projectManagement.task(context.scope, context.projectId, event.aggregateId),
    (current) => current?.data?.id === event.aggregateId
      ? { ...current, data: mergeTaskPatch(current.data, patch) }
      : current,
  );
  const structural = (event.changedFields ?? Object.keys(patch)).some(field => structuralTaskFields.has(field));
  if (structural) return false;
  queryClient.setQueriesData<ApiEnvelope<ProjectManagementTaskPage>>(
    { queryKey: queryKeys.projectManagement.tasksProject(context.scope, context.projectId) },
    (current) => current?.data?.items
      ? { ...current, data: { ...current.data, items: current.data.items.map(item => item.id === event.aggregateId ? mergeTaskPatch(item, patch) : item) } }
      : current,
  );
  return true;
}

/**
 * Invalidates all cached task views for one tenant/application/project, never a global project-management prefix.
 * Callers keep URL state separately so a refreshed view retains its current filter and selection context.
 */
export async function invalidateProjectManagementViewSyncCaches(
  queryClient: Pick<QueryClient, 'invalidateQueries'>,
  context: ProjectManagementViewSyncContext,
  event: ProjectManagementViewSyncInvalidation,
): Promise<void> {
  if (!context.scope.isAvailable || !context.projectId || event.projectId !== context.projectId) return;

  await invalidateProjectManagementViewSyncTargets(queryClient, context, getProjectManagementViewSyncInvalidationTargets(event));
}

function mergeTaskPatch<T extends ProjectManagementTaskListItem | ProjectManagementTaskDetail>(task: T, patch: Record<string, unknown>): T {
  const next = { ...task } as T & Record<string, unknown>;
  const fields = [
    'taskCode', 'title', 'summary', 'status', 'priority', 'milestoneId', 'parentTaskId', 'assigneeUserId',
    'startDate', 'dueDate', 'progressPercent', 'workItemType', 'contentJson', 'contentText', 'riskLevel',
    'requirementType', 'requirementSource', 'storyPoints', 'mentionUserIds', 'versionNo', 'updatedTime',
  ];
  for (const field of fields) if (Object.prototype.hasOwnProperty.call(patch, field)) next[field] = patch[field];
  return next;
}

export async function invalidateProjectManagementViewSyncTargets(
  queryClient: Pick<QueryClient, 'invalidateQueries'>,
  context: ProjectManagementViewSyncContext,
  targets: Iterable<ProjectManagementViewSyncInvalidationTarget>,
): Promise<void> {
  if (!context.scope.isAvailable || !context.projectId) return;

  await Promise.all([...targets].map((target) => {
    switch (target) {
      case 'tasks':
        return queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.tasksProject(context.scope, context.projectId) });
      case 'overview':
        return queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.overview(context.scope, { projectId: context.projectId }) });
      case 'task-attachments':
        return queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.taskAttachmentsProject(context.scope, context.projectId) });
      case 'task-comments':
        return queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.taskCommentsProject(context.scope, context.projectId) });
      case 'task-reminders':
        return queryClient.invalidateQueries({ queryKey: queryKeys.projectManagement.taskRemindersProject(context.scope, context.projectId) });
    }
  }));
}
