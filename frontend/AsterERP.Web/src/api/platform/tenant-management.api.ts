import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';

import type { TenantAppCatalogItemDto, TenantAppInstallRequest, TenantAppListItemDto } from './platform.types';

export function getTenantAppCatalog(): Promise<ApiEnvelope<TenantAppCatalogItemDto[]>> {
  return httpClient.get<TenantAppCatalogItemDto[]>('/tenant/apps/catalog');
}

export function getInstalledTenantApps(): Promise<ApiEnvelope<TenantAppListItemDto[]>> {
  return httpClient.get<TenantAppListItemDto[]>('/tenant/apps');
}

export function installTenantApp(appCode: string, request: TenantAppInstallRequest = {}): Promise<ApiEnvelope<TenantAppListItemDto>> {
  return httpClient.post<TenantAppListItemDto, TenantAppInstallRequest>(`/tenant/apps/${encodeURIComponent(appCode)}/install`, request);
}

export function enableTenantApp(appCode: string): Promise<ApiEnvelope<TenantAppListItemDto>> {
  return httpClient.post<TenantAppListItemDto, undefined>(`/tenant/apps/${encodeURIComponent(appCode)}/enable`, undefined);
}

export function disableTenantApp(appCode: string): Promise<ApiEnvelope<TenantAppListItemDto>> {
  return httpClient.post<TenantAppListItemDto, undefined>(`/tenant/apps/${encodeURIComponent(appCode)}/disable`, undefined);
}
