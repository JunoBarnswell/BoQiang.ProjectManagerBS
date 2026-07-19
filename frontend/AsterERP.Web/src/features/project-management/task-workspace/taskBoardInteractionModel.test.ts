import { describe, expect, it } from 'vitest';

import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

import { applyOptimisticBoardMove, clearOptimisticBoardMove, rollbackOptimisticBoardMove } from './taskBoardInteractionModel';

const task: ProjectManagementTaskListItem = {
  id: 'task-1',
  projectId: 'project-1',
  taskCode: 'T-1',
  title: '任务一',
  status: 'Todo',
  priority: 'Medium',
  progressPercent: 20,
  sortOrder: 7,
  depth: 0,
  versionNo: 4,
  blockedByCount: 0,
  canStart: true,
};

describe('taskBoardInteractionModel', () => {
  it('keeps the original version and ordering fields in an optimistic snapshot', () => {
    const applied = applyOptimisticBoardMove({}, task, 'InProgress', 20);

    expect(applied.rows['task-1']).toMatchObject({ status: 'InProgress', progressPercent: 20, sortOrder: 7, versionNo: 4 });
    expect(applied.snapshot.task).toBe(task);
  });

  it('restores the original row on failure and removes it after reconciliation', () => {
    const applied = applyOptimisticBoardMove({}, task, 'Done', 100);
    expect(rollbackOptimisticBoardMove(applied.rows, applied.snapshot)['task-1']).toBe(task);
    expect(clearOptimisticBoardMove(applied.rows, task.id)).toEqual({});
  });
});
