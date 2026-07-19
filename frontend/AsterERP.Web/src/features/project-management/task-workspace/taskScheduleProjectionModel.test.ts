import { describe, expect, it } from 'vitest';

import type { ProjectManagementTaskDependency, ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

import { adjustTaskSchedule, buildSubtreeScheduleChanges, buildTaskScheduleRows, deriveCriticalTaskIds, validateScheduleMove } from './taskScheduleProjectionModel';

const task = (id: string, dates: Pick<ProjectManagementTaskListItem, 'startDate' | 'dueDate'>, parentTaskId?: string, progressPercent = 0): ProjectManagementTaskListItem => ({
  id,
  projectId: 'project-a',
  parentTaskId,
  taskCode: id,
  title: id,
  status: 'Todo',
  priority: 'Medium',
  startDate: dates.startDate,
  dueDate: dates.dueDate,
  progressPercent,
  sortOrder: 1,
  depth: parentTaskId ? 1 : 0,
  versionNo: 1,
  blockedByCount: 0,
  canStart: true,
  labels: [],
});

const dependency = (predecessorTaskId: string, successorTaskId: string): ProjectManagementTaskDependency => ({
  id: `${predecessorTaskId}-${successorTaskId}`,
  projectId: 'project-a',
  predecessorTaskId,
  successorTaskId,
  dependencyType: 'FinishToStart',
  lagMinutes: 0,
  versionNo: 1,
});

describe('taskScheduleProjectionModel', () => {
  it('derives parent summary dates and progress from descendants', () => {
    const rows = buildTaskScheduleRows([
      task('parent', {}, undefined, 0),
      task('child-a', { startDate: '2026-07-20', dueDate: '2026-07-22' }, 'parent', 40),
      task('child-b', { startDate: '2026-07-23', dueDate: '2026-07-28' }, 'parent', 80),
    ]);

    expect(rows[0]).toMatchObject({ scheduleStartDate: '2026-07-20', scheduleDueDate: '2026-07-28', progressPercent: 60, isSummary: true, childTaskCount: 2 });
  });

  it('marks the longest dependency path as critical', () => {
    const rows = [
      task('a', { startDate: '2026-07-20', dueDate: '2026-07-22' }),
      task('b', { startDate: '2026-07-23', dueDate: '2026-07-28' }),
      task('c', { startDate: '2026-07-20', dueDate: '2026-07-21' }),
    ];

    expect(deriveCriticalTaskIds(rows, [dependency('a', 'b')])).toEqual(new Set(['a', 'b']));
  });

  it('rejects a schedule move that violates a predecessor dependency', () => {
    const rows = [
      task('predecessor', { startDate: '2026-07-20', dueDate: '2026-07-22' }),
      task('successor', { startDate: '2026-07-23', dueDate: '2026-07-25' }),
    ];

    expect(validateScheduleMove({ id: 'successor' }, '2026-07-21', '2026-07-24', rows, [dependency('predecessor', 'successor')])).toBe('不能早于前置任务完成时间');
  });

  it('moves and resizes calendar dates without DST-sensitive timestamps', () => {
    const row = buildTaskScheduleRows([task('dst', { startDate: '2026-03-08', dueDate: '2026-03-10' })])[0];
    expect(adjustTaskSchedule(row, 'move', 2)).toMatchObject({ startDate: '2026-03-10', dueDate: '2026-03-12' });
    expect(adjustTaskSchedule(row, 'resize-start', 1)).toMatchObject({ startDate: '2026-03-09', dueDate: '2026-03-10' });
    expect(adjustTaskSchedule(row, 'resize-end', -3)).toBeUndefined();
  });

  it('moves a subtree only for leaf and manually scheduled summary tasks', () => {
    const rows = buildTaskScheduleRows([
      task('aggregate-parent', {}, undefined),
      task('manual-parent', { startDate: '2026-07-01', dueDate: '2026-07-02' }, 'aggregate-parent'),
      task('leaf', { startDate: '2026-07-03', dueDate: '2026-07-04' }, 'manual-parent'),
    ]);
    expect(buildSubtreeScheduleChanges('aggregate-parent', rows, 2)).toEqual([
      expect.objectContaining({ taskId: 'manual-parent', startDate: '2026-07-03' }),
      expect.objectContaining({ taskId: 'leaf', startDate: '2026-07-05' }),
    ]);
  });
});
