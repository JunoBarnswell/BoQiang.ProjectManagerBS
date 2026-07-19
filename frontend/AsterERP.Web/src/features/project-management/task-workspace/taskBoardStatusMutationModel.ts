import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

export function resolveBoardStatusProgress(task: Pick<ProjectManagementTaskListItem, 'progressPercent' | 'status'>, nextStatus: string): number {
  if (nextStatus === 'Done') return 100;
  if (task.status === 'Done') return 0;
  return task.progressPercent;
}

export function rollbackBoardStatus(
  current: Readonly<Record<string, string>>,
  taskId: string,
  previousStatus: string,
): Record<string, string> {
  return { ...current, [taskId]: previousStatus };
}
