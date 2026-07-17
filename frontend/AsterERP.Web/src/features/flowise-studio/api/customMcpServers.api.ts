import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseCustomMcpServerAuthorizeResultDto,
  FlowiseCustomMcpServerDto,
  FlowiseCustomMcpServerPageResult,
  FlowiseCustomMcpServerToolDto,
  FlowiseCustomMcpServerUpsertRequest
} from '../types/customMcpServer.types';
import type { FlowiseStudioQuery } from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise/custom-mcp-servers';

export const customMcpServersApi = {
  authorize: (id: string) => httpClient.post<FlowiseCustomMcpServerAuthorizeResultDto, Record<string, never>>(`${basePath}/${id}/authorize`, {}),
  create: (request: FlowiseCustomMcpServerUpsertRequest) => httpClient.post<FlowiseCustomMcpServerDto, FlowiseCustomMcpServerUpsertRequest>(basePath, request),
  delete: (id: string) => httpClient.delete<boolean>(`${basePath}/${id}`),
  get: (id: string, signal?: AbortSignal) => httpClient.get<FlowiseCustomMcpServerDto>(`${basePath}/${id}`, undefined, signal),
  list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseCustomMcpServerPageResult>> =>
    httpClient.get<FlowiseCustomMcpServerPageResult>(`${basePath}${buildFlowiseQuery(query)}`, undefined, signal),
  tools: (id: string, signal?: AbortSignal) => httpClient.get<FlowiseCustomMcpServerToolDto[]>(`${basePath}/${id}/tools`, undefined, signal),
  update: (id: string, request: FlowiseCustomMcpServerUpsertRequest) => httpClient.put<FlowiseCustomMcpServerDto, FlowiseCustomMcpServerUpsertRequest>(`${basePath}/${id}`, request)
};
