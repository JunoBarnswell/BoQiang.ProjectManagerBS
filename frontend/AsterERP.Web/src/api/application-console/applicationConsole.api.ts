import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';

import type {
  ApplicationConsoleSummaryDto,
  ApplicationDatabaseBindingRequest,
  ApplicationDatabaseBindingResponseDto
} from './applicationConsole.types';

export function getApplicationConsoleSummary(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationConsoleSummaryDto>> {
  return httpClient.get<ApplicationConsoleSummaryDto>('/application-console/summary', undefined, signal);
}

export function getApplicationDatabaseBindingStatus(signal?: AbortSignal): Promise<ApiEnvelope<ApplicationDatabaseBindingResponseDto>> {
  return httpClient.get<ApplicationDatabaseBindingResponseDto>('/application-console/database-binding/status', undefined, signal);
}

export function testApplicationDatabaseBinding(
  request: ApplicationDatabaseBindingRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDatabaseBindingResponseDto>> {
  return httpClient.post<ApplicationDatabaseBindingResponseDto, ApplicationDatabaseBindingRequest>(
    '/application-console/database-binding/test',
    request,
    undefined,
    signal
  );
}

export function saveApplicationDatabaseBinding(
  request: ApplicationDatabaseBindingRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDatabaseBindingResponseDto>> {
  return httpClient.put<ApplicationDatabaseBindingResponseDto, ApplicationDatabaseBindingRequest>(
    '/application-console/database-binding',
    request,
    120_000,
    signal
  );
}

export function testInitialApplicationDatabaseBinding(
  tenantId: string,
  appCode: string,
  request: ApplicationDatabaseBindingRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDatabaseBindingResponseDto>> {
  return httpClient.post<ApplicationDatabaseBindingResponseDto, ApplicationDatabaseBindingRequest>(
    `/application-auth/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/database-binding/test`,
    request,
    undefined,
    signal
  );
}

export function saveInitialApplicationDatabaseBinding(
  tenantId: string,
  appCode: string,
  request: ApplicationDatabaseBindingRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<ApplicationDatabaseBindingResponseDto>> {
  return httpClient.put<ApplicationDatabaseBindingResponseDto, ApplicationDatabaseBindingRequest>(
    `/application-auth/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/database-binding`,
    request,
    120_000,
    signal
  );
}
