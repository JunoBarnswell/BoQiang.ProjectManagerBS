import type {
  InfrastructureSettingsDto,
  InfrastructureSettingsUpdateRequest,
  SecretSettingUpdate
} from '../../../api/system/abp-infrastructure-settings.api';

import type { InfrastructureFormState, MessageLogSearchState } from './abpInfrastructureTypes';

export function toFormState(settings: InfrastructureSettingsDto): InfrastructureFormState {
  return {
    abpBackgroundJobsEnabled: settings.jobs.abpBackgroundJobsEnabled,
    aliyunAccessKeyId: settings.sms.aliyunAccessKeyId ?? '',
    aliyunAccessKeySecret: '',
    aliyunAccessKeySecretClear: false,
    aliyunSignName: settings.sms.aliyunSignName ?? '',
    aliyunTemplateCode: settings.sms.aliyunTemplateCode ?? '',
    aliyunTemplateParamName: settings.sms.aliyunTemplateParamName ?? 'content',
    auditQueueCapacity: String(settings.audit.queueCapacity),
    cacheDefaultExpirationMinutes: String(settings.cache.defaultExpirationMinutes),
    cacheProvider: settings.cache.provider,
    captureQueryString: settings.audit.captureQueryString,
    defaultFromAddress: settings.email.defaultFromAddress ?? '',
    defaultFromDisplayName: settings.email.defaultFromDisplayName ?? '',
    emailEnabled: settings.email.enabled,
    emailEnableSsl: settings.email.enableSsl,
    emailPassword: '',
    emailPasswordClear: false,
    emailUserName: settings.email.userName ?? '',
    fileSystemAppendContainerName: settings.objectStorage.fileSystemAppendContainerName,
    fileSystemBasePath: settings.objectStorage.fileSystemBasePath ?? './data/uploads',
    jobsTestTimeoutSeconds: String(settings.jobs.testTimeoutSeconds),
    messagingJobsEnabled: settings.jobs.messagingJobsEnabled,
    minioAccessKey: settings.objectStorage.minioAccessKey ?? '',
    minioBucketName: settings.objectStorage.minioBucketName ?? '',
    minioEndpoint: settings.objectStorage.minioEndpoint ?? '',
    minioSecretKey: '',
    minioSecretKeyClear: false,
    minioWithSsl: settings.objectStorage.minioWithSsl,
    objectStorageProvider: settings.objectStorage.provider,
    operationLogEnabled: settings.audit.operationLogEnabled,
    ossAliyunAccessKeyId: settings.objectStorage.aliyunAccessKeyId ?? '',
    ossAliyunAccessKeySecret: '',
    ossAliyunAccessKeySecretClear: false,
    ossAliyunBucketName: settings.objectStorage.aliyunBucketName ?? '',
    ossAliyunEndpoint: settings.objectStorage.aliyunEndpoint ?? '',
    redisConfiguration: '',
    redisConfigurationClear: false,
    smsEnabled: settings.sms.enabled,
    smsProvider: settings.sms.provider,
    smtpHost: settings.email.smtpHost ?? '',
    smtpPort: settings.email.smtpPort ? String(settings.email.smtpPort) : '',
    tencentRegion: settings.sms.tencentRegion ?? 'ap-guangzhou',
    tencentSdkAppId: settings.sms.tencentSdkAppId ?? '',
    tencentSecretId: settings.sms.tencentSecretId ?? '',
    tencentSecretKey: '',
    tencentSecretKeyClear: false,
    tencentSignName: settings.sms.tencentSignName ?? '',
    tencentTemplateId: settings.sms.tencentTemplateId ?? ''
  };
}

