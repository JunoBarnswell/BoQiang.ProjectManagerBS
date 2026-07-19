import type { ProjectManagementTaskAttachment } from '../../../api/project-management/projectManagement.types';
import { appEnv } from '../../../core/config/env';
import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { getAccessToken } from '../../../core/http/tokenStorage';
import { getStoredWorkspace } from '../../../core/http/workspaceStorage';

export interface TaskAttachmentUploadOptions {
  onProgress?: (loaded: number, total: number) => void;
  signal?: AbortSignal;
}

export function uploadProjectManagementTaskAttachmentWithProgress(
  taskId: string,
  file: File,
  options: TaskAttachmentUploadOptions = {},
): Promise<ApiEnvelope<ProjectManagementTaskAttachment>> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    const baseUrl = appEnv.apiBaseUrl.replace(/\/+$/, '');
    const path = `/project-management/tasks/${encodeURIComponent(taskId)}/attachments`;
    const workspace = getStoredWorkspace();
    const accessToken = getAccessToken();
    const formData = new FormData();
    formData.append('file', file);

    xhr.open('POST', `${baseUrl}${path}`);
    xhr.withCredentials = true;
    xhr.timeout = 120_000;
    if (accessToken) xhr.setRequestHeader('Authorization', `Bearer ${accessToken}`);
    if (workspace) {
      xhr.setRequestHeader('X-Tenant-Id', workspace.tenantId);
      xhr.setRequestHeader('X-App-Code', workspace.appCode);
      xhr.setRequestHeader('X-Workspace-Level', workspace.workspaceLevel);
    }
    const csrfToken = readCookie('astererp_csrf');
    if (csrfToken) xhr.setRequestHeader('X-CSRF-Token', csrfToken);

    const abort = () => xhr.abort();
    if (options.signal?.aborted) {
      reject(new DOMException('Upload aborted', 'AbortError'));
      return;
    }
    options.signal?.addEventListener('abort', abort, { once: true });
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) options.onProgress?.(event.loaded, event.total);
    };
    xhr.onload = () => {
      options.signal?.removeEventListener('abort', abort);
      const payload = parsePayload(xhr.responseText);
      if (xhr.status >= 200 && xhr.status < 300 && payload && payload.code < 400) {
        resolve(payload as ApiEnvelope<ProjectManagementTaskAttachment>);
        return;
      }
      reject(new Error(payload?.message ?? `附件上传失败（HTTP ${xhr.status}）`));
    };
    xhr.onerror = () => {
      options.signal?.removeEventListener('abort', abort);
      reject(new Error('附件上传网络失败'));
    };
    xhr.ontimeout = () => {
      options.signal?.removeEventListener('abort', abort);
      reject(new Error('附件上传超时'));
    };
    xhr.onabort = () => {
      options.signal?.removeEventListener('abort', abort);
      reject(new DOMException('Upload aborted', 'AbortError'));
    };
    xhr.send(formData);
  });
}

function readCookie(name: string): string | null {
  const prefix = `${encodeURIComponent(name)}=`;
  const item = document.cookie.split('; ').find((cookie) => cookie.startsWith(prefix));
  return item ? decodeURIComponent(item.slice(prefix.length)) : null;
}

function parsePayload(value: string): ApiEnvelope<unknown> | null {
  if (!value) return null;
  try {
    const payload = JSON.parse(value) as unknown;
    return payload && typeof payload === 'object' && 'code' in payload && 'message' in payload && 'data' in payload
      ? payload as ApiEnvelope<unknown>
      : null;
  } catch {
    return null;
  }
}
