import { describe, expect, it } from 'vitest';

import type { ProjectManagementTaskBatchExecutionResult } from '../../../api/project-management/projectManagement.types';

import { taskBatchResultToCsv } from './taskBatchExecutionModel';

describe('taskBatchResultToCsv', () => {
  it('keeps every partial result and escapes CSV values', () => {
    const result: ProjectManagementTaskBatchExecutionResult = {
      conflictCount: 1,
      failedCount: 1,
      items: [
        { message: '版本冲突', status: 'conflict', taskCode: 'T-1', taskId: '1', versionNo: 2 },
        { message: '字段,无效', status: 'failed', taskCode: 'T-2', taskId: '2' },
      ],
      operationId: 'op-1',
      projectId: 'p-1',
      requestedCount: 2,
      skippedCount: 0,
      succeededCount: 0,
    };

    const csv = taskBatchResultToCsv(result);
    expect(csv).toContain('"op-1","p-1","1","T-1","冲突","版本冲突"');
    expect(csv).toContain('"2","T-2","失败","字段,无效"');
  });
});
