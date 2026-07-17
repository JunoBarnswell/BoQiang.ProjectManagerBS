import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString } from '../queryString';

import type {
  ApplicationDevelopmentAppConfig,
  ApplicationDevelopmentAppConfigRequest,
  ApplicationDevelopmentEnvironmentCheckRequest,
  ApplicationDevelopmentEnvironmentCheckResponse,
  ApplicationDevelopmentArtifactRollbackRequest,
  ApplicationDevelopmentArtifactRollbackResponse,
  ApplicationDevelopmentModuleTreeNode,
  ApplicationDevelopmentModuleUpsertRequest,
  ApplicationDevelopmentOverview,
  ApplicationDevelopmentPageCreateRequest,
  ApplicationDevelopmentPageDetail,
  ApplicationDevelopmentPageListItem,
  ApplicationDevelopmentPageUpsertRequest,
  ApplicationDevelopmentPermissionOptions,
  ApplicationDevelopmentPreviewArtifactRequest,
  ApplicationDevelopmentPreviewSchemaResponse,
  ApplicationDevelopmentPublishResponse,
  ApplicationDevelopmentSharedResourceDetail,
  ApplicationDevelopmentSharedResourceListItem,
  ApplicationDevelopmentSharedResourceUpsertRequest,
  ApplicationDevelopmentWorkspace,
  ApplicationDevelopmentVersion,
  ApplicationDevelopmentVersionUpsertRequest
} from './applicationDevelopmentCenter.types';

const basePath = '/application-development-center';

