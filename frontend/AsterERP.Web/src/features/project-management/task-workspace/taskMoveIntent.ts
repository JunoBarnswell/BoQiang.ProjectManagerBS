import type { ProjectManagementTask } from '../../../api/project-management/projectManagement.types';

export type TaskMoveDropTarget =
  | { kind: 'before'; task: ProjectManagementTask }
  | { kind: 'child'; task: ProjectManagementTask }
  | { kind: 'root' };

export interface ProjectManagementTaskMoveRequest {
  beforeTaskId?: string;
  parentTaskId?: string;
  sortOrder: number;
  versionNo: number;
}

const appendSortOrder = 2_147_483_647;

export function createTaskMoveRequest(
  task: ProjectManagementTask,
  target: TaskMoveDropTarget,
): ProjectManagementTaskMoveRequest | null {
  if (target.kind === 'before') {
    if (target.task.id === task.id) return null;
    return {
      parentTaskId: target.task.parentTaskId,
      beforeTaskId: target.task.id,
      sortOrder: 0,
      versionNo: task.versionNo,
    };
  }

  if (target.kind === 'child') {
    if (target.task.id === task.id) return null;
    return {
      parentTaskId: target.task.id,
      sortOrder: appendSortOrder,
      versionNo: task.versionNo,
    };
  }

  return {
    sortOrder: appendSortOrder,
    versionNo: task.versionNo,
  };
}
