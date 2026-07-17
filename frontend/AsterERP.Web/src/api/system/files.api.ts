import { httpClient } from '../../core/http/httpClient';
import { buildQueryString } from '../queryString';
import type { GridPageResult } from '../shared.types';

import type {
  SystemFilePreviewFormatDto,
  SystemFileQuery,
  SystemFileRecordDto,
  SystemFileUploadResponse
} from './files.types';

const filesRoot = '/system/files';

export const systemFilesApi = {
  list: (query: SystemFileQuery, signal?: AbortSignal) =>
    httpClient.get<GridPageResult<SystemFileRecordDto>>(`${filesRoot}${buildQueryString(query)}`, undefined, signal),

  upload: (file: File, remark?: string, signal?: AbortSignal) => {
    const formData = new FormData();
    formData.append('file', file);
    if (remark?.trim()) {
      formData.append('remark', remark.trim());
    }

    return httpClient.postForm<SystemFileUploadResponse>(`${filesRoot}/upload`, formData, { signal, timeoutMs: 120_000 });
  },

  delete: (id: string) => httpClient.delete<boolean>(`${filesRoot}/${encodeURIComponent(id)}`),

  formats: (signal?: AbortSignal) =>
    httpClient.get<SystemFilePreviewFormatDto[]>(`${filesRoot}/formats`, undefined, signal),

  downloadBlob: (file: SystemFileRecordDto, signal?: AbortSignal) =>
    httpClient.downloadBlob(normalizeApiPath(file.downloadUrl || buildDownloadUrl(file.id)), { signal, timeoutMs: 120_000 }),

  previewBlob: (file: SystemFileRecordDto, signal?: AbortSignal) =>
    httpClient.downloadBlob(normalizeApiPath(file.previewUrl || buildPreviewUrl(file.id)), { signal, timeoutMs: 120_000 })
};

export function buildPreviewUrl(id: string): string {
  return `${filesRoot}/${encodeURIComponent(id)}/preview`;
}

export function buildDownloadUrl(id: string): string {
  return `${filesRoot}/${encodeURIComponent(id)}/download`;
}

function normalizeApiPath(path: string): string {
  const trimmed = path.trim();
  if (!trimmed) {
    return filesRoot;
  }

  if (trimmed.startsWith('/api/')) {
    return trimmed.slice(4);
  }

  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
}
