import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type FilterQueryRule, type SortQueryRule } from '../queryString';
import type { GridPageResult } from '../shared.types';

export type LoginLogResult = 'Success' | 'AccountNotFound' | 'PasswordError' | 'AccountDisabled';

export interface SystemLoginLogListItemDto {
  id: string;
  traceId: string;
  userName: string;
  userId?: string | null;
  userDisplayName?: string | null;
  loginResult: LoginLogResult;
  isSuccess: boolean;
  failureReason?: string | null;
  clientIp?: string | null;
  userAgent?: string | null;
  createdTime: string;
}

export interface SystemLoginLogQuery {
  filters?: FilterQueryRule[];
  keyword?: string;
  loginResult?: string;
  startTime?: string;
  endTime?: string;
  pageIndex: number;
  pageSize: number;
  sorts?: SortQueryRule[];
}

export const systemLoginLogsApi = {
  list: (query: SystemLoginLogQuery, signal?: AbortSignal) =>
    httpClient.get<GridPageResult<SystemLoginLogListItemDto>>(`/system/login-logs${buildQueryString(query)}`, undefined, signal)
};
