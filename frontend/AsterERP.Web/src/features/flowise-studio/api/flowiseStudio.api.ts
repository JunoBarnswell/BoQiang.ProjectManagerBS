import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseAccountSettingsDto,
  FlowiseCanvasDto,
  FlowiseCanvasUpsertRequest,
  FlowiseExecutionDto,
  FlowiseExecutionStartRequest,
  FlowiseNodeCatalogItemDto,
  FlowiseOverviewDto,
  FlowiseResourceTypeDto,
  FlowiseSharedWorkspaceDto,
  FlowiseShareWorkspacesRequest,
  FlowiseStudioQuery,
  FlowiseWorkspaceDto,
  FlowiseWorkspaceUpsertRequest,
  GridPageResult
} from '../flowiseStudio.types';

const basePath = '/ai/flowise';

function buildQuery(query: Partial<FlowiseStudioQuery> = {}): string {
  const params = new URLSearchParams();
  Object.entries(query).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    params.set(key, String(value));
  });
  const queryString = params.toString();
  return queryString ? `?${queryString}` : '';
}

export const flowiseStudioApi = {
  overview: (signal?: AbortSignal): Promise<ApiEnvelope<FlowiseOverviewDto>> =>
    httpClient.get<FlowiseOverviewDto>(`${basePath}/overview`, undefined, signal),

  resourceTypes: (signal?: AbortSignal): Promise<ApiEnvelope<FlowiseResourceTypeDto[]>> =>
    httpClient.get<FlowiseResourceTypeDto[]>(`${basePath}/resource-types`, undefined, signal),

  workspaces: {
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseWorkspaceDto>>> =>
      httpClient.get<GridPageResult<FlowiseWorkspaceDto>>(`${basePath}/workspaces${buildQuery(query)}`, undefined, signal),
    create: (request: FlowiseWorkspaceUpsertRequest): Promise<ApiEnvelope<FlowiseWorkspaceDto>> =>
      httpClient.post<FlowiseWorkspaceDto, FlowiseWorkspaceUpsertRequest>(`${basePath}/workspaces`, request),
    update: (id: string, request: FlowiseWorkspaceUpsertRequest): Promise<ApiEnvelope<FlowiseWorkspaceDto>> =>
      httpClient.put<FlowiseWorkspaceDto, FlowiseWorkspaceUpsertRequest>(`${basePath}/workspaces/${id}`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> =>
      httpClient.delete<boolean>(`${basePath}/workspaces/${id}`)
  },

  sharedWorkspaces: {
    list: (itemId: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseSharedWorkspaceDto[]>> =>
      httpClient.get<FlowiseSharedWorkspaceDto[]>(`${basePath}/shared-workspaces/${itemId}`, undefined, signal),
    save: (itemId: string, request: FlowiseShareWorkspacesRequest): Promise<ApiEnvelope<FlowiseSharedWorkspaceDto[]>> =>
      httpClient.put<FlowiseSharedWorkspaceDto[], FlowiseShareWorkspacesRequest>(`${basePath}/shared-workspaces/${itemId}`, request)
  },

  canvas: {
    nodes: (signal?: AbortSignal): Promise<ApiEnvelope<FlowiseNodeCatalogItemDto[]>> =>
      httpClient.get<FlowiseNodeCatalogItemDto[]>(`${basePath}/canvas/nodes`, undefined, signal),
    get: (resourceId: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseCanvasDto>> =>
      httpClient.get<FlowiseCanvasDto>(`${basePath}/canvas/${resourceId}`, undefined, signal),
    save: (request: FlowiseCanvasUpsertRequest): Promise<ApiEnvelope<FlowiseCanvasDto>> =>
      httpClient.put<FlowiseCanvasDto, FlowiseCanvasUpsertRequest>(`${basePath}/canvas`, request)
  },

  executions: {
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseExecutionDto>>> =>
      httpClient.get<GridPageResult<FlowiseExecutionDto>>(`${basePath}/executions${buildQuery(query)}`, undefined, signal),
    get: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseExecutionDto>> =>
      httpClient.get<FlowiseExecutionDto>(`${basePath}/executions/${id}`, undefined, signal),
    run: (request: FlowiseExecutionStartRequest): Promise<ApiEnvelope<FlowiseExecutionDto>> =>
      httpClient.post<FlowiseExecutionDto, FlowiseExecutionStartRequest>(`${basePath}/executions/run`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> =>
      httpClient.delete<boolean>(`${basePath}/executions/${id}`)
  },

  account: {
    get: (signal?: AbortSignal): Promise<ApiEnvelope<FlowiseAccountSettingsDto>> =>
      httpClient.get<FlowiseAccountSettingsDto>(`${basePath}/account/settings`, undefined, signal),
    update: (request: FlowiseAccountSettingsDto): Promise<ApiEnvelope<FlowiseAccountSettingsDto>> =>
      httpClient.put<FlowiseAccountSettingsDto, FlowiseAccountSettingsDto>(`${basePath}/account/settings`, request)
  }
};
