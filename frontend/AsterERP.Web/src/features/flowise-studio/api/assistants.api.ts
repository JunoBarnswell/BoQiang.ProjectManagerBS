import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type { FlowiseAssistantDto, FlowiseAssistantUpsertRequest } from '../types/assistant.types';
import type { FlowiseStudioQuery, GridPageResult } from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise/assistants';

export const flowiseAssistantsApi = {
  create: (request: FlowiseAssistantUpsertRequest): Promise<ApiEnvelope<FlowiseAssistantDto>> =>
    httpClient.post<FlowiseAssistantDto, FlowiseAssistantUpsertRequest>(basePath, request),
  delete: (id: string): Promise<ApiEnvelope<boolean>> =>
    httpClient.delete<boolean>(`${basePath}/${id}`),
  get: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseAssistantDto>> =>
    httpClient.get<FlowiseAssistantDto>(`${basePath}/${id}`, undefined, signal),
  list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseAssistantDto>>> =>
    httpClient.get<GridPageResult<FlowiseAssistantDto>>(`${basePath}${buildFlowiseQuery(query)}`, undefined, signal),
  update: (id: string, request: FlowiseAssistantUpsertRequest): Promise<ApiEnvelope<FlowiseAssistantDto>> =>
    httpClient.put<FlowiseAssistantDto, FlowiseAssistantUpsertRequest>(`${basePath}/${id}`, request)
};
