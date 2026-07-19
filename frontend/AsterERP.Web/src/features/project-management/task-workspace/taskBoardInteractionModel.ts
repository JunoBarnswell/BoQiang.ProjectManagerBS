import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

export interface TaskBoardMoveSnapshot {
  task: ProjectManagementTaskListItem;
  nextStatus: string;
  nextProgress: number;
}

export function applyOptimisticBoardMove(
  current: Readonly<Record<string, ProjectManagementTaskListItem>>,
  task: ProjectManagementTaskListItem,
  nextStatus: string,
  nextProgress: number,
): { rows: Record<string, ProjectManagementTaskListItem>; snapshot: TaskBoardMoveSnapshot } {
  return {
    rows: {
      ...current,
      [task.id]: { ...task, status: nextStatus, progressPercent: nextProgress },
    },
    snapshot: { task, nextStatus, nextProgress },
  };
}

export function rollbackOptimisticBoardMove(
  current: Readonly<Record<string, ProjectManagementTaskListItem>>,
  snapshot: TaskBoardMoveSnapshot,
): Record<string, ProjectManagementTaskListItem> {
  const next = { ...current };
  next[snapshot.task.id] = snapshot.task;
  return next;
}

export function clearOptimisticBoardMove(
  current: Readonly<Record<string, ProjectManagementTaskListItem>>,
  taskId: string,
): Record<string, ProjectManagementTaskListItem> {
  const next = { ...current };
  delete next[taskId];
  return next;
}
