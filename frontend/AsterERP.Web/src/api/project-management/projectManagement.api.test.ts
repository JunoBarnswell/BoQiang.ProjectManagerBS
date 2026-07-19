import { describe, expect, it, vi } from 'vitest';

const get = vi.hoisted(() => vi.fn());
const post = vi.hoisted(() => vi.fn());
const downloadBlob = vi.hoisted(() => vi.fn());

vi.mock('../../core/http/httpClient', () => ({
  httpClient: { get, post, downloadBlob }
}));

import {
  getProjectManagementActivities,
  getProjectManagementReversibleCommandStack,
  downloadProjectManagementTaskAttachment,
  previewProjectManagementTaskAttachment,
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

  it('uses task-scoped preview and download URLs and preserves cancellation', () => {
    const signal = new AbortController().signal;
    const attachment = {
      id: 'attachment-1',
      projectId: 'project-a',
      taskId: 'task-a',
      fileId: 'file-1',
      fileName: '方案 1.pdf',
      contentType: 'application/pdf',
      fileSize: 12,
      downloadUrl: '/api/project-management/tasks/task-a/attachments/attachment-1/download',
      previewUrl: '/api/project-management/tasks/task-a/attachments/attachment-1/preview',
      uploadedByUserId: 'operator',
      createdTime: '2026-07-19T00:00:00Z',
      versionNo: 1,
      previewSupported: true,
      previewType: 'pdf',
      previewPipeline: 'PDF'
    };
    const response = Promise.resolve({ blob: new Blob(['file']), fileName: '方案 1.pdf' });
    downloadBlob.mockReturnValue(response);

    expect(downloadProjectManagementTaskAttachment(attachment, signal)).toBe(response);
    expect(previewProjectManagementTaskAttachment(attachment, signal)).toBe(response);
    expect(downloadBlob).toHaveBeenNthCalledWith(1, '/project-management/tasks/task-a/attachments/attachment-1/download', { signal, timeoutMs: 120_000 });
    expect(downloadBlob).toHaveBeenNthCalledWith(2, '/project-management/tasks/task-a/attachments/attachment-1/preview', { signal, timeoutMs: 120_000 });
  });

  it('fails closed for unsupported formats and missing links', async () => {
    const base = {
      id: 'attachment-1', projectId: 'project-a', taskId: 'task-a', fileId: 'file-1', fileName: 'data.bin',
      contentType: 'application/octet-stream', fileSize: 1, uploadedByUserId: 'operator', createdTime: '2026-07-19T00:00:00Z', versionNo: 1,
      downloadUrl: '', previewUrl: '', previewSupported: false
    };
    await expect(previewProjectManagementTaskAttachment(base)).rejects.toThrow('不支持在线预览');
    await expect(downloadProjectManagementTaskAttachment(base)).rejects.toThrow('下载链接已失效');
  });
});
