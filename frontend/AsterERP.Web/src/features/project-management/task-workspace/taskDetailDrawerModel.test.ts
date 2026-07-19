import { describe, expect, it } from 'vitest';

import { HttpError } from '../../../core/http/httpError';

import { readProjectManagementTaskConflict, taskDetailSections, taskDetailToForm } from './taskDetailDrawerModel';

const detail = {
  id: 'task-1',
  projectId: 'project-1',
  taskCode: 'T-001',
  title: '服务器任务',
  status: 'InProgress',
  priority: 'High',
  progressPercent: 40,
  sortOrder: 1,
  depth: 0,
  versionNo: 7,
  blockedByCount: 0,
  canStart: true,
  weight: 1,
  actualMinutes: 10,
  createdTime: '2026-07-19T00:00:00Z',
  summary: '服务器摘要',
} as const;

describe('taskDetailDrawerModel', () => {
  it('builds the unified edit form without losing summary, markdown or version', () => {
    const form = taskDetailToForm({ ...detail, markdown: '# 服务器描述' } as never);
    expect(form).toMatchObject({
      markdown: '# 服务器描述',
      summary: '服务器摘要',
      title: '服务器任务',
      versionNo: 7,
    });
  });

  it('reads a structured 409 task conflict for field comparison', () => {
    const error = new HttpError({
      data: {
        fieldConflicts: [{ displayName: '任务标题', field: 'Title', localValue: '本地标题', serverValue: '服务器标题' }],
        localValues: { operation: 'update', submittedFields: ['Title'], title: '本地标题', versionNo: 6 },
        serverValues: detail,
      },
      message: '冲突',
      status: 409,
    });
    const conflict = readProjectManagementTaskConflict(error);
    expect(conflict?.serverValues.versionNo).toBe(7);
    expect(conflict?.fieldConflicts[0].localValue).toBe('本地标题');
  });

  it('rejects non-conflict failures and keeps the tab contract stable', () => {
    expect(readProjectManagementTaskConflict(new Error('保存失败'))).toBeNull();
    expect(taskDetailSections.map((section) => section.key)).toEqual([
      'basic', 'children', 'comments', 'attachments', 'reminders', 'dependencies', 'activity',
    ]);
  });
});
