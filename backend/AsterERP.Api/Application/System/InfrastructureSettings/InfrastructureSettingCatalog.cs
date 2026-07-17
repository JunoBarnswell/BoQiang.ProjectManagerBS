using AsterERP.Api.Infrastructure.Abp.Settings;
using Volo.Abp.Emailing;

namespace AsterERP.Api.Application.System.InfrastructureSettings;

public static class InfrastructureSettingCatalog
{
    public const string EmailCategory = "infrastructure.email";
    public const string SmsCategory = "infrastructure.sms";
    public const string ObjectStorageCategory = "infrastructure.object-storage";
    public const string CacheCategory = "infrastructure.cache";
    public const string JobsCategory = "infrastructure.jobs";
    public const string AuditCategory = "infrastructure.audit";

    private static readonly IReadOnlyDictionary<string, InfrastructureSettingDescriptor> Descriptors =
        new[]
        {
            new InfrastructureSettingDescriptor(AsterErpSettingNames.EmailEnabled, "邮件发送启用", EmailCategory, "false"),
            new InfrastructureSettingDescriptor(EmailSettingNames.Smtp.Host, "SMTP 主机", EmailCategory),
            new InfrastructureSettingDescriptor(EmailSettingNames.Smtp.Port, "SMTP 端口", EmailCategory, "25"),
            new InfrastructureSettingDescriptor(EmailSettingNames.Smtp.UserName, "SMTP 用户名", EmailCategory),
            new InfrastructureSettingDescriptor(EmailSettingNames.Smtp.Password, "SMTP 密码", EmailCategory, IsSecret: true),
            new InfrastructureSettingDescriptor(EmailSettingNames.Smtp.EnableSsl, "SMTP SSL", EmailCategory, "false"),
            new InfrastructureSettingDescriptor(EmailSettingNames.Smtp.UseDefaultCredentials, "SMTP 默认凭据", EmailCategory, "false"),
            new InfrastructureSettingDescriptor(EmailSettingNames.DefaultFromAddress, "默认发件地址", EmailCategory),
            new InfrastructureSettingDescriptor(EmailSettingNames.DefaultFromDisplayName, "默认发件名称", EmailCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsEnabled, "短信发送启用", SmsCategory, "false"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsProvider, "短信 Provider", SmsCategory, "Null"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsAliyunAccessKeyId, "阿里云短信 AccessKeyId", SmsCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsAliyunAccessKeySecret, "阿里云短信 AccessKeySecret", SmsCategory, IsSecret: true),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsAliyunSignName, "阿里云短信签名", SmsCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsAliyunTemplateCode, "阿里云短信模板码", SmsCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsAliyunTemplateParamName, "阿里云短信模板参数名", SmsCategory, "content"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsTencentSecretId, "腾讯云短信 SecretId", SmsCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsTencentSecretKey, "腾讯云短信 SecretKey", SmsCategory, IsSecret: true),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsTencentSdkAppId, "腾讯云短信 SdkAppId", SmsCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsTencentSignName, "腾讯云短信签名", SmsCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsTencentTemplateId, "腾讯云短信模板 ID", SmsCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.SmsTencentRegion, "腾讯云短信 Region", SmsCategory, "ap-guangzhou"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageProvider, "对象存储 Provider", ObjectStorageCategory, "FileSystem"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageFileSystemBasePath, "本地对象存储根路径", ObjectStorageCategory, "./data/uploads"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageFileSystemAppendContainerName, "本地路径追加容器名", ObjectStorageCategory, "false"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageAliyunEndpoint, "阿里云 OSS Endpoint", ObjectStorageCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageAliyunBucketName, "阿里云 OSS Bucket", ObjectStorageCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageAliyunAccessKeyId, "阿里云 OSS AccessKeyId", ObjectStorageCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageAliyunAccessKeySecret, "阿里云 OSS AccessKeySecret", ObjectStorageCategory, IsSecret: true),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageMinioEndpoint, "MinIO Endpoint", ObjectStorageCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageMinioBucketName, "MinIO Bucket", ObjectStorageCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageMinioAccessKey, "MinIO AccessKey", ObjectStorageCategory),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageMinioSecretKey, "MinIO SecretKey", ObjectStorageCategory, IsSecret: true),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.ObjectStorageMinioWithSsl, "MinIO SSL", ObjectStorageCategory, "false"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.CacheProvider, "缓存 Provider", CacheCategory, "Memory"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.CacheRedisConfiguration, "Redis 连接串", CacheCategory, IsSecret: true),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.CacheDefaultExpirationMinutes, "默认缓存过期分钟", CacheCategory, "30"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.JobsAbpBackgroundJobsEnabled, "ABP 后台任务启用", JobsCategory, "true"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.JobsMessagingJobsEnabled, "消息异步任务启用", JobsCategory, "false"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.JobsTestTimeoutSeconds, "基础设施测试超时秒数", JobsCategory, "10"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.AuditOperationLogEnabled, "操作日志启用", AuditCategory, "true"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.AuditCaptureQueryString, "审计采集 QueryString", AuditCategory, "true"),
            new InfrastructureSettingDescriptor(AsterErpSettingNames.AuditQueueCapacity, "操作日志队列容量", AuditCategory, "2048")
        }.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

    public static InfrastructureSettingDescriptor Get(string key) => Descriptors[key];
}
