import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type { FlowiseResourceDto, FlowiseResourceUpsertRequest, FlowiseStudioQuery, GridPageResult } from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise';

export interface FlowiseConfigurationResourceApi {
  create: (request: FlowiseResourceUpsertRequest) => Promise<ApiEnvelope<FlowiseResourceDto>>;
  delete: (id: string) => Promise<ApiEnvelope<boolean>>;
  list: (query: FlowiseStudioQuery, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<FlowiseResourceDto>>>;
  reveal?: (id: string) => Promise<ApiEnvelope<FlowiseResourceDto>>;
  update: (id: string, request: FlowiseResourceUpsertRequest) => Promise<ApiEnvelope<FlowiseResourceDto>>;
}

function createApi(routeSegment: 'tools' | 'credentials' | 'variables' | 'api-keys', supportsReveal: boolean): FlowiseConfigurationResourceApi {
  return {
    create: (request) => httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/${routeSegment}`, request),
    delete: (id) => httpClient.delete<boolean>(`${basePath}/${routeSegment}/${id}`),
    list: (query, signal) => httpClient.get<GridPageResult<FlowiseResourceDto>>(`${basePath}/${routeSegment}${buildFlowiseQuery(query)}`, undefined, signal),
    reveal: supportsReveal ? (id) => httpClient.post<FlowiseResourceDto, Record<string, never>>(`${basePath}/${routeSegment}/${id}/reveal`, {}) : undefined,
    update: (id, request) => httpClient.put<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/${routeSegment}/${id}`, request)
  };
}

export const flowiseConfigurationResourcesApi = {
  apiKeys: createApi('api-keys', false),
  credentials: createApi('credentials', true),
  tools: createApi('tools', false),
  variables: createApi('variables', true)
};
