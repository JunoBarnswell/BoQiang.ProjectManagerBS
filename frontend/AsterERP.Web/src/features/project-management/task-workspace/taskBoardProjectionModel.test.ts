import { describe, expect, it } from 'vitest';

import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

import { buildTaskBoardColumnQuery, buildTaskBoardColumns, summarizeTaskBoardColumns } from './taskBoardProjectionModel';

const task = (id: string, status: string, assigneeUserId?: string) => ({ id, assigneeUserId, blockedByCount: 0, canStart: true, depth: 0, dueDate: undefined, labels: [], parentTaskId: undefined, priority: 'Medium', progressPercent: 0, projectId: 'project-a', sortOrder: 1, startDate: undefined, status, taskCode: id, title: id, versionNo: 1 } as ProjectManagementTaskListItem);

describe('taskBoardProjectionModel', () => {
  it('keeps state-machine columns and groups each column into swimlanes', () => {
    const columns = buildTaskBoardColumns([task('todo-a', 'Todo', 'u1'), task('todo-b', 'Todo', 'u2'), task('done-a', 'Done')], 'assignee');

    expect(columns.map((column) => column.status)).toEqual(['Backlog', 'Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled']);
    expect(columns[1]?.rows).toHaveLength(2);
    expect(columns[1]?.lanes.map((lane) => lane.key)).toEqual(['u1', 'u2']);
    expect(columns[4]?.rows).toHaveLength(1);
  });

  it('builds an independent status query without changing shared filters', () => {
    const query = buildTaskBoardColumnQuery({ projectId: 'project-a', keyword: 'release', pageIndex: 3, pageSize: 25, viewKey: 'board', includeCompleted: true }, 'Blocked', 2);

    expect(query).toMatchObject({ projectId: 'project-a', keyword: 'release', pageIndex: 2, pageSize: 25, status: 'Blocked', viewKey: 'board' });
  });

  it('summarizes server totals and loaded rows only when every column has a total', () => {
    expect(summarizeTaskBoardColumns([
      { status: 'Backlog', loaded: 2, total: 5 },
      { status: 'Todo', loaded: 1, total: 1 },
    ])).toEqual({ loaded: 3, total: 6 });
    expect(summarizeTaskBoardColumns([
      { status: 'Backlog', loaded: 2, total: 5 },
      { status: 'Todo', loaded: 0 },
    ])).toEqual({ loaded: 2, total: undefined });
  });
});