export function buildUpdateRequest(state: InfrastructureFormState): InfrastructureSettingsUpdateRequest {
  return {
    audit: {
      captureQueryString: state.captureQueryString,
      operationLogEnabled: state.operationLogEnabled,
      queueCapacity: parseOptionalInt(state.auditQueueCapacity)
    },
    cache: {
      defaultExpirationMinutes: parseOptionalInt(state.cacheDefaultExpirationMinutes),
      provider: state.cacheProvider,
      redisConfiguration: toSecretUpdate(state.redisConfiguration, state.redisConfigurationClear)
    },
    email: {
      defaultFromAddress: trimToEmpty(state.defaultFromAddress),
      defaultFromDisplayName: trimToEmpty(state.defaultFromDisplayName),
      enabled: state.emailEnabled,
      enableSsl: state.emailEnableSsl,
      password: toSecretUpdate(state.emailPassword, state.emailPasswordClear),
      smtpHost: trimToEmpty(state.smtpHost),
      smtpPort: parseOptionalInt(state.smtpPort),
      userName: trimToEmpty(state.emailUserName)
    },
    jobs: {
      abpBackgroundJobsEnabled: state.abpBackgroundJobsEnabled,
      messagingJobsEnabled: state.messagingJobsEnabled,
      testTimeoutSeconds: parseOptionalInt(state.jobsTestTimeoutSeconds)
    },
    objectStorage: {
      aliyunAccessKeyId: trimToEmpty(state.ossAliyunAccessKeyId),
      aliyunAccessKeySecret: toSecretUpdate(state.ossAliyunAccessKeySecret, state.ossAliyunAccessKeySecretClear),
      aliyunBucketName: trimToEmpty(state.ossAliyunBucketName),
      aliyunEndpoint: trimToEmpty(state.ossAliyunEndpoint),
      fileSystemAppendContainerName: state.fileSystemAppendContainerName,
      fileSystemBasePath: trimToEmpty(state.fileSystemBasePath),
      minioAccessKey: trimToEmpty(state.minioAccessKey),
      minioBucketName: trimToEmpty(state.minioBucketName),
      minioEndpoint: trimToEmpty(state.minioEndpoint),
      minioSecretKey: toSecretUpdate(state.minioSecretKey, state.minioSecretKeyClear),
      minioWithSsl: state.minioWithSsl,
      provider: state.objectStorageProvider
    },
    sms: {
      aliyunAccessKeyId: trimToEmpty(state.aliyunAccessKeyId),
      aliyunAccessKeySecret: toSecretUpdate(state.aliyunAccessKeySecret, state.aliyunAccessKeySecretClear),
      aliyunSignName: trimToEmpty(state.aliyunSignName),
      aliyunTemplateCode: trimToEmpty(state.aliyunTemplateCode),
      aliyunTemplateParamName: trimToEmpty(state.aliyunTemplateParamName),
      enabled: state.smsEnabled,
      provider: state.smsProvider,
      tencentRegion: trimToEmpty(state.tencentRegion),
      tencentSdkAppId: trimToEmpty(state.tencentSdkAppId),
      tencentSecretId: trimToEmpty(state.tencentSecretId),
      tencentSecretKey: toSecretUpdate(state.tencentSecretKey, state.tencentSecretKeyClear),
      tencentSignName: trimToEmpty(state.tencentSignName),
      tencentTemplateId: trimToEmpty(state.tencentTemplateId)
    }
  };
}

export function validateFormState(state: InfrastructureFormState, translate: (key: string) => string): string | null {
  const smtpPort = parseOptionalInt(state.smtpPort);
  if (smtpPort !== null && (smtpPort < 1 || smtpPort > 65535)) {
    return translate('page.abpInfrastructureSettings.validation.smtpPortRange');
  }

  const cacheExpiration = parseOptionalInt(state.cacheDefaultExpirationMinutes);
  if (cacheExpiration !== null && (cacheExpiration < 1 || cacheExpiration > 1440)) {
    return translate('page.abpInfrastructureSettings.validation.cacheExpirationRange');
  }

  const jobTimeout = parseOptionalInt(state.jobsTestTimeoutSeconds);
  if (jobTimeout !== null && (jobTimeout < 1 || jobTimeout > 120)) {
    return translate('page.abpInfrastructureSettings.validation.jobTimeoutRange');
  }

  const auditCapacity = parseOptionalInt(state.auditQueueCapacity);
  if (auditCapacity !== null && (auditCapacity < 128 || auditCapacity > 100000)) {
    return translate('page.abpInfrastructureSettings.validation.auditCapacityRange');
  }

  return null;
}

export function normalizeLogSearch(value: MessageLogSearchState): MessageLogSearchState {
  return {
    channel: value.channel.trim(),
    provider: value.provider.trim(),
    result: value.result.trim(),
    traceId: value.traceId.trim()
  };
}

export function trimToUndefined(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed ? trimmed : undefined;
}

export function formatDateTime(value: string) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function toSecretUpdate(value: string, clear: boolean): SecretSettingUpdate | undefined {
  const trimmed = value.trim();
  if (clear) {
    return { clear: true };
  }

  return trimmed ? { value: trimmed } : undefined;
}

function parseOptionalInt(value: string): number | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  const parsed = Number.parseInt(trimmed, 10);
  return Number.isFinite(parsed) ? parsed : null;
}

function trimToEmpty(value: string): string {
  return value.trim();
}
