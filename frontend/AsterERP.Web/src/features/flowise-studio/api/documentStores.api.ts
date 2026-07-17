import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseDocumentStoreChunkDto,
  FlowiseDocumentStoreDto,
  FlowiseDocumentStoreFileDto,
  FlowiseDocumentStoreListItemDto,
  FlowiseDocumentStoreQueryRequest,
  FlowiseDocumentStoreQueryResultDto,
  FlowiseDocumentStoreSaveRequest,
  FlowiseDocumentStoreUpsertHistoryDto,
  FlowiseDocumentStoreUpsertRequest,
  FlowiseVectorStoreConfigDto
} from '../types/documentStore.types';
import type { FlowiseStudioQuery, GridPageResult } from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise/document-stores';

export const documentStoresApi = {
  chunks: (storeId: string, fileId?: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseDocumentStoreChunkDto[]>> =>
    httpClient.get<FlowiseDocumentStoreChunkDto[]>(`${basePath}/${storeId}/chunks${fileId ? `/${fileId}` : ''}`, undefined, signal),
  create: (request: FlowiseDocumentStoreSaveRequest): Promise<ApiEnvelope<FlowiseDocumentStoreListItemDto>> =>
    httpClient.post<FlowiseDocumentStoreListItemDto, FlowiseDocumentStoreSaveRequest>(basePath, request),
  delete: (id: string): Promise<ApiEnvelope<boolean>> => httpClient.delete<boolean>(`${basePath}/${id}`),
  files: (storeId: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseDocumentStoreFileDto[]>> =>
    httpClient.get<FlowiseDocumentStoreFileDto[]>(`${basePath}/${storeId}/files`, undefined, signal),
  get: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseDocumentStoreDto>> =>
    httpClient.get<FlowiseDocumentStoreDto>(`${basePath}/${id}/detail`, undefined, signal),
  list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseDocumentStoreListItemDto>>> =>
    httpClient.get<GridPageResult<FlowiseDocumentStoreListItemDto>>(`${basePath}${buildFlowiseQuery(query)}`, undefined, signal),
  query: (request: FlowiseDocumentStoreQueryRequest): Promise<ApiEnvelope<FlowiseDocumentStoreQueryResultDto>> =>
    httpClient.post<FlowiseDocumentStoreQueryResultDto, FlowiseDocumentStoreQueryRequest>(`${basePath}/${request.storeId}/query`, request),
  update: (id: string, request: FlowiseDocumentStoreSaveRequest): Promise<ApiEnvelope<FlowiseDocumentStoreListItemDto>> =>
    httpClient.put<FlowiseDocumentStoreListItemDto, FlowiseDocumentStoreSaveRequest>(`${basePath}/${id}`, request),
  upsert: (request: FlowiseDocumentStoreUpsertRequest): Promise<ApiEnvelope<FlowiseDocumentStoreUpsertHistoryDto>> =>
    httpClient.post<FlowiseDocumentStoreUpsertHistoryDto, FlowiseDocumentStoreUpsertRequest>(`${basePath}/${request.storeId}/upsert`, request),
  upsertHistory: (storeId: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseDocumentStoreUpsertHistoryDto[]>> =>
    httpClient.get<FlowiseDocumentStoreUpsertHistoryDto[]>(`${basePath}/${storeId}/upsert-history`, undefined, signal),
  vectorConfig: (storeId: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseVectorStoreConfigDto | null>> =>
    httpClient.get<FlowiseVectorStoreConfigDto | null>(`${basePath}/${storeId}/vector-config`, undefined, signal)
};
