namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record ObjectStorageInfrastructureSettings(
    string Provider,
    string? FileSystemBasePath,
    bool FileSystemAppendContainerName,
    string? AliyunEndpoint,
    string? AliyunBucketName,
    string? AliyunAccessKeyId,
    SecretSettingState AliyunAccessKeySecret,
    string? MinioEndpoint,
    string? MinioBucketName,
    string? MinioAccessKey,
    SecretSettingState MinioSecretKey,
    bool MinioWithSsl);
