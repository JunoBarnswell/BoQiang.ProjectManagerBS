import { describe, expect, it, vi } from 'vitest';

const get = vi.hoisted(() => vi.fn());

vi.mock('../../../core/http/httpClient', () => ({
  httpClient: { get }
}));

import {
  getProjectManagementDashboardOverview,
  getProjectManagementDashboardWorkload
} from './projectManagementDashboard.api';

describe('project management dashboard API contract', () => {
  it('queries the project overview with the paged backend contract', () => {
    const signal = new AbortController().signal;
    const response = Promise.resolve({ code: 200, data: { total: 1, items: [] }, message: 'ok', traceId: 'trace-1' });
    get.mockReturnValue(response);

    expect(getProjectManagementDashboardOverview({ projectId: 'project-a', pageIndex: 1, pageSize: 1 }, signal)).toBe(response);
    expect(get).toHaveBeenCalledWith('/project-management/overview?projectId=project-a&pageIndex=1&pageSize=1', undefined, signal);
  });

  it('queries workload through the project-scoped permission-protected endpoint', () => {
    const signal = new AbortController().signal;
    const response = Promise.resolve({ code: 200, data: [], message: 'ok', traceId: 'trace-2' });
    get.mockReturnValue(response);

    expect(getProjectManagementDashboardWorkload('project-a', signal)).toBe(response);
    expect(get).toHaveBeenCalledWith('/project-management/workloads?projectId=project-a', undefined, signal);
  });
});
