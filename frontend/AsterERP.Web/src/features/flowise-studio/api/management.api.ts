import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseAuditLogDto,
  FlowiseLoginActivityDto,
  FlowiseRoleDto,
  FlowiseSsoConfigDto,
  FlowiseUserDto
} from '../types/management.types';
import type {
  FlowiseAccountSettingsDto,
  FlowiseResourceDto,
  FlowiseResourceUpsertRequest,
  FlowiseStudioQuery,
  FlowiseWorkspaceDto,
  FlowiseWorkspaceUpsertRequest,
  GridPageResult
} from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise';

export const managementApi = {
  account: {
    get: (signal?: AbortSignal): Promise<ApiEnvelope<FlowiseAccountSettingsDto>> =>
      httpClient.get<FlowiseAccountSettingsDto>(`${basePath}/account/settings`, undefined, signal),
    update: (request: FlowiseAccountSettingsDto): Promise<ApiEnvelope<FlowiseAccountSettingsDto>> =>
      httpClient.put<FlowiseAccountSettingsDto, FlowiseAccountSettingsDto>(`${basePath}/account/settings`, request)
  },
  logs: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseAuditLogDto>>> =>
    httpClient.get<GridPageResult<FlowiseAuditLogDto>>(`${basePath}/logs/detail${buildFlowiseQuery(query)}`, undefined, signal),
  logResources: {
    create: (request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/logs`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> => httpClient.delete<boolean>(`${basePath}/logs/${id}`),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseResourceDto>>> =>
      httpClient.get<GridPageResult<FlowiseResourceDto>>(`${basePath}/logs${buildFlowiseQuery(query)}`, undefined, signal),
    update: (id: string, request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.put<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/logs/${id}`, request)
  },
  loginActivity: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseLoginActivityDto>>> =>
    httpClient.get<GridPageResult<FlowiseLoginActivityDto>>(`${basePath}/login-activity/detail${buildFlowiseQuery(query)}`, undefined, signal),
  loginActivityResources: {
    create: (request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/login-activity`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> => httpClient.delete<boolean>(`${basePath}/login-activity/${id}`),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseResourceDto>>> =>
      httpClient.get<GridPageResult<FlowiseResourceDto>>(`${basePath}/login-activity${buildFlowiseQuery(query)}`, undefined, signal),
    update: (id: string, request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.put<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/login-activity/${id}`, request)
  },
  roles: {
    create: (request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/roles`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> => httpClient.delete<boolean>(`${basePath}/roles/${id}`),
    detail: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseRoleDto>> =>
      httpClient.get<FlowiseRoleDto>(`${basePath}/roles/${id}/detail`, undefined, signal),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseResourceDto>>> =>
      httpClient.get<GridPageResult<FlowiseResourceDto>>(`${basePath}/roles${buildFlowiseQuery(query)}`, undefined, signal),
    update: (id: string, request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.put<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/roles/${id}`, request)
  },
  sso: {
    create: (request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/sso-config`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> => httpClient.delete<boolean>(`${basePath}/sso-config/${id}`),
    get: (signal?: AbortSignal): Promise<ApiEnvelope<FlowiseSsoConfigDto | null>> =>
      httpClient.get<FlowiseSsoConfigDto | null>(`${basePath}/sso-config/detail`, undefined, signal),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseResourceDto>>> =>
      httpClient.get<GridPageResult<FlowiseResourceDto>>(`${basePath}/sso-config${buildFlowiseQuery(query)}`, undefined, signal),
    update: (id: string, request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.put<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/sso-config/${id}`, request)
  },
  users: {
    create: (request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/users`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> => httpClient.delete<boolean>(`${basePath}/users/${id}`),
    detail: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseUserDto>> =>
      httpClient.get<FlowiseUserDto>(`${basePath}/users/${id}/detail`, undefined, signal),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseResourceDto>>> =>
      httpClient.get<GridPageResult<FlowiseResourceDto>>(`${basePath}/users${buildFlowiseQuery(query)}`, undefined, signal),
    update: (id: string, request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
      httpClient.put<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/users/${id}`, request)
  },
  workspaces: {
    create: (request: FlowiseWorkspaceUpsertRequest): Promise<ApiEnvelope<FlowiseWorkspaceDto>> =>
      httpClient.post<FlowiseWorkspaceDto, FlowiseWorkspaceUpsertRequest>(`${basePath}/workspaces`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> => httpClient.delete<boolean>(`${basePath}/workspaces/${id}`),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseWorkspaceDto>>> =>
      httpClient.get<GridPageResult<FlowiseWorkspaceDto>>(`${basePath}/workspaces${buildFlowiseQuery(query)}`, undefined, signal),
    update: (id: string, request: FlowiseWorkspaceUpsertRequest): Promise<ApiEnvelope<FlowiseWorkspaceDto>> =>
      httpClient.put<FlowiseWorkspaceDto, FlowiseWorkspaceUpsertRequest>(`${basePath}/workspaces/${id}`, request)
  }
};
