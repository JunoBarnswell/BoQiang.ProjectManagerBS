import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

export type TaskMoveDropTarget =
  | { kind: 'before'; task: ProjectManagementTaskListItem }
  | { kind: 'child'; task: ProjectManagementTaskListItem }
  | { kind: 'root' };

export type TaskGroupBy = 'assignee' | 'milestone' | 'parent' | 'label';
export interface TaskGroupDropTarget {
  kind: 'group';
  groupBy: TaskGroupBy;
  groupValue: string;
  status?: string;
}

export interface ProjectManagementTaskMoveRequest {
  beforeTaskId?: string;
  parentTaskId?: string;
  sortOrder: number;
  versionNo: number;
}

const appendSortOrder = 2_147_483_647;

export function createTaskMoveRequest(
  task: ProjectManagementTaskListItem,
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
