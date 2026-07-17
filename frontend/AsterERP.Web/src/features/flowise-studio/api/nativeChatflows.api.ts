import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseChatflowDto,
  FlowiseChatflowQuery,
  FlowiseChatflowType,
  FlowiseChatflowUpsertRequest
} from '../types/chatflow.types';
import type { GridPageResult } from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise';

function pathFor(type: FlowiseChatflowType) {
  return type === 'AGENTFLOW' ? `${basePath}/agentflows` : `${basePath}/chatflows`;
}

export const nativeChatflowsApi = {
  list: (type: FlowiseChatflowType, query: FlowiseChatflowQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseChatflowDto>>> =>
    httpClient.get<GridPageResult<FlowiseChatflowDto>>(`${pathFor(type)}${buildFlowiseQuery(query)}`, undefined, signal),
  get: (type: FlowiseChatflowType, id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseChatflowDto>> =>
    httpClient.get<FlowiseChatflowDto>(`${pathFor(type)}/${id}`, undefined, signal),
  create: (type: FlowiseChatflowType, request: FlowiseChatflowUpsertRequest): Promise<ApiEnvelope<FlowiseChatflowDto>> =>
    httpClient.post<FlowiseChatflowDto, FlowiseChatflowUpsertRequest>(pathFor(type), { ...request, type }),
  update: (type: FlowiseChatflowType, id: string, request: FlowiseChatflowUpsertRequest): Promise<ApiEnvelope<FlowiseChatflowDto>> =>
    httpClient.put<FlowiseChatflowDto, FlowiseChatflowUpsertRequest>(`${pathFor(type)}/${id}`, { ...request, type }),
  updateConfiguration: (
    type: FlowiseChatflowType,
    id: string,
    request: Pick<FlowiseChatflowUpsertRequest, 'analytic' | 'apiConfig' | 'chatbotConfig' | 'followUpPrompts' | 'mcpServerConfig' | 'speechToText' | 'textToSpeech' | 'webhookSecret'>
  ): Promise<ApiEnvelope<FlowiseChatflowDto>> =>
    httpClient.put<FlowiseChatflowDto, typeof request>(`${pathFor(type)}/${id}/configuration`, request),
  updateDomains: (
    type: FlowiseChatflowType,
    id: string,
    request: Pick<FlowiseChatflowUpsertRequest, 'chatbotConfig'>
  ): Promise<ApiEnvelope<FlowiseChatflowDto>> =>
    httpClient.put<FlowiseChatflowDto, typeof request>(`${pathFor(type)}/${id}/domains`, request),
  delete: (type: FlowiseChatflowType, id: string): Promise<ApiEnvelope<boolean>> =>
    httpClient.delete<boolean>(`${pathFor(type)}/${id}`)
};
