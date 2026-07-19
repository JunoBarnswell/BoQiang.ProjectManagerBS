import { describe, expect, it } from 'vitest';

import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';
import { buildTaskBoardColumns } from './taskBoardProjectionModel';

const task = (id: string, status: string, assigneeUserId?: string) => ({ id, assigneeUserId, blockedByCount: 0, canStart: true, depth: 0, dueDate: undefined, labels: [], parentTaskId: undefined, priority: 'Medium', progressPercent: 0, projectId: 'project-a', sortOrder: 1, startDate: undefined, status, taskCode: id, title: id, versionNo: 1 } as ProjectManagementTaskListItem);

describe('taskBoardProjectionModel', () => {
  it('keeps state-machine columns and groups each column into swimlanes', () => {
    const columns = buildTaskBoardColumns([task('todo-a', 'Todo', 'u1'), task('todo-b', 'Todo', 'u2'), task('done-a', 'Done')], 'assignee');

    expect(columns.map((column) => column.status)).toEqual(['Todo', 'InProgress', 'Blocked', 'Done', 'Cancelled']);
    expect(columns[0]?.rows).toHaveLength(2);
    expect(columns[0]?.lanes.map((lane) => lane.key)).toEqual(['u1', 'u2']);
    expect(columns[3]?.rows).toHaveLength(1);
  });
});
