import { describe, expect, it, vi } from 'vitest';

const get = vi.hoisted(() => vi.fn());

vi.mock('../../core/http/httpClient', () => ({
  httpClient: { get }
}));

import { getProjectManagementActivities } from './projectManagement.api';

describe('project management activity API contract', () => {
  it('requests the paged activity contract with backend query parameter names', () => {
    const signal = new AbortController().signal;
    const response = Promise.resolve({ code: 200, data: { total: 1, items: [] }, message: 'ok', traceId: 'trace-1' });
    get.mockReturnValue(response);

    const result = getProjectManagementActivities('project-a', {
      activityType: 'task.updated',
      pageIndex: 2,
      pageSize: 20
    }, signal);

    expect(result).toBe(response);
    expect(get).toHaveBeenCalledWith(
      '/project-management/projects/project-a/activities?activityType=task.updated&pageIndex=2&pageSize=20',
      undefined,
      signal
    );
  });
});
