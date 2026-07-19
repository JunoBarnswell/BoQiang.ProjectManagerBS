import { describe, expect, it } from 'vitest';

import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

import { getTaskCardRisks, groupTaskCards, isOverdue } from './taskCardProjectionModel';

function task(id: string, overrides: Partial<ProjectManagementTaskListItem> = {}): ProjectManagementTaskListItem {
  return {
    id,
    projectId: 'project-1',
    taskCode: id,
    title: id,
    status: 'Todo',
    priority: 'Medium',
    progressPercent: 0,
    sortOrder: 0,
    depth: 0,
    versionNo: 1,
    blockedByCount: 0,
    canStart: true,
    ...overrides,
  };
}

describe('taskCardProjection', () => {
  it('分组数量与卡片明细保持一致', () => {
    const rows = [task('a', { status: 'Todo' }), task('b', { status: 'Done' }), task('c', { status: 'Todo' })];
    const groups = groupTaskCards(rows, 'status');
    expect(groups.map((group) => [group.label, group.rows.length])).toEqual([['待开始', 2], ['已完成', 1]]);
    expect(groups.flatMap((group) => group.rows).map((row) => row.id)).toEqual(['a', 'c', 'b']);
  });

  it('识别逾期、紧急、阻塞和完成风险视觉', () => {
    const now = new Date(2026, 6, 19, 12);
    expect(getTaskCardRisks(task('a', { dueDate: '2026-07-18', priority: 'Urgent', blockedByCount: 1, canStart: false }), now)).toEqual(['urgent', 'blocked', 'overdue']);
    expect(getTaskCardRisks(task('b', { status: 'Done', dueDate: '2026-07-01' }), now)).toEqual(['done']);
    expect(isOverdue('2026-07-18', 'Todo', now)).toBe(true);
    expect(isOverdue('2026-07-18', 'Cancelled', now)).toBe(false);
  });

  it('空结果保留可渲染的单一空分组', () => {
    expect(groupTaskCards([], 'assignee')).toEqual([{ key: 'all', label: '全部任务', rows: [] }]);
  });
});
