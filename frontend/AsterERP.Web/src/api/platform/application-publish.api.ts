import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString } from '../queryString';
import type { GridPageResult } from '../shared.types';

export interface ApplicationPublishRequest {
  backendHost?: string | null;
  backendPort?: number | null;
  cleanOutput: boolean;
  frontendApiBaseUrl?: string | null;
  frontendBasePath?: string | null;
  includeBackend: boolean;
  includeFrontend: boolean;
  remark?: string | null;
  tenantId?: string | null;
  version?: string | null;
}

export interface ApplicationPublishTaskDto {
  id: string;
  appId: string;
  appCode: string;
  appName: string;
  tenantId?: string | null;
  version?: string | null;
  status: string;
  stage: string;
  progressPercent: number;
  startedAt?: string | null;
  finishedAt?: string | null;
  durationMs: number;
  sourceProjectPath?: string | null;
  releasePath?: string | null;
  artifactPath?: string | null;
  backendHost: string;
  backendPort: number;
  frontendBasePath: string;
  frontendApiBaseUrl: string;
  errorMessage?: string | null;
  traceId: string;
  createdTime: string;
  remark?: string | null;
}

export interface ApplicationPublishLogDto {
  id: string;
  taskId: string;
  level: string;
  stage: string;
  message: string;
  traceId: string;
  createdTime: string;
}

export interface ApplicationPublishArtifactDto {
  id: string;
  taskId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  storedPath: string;
  createdTime: string;
  expiresAt?: string | null;
  downloadUrl: string;
}

export function publishApplication(appId: string, request: ApplicationPublishRequest): Promise<ApiEnvelope<ApplicationPublishTaskDto>> {
  return httpClient.post<ApplicationPublishTaskDto, ApplicationPublishRequest>(`/platform/applications/${encodeURIComponent(appId)}/publish`, request, 120_000);
}

export function getApplicationPublishTasks(appId: string, pageIndex: number, pageSize: number, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<ApplicationPublishTaskDto>>> {
  return httpClient.get<GridPageResult<ApplicationPublishTaskDto>>(`/platform/applications/${encodeURIComponent(appId)}/publish-tasks${buildQueryString({ pageIndex, pageSize })}`, undefined, signal);
}

export function getApplicationPublishTask(taskId: string, signal?: AbortSignal): Promise<ApiEnvelope<ApplicationPublishTaskDto>> {
  return httpClient.get<ApplicationPublishTaskDto>(`/platform/application-publish-tasks/${encodeURIComponent(taskId)}`, undefined, signal);
}

export function getApplicationPublishLogs(taskId: string, pageIndex: number, pageSize: number, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<ApplicationPublishLogDto>>> {
  return httpClient.get<GridPageResult<ApplicationPublishLogDto>>(`/platform/application-publish-tasks/${encodeURIComponent(taskId)}/logs${buildQueryString({ pageIndex, pageSize })}`, undefined, signal);
}

export function packageApplicationPublishTask(taskId: string): Promise<ApiEnvelope<ApplicationPublishArtifactDto>> {
  return httpClient.post<ApplicationPublishArtifactDto, Record<string, never>>(`/platform/application-publish-tasks/${encodeURIComponent(taskId)}/package`, {}, 120_000);
}

export function getApplicationPublishArtifacts(appId: string, pageIndex: number, pageSize: number, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<ApplicationPublishArtifactDto>>> {
  return httpClient.get<GridPageResult<ApplicationPublishArtifactDto>>(`/platform/applications/${encodeURIComponent(appId)}/publish-artifacts${buildQueryString({ pageIndex, pageSize })}`, undefined, signal);
}

export function downloadApplicationPublishArtifact(artifact: ApplicationPublishArtifactDto): Promise<{ blob: Blob; fileName: string }> {
  return httpClient.downloadBlob(normalizeDownloadPath(artifact.downloadUrl), 120_000);
}

export function deleteApplicationPublishArtifact(artifactId: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/platform/application-publish-artifacts/${encodeURIComponent(artifactId)}`);
}

function normalizeDownloadPath(downloadUrl: string): string {
  if (downloadUrl.startsWith('/api/')) {
    return downloadUrl.slice(4);
  }

  return downloadUrl;
}
