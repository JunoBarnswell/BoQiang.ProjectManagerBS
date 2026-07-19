import { describe, expect, it } from 'vitest';

import { resolveBoardStatusProgress, rollbackBoardStatus } from './taskBoardStatusMutationModel';

describe('taskBoardStatusMutationModel', () => {
  it('normalizes Done entry and Done exit progress', () => {
    expect(resolveBoardStatusProgress({ progressPercent: 35, status: 'InProgress' }, 'Done')).toBe(100);
    expect(resolveBoardStatusProgress({ progressPercent: 100, status: 'Done' }, 'Todo')).toBe(0);
    expect(resolveBoardStatusProgress({ progressPercent: 35, status: 'Todo' }, 'InProgress')).toBe(35);
  });

  it('rolls back only the task that failed', () => {
    expect(rollbackBoardStatus({ taskA: 'Done', taskB: 'InProgress' }, 'taskA', 'Todo')).toEqual({ taskA: 'Todo', taskB: 'InProgress' });
  });
});
