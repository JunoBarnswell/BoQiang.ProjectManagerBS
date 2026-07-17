import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type QueryStringValue } from '../queryString';

export type ApplicationDataHttpMethod = 'DELETE' | 'GET' | 'PATCH' | 'POST' | 'PUT';

export interface ApplicationDataInvokeRequest {
  body?: Record<string, unknown> | null;
  method?: ApplicationDataHttpMethod | string | null;
  query?: Record<string, unknown> | null;
  routePath: string;
  signal?: AbortSignal;
}

export function invokeApplicationDataApi<TResponse = unknown>({
  body,
  method,
  query,
  routePath,
  signal
}: ApplicationDataInvokeRequest): Promise<ApiEnvelope<TResponse>> {
  const normalizedMethod = normalizeMethod(method);
  const path = `${normalizeRoute(routePath)}${buildQueryString(normalizeQuery(query))}`;

  if (normalizedMethod === 'GET') {
    return httpClient.get<TResponse>(path, undefined, signal);
  }

  if (normalizedMethod === 'DELETE') {
    return httpClient.request<TResponse, Record<string, unknown> | undefined>({
      body: body ?? undefined,
      method: 'DELETE',
      path,
      signal
    });
  }

  return httpClient.request<TResponse, Record<string, unknown> | undefined>({
    body: body ?? {},
    method: normalizedMethod,
    path,
    signal
  });
}

function normalizeRoute(routePath: string): string {
  const trimmed = routePath.trim();
  const withoutApiPrefix = trimmed.replace(/^\/?api\/application-data\/?/i, '');
  const withoutLeadingSlash = withoutApiPrefix.replace(/^\/+/, '');
  if (!withoutLeadingSlash) {
    throw new Error('应用数据接口路径不能为空');
  }

  return `/application-data/${withoutLeadingSlash}`;
}

function normalizeMethod(method: ApplicationDataInvokeRequest['method']): ApplicationDataHttpMethod {
  const normalized = String(method || 'GET').trim().toUpperCase();
  return ['DELETE', 'GET', 'PATCH', 'POST', 'PUT'].includes(normalized)
    ? normalized as ApplicationDataHttpMethod
    : 'GET';
}

function normalizeQuery(query: Record<string, unknown> | null | undefined): Record<string, QueryStringValue> {
  const result: Record<string, QueryStringValue> = {};
  Object.entries(query ?? {}).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    if (typeof value === 'boolean' || typeof value === 'number' || typeof value === 'string' || value instanceof Date) {
      result[key] = value;
      return;
    }

    result[key] = JSON.stringify(value);
  });
  return result;
}
