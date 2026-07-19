import { describe, expect, it } from 'vitest';

import {
  createTaskWorkspaceState,
  normalizeTaskWorkspaceState,
  taskWorkspaceStateToQuery,
  taskWorkspaceStateToSavedView,
} from './taskWorkspaceState';

describe('task workspace state', () => {
  it('normalizes unsafe URL state into the shared task query contract', () => {
    const state = normalizeTaskWorkspaceState('calendar', {
      dueFrom: '2026-07-01',
      dueTo: 'invalid',
      groupBy: 'not-supported' as never,
      includeCompleted: false,
      keyword: ' release ',
      pageIndex: 0,
      pageSize: 1000,
      sortBy: 'not-supported' as never,
      sortDirection: 'desc',
      status: ' InProgress ',
    });

    expect(state).toMatchObject({
      dueFrom: '2026-07-01',
      dueTo: undefined,
      groupBy: undefined,
      includeCompleted: false,
      keyword: 'release',
      pageIndex: 1,
      pageSize: 50,
      sortBy: 'dueDate',
      sortDirection: 'desc',
      status: 'InProgress',
      viewKey: 'calendar',
    });
  });

  it('keeps the same semantic query while changing only the projection key', () => {
    const source = normalizeTaskWorkspaceState('tree', {
      assigneeUserId: 'alice',
      keyword: 'release',
      milestoneId: 'm-1',
      selectedTaskId: 'task-1',
      status: 'Todo',
    });
    const board = normalizeTaskWorkspaceState('board', source);

    expect(taskWorkspaceStateToQuery('project-1', board)).toMatchObject({
      assigneeUserId: 'alice',
      keyword: 'release',
      milestoneId: 'm-1',
      projectId: 'project-1',
      status: 'Todo',
      viewKey: 'board',
    });
    expect(board.selectedTaskId).toBe('task-1');
  });

  it('excludes transient selection and pagination from a saved view', () => {
    const state = normalizeTaskWorkspaceState('board', { pageIndex: 3, selectedTaskId: 'task-a', status: 'Todo' });

    expect(taskWorkspaceStateToSavedView(state)).toMatchObject({ status: 'Todo', version: 2, viewKey: 'board' });
    expect(taskWorkspaceStateToSavedView(state)).not.toHaveProperty('selectedTaskId');
    expect(taskWorkspaceStateToSavedView(state)).not.toHaveProperty('pageIndex');
    expect(createTaskWorkspaceState('board').viewKey).toBe('board');
  });

  it('keeps label filters, table columns and gantt zoom in the saved-view state only', () => {
    const state = normalizeTaskWorkspaceState('gantt', { ganttZoom: 84, labelIds: ['label-a', 'label-a'], labelMatchMode: 'All', visibleColumns: ['title', 'dueDate', 'unknown'] });
    const saved = taskWorkspaceStateToSavedView(state);

    expect(saved).toMatchObject({ ganttZoom: 84, labelIds: ['label-a'], labelMatchMode: 'All', visibleColumns: ['title', 'dueDate'] });
    expect(saved).not.toHaveProperty('pageSize');
  });
});
