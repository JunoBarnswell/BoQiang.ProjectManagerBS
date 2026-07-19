import { describe, expect, it } from 'vitest';

import type { ProjectManagementTaskListItem } from '../../../api/project-management/projectManagement.types';

import {
  buildVisibleTaskTreeRows,
  taskTreeAriaLevel,
  taskTreeExpansionPreferenceKey,
  toggleTaskTreeExpansion,
} from './taskTreeState';

function task(id: string, parentTaskId: string | undefined, depth: number, progressPercent: number, hasChildren = false): ProjectManagementTaskListItem & { hasChildren: boolean } {
  return {
    id,
    projectId: 'project-1',
    parentTaskId,
    taskCode: id,
    title: id,
    status: 'Todo',
    priority: 'Medium',
    progressPercent,
    sortOrder: depth,
    depth,
    versionNo: 1,
    blockedByCount: 0,
    canStart: true,
    hasChildren,
  };
}

describe('taskTreeState', () => {
  it('隔离用户、租户、应用和项目的展开状态 key', () => {
    expect(taskTreeExpansionPreferenceKey('user-a', 'tenant-a', 'SYSTEM', 'project-a')).not.toBe(taskTreeExpansionPreferenceKey('user-b', 'tenant-a', 'SYSTEM', 'project-a'));
    expect(taskTreeExpansionPreferenceKey('user-a', 'tenant-a', 'SYSTEM', 'project-a')).not.toBe(taskTreeExpansionPreferenceKey('user-a', 'tenant-a', 'SYSTEM', 'project-b'));
  });

  it('按树层级隐藏未展开子任务并保留服务端父任务进度', () => {
    const rows = [task('root', undefined, 0, 40, true), task('child', 'root', 1, 80), task('grandchild', 'child', 2, 100)];
    expect(buildVisibleTaskTreeRows(rows, new Set())).toHaveLength(1);
    const visible = buildVisibleTaskTreeRows(rows, new Set(['root', 'child']));
    expect(visible.map((row) => row.id)).toEqual(['root', 'child', 'grandchild']);
    expect(visible[0]?.progressPercent).toBe(40);
    expect(taskTreeAriaLevel(visible[2]!)).toBe(3);
  });

  it('展开状态可逆且不重复记录任务', () => {
    const expanded = toggleTaskTreeExpansion({ expandedTaskIds: [] }, 'root');
    expect(expanded).toEqual({ expandedTaskIds: ['root'] });
    expect(toggleTaskTreeExpansion(expanded, 'root')).toEqual({ expandedTaskIds: [] });
  });
});
