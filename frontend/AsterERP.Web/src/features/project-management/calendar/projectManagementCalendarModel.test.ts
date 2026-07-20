import { describe, expect, it } from 'vitest';

import type { TaskScheduleRow } from '../state/projectManagementScheduleModel';

import {
  buildProjectManagementCalendarSegments,
  buildProjectManagementCalendarWeeks,
  formatProjectManagementCalendarDate,
  shiftProjectManagementCalendarTask,
} from './projectManagementCalendarModel';

const row = (id: string, startDate: string, dueDate: string): TaskScheduleRow => ({
  blockedByCount: 0,
  canStart: true,
  childTaskCount: 0,
  depth: 0,
  dueDate,
  id,
  isCritical: false,
  isSummary: false,
  priority: 'Medium',
  progressPercent: 0,
  projectId: 'project-a',
  scheduleDueDate: dueDate,
  scheduleStartDate: startDate,
  sortOrder: 1,
  startDate,
  status: 'Todo',
  taskCode: id,
  title: id,
  versionNo: 1,
});

describe('projectManagementCalendarModel', () => {
  it('creates a complete six-week month grid from its Sunday boundary', () => {
    const weeks = buildProjectManagementCalendarWeeks(new Date('2026-07-19T00:00:00'), 'month');

    expect(weeks).toHaveLength(6);
    expect(formatProjectManagementCalendarDate(weeks[0]?.days[0] ?? new Date())).toBe('2026-06-28');
    expect(formatProjectManagementCalendarDate(weeks.at(-1)?.days.at(-1) ?? new Date())).toBe('2026-08-08');
  });

  it('keeps a cross-week task as a continuous segment within each week', () => {
    const task = row('cross-week', '2026-07-03', '2026-07-08');
    const weeks = buildProjectManagementCalendarWeeks(new Date('2026-07-19T00:00:00'), 'month');

    expect(buildProjectManagementCalendarSegments([task], weeks[0]?.days ?? [])).toMatchObject([{ startColumn: 5, endColumn: 6 }]);
    expect(buildProjectManagementCalendarSegments([task], weeks[1]?.days ?? [])).toMatchObject([{ startColumn: 0, endColumn: 3 }]);
  });

  it('preserves the task duration when moving its start date', () => {
    expect(shiftProjectManagementCalendarTask(row('move', '2026-07-03', '2026-07-08'), '2026-07-10')).toEqual({
      startDate: '2026-07-10',
      dueDate: '2026-07-15',
    });
  });
});
