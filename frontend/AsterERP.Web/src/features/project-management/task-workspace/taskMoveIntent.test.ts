import { describe, expect, it } from 'vitest';

import type { ProjectManagementTask } from '../../../api/project-management/projectManagement.types';

import { createTaskMoveRequest } from './taskMoveIntent';

const task = (id: string, parentTaskId?: string): ProjectManagementTask => ({
  actualMinutes: 0,
  blockedByCount: 0,
  canStart: true,
  createdTime: '2026-07-18T00:00:00.000Z',
  depth: parentTaskId ? 1 : 0,
  id,
  priority: 'Medium',
  progressPercent: 0,
  projectId: 'project-a',
  sortOrder: 1024,
  status: 'Todo',
  taskCode: id,
  title: id,
  versionNo: 3,
  weight: 1,
  parentTaskId,
});

describe('createTaskMoveRequest', () => {
  it('places a task before a sibling using the sibling parent and version', () => {
    expect(createTaskMoveRequest(task('dragged', 'parent-a'), { kind: 'before', task: task('target', 'parent-b') }))
      .toEqual({ beforeTaskId: 'target', parentTaskId: 'parent-b', sortOrder: 0, versionNo: 3 });
  });

  it('moves a task under a target or appends it at the root', () => {
    expect(createTaskMoveRequest(task('dragged'), { kind: 'child', task: task('target') }))
      .toEqual({ parentTaskId: 'target', sortOrder: 2_147_483_647, versionNo: 3 });
    expect(createTaskMoveRequest(task('dragged'), { kind: 'root' }))
      .toEqual({ sortOrder: 2_147_483_647, versionNo: 3 });
  });

  it('rejects self-targets before issuing a request', () => {
    const dragged = task('dragged');
    expect(createTaskMoveRequest(dragged, { kind: 'before', task: dragged })).toBeNull();
    expect(createTaskMoveRequest(dragged, { kind: 'child', task: dragged })).toBeNull();
  });
});
