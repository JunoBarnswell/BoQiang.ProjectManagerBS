import type { QueryClient } from '@tanstack/react-query';

import { queryKeys } from '../../../core/query/queryKeys';
import type { ProjectManagementWorkspaceScope } from '../state/projectManagementWorkspaceScope';
import {
  getProjectManagementViewSyncInvalidationTargets,
  type ProjectManagementViewSyncInvalidation,
} from '../view-sync/projectManagementViewSyncModel';

export interface ProjectManagementViewSyncContext {
  projectId: string;
  scope: ProjectManagementWorkspaceScope;
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

  const targets = getProjectManagementViewSyncInvalidationTargets(event);
  await Promise.all(targets.map((target) => {
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
