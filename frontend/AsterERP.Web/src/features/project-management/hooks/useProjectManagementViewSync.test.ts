import type { QueryClient } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';


import { applyProjectManagementTaskPatch, invalidateProjectManagementViewSyncCaches } from './useProjectManagementViewSync';

const scope = { appCode: 'MES', isAvailable: true, tenantId: 'tenant-a' };

describe('invalidateProjectManagementViewSyncCaches', () => {
  it('invalidates every cached view only for the affected tenant/application/project', async () => {
    const invalidateQueries = vi.fn().mockResolvedValue(undefined);

    await invalidateProjectManagementViewSyncCaches({ invalidateQueries }, { projectId: 'project-a', scope }, {
      aggregateId: 'task-a', aggregateType: 'Task', eventType: 'Updated', projectId: 'project-a', version: 2,
    });

    expect(invalidateQueries).toHaveBeenCalledTimes(2);
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['astererp', 'project-management', 'tenant-a', 'MES', 'tasks', 'project-a'] });
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['astererp', 'project-management', 'tenant-a', 'MES', 'overview', 1, 20, 'project-a', ''] });
  });

  it('ignores events from another project before touching the query cache', async () => {
    const invalidateQueries = vi.fn().mockResolvedValue(undefined);

    await invalidateProjectManagementViewSyncCaches({ invalidateQueries }, { projectId: 'project-a', scope }, {
      aggregateId: 'task-b', aggregateType: 'Task', eventType: 'Updated', projectId: 'project-b', version: 2,
    });

    expect(invalidateQueries).not.toHaveBeenCalled();
  });
});

describe('applyProjectManagementTaskPatch', () => {
  it('merges field-only task patches into detail and loaded list pages', () => {
    const queryClient = {
      setQueryData: vi.fn(),
      setQueriesData: vi.fn(),
    } as unknown as Pick<QueryClient, 'setQueryData' | 'setQueriesData'>;

    const applied = applyProjectManagementTaskPatch(queryClient, { projectId: 'project-a', scope }, {
      aggregateId: 'task-a',
      aggregateType: 'Task',
      eventType: 'Updated',
      projectId: 'project-a',
      version: 3,
      changedFields: ['title', 'progressPercent'],
      patch: { title: '已更新标题', progressPercent: 60, versionNo: 3 },
    });

    expect(applied).toBe(true);
    expect(queryClient.setQueryData).toHaveBeenCalledTimes(1);
    expect(queryClient.setQueriesData).toHaveBeenCalledTimes(1);
  });

  it('returns false for structural patches so the caller can invalidate membership-sensitive views', () => {
    const queryClient = {
      setQueryData: vi.fn(),
      setQueriesData: vi.fn(),
    } as unknown as Pick<QueryClient, 'setQueryData' | 'setQueriesData'>;

    const applied = applyProjectManagementTaskPatch(queryClient, { projectId: 'project-a', scope }, {
      aggregateId: 'task-a',
      aggregateType: 'Task',
      eventType: 'Updated',
      projectId: 'project-a',
      version: 4,
      changedFields: ['status', 'parentTaskId'],
      patch: { status: 'Done', parentTaskId: 'task-parent', versionNo: 4 },
    });

    expect(applied).toBe(false);
    expect(queryClient.setQueryData).toHaveBeenCalledTimes(1);
    expect(queryClient.setQueriesData).not.toHaveBeenCalled();
  });
});
