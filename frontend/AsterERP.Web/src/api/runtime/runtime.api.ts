import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString } from '../queryString';

import type { RuntimeModelQueryRequest, RuntimeModelQueryResponse, RuntimePageSchemaDto } from './runtime.types';

export function getRuntimePageSchema(pageCode: string, previewPageId?: string | null, signal?: AbortSignal): Promise<ApiEnvelope<RuntimePageSchemaDto>> {
  return httpClient.get<RuntimePageSchemaDto>(
    `/runtime/pages/${encodeURIComponent(pageCode)}${buildQueryString({ previewPageId: previewPageId ?? '' })}`,
    undefined,
    signal
  );
}

export function queryRuntimeModel(
  modelCode: string,
  request: RuntimeModelQueryRequest,
  signal?: AbortSignal
): Promise<ApiEnvelope<RuntimeModelQueryResponse>> {
  return httpClient.post<RuntimeModelQueryResponse, RuntimeModelQueryRequest>(
    `/runtime/models/${encodeURIComponent(modelCode)}/query`,
    request,
    undefined,
    signal
  );
}
