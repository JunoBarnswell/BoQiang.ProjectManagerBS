namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class SmsInfrastructureSettingsUpdate
{
    public bool? Enabled { get; set; }

    public string? Provider { get; set; }

    public string? AliyunAccessKeyId { get; set; }

    public SecretSettingUpdate? AliyunAccessKeySecret { get; set; }

    public string? AliyunSignName { get; set; }

    public string? AliyunTemplateCode { get; set; }

    public string? AliyunTemplateParamName { get; set; }

    public string? TencentSecretId { get; set; }

    public SecretSettingUpdate? TencentSecretKey { get; set; }

    public string? TencentSdkAppId { get; set; }

    public string? TencentSignName { get; set; }

    public string? TencentTemplateId { get; set; }

    public string? TencentRegion { get; set; }
}
