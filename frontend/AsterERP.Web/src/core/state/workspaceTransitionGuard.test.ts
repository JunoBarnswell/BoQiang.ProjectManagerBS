import { afterEach, describe, expect, it } from 'vitest';

import { getWorkspaceTransitionBlockers, registerWorkspaceTransitionBlocker } from './workspaceTransitionGuard';

describe('workspaceTransitionGuard', () => {
  afterEach(() => {
    registerWorkspaceTransitionBlocker('test', { isDirty: () => false, reason: 'test' })();
  });

  it('returns only active blockers and removes them on unregister', () => {
    let dirty = true;
    const unregister = registerWorkspaceTransitionBlocker('editor', { isDirty: () => dirty, reason: '编辑器有未保存内容' });

    expect(getWorkspaceTransitionBlockers()).toEqual(['编辑器有未保存内容']);
    dirty = false;
    expect(getWorkspaceTransitionBlockers()).toEqual([]);
    dirty = true;
    unregister();
    expect(getWorkspaceTransitionBlockers()).toEqual([]);
  });
});
