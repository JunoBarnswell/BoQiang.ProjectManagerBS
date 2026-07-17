import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type FilterQueryRule, type SortQueryRule } from '../queryString';
import type { GridPageResult } from '../shared.types';

export interface SystemParameterListItemDto {
  id: string;
  paramName: string;
  paramKey: string;
  paramValue: string;
  isSensitive: boolean;
  category: string;
  isEnabled: boolean;
  remark?: string | null;
}

export interface SystemParameterUpsertRequest {
  paramName: string;
  paramKey: string;
  paramValue: string;
  category: string;
  isEnabled: boolean;
  remark?: string | null;
}

export interface SystemParameterListQuery {
  category?: string;
  filters?: FilterQueryRule[];
  keyword?: string;
  pageIndex: number;
  pageSize: number;
  sorts?: SortQueryRule[];
  status?: string;
}

export interface SystemParameterStatusUpdateRequest {
  ids: string[];
  status: 'Enabled' | 'Disabled';
}

export const systemParameterApi = {
  list: (query: SystemParameterListQuery, signal?: AbortSignal) =>
    httpClient.get<GridPageResult<SystemParameterListItemDto>>(`/system/parameters${buildQueryString(query)}`, undefined, signal),

  create: (request: SystemParameterUpsertRequest) =>
    httpClient.post<SystemParameterListItemDto, SystemParameterUpsertRequest>('/system/parameters', request),

  update: (id: string, request: SystemParameterUpsertRequest) =>
    httpClient.put<SystemParameterListItemDto, SystemParameterUpsertRequest>(`/system/parameters/${id}`, request),

  delete: (id: string) =>
    httpClient.delete<boolean>(`/system/parameters/${id}`),

  batchUpdateStatus: (request: SystemParameterStatusUpdateRequest) =>
    httpClient.post<boolean, SystemParameterStatusUpdateRequest>('/system/parameters/batch-status', request)
};
