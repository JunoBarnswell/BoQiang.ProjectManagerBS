import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseMcpServerConfigDto,
  FlowiseMcpServerUpsertRequest,
  FlowiseScheduleStatusDto,
  FlowiseScheduleTriggerLogDto
} from '../types/chatflow.types';
import type {
  FlowiseExportDto,
  FlowiseImportRequest,
  FlowiseImportResultDto,
  FlowiseResourceDto,
  FlowiseResourceUpsertRequest,
  FlowiseStudioQuery,
  GridPageResult
} from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise';

function resourcePath(kind: 'chatflows' | 'agentflows') {
  return `${basePath}/${kind}`;
}

export const chatflowsApi = {
  list: (kind: 'chatflows' | 'agentflows', query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseResourceDto>>> =>
    httpClient.get<GridPageResult<FlowiseResourceDto>>(`${resourcePath(kind)}${buildFlowiseQuery(query)}`, undefined, signal),
  get: (kind: 'chatflows' | 'agentflows', id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseResourceDto>> =>
    httpClient.get<FlowiseResourceDto>(`${resourcePath(kind)}/${id}`, undefined, signal),
  create: (kind: 'chatflows' | 'agentflows', request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
    httpClient.post<FlowiseResourceDto, FlowiseResourceUpsertRequest>(resourcePath(kind), request),
  update: (kind: 'chatflows' | 'agentflows', id: string, request: FlowiseResourceUpsertRequest): Promise<ApiEnvelope<FlowiseResourceDto>> =>
    httpClient.put<FlowiseResourceDto, FlowiseResourceUpsertRequest>(`${resourcePath(kind)}/${id}`, request),
  delete: (kind: 'chatflows' | 'agentflows', id: string): Promise<ApiEnvelope<boolean>> =>
    httpClient.delete<boolean>(`${resourcePath(kind)}/${id}`),
  import: (kind: 'chatflows' | 'agentflows', request: FlowiseImportRequest): Promise<ApiEnvelope<FlowiseImportResultDto>> =>
    httpClient.post<FlowiseImportResultDto, FlowiseImportRequest>(`${resourcePath(kind)}/import`, request),
  export: (kind: 'chatflows' | 'agentflows', query: FlowiseStudioQuery): Promise<ApiEnvelope<FlowiseExportDto>> =>
    httpClient.post<FlowiseExportDto, FlowiseStudioQuery>(`${resourcePath(kind)}/export`, query),
  schedule: {
    status: (kind: 'chatflows' | 'agentflows', id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseScheduleStatusDto>> =>
      httpClient.get<FlowiseScheduleStatusDto>(`${resourcePath(kind)}/${id}/schedule/status`, undefined, signal),
    logs: (
      kind: 'chatflows' | 'agentflows',
      id: string,
      query: { pageIndex?: number; pageSize?: number; status?: string | null },
      signal?: AbortSignal
    ): Promise<ApiEnvelope<GridPageResult<FlowiseScheduleTriggerLogDto>>> =>
      httpClient.get<GridPageResult<FlowiseScheduleTriggerLogDto>>(`${resourcePath(kind)}/${id}/schedule/logs${buildFlowiseQuery(query)}`, undefined, signal)
  },
  mcpServer: {
    get: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseMcpServerConfigDto>> =>
      httpClient.get<FlowiseMcpServerConfigDto>(`${basePath}/mcp-server/${id}`, undefined, signal),
    create: (id: string, request: FlowiseMcpServerUpsertRequest): Promise<ApiEnvelope<FlowiseMcpServerConfigDto>> =>
      httpClient.post<FlowiseMcpServerConfigDto, FlowiseMcpServerUpsertRequest>(`${basePath}/mcp-server/${id}`, request),
    update: (id: string, request: FlowiseMcpServerUpsertRequest): Promise<ApiEnvelope<FlowiseMcpServerConfigDto>> =>
      httpClient.put<FlowiseMcpServerConfigDto, FlowiseMcpServerUpsertRequest>(`${basePath}/mcp-server/${id}`, request),
    disable: (id: string): Promise<ApiEnvelope<FlowiseMcpServerConfigDto>> =>
      httpClient.delete<FlowiseMcpServerConfigDto>(`${basePath}/mcp-server/${id}`),
    refresh: (id: string): Promise<ApiEnvelope<FlowiseMcpServerConfigDto>> =>
      httpClient.post<FlowiseMcpServerConfigDto, Record<string, never>>(`${basePath}/mcp-server/${id}/refresh`, {})
  }
};
