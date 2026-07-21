import { describe, expect, it, vi } from 'vitest';

import { HttpError } from '../../../core/http/httpError';

import {
  isTaskStatusRevisionConflict,
  mergeTaskStatusFields,
  patchTaskListCaches,
  patchTaskListCachesFromDetail,
} from './projectManagementTaskStatusCache';

const baseTask = {
  id: 'task-a',
  projectId: 'project-a',
  taskCode: 'REQ-1',
  title: '需求 A',
  status: 'Backlog',
  priority: 'Medium',
  progressPercent: 0,
  weight: 1,
  sortOrder: 1,
  depth: 0,
  versionNo: 3,
  createdTime: '2026-01-01T00:00:00Z',
  blockedByCount: 0,
  canStart: true,
} as const;

describe('projectManagementTaskStatusCache', () => {
  it('merges confirmed status fields including versionNo', () => {
    const items = mergeTaskStatusFields([baseTask], 'task-a', {
      status: 'Todo',
      progressPercent: 10,
      versionNo: 4,
    });
    expect(items[0]).toMatchObject({ status: 'Todo', progressPercent: 10, versionNo: 4 });
  });

  it('patches only list-shaped caches and leaves unrelated payloads alone', () => {
    const setQueriesData = vi.fn((_filter, updater: (current: unknown) => unknown) => {
      expect(updater({ data: { total: 1, items: [baseTask] } })).toEqual({
        data: {
          total: 1,
          items: [{ ...baseTask, status: 'InProgress', progressPercent: 40, versionNo: 5 }],
        },
      });
      expect(updater({ edges: [] })).toEqual({ edges: [] });
      expect(updater(undefined)).toBeUndefined();
    });

    patchTaskListCaches({ setQueriesData }, ['tasks', 'project-a'], 'task-a', { status: 'InProgress' });
    patchTaskListCachesFromDetail({ setQueriesData }, ['tasks', 'project-a'], {
      id: 'task-a',
      status: 'InProgress',
      progressPercent: 40,
      versionNo: 5,
    });
    expect(setQueriesData).toHaveBeenCalledTimes(2);
  });

  it('detects revision conflicts from 409 payloads', () => {
    expect(isTaskStatusRevisionConflict(new HttpError({
      data: {
        fieldConflicts: [],
        localValues: { title: '本地' },
        serverValues: {
          id: 'task-a',
          taskCode: 'REQ-1',
          title: '服务端',
          versionNo: 9,
        },
      },
      message: 'conflict',
      status: 409,
    }))).toBe(true);
    expect(isTaskStatusRevisionConflict(new HttpError({ message: 'conflict', status: 409 }))).toBe(true);
    expect(isTaskStatusRevisionConflict(new HttpError({ message: 'bad', status: 400 }))).toBe(false);
  });
});
