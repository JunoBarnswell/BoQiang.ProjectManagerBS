import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type { FlowiseResourceDto, FlowiseResourceUpsertRequest, FlowiseStudioQuery, GridPageResult } from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise';

export interface FlowiseNativeResourceApi {
  create: (request: FlowiseResourceUpsertRequest) => Promise<ApiEnvelope<FlowiseResourceDto>>;
  createFromFlowTemplate?: (request: FlowiseResourceUpsertRequest) => Promise<ApiEnvelope<FlowiseResourceDto>>;
  delete: (id: string) => Promise<ApiEnvelope<boolean>>;
  get: (id: string, signal?: AbortSignal) => Promise<ApiEnvelope<FlowiseResourceDto>>;
  list: (query: FlowiseStudioQuery, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<FlowiseResourceDto>>>;
  update: (id: string, request: FlowiseResourceUpsertRequest) => Promise<ApiEnvelope<FlowiseResourceDto>>;
}

function createApi(routeSegment: 'assistants' | 'marketplaces'): FlowiseNativeResourceApi {
  return {
    create: (request) => httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/${routeSegment}`, request),
    delete: (id) => httpClient.delete<boolean>(`${basePath}/${routeSegment}/${id}`),
    get: (id, signal) => httpClient.get<FlowiseResourceDto>(`${basePath}/${routeSegment}/${id}`, undefined, signal),
    list: (query, signal) => httpClient.get<GridPageResult<FlowiseResourceDto>>(`${basePath}/${routeSegment}${buildFlowiseQuery(query)}`, undefined, signal),
    update: (id, request) => httpClient.put<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/${routeSegment}/${id}`, request)
  };
}

export const flowiseNativeResourcesApi = {
  assistants: createApi('assistants'),
  marketplaces: {
    ...createApi('marketplaces'),
    createFromFlowTemplate: (request: FlowiseResourceUpsertRequest) =>
      httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${basePath}/marketplaces/from-flow-template`, request)
  }
};
