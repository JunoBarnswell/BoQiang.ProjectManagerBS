namespace AsterERP.Api.Infrastructure.Abp.Settings;

public static class AsterErpSettingNames
{
    public const string EmailEnabled = "AsterERP.Messaging.Email.Enabled";
    public const string SmsEnabled = "AsterERP.Messaging.Sms.Enabled";
    public const string SmsProvider = "AsterERP.Messaging.Sms.Provider";
    public const string SmsAliyunAccessKeyId = "AsterERP.Messaging.Sms.Aliyun.AccessKeyId";
    public const string SmsAliyunAccessKeySecret = "AsterERP.Messaging.Sms.Aliyun.AccessKeySecret";
    public const string SmsAliyunSignName = "AsterERP.Messaging.Sms.Aliyun.SignName";
    public const string SmsAliyunTemplateCode = "AsterERP.Messaging.Sms.Aliyun.TemplateCode";
    public const string SmsAliyunTemplateParamName = "AsterERP.Messaging.Sms.Aliyun.TemplateParamName";
    public const string SmsTencentSecretId = "AsterERP.Messaging.Sms.Tencent.SecretId";
    public const string SmsTencentSecretKey = "AsterERP.Messaging.Sms.Tencent.SecretKey";
    public const string SmsTencentSdkAppId = "AsterERP.Messaging.Sms.Tencent.SmsSdkAppId";
    public const string SmsTencentSignName = "AsterERP.Messaging.Sms.Tencent.SignName";
    public const string SmsTencentTemplateId = "AsterERP.Messaging.Sms.Tencent.TemplateId";
    public const string SmsTencentRegion = "AsterERP.Messaging.Sms.Tencent.Region";

    public const string ObjectStorageProvider = "AsterERP.ObjectStorage.Provider";
    public const string ObjectStorageFileSystemBasePath = "AsterERP.ObjectStorage.FileSystem.BasePath";
    public const string ObjectStorageFileSystemAppendContainerName = "AsterERP.ObjectStorage.FileSystem.AppendContainerNameToBasePath";
    public const string ObjectStorageAliyunEndpoint = "AsterERP.ObjectStorage.Aliyun.Endpoint";
    public const string ObjectStorageAliyunBucketName = "AsterERP.ObjectStorage.Aliyun.BucketName";
    public const string ObjectStorageAliyunAccessKeyId = "AsterERP.ObjectStorage.Aliyun.AccessKeyId";
    public const string ObjectStorageAliyunAccessKeySecret = "AsterERP.ObjectStorage.Aliyun.AccessKeySecret";
    public const string ObjectStorageMinioEndpoint = "AsterERP.ObjectStorage.Minio.Endpoint";
    public const string ObjectStorageMinioBucketName = "AsterERP.ObjectStorage.Minio.BucketName";
    public const string ObjectStorageMinioAccessKey = "AsterERP.ObjectStorage.Minio.AccessKey";
    public const string ObjectStorageMinioSecretKey = "AsterERP.ObjectStorage.Minio.SecretKey";
    public const string ObjectStorageMinioWithSsl = "AsterERP.ObjectStorage.Minio.WithSSL";

    public const string CacheProvider = "AsterERP.Cache.Provider";
    public const string CacheRedisConfiguration = "AsterERP.Cache.Redis.Configuration";
    public const string CacheDefaultExpirationMinutes = "AsterERP.Cache.DefaultExpirationMinutes";

    public const string JobsAbpBackgroundJobsEnabled = "AsterERP.Jobs.AbpBackgroundJobs.Enabled";
    public const string JobsMessagingJobsEnabled = "AsterERP.Jobs.MessagingJobs.Enabled";
    public const string JobsTestTimeoutSeconds = "AsterERP.Jobs.TestTimeoutSeconds";

    public const string AuditOperationLogEnabled = "AsterERP.Audit.OperationLog.Enabled";
    public const string AuditCaptureQueryString = "AsterERP.Audit.CaptureQueryString";
    public const string AuditQueueCapacity = "AsterERP.Audit.QueueCapacity";

    public const string ProjectManagementTaskHierarchyMaxDepth = "AsterERP.ProjectManagement.TaskHierarchy.MaxDepth";
}
