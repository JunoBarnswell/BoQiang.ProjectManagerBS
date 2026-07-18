import { describe, expect, it, vi } from 'vitest';

const get = vi.hoisted(() => vi.fn());
const post = vi.hoisted(() => vi.fn());

vi.mock('../../core/http/httpClient', () => ({
  httpClient: { get, post }
}));

import {
  getProjectManagementActivities,
  getProjectManagementReversibleCommandStack,
  redoProjectManagementReversibleCommand,
  undoProjectManagementReversibleCommand,
} from './projectManagement.api';

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

  it('uses the reversible command stack and replay endpoints with caller request ids', () => {
    const signal = new AbortController().signal;
    const stackResponse = Promise.resolve({ code: 200, data: { canRedo: false, canUndo: true, commands: [] }, message: 'ok', traceId: 'trace-2' });
    const undoResponse = Promise.resolve({ code: 200, data: { id: 'command-1' }, message: 'ok', traceId: 'trace-3' });
    const redoResponse = Promise.resolve({ code: 200, data: { id: 'command-1' }, message: 'ok', traceId: 'trace-4' });
    get.mockReturnValue(stackResponse);
    post.mockReturnValueOnce(undoResponse).mockReturnValueOnce(redoResponse);

    expect(getProjectManagementReversibleCommandStack(signal)).toBe(stackResponse);
    expect(undoProjectManagementReversibleCommand({ requestId: 'undo-1' })).toBe(undoResponse);
    expect(redoProjectManagementReversibleCommand({ requestId: 'redo-1' })).toBe(redoResponse);
    expect(get).toHaveBeenLastCalledWith('/project-management/reversible-commands', undefined, signal);
    expect(post).toHaveBeenNthCalledWith(1, '/project-management/reversible-commands/undo', { requestId: 'undo-1' });
    expect(post).toHaveBeenNthCalledWith(2, '/project-management/reversible-commands/redo', { requestId: 'redo-1' });
  });
});
