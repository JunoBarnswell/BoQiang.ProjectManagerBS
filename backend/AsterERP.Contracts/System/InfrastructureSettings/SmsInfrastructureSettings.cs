namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record SmsInfrastructureSettings(
    bool Enabled,
    string Provider,
    string? AliyunAccessKeyId,
    SecretSettingState AliyunAccessKeySecret,
    string? AliyunSignName,
    string? AliyunTemplateCode,
    string? AliyunTemplateParamName,
    string? TencentSecretId,
    SecretSettingState TencentSecretKey,
    string? TencentSdkAppId,
    string? TencentSignName,
    string? TencentTemplateId,
    string? TencentRegion);
