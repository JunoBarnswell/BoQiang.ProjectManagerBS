import { describe, expect, it, vi } from 'vitest';

const get = vi.hoisted(() => vi.fn());

vi.mock('../../../core/http/httpClient', () => ({
  httpClient: { get }
}));

import { getProjectManagementMyWork } from '../../../api/project-management/projectManagement.api';

describe('my work API contract', () => {
  it('preserves category, paging and sort semantics in the shared query transport', () => {
    const signal = new AbortController().signal;
    const response = Promise.resolve({ code: 200, data: { total: 0, items: [] }, message: 'ok', traceId: 'trace-1' });
    get.mockReturnValue(response);

    expect(getProjectManagementMyWork({ category: 'overdue', pageIndex: 2, pageSize: 50, sortBy: 'updated', sortDirection: 'asc' }, signal)).toBe(response);
    expect(get).toHaveBeenCalledWith('/project-management/my-work?category=overdue&pageIndex=2&pageSize=50&sortBy=updated&sortDirection=asc', undefined, signal);
  });
});
