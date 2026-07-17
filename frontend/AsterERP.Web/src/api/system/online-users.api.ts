import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type FilterQueryRule, type SortQueryRule } from '../queryString';
import type { GridPageResult } from '../shared.types';

export interface OnlineUserListItemDto {
  clientIp?: string | null;
  createdTime: string;
  deptId?: string | null;
  displayName: string;
  expiresAt: string;
  lastSeenTime?: string | null;
  sessionId: string;
  userAgent?: string | null;
  userId: string;
  userName: string;
}

export interface OnlineUserQuery {
  filters?: FilterQueryRule[];
  keyword?: string;
  pageIndex: number;
  pageSize: number;
  sorts?: SortQueryRule[];
}

export type OnlineUserListQuery = OnlineUserQuery;

export const systemOnlineUserApi = {
  list: (query: OnlineUserQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<OnlineUserListItemDto>>> =>
    httpClient.get<GridPageResult<OnlineUserListItemDto>>(`/system/online-users${buildQueryString(query)}`, undefined, signal),
  forceLogout: (sessionId: string): Promise<ApiEnvelope<boolean>> =>
    httpClient.post<boolean, Record<string, never>>(`/system/online-users/${encodeURIComponent(sessionId)}/force-logout`, {})
};

export const systemOnlineUsersApi = systemOnlineUserApi;
