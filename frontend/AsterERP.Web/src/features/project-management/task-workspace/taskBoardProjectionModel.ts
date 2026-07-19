import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import type { TaskWorkspaceGroupBy } from '../state/taskWorkspaceState';
import { groupTaskCards, type TaskCardGroup } from './taskCardProjectionModel';

export const taskBoardStatuses = ['Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled'] as const;

export interface TaskBoardColumn {
  lanes: TaskCardGroup[];
  rows: ProjectManagementTaskListItem[];
  status: (typeof taskBoardStatuses)[number];
}

export function buildTaskBoardColumns(rows: ProjectManagementTaskListItem[], groupBy?: TaskWorkspaceGroupBy): TaskBoardColumn[] {
  return taskBoardStatuses.map((status) => {
    const statusRows = rows.filter((task) => task.status === status);
    return { lanes: groupTaskCards(statusRows, groupBy), rows: statusRows, status };
  });
}
