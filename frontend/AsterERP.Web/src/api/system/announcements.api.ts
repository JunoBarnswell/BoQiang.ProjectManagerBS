import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type FilterQueryRule, type SortQueryRule } from '../queryString';
import type { GridPageResult } from '../shared.types';

export type AnnouncementStatus = 'Draft' | 'Published' | 'Withdrawn';
export type AnnouncementEffectiveStatus = AnnouncementStatus | 'Expired';

export interface SystemAnnouncementListItemDto {
  announcementType: string;
  content: string;
  createdTime: string;
  effectiveStatus: AnnouncementEffectiveStatus;
  expiresAt?: string | null;
  id: string;
  isPinned: boolean;
  priority: number;
  publishedAt?: string | null;
  publishedBy?: string | null;
  remark?: string | null;
  revokedAt?: string | null;
  scope: string;
  status: AnnouncementStatus;
  title: string;
  updatedTime?: string | null;
}

export interface SystemAnnouncementListQuery {
  announcementType?: string;
  filters?: FilterQueryRule[];
  keyword?: string;
  pageIndex: number;
  pageSize: number;
  sorts?: SortQueryRule[];
  status?: AnnouncementEffectiveStatus | '';
}

export interface SystemAnnouncementUpsertRequest {
  announcementType?: string;
  content: string;
  expiresAt?: string | null;
  isPinned?: boolean;
  priority?: number;
  remark?: string | null;
  scope?: string;
  title: string;
}

export const systemAnnouncementApi = {
  list: (query: SystemAnnouncementListQuery, signal?: AbortSignal): Promise<ApiEnvelope<GridPageResult<SystemAnnouncementListItemDto>>> =>
    httpClient.get<GridPageResult<SystemAnnouncementListItemDto>>(`/system/announcements${buildQueryString(query)}`, undefined, signal),

  create: (request: SystemAnnouncementUpsertRequest): Promise<ApiEnvelope<SystemAnnouncementListItemDto>> =>
    httpClient.post<SystemAnnouncementListItemDto, SystemAnnouncementUpsertRequest>('/system/announcements', request),

  update: (id: string, request: SystemAnnouncementUpsertRequest): Promise<ApiEnvelope<SystemAnnouncementListItemDto>> =>
    httpClient.put<SystemAnnouncementListItemDto, SystemAnnouncementUpsertRequest>(`/system/announcements/${id}`, request),

  delete: (id: string): Promise<ApiEnvelope<boolean>> =>
    httpClient.delete<boolean>(`/system/announcements/${id}`),

  publish: (id: string): Promise<ApiEnvelope<SystemAnnouncementListItemDto>> =>
    httpClient.post<SystemAnnouncementListItemDto, Record<string, never>>(`/system/announcements/${id}/publish`, {}),

  withdraw: (id: string): Promise<ApiEnvelope<SystemAnnouncementListItemDto>> =>
    httpClient.post<SystemAnnouncementListItemDto, Record<string, never>>(`/system/announcements/${id}/withdraw`, {}),

  revoke: (id: string): Promise<ApiEnvelope<SystemAnnouncementListItemDto>> =>
    httpClient.post<SystemAnnouncementListItemDto, Record<string, never>>(`/system/announcements/${id}/withdraw`, {}),

  setTop: (id: string, isPinned: boolean): Promise<ApiEnvelope<SystemAnnouncementListItemDto>> =>
    httpClient.post<SystemAnnouncementListItemDto, { isTop: boolean }>(`/system/announcements/${id}/top`, { isTop: isPinned })
};
