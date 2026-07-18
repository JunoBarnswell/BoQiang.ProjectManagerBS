import { describe, expect, it } from 'vitest';

import type { ProjectManagementWorkspaceScope } from '../../features/project-management/state/projectManagementWorkspaceScope';

import { projectManagementQueryKeys } from './projectManagementQueryKeys';

const mesScope: ProjectManagementWorkspaceScope = {
  appCode: 'MES',
  isAvailable: true,
  tenantId: 'tenant-a',
};

describe('projectManagementQueryKeys', () => {
  it('isolates every project query by tenant and application', () => {
    const mesKey = projectManagementQueryKeys.tasks(mesScope, {
      pageIndex: 1,
      pageSize: 50,
      projectId: 'project-a',
      viewKey: 'board',
    });
    const wmsKey = projectManagementQueryKeys.tasks(
      { ...mesScope, appCode: 'WMS' },
      { pageIndex: 1, pageSize: 50, projectId: 'project-a', viewKey: 'board' },
    );

    expect(mesKey).not.toEqual(wmsKey);
    expect(mesKey.slice(0, 4)).toEqual(['astererp', 'project-management', 'tenant-a', 'MES']);
  });

  it('keeps each task query dimension in the cache key', () => {
    expect(
      projectManagementQueryKeys.tasks(mesScope, {
        assigneeUserId: 'alice',
        dueFrom: '2026-07-01',
        dueTo: '2026-07-31',
        groupBy: 'status',
        includeCompleted: false,
        keyword: 'release',
        milestoneId: 'milestone-a',
        pageIndex: 2,
        pageSize: 10,
        projectId: 'project-a',
        sortBy: 'dueDate',
        sortDirection: 'desc',
        status: 'InProgress',
        viewKey: 'calendar',
      }),
    ).toEqual([
      'astererp',
      'project-management',
      'tenant-a',
      'MES',
      'tasks',
      'project-a',
      'calendar',
      2,
      10,
      'release',
      'InProgress',
      'alice',
      'status',
      'dueDate',
      'desc',
      'milestone-a',
      '',
      '2026-07-01',
      '2026-07-31',
      false,
    ]);
  });

  it('provides a project root for precise cross-view invalidation', () => {
    expect(projectManagementQueryKeys.tasksProject(mesScope, 'project-a')).toEqual([
      'astererp',
      'project-management',
      'tenant-a',
      'MES',
      'tasks',
      'project-a',
    ]);
  });

  it('creates project-scoped roots for task comments and attachments', () => {
    expect(projectManagementQueryKeys.taskCommentsProject(mesScope, 'project-a')).toEqual([
      'astererp', 'project-management', 'tenant-a', 'MES', 'task-comments', 'project-a',
    ]);
    expect(projectManagementQueryKeys.taskAttachmentsProject(mesScope, 'project-a')).toEqual([
      'astererp', 'project-management', 'tenant-a', 'MES', 'task-attachments', 'project-a',
    ]);
  });

  it('isolates my-work pagination, project selection and category', () => {
    expect(projectManagementQueryKeys.myWork(mesScope, {
      category: 'overdue', pageIndex: 2, pageSize: 50, projectId: 'project-a', sortBy: 'updated', sortDirection: 'asc',
    })).toEqual([
      'astererp', 'project-management', 'tenant-a', 'MES', 'my-work', 2, 50, 'project-a', 'overdue', 'updated', 'asc', false,
    ]);
  });
});
