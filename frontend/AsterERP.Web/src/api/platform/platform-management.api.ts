import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type FilterQueryRule, type SortQueryRule } from '../queryString';
import type { BatchStatusUpdateRequest, GridPageResult } from '../shared.types';

import type { ApplicationBackendEntryRequest, ApplicationBackendEntryResponseDto, ApplicationListItemDto, ApplicationUpsertRequest, TenantAppListItemDto, TenantAppUpsertRequest, TenantListItemDto, TenantUpsertRequest, UserAppRoleDto, UserAppRoleUpsertRequest, UserTenantMembershipDto, UserTenantMembershipUpsertRequest } from './platform.types';

export function getTenants(query: { filters?: FilterQueryRule[]; keyword?: string; pageIndex?: number; pageSize?: number; sorts?: SortQueryRule[]; status?: string }, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<TenantListItemDto>>> {
  return httpClient.get<GridPageResult<TenantListItemDto>>(`/platform/tenants${buildQueryString(query)}`, undefined, signal);
}

export function createTenant(request: TenantUpsertRequest): Promise<ApiEnvelope<TenantListItemDto>> {
  return httpClient.post<TenantListItemDto, TenantUpsertRequest>('/platform/tenants', request);
}

export function updateTenant(id: string, request: TenantUpsertRequest): Promise<ApiEnvelope<TenantListItemDto>> {
  return httpClient.put<TenantListItemDto, TenantUpsertRequest>(`/platform/tenants/${id}`, request);
}

export function deleteTenant(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/platform/tenants/${id}`);
}

export function updateTenantStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/platform/tenants/batch-status', { ids, status });
}

export function getApplications(query: { filters?: FilterQueryRule[]; keyword?: string; pageIndex?: number; pageSize?: number; sorts?: SortQueryRule[]; status?: string }, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<ApplicationListItemDto>>> {
  return httpClient.get<GridPageResult<ApplicationListItemDto>>(`/platform/applications${buildQueryString(query)}`, undefined, signal);
}

export function createApplication(request: ApplicationUpsertRequest): Promise<ApiEnvelope<ApplicationListItemDto>> {
  return httpClient.post<ApplicationListItemDto, ApplicationUpsertRequest>('/platform/applications', request);
}

export function updateApplication(id: string, request: ApplicationUpsertRequest): Promise<ApiEnvelope<ApplicationListItemDto>> {
  return httpClient.put<ApplicationListItemDto, ApplicationUpsertRequest>(`/platform/applications/${id}`, request);
}

export function deleteApplication(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/platform/applications/${id}`);
}

export function updateApplicationStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/platform/applications/batch-status', { ids, status });
}

export function enterApplicationBackend(appCode: string, request: ApplicationBackendEntryRequest): Promise<ApiEnvelope<ApplicationBackendEntryResponseDto>> {
  return httpClient.post<ApplicationBackendEntryResponseDto, ApplicationBackendEntryRequest>(
    `/platform/applications/${encodeURIComponent(appCode.trim().toUpperCase())}/enter`,
    request
  );
}

export function getTenantApps(query: { appCode?: string; filters?: FilterQueryRule[]; keyword?: string; pageIndex?: number; pageSize?: number; sorts?: SortQueryRule[]; status?: string; tenantId?: string }, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<TenantAppListItemDto>>> {
  return httpClient.get<GridPageResult<TenantAppListItemDto>>(`/platform/tenant-apps${buildQueryString(query)}`, undefined, signal);
}

export function createTenantApp(request: TenantAppUpsertRequest): Promise<ApiEnvelope<TenantAppListItemDto>> {
  return httpClient.post<TenantAppListItemDto, TenantAppUpsertRequest>('/platform/tenant-apps', request);
}

export function updateTenantApp(id: string, request: TenantAppUpsertRequest): Promise<ApiEnvelope<TenantAppListItemDto>> {
  return httpClient.put<TenantAppListItemDto, TenantAppUpsertRequest>(`/platform/tenant-apps/${id}`, request);
}

export function deleteTenantApp(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/platform/tenant-apps/${id}`);
}

export function updateTenantAppStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/platform/tenant-apps/batch-status', { ids, status });
}

export function getUserTenants(query: { filters?: FilterQueryRule[]; keyword?: string; pageIndex?: number; pageSize?: number; sorts?: SortQueryRule[]; status?: string; tenantId?: string; userId?: string }, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<UserTenantMembershipDto>>> {
  return httpClient.get<GridPageResult<UserTenantMembershipDto>>(`/platform/user-tenants${buildQueryString(query)}`, undefined, signal);
}

export function createUserTenant(request: UserTenantMembershipUpsertRequest): Promise<ApiEnvelope<UserTenantMembershipDto>> {
  return httpClient.post<UserTenantMembershipDto, UserTenantMembershipUpsertRequest>('/platform/user-tenants', request);
}

export function updateUserTenant(id: string, request: UserTenantMembershipUpsertRequest): Promise<ApiEnvelope<UserTenantMembershipDto>> {
  return httpClient.put<UserTenantMembershipDto, UserTenantMembershipUpsertRequest>(`/platform/user-tenants/${id}`, request);
}

export function deleteUserTenant(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/platform/user-tenants/${id}`);
}

export function updateUserTenantStatus(ids: string[], status: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.post<boolean, BatchStatusUpdateRequest>('/platform/user-tenants/batch-status', { ids, status });
}

export function getUserAppRoles(query: { appCode?: string; filters?: FilterQueryRule[]; keyword?: string; pageIndex?: number; pageSize?: number; sorts?: SortQueryRule[]; tenantId?: string; userId?: string }, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<UserAppRoleDto>>> {
  return httpClient.get<GridPageResult<UserAppRoleDto>>(`/platform/user-app-roles${buildQueryString(query)}`, undefined, signal);
}

export function createUserAppRole(request: UserAppRoleUpsertRequest): Promise<ApiEnvelope<UserAppRoleDto>> {
  return httpClient.post<UserAppRoleDto, UserAppRoleUpsertRequest>('/platform/user-app-roles', request);
}

export function updateUserAppRole(id: string, request: UserAppRoleUpsertRequest): Promise<ApiEnvelope<UserAppRoleDto>> {
  return httpClient.put<UserAppRoleDto, UserAppRoleUpsertRequest>(`/platform/user-app-roles/${id}`, request);
}

export function deleteUserAppRole(id: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`/platform/user-app-roles/${id}`);
}
