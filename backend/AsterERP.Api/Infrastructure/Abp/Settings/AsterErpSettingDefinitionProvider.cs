using Volo.Abp.Settings;
using Volo.Abp.Emailing;

namespace AsterERP.Api.Infrastructure.Abp.Settings;

public sealed class AsterErpSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(
            Visible(AsterErpSettingNames.EmailEnabled, "false"),
            Visible(EmailSettingNames.Smtp.Host),
            Visible(EmailSettingNames.Smtp.Port, "25"),
            Visible(EmailSettingNames.Smtp.UserName),
            Secret(EmailSettingNames.Smtp.Password),
            Visible(EmailSettingNames.Smtp.EnableSsl, "false"),
            Visible(EmailSettingNames.Smtp.UseDefaultCredentials, "false"),
            Visible(EmailSettingNames.DefaultFromAddress),
            Visible(EmailSettingNames.DefaultFromDisplayName),
            Visible(AsterErpSettingNames.SmsEnabled, "false"),
            Visible(AsterErpSettingNames.SmsProvider, "Null"),
            Secret(AsterErpSettingNames.SmsAliyunAccessKeyId),
            Secret(AsterErpSettingNames.SmsAliyunAccessKeySecret),
            Visible(AsterErpSettingNames.SmsAliyunSignName),
            Visible(AsterErpSettingNames.SmsAliyunTemplateCode),
            Visible(AsterErpSettingNames.SmsAliyunTemplateParamName, "content"),
            Secret(AsterErpSettingNames.SmsTencentSecretId),
            Secret(AsterErpSettingNames.SmsTencentSecretKey),
            Visible(AsterErpSettingNames.SmsTencentSdkAppId),
            Visible(AsterErpSettingNames.SmsTencentSignName),
            Visible(AsterErpSettingNames.SmsTencentTemplateId),
            Visible(AsterErpSettingNames.SmsTencentRegion, "ap-guangzhou"),
            Visible(AsterErpSettingNames.ObjectStorageProvider, "FileSystem"),
            Visible(AsterErpSettingNames.ObjectStorageFileSystemBasePath, "./data/uploads"),
            Visible(AsterErpSettingNames.ObjectStorageFileSystemAppendContainerName, "false"),
            Visible(AsterErpSettingNames.ObjectStorageAliyunEndpoint),
            Visible(AsterErpSettingNames.ObjectStorageAliyunBucketName),
            Secret(AsterErpSettingNames.ObjectStorageAliyunAccessKeyId),
            Secret(AsterErpSettingNames.ObjectStorageAliyunAccessKeySecret),
            Visible(AsterErpSettingNames.ObjectStorageMinioEndpoint),
            Visible(AsterErpSettingNames.ObjectStorageMinioBucketName),
            Secret(AsterErpSettingNames.ObjectStorageMinioAccessKey),
            Secret(AsterErpSettingNames.ObjectStorageMinioSecretKey),
            Visible(AsterErpSettingNames.ObjectStorageMinioWithSsl, "false"),
            Visible(AsterErpSettingNames.CacheProvider, "Memory"),
            Secret(AsterErpSettingNames.CacheRedisConfiguration),
            Visible(AsterErpSettingNames.CacheDefaultExpirationMinutes, "30"),
            Visible(AsterErpSettingNames.JobsAbpBackgroundJobsEnabled, "true"),
            Visible(AsterErpSettingNames.JobsMessagingJobsEnabled, "false"),
            Visible(AsterErpSettingNames.JobsTestTimeoutSeconds, "10"),
            Visible(AsterErpSettingNames.AuditOperationLogEnabled, "true"),
            Visible(AsterErpSettingNames.AuditCaptureQueryString, "true"),
            Visible(AsterErpSettingNames.AuditQueueCapacity, "2048"),
            Visible(AsterErpSettingNames.ProjectManagementTaskHierarchyMaxDepth, "5"));
    }

    private static SettingDefinition Visible(string name, string? defaultValue = null) =>
        new(name, defaultValue, isVisibleToClients: false);

    private static SettingDefinition Secret(string name) =>
        new(name, isVisibleToClients: false, isEncrypted: true);
}
