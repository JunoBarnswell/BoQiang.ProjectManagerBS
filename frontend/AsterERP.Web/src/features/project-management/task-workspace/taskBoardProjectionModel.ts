import type { ProjectManagementTaskListItem, ProjectManagementTaskQuery } from '../../../api/project-management/projectManagement.types';
import type { TaskWorkspaceGroupBy } from '../state/taskWorkspaceState';

import { groupTaskCards, type TaskCardGroup } from './taskCardProjectionModel';

export const taskBoardStatuses = ['Backlog', 'Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled'] as const;
export type TaskBoardStatus = (typeof taskBoardStatuses)[number];

export interface TaskBoardColumn {
  lanes: TaskCardGroup[];
  rows: ProjectManagementTaskListItem[];
  status: TaskBoardStatus;
}

export interface TaskBoardColumnMetric {
  loaded: number;
  status: TaskBoardStatus;
  total?: number;
}

export function buildTaskBoardColumnQuery(
  baseQuery: ProjectManagementTaskQuery,
  status: TaskBoardStatus,
  pageIndex: number,
): ProjectManagementTaskQuery {
  return {
    ...baseQuery,
    pageIndex: Math.max(pageIndex, 1),
    pageSize: baseQuery.pageSize ?? 50,
    status,
    viewKey: 'board',
  };
}

export function summarizeTaskBoardColumns(metrics: readonly TaskBoardColumnMetric[]) {
  const loaded = metrics.reduce((sum, metric) => sum + metric.loaded, 0);
  const total = metrics.every((metric) => typeof metric.total === 'number')
    ? metrics.reduce((sum, metric) => sum + (metric.total ?? 0), 0)
    : undefined;
  return { loaded, total };
}

export function buildTaskBoardColumns(rows: ProjectManagementTaskListItem[], groupBy?: TaskWorkspaceGroupBy): TaskBoardColumn[] {
  return taskBoardStatuses.map((status) => {
    const statusRows = rows.filter((task) => task.status === status);
    return { lanes: groupTaskCards(statusRows, groupBy), rows: statusRows, status };
  });
}
