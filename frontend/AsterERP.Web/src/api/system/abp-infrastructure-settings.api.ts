import { httpClient } from '../../core/http/httpClient';
import { buildQueryString, type FilterQueryRule, type SortQueryRule } from '../queryString';
import type { GridPageResult } from '../shared.types';

export interface SecretSettingStateDto {
  isConfigured: boolean;
}

export interface SecretSettingUpdate {
  clear?: boolean;
  value?: string | null;
}

export interface EmailInfrastructureSettingsDto {
  enabled: boolean;
  smtpHost?: string | null;
  smtpPort?: number | null;
  userName?: string | null;
  password: SecretSettingStateDto;
  defaultFromAddress?: string | null;
  defaultFromDisplayName?: string | null;
  enableSsl: boolean;
}

export interface SmsInfrastructureSettingsDto {
  enabled: boolean;
  provider: string;
  aliyunAccessKeyId?: string | null;
  aliyunAccessKeySecret: SecretSettingStateDto;
  aliyunSignName?: string | null;
  aliyunTemplateCode?: string | null;
  aliyunTemplateParamName?: string | null;
  tencentSecretId?: string | null;
  tencentSecretKey: SecretSettingStateDto;
  tencentSdkAppId?: string | null;
  tencentSignName?: string | null;
  tencentTemplateId?: string | null;
  tencentRegion?: string | null;
}

export interface ObjectStorageInfrastructureSettingsDto {
  provider: string;
  fileSystemBasePath?: string | null;
  fileSystemAppendContainerName: boolean;
  aliyunEndpoint?: string | null;
  aliyunBucketName?: string | null;
  aliyunAccessKeyId?: string | null;
  aliyunAccessKeySecret: SecretSettingStateDto;
  minioEndpoint?: string | null;
  minioBucketName?: string | null;
  minioAccessKey?: string | null;
  minioSecretKey: SecretSettingStateDto;
  minioWithSsl: boolean;
}

export interface CacheInfrastructureSettingsDto {
  provider: string;
  redisConfiguration: SecretSettingStateDto;
  defaultExpirationMinutes: number;
}

export interface JobsInfrastructureSettingsDto {
  abpBackgroundJobsEnabled: boolean;
  messagingJobsEnabled: boolean;
  testTimeoutSeconds: number;
}

export interface AuditInfrastructureSettingsDto {
  operationLogEnabled: boolean;
  captureQueryString: boolean;
  queueCapacity: number;
}

export interface InfrastructureSettingsDto {
  email: EmailInfrastructureSettingsDto;
  sms: SmsInfrastructureSettingsDto;
  objectStorage: ObjectStorageInfrastructureSettingsDto;
  cache: CacheInfrastructureSettingsDto;
  jobs: JobsInfrastructureSettingsDto;
  audit: AuditInfrastructureSettingsDto;
}

export interface EmailInfrastructureSettingsUpdate {
  enabled?: boolean;
  smtpHost?: string | null;
  smtpPort?: number | null;
  userName?: string | null;
  password?: SecretSettingUpdate | null;
  defaultFromAddress?: string | null;
  defaultFromDisplayName?: string | null;
  enableSsl?: boolean;
}

export interface SmsInfrastructureSettingsUpdate {
  enabled?: boolean;
  provider?: string | null;
  aliyunAccessKeyId?: string | null;
  aliyunAccessKeySecret?: SecretSettingUpdate | null;
  aliyunSignName?: string | null;
  aliyunTemplateCode?: string | null;
  aliyunTemplateParamName?: string | null;
  tencentSecretId?: string | null;
  tencentSecretKey?: SecretSettingUpdate | null;
  tencentSdkAppId?: string | null;
  tencentSignName?: string | null;
  tencentTemplateId?: string | null;
  tencentRegion?: string | null;
}

