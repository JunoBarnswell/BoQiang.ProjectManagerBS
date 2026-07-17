import type { DataTableQueryState } from '../../../shared/table/tableTypes';

export type SectionKey = 'email' | 'sms' | 'objectStorage' | 'cache' | 'jobs' | 'audit';

export interface InfrastructureSectionDescriptor {
  icon: string;
  key: SectionKey;
  labelKey: string;
}

export interface InfrastructureProviderOption {
  labelKey: string;
  value: string;
}

export interface InfrastructureFormState {
  emailEnabled: boolean;
  smtpHost: string;
  smtpPort: string;
  emailUserName: string;
  emailPassword: string;
  emailPasswordClear: boolean;
  defaultFromAddress: string;
  defaultFromDisplayName: string;
  emailEnableSsl: boolean;
  smsEnabled: boolean;
  smsProvider: string;
  aliyunAccessKeyId: string;
  aliyunAccessKeySecret: string;
  aliyunAccessKeySecretClear: boolean;
  aliyunSignName: string;
  aliyunTemplateCode: string;
  aliyunTemplateParamName: string;
  tencentSecretId: string;
  tencentSecretKey: string;
  tencentSecretKeyClear: boolean;
  tencentSdkAppId: string;
  tencentSignName: string;
  tencentTemplateId: string;
  tencentRegion: string;
  objectStorageProvider: string;
  fileSystemBasePath: string;
  fileSystemAppendContainerName: boolean;
  ossAliyunEndpoint: string;
  ossAliyunBucketName: string;
  ossAliyunAccessKeyId: string;
  ossAliyunAccessKeySecret: string;
  ossAliyunAccessKeySecretClear: boolean;
  minioEndpoint: string;
  minioBucketName: string;
  minioAccessKey: string;
  minioSecretKey: string;
  minioSecretKeyClear: boolean;
  minioWithSsl: boolean;
  cacheProvider: string;
  redisConfiguration: string;
  redisConfigurationClear: boolean;
  cacheDefaultExpirationMinutes: string;
  abpBackgroundJobsEnabled: boolean;
  messagingJobsEnabled: boolean;
  jobsTestTimeoutSeconds: string;
  operationLogEnabled: boolean;
  captureQueryString: boolean;
  auditQueueCapacity: string;
}

export interface TestFormState {
  emailTo: string;
  smsPhoneNumber: string;
  objectStorageProvider: string;
}

export interface MessageLogSearchState {
  channel: string;
  provider: string;
  result: string;
  traceId: string;
}

export const defaultTableQuery: DataTableQueryState = { conditions: [], matchMode: 'and' };

export const defaultTestForm: TestFormState = {
  emailTo: '',
  objectStorageProvider: '',
  smsPhoneNumber: ''
};

export const defaultMessageLogSearch: MessageLogSearchState = {
  channel: '',
  provider: '',
  result: '',
  traceId: ''
};

export const smsProviders: InfrastructureProviderOption[] = [
  { labelKey: 'page.abpInfrastructureSettings.provider.null', value: 'Null' },
  { labelKey: 'page.abpInfrastructureSettings.provider.aliyun', value: 'Aliyun' },
  { labelKey: 'page.abpInfrastructureSettings.provider.tencent', value: 'Tencent' }
];

export const objectStorageProviders: InfrastructureProviderOption[] = [
  { labelKey: 'page.abpInfrastructureSettings.provider.fileSystem', value: 'FileSystem' },
  { labelKey: 'page.abpInfrastructureSettings.provider.minio', value: 'Minio' },
  { labelKey: 'page.abpInfrastructureSettings.provider.aliyun', value: 'Aliyun' }
];

export const cacheProviders: InfrastructureProviderOption[] = [
  { labelKey: 'page.abpInfrastructureSettings.provider.memory', value: 'Memory' },
  { labelKey: 'page.abpInfrastructureSettings.provider.redis', value: 'Redis' }
];

export const sections: InfrastructureSectionDescriptor[] = [
  { key: 'email', labelKey: 'page.abpInfrastructureSettings.section.email', icon: 'envelope-simple' },
  { key: 'sms', labelKey: 'page.abpInfrastructureSettings.section.sms', icon: 'chat-circle-text' },
  { key: 'objectStorage', labelKey: 'page.abpInfrastructureSettings.section.objectStorage', icon: 'hard-drives' },
  { key: 'cache', labelKey: 'page.abpInfrastructureSettings.section.cache', icon: 'database' },
  { key: 'jobs', labelKey: 'page.abpInfrastructureSettings.section.jobs', icon: 'clock-counter-clockwise' },
  { key: 'audit', labelKey: 'page.abpInfrastructureSettings.section.audit', icon: 'shield-check' }
];
