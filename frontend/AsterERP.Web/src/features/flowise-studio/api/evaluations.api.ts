import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import type {
  FlowiseDatasetDto,
  FlowiseDatasetCsvImportDto,
  FlowiseDatasetListItemDto,
  FlowiseDatasetRowDto,
  FlowiseDatasetSaveRequest,
  FlowiseEvaluationDto,
  FlowiseEvaluationListItemDto,
  FlowiseEvaluationResultDto,
  FlowiseEvaluationSaveRequest,
  FlowiseEvaluatorDto,
  FlowiseEvaluatorListItemDto,
  FlowiseEvaluatorSaveRequest
} from '../types/evaluation.types';
import type { FlowiseStudioQuery, GridPageResult } from '../types/shared.types';

import { buildFlowiseQuery } from './queryString';

const basePath = '/ai/flowise';

export const evaluationsApi = {
  datasets: {
    create: (request: FlowiseDatasetSaveRequest): Promise<ApiEnvelope<FlowiseDatasetListItemDto>> =>
      httpClient.post<FlowiseDatasetListItemDto, FlowiseDatasetSaveRequest>(`${basePath}/datasets`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> =>
      httpClient.delete<boolean>(`${basePath}/datasets/${id}`),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseDatasetListItemDto>>> =>
      httpClient.get<GridPageResult<FlowiseDatasetListItemDto>>(`${basePath}/datasets${buildFlowiseQuery(query)}`, undefined, signal),
    detail: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseDatasetDto>> =>
      httpClient.get<FlowiseDatasetDto>(`${basePath}/datasets/${id}/detail`, undefined, signal),
    rows: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseDatasetRowDto[]>> =>
      httpClient.get<FlowiseDatasetRowDto[]>(`${basePath}/datasets/${id}/rows`, undefined, signal),
    uploadCsv: (id: string, file: File, firstRowHeaders: boolean): Promise<ApiEnvelope<FlowiseDatasetCsvImportDto>> => {
      const form = new FormData();
      form.append('file', file);
      form.append('firstRowHeaders', String(firstRowHeaders));
      return httpClient.postForm<FlowiseDatasetCsvImportDto>(`${basePath}/datasets/${id}/upload-csv`, form);
    },
    update: (id: string, request: FlowiseDatasetSaveRequest): Promise<ApiEnvelope<FlowiseDatasetListItemDto>> =>
      httpClient.put<FlowiseDatasetListItemDto, FlowiseDatasetSaveRequest>(`${basePath}/datasets/${id}`, request)
  },
  evaluators: {
    create: (request: FlowiseEvaluatorSaveRequest): Promise<ApiEnvelope<FlowiseEvaluatorListItemDto>> =>
      httpClient.post<FlowiseEvaluatorListItemDto, FlowiseEvaluatorSaveRequest>(`${basePath}/evaluators`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> =>
      httpClient.delete<boolean>(`${basePath}/evaluators/${id}`),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseEvaluatorListItemDto>>> =>
      httpClient.get<GridPageResult<FlowiseEvaluatorListItemDto>>(`${basePath}/evaluators${buildFlowiseQuery(query)}`, undefined, signal),
    detail: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseEvaluatorDto>> =>
      httpClient.get<FlowiseEvaluatorDto>(`${basePath}/evaluators/${id}/detail`, undefined, signal),
    update: (id: string, request: FlowiseEvaluatorSaveRequest): Promise<ApiEnvelope<FlowiseEvaluatorListItemDto>> =>
      httpClient.put<FlowiseEvaluatorListItemDto, FlowiseEvaluatorSaveRequest>(`${basePath}/evaluators/${id}`, request)
  },
  evaluations: {
    create: (request: FlowiseEvaluationSaveRequest): Promise<ApiEnvelope<FlowiseEvaluationListItemDto>> =>
      httpClient.post<FlowiseEvaluationListItemDto, FlowiseEvaluationSaveRequest>(`${basePath}/evaluations`, request),
    delete: (id: string): Promise<ApiEnvelope<boolean>> =>
      httpClient.delete<boolean>(`${basePath}/evaluations/${id}`),
    detail: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseEvaluationDto>> =>
      httpClient.get<FlowiseEvaluationDto>(`${basePath}/evaluations/${id}/detail`, undefined, signal),
    list: (query: FlowiseStudioQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<FlowiseEvaluationListItemDto>>> =>
      httpClient.get<GridPageResult<FlowiseEvaluationListItemDto>>(`${basePath}/evaluations${buildFlowiseQuery(query)}`, undefined, signal),
    result: (id: string, signal?: AbortSignal): Promise<ApiEnvelope<FlowiseEvaluationResultDto>> =>
      httpClient.get<FlowiseEvaluationResultDto>(`${basePath}/evaluations/${id}/result`, undefined, signal),
    runAgain: (id: string): Promise<ApiEnvelope<FlowiseEvaluationResultDto>> =>
      httpClient.post<FlowiseEvaluationResultDto, Record<string, never>>(`${basePath}/evaluations/${id}/run-again`, {}),
    update: (id: string, request: FlowiseEvaluationSaveRequest): Promise<ApiEnvelope<FlowiseEvaluationListItemDto>> =>
      httpClient.put<FlowiseEvaluationListItemDto, FlowiseEvaluationSaveRequest>(`${basePath}/evaluations/${id}`, request)
  }
};