export interface ObjectStorageInfrastructureSettingsUpdate {
  provider?: string | null;
  fileSystemBasePath?: string | null;
  fileSystemAppendContainerName?: boolean;
  aliyunEndpoint?: string | null;
  aliyunBucketName?: string | null;
  aliyunAccessKeyId?: string | null;
  aliyunAccessKeySecret?: SecretSettingUpdate | null;
  minioEndpoint?: string | null;
  minioBucketName?: string | null;
  minioAccessKey?: string | null;
  minioSecretKey?: SecretSettingUpdate | null;
  minioWithSsl?: boolean;
}

export interface CacheInfrastructureSettingsUpdate {
  provider?: string | null;
  redisConfiguration?: SecretSettingUpdate | null;
  defaultExpirationMinutes?: number | null;
}

export interface JobsInfrastructureSettingsUpdate {
  abpBackgroundJobsEnabled?: boolean;
  messagingJobsEnabled?: boolean;
  testTimeoutSeconds?: number | null;
}

export interface AuditInfrastructureSettingsUpdate {
  operationLogEnabled?: boolean;
  captureQueryString?: boolean;
  queueCapacity?: number | null;
}

export interface InfrastructureSettingsUpdateRequest {
  email?: EmailInfrastructureSettingsUpdate | null;
  sms?: SmsInfrastructureSettingsUpdate | null;
  objectStorage?: ObjectStorageInfrastructureSettingsUpdate | null;
  cache?: CacheInfrastructureSettingsUpdate | null;
  jobs?: JobsInfrastructureSettingsUpdate | null;
  audit?: AuditInfrastructureSettingsUpdate | null;
}

export interface InfrastructureEmailTestRequest {
  to: string;
  subject?: string;
  body?: string;
  isBodyHtml?: boolean;
}

export interface InfrastructureSmsTestRequest {
  phoneNumber: string;
  text?: string;
}

export interface InfrastructureObjectStorageTestRequest {
  provider?: string | null;
}

export interface InfrastructureTestResultDto {
  success: boolean;
  provider: string;
  traceId: string;
  message: string;
  durationMs: number;
}

export interface MessageSendLogDto {
  id: string;
  channel: string;
  provider: string;
  maskedTarget?: string | null;
  traceId: string;
  correlationId?: string | null;
  result: string;
  errorSummary?: string | null;
  durationMs: number;
  createdTime: string;
}

export interface MessageSendLogQuery {
  pageIndex: number;
  pageSize: number;
  startTime?: string;
  endTime?: string;
  channel?: string;
  provider?: string;
  result?: string;
  traceId?: string;
  filters?: FilterQueryRule[];
  sorts?: SortQueryRule[];
}

export const abpInfrastructureSettingsApi = {
  get: (signal?: AbortSignal) =>
    httpClient.get<InfrastructureSettingsDto>('/system/infrastructure-settings', undefined, signal),

  update: (request: InfrastructureSettingsUpdateRequest) =>
    httpClient.put<InfrastructureSettingsDto, InfrastructureSettingsUpdateRequest>('/system/infrastructure-settings', request),

  testEmail: (request: InfrastructureEmailTestRequest) =>
    httpClient.post<InfrastructureTestResultDto, InfrastructureEmailTestRequest>('/system/infrastructure-settings/email/test', request, 120_000),

  testSms: (request: InfrastructureSmsTestRequest) =>
    httpClient.post<InfrastructureTestResultDto, InfrastructureSmsTestRequest>('/system/infrastructure-settings/sms/test', request, 120_000),

  testObjectStorage: (request: InfrastructureObjectStorageTestRequest) =>
    httpClient.post<InfrastructureTestResultDto, InfrastructureObjectStorageTestRequest>('/system/infrastructure-settings/object-storage/test', request, 120_000),

  messageLogs: (query: MessageSendLogQuery, signal?: AbortSignal) =>
    httpClient.get<GridPageResult<MessageSendLogDto>>(`/system/infrastructure-settings/message-logs${buildQueryString(query)}`, undefined, signal)
};
