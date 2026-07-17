import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type FilterQueryRule, type SortQueryRule } from '../queryString';
import type { GridPageResult } from '../shared.types';

export interface OperationLogQuery {
  endTime?: string;
  filters?: FilterQueryRule[];
  isSuccess?: boolean;
  moduleName?: string;
  pageIndex: number;
  pageSize: number;
  requestMethod?: string;
  requestPath?: string;
  sorts?: SortQueryRule[];
  startTime?: string;
  traceId?: string;
  user?: string;
}

export interface OperationLogListItemDto {
  clientIp?: string | null;
  correlationId?: string | null;
  createdTime: string;
  durationMs: number;
  id: string;
  isSuccess: boolean;
  moduleName?: string | null;
  operationType?: string | null;
  actionName?: string | null;
  requestMethod: string;
  requestPath: string;
  routeDisplayName?: string | null;
  statusCode: number;
  traceId: string;
  userName?: string | null;
}

export interface OperationLogDetailDto extends OperationLogListItemDto {
  errorMessage?: string | null;
  exceptionSummary?: string | null;
  requestQuery?: string | null;
  userId?: string | null;
}

export const systemOperationLogApi = {
  detail: (id: string): Promise<ApiEnvelope<OperationLogDetailDto>> =>
    httpClient.get<OperationLogDetailDto>(`/system/operation-logs/${id}`),

  list: (query: OperationLogQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<OperationLogListItemDto>>> =>
    httpClient.get<GridPageResult<OperationLogListItemDto>>(`/system/operation-logs${buildQueryString(query)}`, undefined, signal),

  recent: (take = 20): Promise<ApiEnvelope<OperationLogListItemDto[]>> =>
    httpClient.get<OperationLogListItemDto[]>(`/system/operation-logs/recent${buildQueryString({ take })}`)
};
