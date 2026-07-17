import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type FilterQueryRule, type SortQueryRule } from '../queryString';
import type { GridPageResult } from '../shared.types';

export type ScheduledJobType = 'HttpCallback' | 'Preset';
export type ScheduledJobStatus = 'Enabled' | 'Paused';
export type ScheduledJobResult = 'Failed' | 'Queued' | 'Success';
export type ScheduledJobScheduleKind = 'Daily' | 'EveryHours' | 'EveryMinutes' | 'Monthly' | 'Weekly';

export interface ScheduleConfigDto {
  intervalValue?: number | null;
  kind: ScheduledJobScheduleKind;
  monthDays?: number[] | null;
  timeOfDay?: string | null;
  timeZone?: string | null;
  weekDays?: number[] | null;
}

export interface HttpCallbackConfigDto {
  bodyJson?: string | null;
  headers?: Record<string, string> | null;
  method: 'GET' | 'POST';
  url: string;
}

export interface ScheduledJobUpsertRequest {
  code: string;
  httpCallback?: HttpCallbackConfigDto | null;
  jobType: ScheduledJobType;
  name: string;
  parameters?: string | null;
  presetJobCode?: string | null;
  remark?: string | null;
  schedule: ScheduleConfigDto;
  status: ScheduledJobStatus;
}

export interface ScheduledJobListItemDto {
  code: string;
  createdTime: string;
  friendlySchedule: string;
  id: string;
  jobType: ScheduledJobType;
  lastResult?: ScheduledJobResult | null;
  lastRunAt?: string | null;
  name: string;
  nextRunAt?: string | null;
  presetJobCode?: string | null;
  remark?: string | null;
  scheduleSyncStatus: string;
  status: ScheduledJobStatus;
}

export interface ScheduledJobDetailDto extends ScheduledJobListItemDto {
  httpCallback?: HttpCallbackConfigDto | null;
  lastErrorMessage?: string | null;
  lastSyncError?: string | null;
  parameters?: string | null;
  schedule: ScheduleConfigDto;
}

export interface ScheduledJobLogDto {
  durationMs: number;
  endTime?: string | null;
  errorMessage?: string | null;
  id: string;
  jobId?: string | null;
  outputSummary?: string | null;
  result: ScheduledJobResult;
  startTime: string;
  traceId: string;
  triggerType: 'Automatic' | 'Manual';
}

export interface ScheduledJobSummaryDto {
  enabled: number;
  failed: number;
  paused: number;
  success: number;
  total: number;
}

export interface ScheduledJobTypeOptionDto {
  code: string;
  description: string;
  name: string;
  supportsParameters: boolean;
}

export interface ScheduledJobTypesDto {
  httpMethods: string[];
  jobTypes: string[];
  presetJobs: ScheduledJobTypeOptionDto[];
  scheduleKinds: string[];
}

export interface ScheduledJobListQuery {
  filters?: FilterQueryRule[];
  jobType?: string;
  keyword?: string;
  pageIndex: number;
  pageSize: number;
  result?: string;
  sorts?: SortQueryRule[];
  status?: string;
}

export interface ScheduledJobLogQuery {
  filters?: FilterQueryRule[];
  pageIndex: number;
  pageSize: number;
  result?: string;
  sorts?: SortQueryRule[];
}

export const systemScheduledJobApi = {
  create: (request: ScheduledJobUpsertRequest) =>
    httpClient.post<ScheduledJobListItemDto, ScheduledJobUpsertRequest>('/system/scheduled-jobs', request),

  delete: (id: string) =>
    httpClient.delete<boolean>(`/system/scheduled-jobs/${id}`),

  detail: (id: string) =>
    httpClient.get<ScheduledJobDetailDto>(`/system/scheduled-jobs/${id}`),

  jobTypes: () =>
    httpClient.get<ScheduledJobTypesDto>('/system/scheduled-jobs/job-types'),

  list: (query: ScheduledJobListQuery, signal?: AbortSignal) =>
    httpClient.get<GridPageResult<ScheduledJobListItemDto>>(`/system/scheduled-jobs${buildQueryString(query)}`, undefined, signal),

  logs: (id: string, query: ScheduledJobLogQuery, signal?: AbortSignal) =>
    httpClient.get<GridPageResult<ScheduledJobLogDto>>(`/system/scheduled-jobs/${id}/logs${buildQueryString(query)}`, undefined, signal),

  pause: (id: string) =>
    httpClient.post<boolean, Record<string, never>>(`/system/scheduled-jobs/${id}/pause`, {}),

  resume: (id: string) =>
    httpClient.post<boolean, Record<string, never>>(`/system/scheduled-jobs/${id}/resume`, {}),

  summary: () =>
    httpClient.get<ScheduledJobSummaryDto>('/system/scheduled-jobs/summary'),

  trigger: (id: string) =>
    httpClient.post<string, Record<string, never>>(`/system/scheduled-jobs/${id}/trigger`, {}),

  update: (id: string, request: ScheduledJobUpsertRequest) =>
    httpClient.put<ScheduledJobListItemDto, ScheduledJobUpsertRequest>(`/system/scheduled-jobs/${id}`, request)
};
