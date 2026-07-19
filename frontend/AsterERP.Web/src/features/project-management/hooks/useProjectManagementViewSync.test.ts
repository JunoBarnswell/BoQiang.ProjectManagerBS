import { describe, expect, it, vi } from 'vitest';

import { invalidateProjectManagementViewSyncCaches } from './useProjectManagementViewSync';

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