export function getApplicationDevelopmentOverview(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationDevelopmentOverview>> {
  return httpClient.get<ApplicationDevelopmentOverview>(`${basePath}/overview`, undefined, signal);
}

export function getApplicationDevelopmentAppConfig(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationDevelopmentAppConfig>> {
  return httpClient.get<ApplicationDevelopmentAppConfig>(`${basePath}/app-config`, undefined, signal);
}

export function saveApplicationDevelopmentAppConfig(
  request: ApplicationDevelopmentAppConfigRequest
): Promise<ApiEnvelope<ApplicationDevelopmentAppConfig>> {
  return httpClient.put<ApplicationDevelopmentAppConfig, ApplicationDevelopmentAppConfigRequest>(`${basePath}/app-config`, request);
}

export function listApplicationDevelopmentVersions(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationDevelopmentVersion[]>> {
  return httpClient.get<ApplicationDevelopmentVersion[]>(`${basePath}/versions`, undefined, signal);
}

export function getApplicationDevelopmentWorkspace(
  versionId?: string | null,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentWorkspace>> {
  return httpClient.get<ApplicationDevelopmentWorkspace>(
    `${basePath}/workspace${buildQueryString({ versionId: versionId ?? '' })}`,
    undefined,
    signal
  );
}

export function createApplicationDevelopmentVersion(
  request: ApplicationDevelopmentVersionUpsertRequest
): Promise<ApiEnvelope<ApplicationDevelopmentVersion>> {
  return httpClient.post<ApplicationDevelopmentVersion, ApplicationDevelopmentVersionUpsertRequest>(`${basePath}/versions`, request);
}

export function updateApplicationDevelopmentVersion(
  id: string,
  request: ApplicationDevelopmentVersionUpsertRequest
): Promise<ApiEnvelope<ApplicationDevelopmentVersion>> {
  return httpClient.put<ApplicationDevelopmentVersion, ApplicationDevelopmentVersionUpsertRequest>(
    `${basePath}/versions/${encodeURIComponent(id)}`,
    request
  );
}

export function listApplicationDevelopmentModules(
  versionId: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentModuleTreeNode[]>> {
  return httpClient.get<ApplicationDevelopmentModuleTreeNode[]>(
    `${basePath}/modules${buildQueryString({ versionId })}`,
    undefined,
    signal
  );
}

export function createApplicationDevelopmentModule(
  request: ApplicationDevelopmentModuleUpsertRequest
): Promise<ApiEnvelope<ApplicationDevelopmentModuleTreeNode>> {
  return httpClient.post<ApplicationDevelopmentModuleTreeNode, ApplicationDevelopmentModuleUpsertRequest>(`${basePath}/modules`, request);
}

export function updateApplicationDevelopmentModule(
  id: string,
  request: ApplicationDevelopmentModuleUpsertRequest
): Promise<ApiEnvelope<ApplicationDevelopmentModuleTreeNode>> {
  return httpClient.put<ApplicationDevelopmentModuleTreeNode, ApplicationDevelopmentModuleUpsertRequest>(
    `${basePath}/modules/${encodeURIComponent(id)}`,
    request
  );
}

export function deleteApplicationDevelopmentModule(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`${basePath}/modules/${encodeURIComponent(id)}`);
}

export function listApplicationDevelopmentPages(
  versionId: string,
  moduleId?: string | null,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentPageListItem[]>> {
  return httpClient.get<ApplicationDevelopmentPageListItem[]>(
    `${basePath}/pages${buildQueryString({ moduleId: moduleId ?? '', versionId })}`,
    undefined,
    signal
  );
}

export function getApplicationDevelopmentPage(
  id: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentPageDetail>> {
  return httpClient.get<ApplicationDevelopmentPageDetail>(`${basePath}/pages/${encodeURIComponent(id)}`, undefined, signal);
}

export function createApplicationDevelopmentPage(
  request: ApplicationDevelopmentPageCreateRequest
): Promise<ApiEnvelope<ApplicationDevelopmentPageDetail>> {
  return httpClient.post<ApplicationDevelopmentPageDetail, ApplicationDevelopmentPageCreateRequest>(`${basePath}/pages`, request);
}

export function updateApplicationDevelopmentPage(
  id: string,
  request: ApplicationDevelopmentPageUpsertRequest
): Promise<ApiEnvelope<ApplicationDevelopmentPageDetail>> {
  return httpClient.put<ApplicationDevelopmentPageDetail, ApplicationDevelopmentPageUpsertRequest>(
    `${basePath}/pages/${encodeURIComponent(id)}`,
    request
  );
}

export function deleteApplicationDevelopmentPage(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`${basePath}/pages/${encodeURIComponent(id)}`);
}

export function getApplicationDevelopmentPreviewSchema(
  pageId: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentPreviewSchemaResponse>> {
  return httpClient.get<ApplicationDevelopmentPreviewSchemaResponse>(
    `${basePath}/pages/${encodeURIComponent(pageId)}/preview-schema`,
    undefined,
    signal
  );
}

export function compileApplicationDevelopmentPreviewArtifact(
  pageId: string,
  request: ApplicationDevelopmentPreviewArtifactRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentPreviewSchemaResponse>> {
  return httpClient.post<ApplicationDevelopmentPreviewSchemaResponse, ApplicationDevelopmentPreviewArtifactRequest>(
    `${basePath}/pages/${encodeURIComponent(pageId)}/preview-artifact`,
    request,
    undefined,
    signal
  );
}

export function checkApplicationDevelopmentPageEnvironment(
  pageId: string,
  request: ApplicationDevelopmentEnvironmentCheckRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentEnvironmentCheckResponse>> {
  return httpClient.post<ApplicationDevelopmentEnvironmentCheckResponse, ApplicationDevelopmentEnvironmentCheckRequest>(
    `${basePath}/pages/${encodeURIComponent(pageId)}/environment-check`, request, undefined, signal
  );
}

export function listApplicationDevelopmentSharedResources(
  versionId?: string | null,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentSharedResourceListItem[]>> {
  return httpClient.get<ApplicationDevelopmentSharedResourceListItem[]>(
    `${basePath}/shared-resources${buildQueryString({ versionId: versionId ?? '' })}`,
    undefined,
    signal
  );
}

export function getApplicationDevelopmentSharedResource(
  id: string,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentSharedResourceDetail>> {
  return httpClient.get<ApplicationDevelopmentSharedResourceDetail>(
    `${basePath}/shared-resources/${encodeURIComponent(id)}`,
    undefined,
    signal
  );
}

export function createApplicationDevelopmentSharedResource(
  request: ApplicationDevelopmentSharedResourceUpsertRequest
): Promise<ApiEnvelope<ApplicationDevelopmentSharedResourceDetail>> {
  return httpClient.post<ApplicationDevelopmentSharedResourceDetail, ApplicationDevelopmentSharedResourceUpsertRequest>(
    `${basePath}/shared-resources`,
    request
  );
}

export function updateApplicationDevelopmentSharedResource(
  id: string,
  request: ApplicationDevelopmentSharedResourceUpsertRequest
): Promise<ApiEnvelope<ApplicationDevelopmentSharedResourceDetail>> {
  return httpClient.put<ApplicationDevelopmentSharedResourceDetail, ApplicationDevelopmentSharedResourceUpsertRequest>(
    `${basePath}/shared-resources/${encodeURIComponent(id)}`,
    request
  );
}

export function getApplicationDevelopmentPermissionOptions(
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentPermissionOptions>> {
  return httpClient.get<ApplicationDevelopmentPermissionOptions>(`${basePath}/permission-options`, undefined, signal);
}

export function refreshApplicationDevelopmentPreviewMenu(pageId: string): Promise<ApiEnvelope<ApplicationDevelopmentPageDetail>> {
  return httpClient.post<ApplicationDevelopmentPageDetail, Record<string, never>>(
    `${basePath}/pages/${encodeURIComponent(pageId)}/refresh-preview-menu`,
    {}
  );
}

export function publishApplicationDevelopmentPage(
  pageId: string
): Promise<ApiEnvelope<ApplicationDevelopmentPublishResponse>> {
  return httpClient.post<ApplicationDevelopmentPublishResponse, Record<string, never>>(
    `${basePath}/pages/${encodeURIComponent(pageId)}/publish`,
    {}
  );
}

export function publishApplicationDevelopmentVersion(
  versionId: string
): Promise<ApiEnvelope<ApplicationDevelopmentPublishResponse>> {
  return httpClient.post<ApplicationDevelopmentPublishResponse, Record<string, never>>(
    `${basePath}/versions/${encodeURIComponent(versionId)}/publish`,
    {}
  );
}

export function rollbackApplicationDevelopmentArtifact(
  pageId: string,
  request: ApplicationDevelopmentArtifactRollbackRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDevelopmentArtifactRollbackResponse>> {
  return httpClient.post<ApplicationDevelopmentArtifactRollbackResponse, ApplicationDevelopmentArtifactRollbackRequest>(
    `${basePath}/pages/${encodeURIComponent(pageId)}/rollback`,
    request,
    undefined,
    signal
  );
}
