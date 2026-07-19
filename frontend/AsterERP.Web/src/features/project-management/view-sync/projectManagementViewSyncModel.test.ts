import { describe, expect, it } from 'vitest';

import {
  getProjectManagementViewSyncInvalidationTargets,
  parseProjectManagementViewSyncState,
  preserveProjectManagementViewSyncState,
  serializeProjectManagementViewSyncState,
  shouldClearProjectManagementViewSyncSelection,
} from './projectManagementViewSyncModel';

describe('projectManagementViewSyncModel', () => {
  it('preserves all sharable filters and selection while changing views', () => {
    const state = parseProjectManagementViewSyncState('board', new URLSearchParams('q=phase+one&status=InProgress&assignee=user-a&milestoneId=ms-a&groupBy=assignee&dueFrom=2026-07-01&dueTo=2026-07-31&taskId=task-a&completed=false&sortBy=updated&sortDirection=desc&page=2&pageSize=100'));

    const calendar = preserveProjectManagementViewSyncState(state, 'calendar');
    expect(calendar).toMatchObject({ milestoneId: 'ms-a', selectedTaskId: 'task-a', status: 'InProgress', viewKey: 'calendar' });
    expect(serializeProjectManagementViewSyncState(calendar).toString()).toContain('taskId=task-a');
    expect(serializeProjectManagementViewSyncState(calendar).toString()).toContain('milestoneId=ms-a');
  });

  it('uses the calendar due-date default without writing redundant URL state', () => {
    const state = parseProjectManagementViewSyncState('calendar', new URLSearchParams());

    expect(state.sortBy).toBe('dueDate');
    expect(serializeProjectManagementViewSyncState(state).toString()).not.toContain('sortBy=');
  });

  it('maps task changes to every project task view and clears a deleted selection only', () => {
    const taskDeleted = { aggregateId: 'task-a', aggregateType: 'Task' as const, eventType: 'Deleted', projectId: 'project-a', version: 5 };

    expect(getProjectManagementViewSyncInvalidationTargets(taskDeleted)).toEqual(['tasks', 'overview']);
    expect(shouldClearProjectManagementViewSyncSelection(taskDeleted, 'task-a')).toBe(true);
    expect(shouldClearProjectManagementViewSyncSelection({ ...taskDeleted, aggregateId: 'task-b' }, 'task-a')).toBe(false);
    expect(shouldClearProjectManagementViewSyncSelection({ ...taskDeleted, eventType: 'Updated' }, 'task-a')).toBe(false);
  });
});
