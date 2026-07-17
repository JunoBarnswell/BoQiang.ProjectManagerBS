namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class ObjectStorageInfrastructureSettingsUpdate
{
    public string? Provider { get; set; }

    public string? FileSystemBasePath { get; set; }

    public bool? FileSystemAppendContainerName { get; set; }

    public string? AliyunEndpoint { get; set; }

    public string? AliyunBucketName { get; set; }

    public string? AliyunAccessKeyId { get; set; }

    public SecretSettingUpdate? AliyunAccessKeySecret { get; set; }

    public string? MinioEndpoint { get; set; }

    public string? MinioBucketName { get; set; }

    public string? MinioAccessKey { get; set; }

    public SecretSettingUpdate? MinioSecretKey { get; set; }

    public bool? MinioWithSsl { get; set; }
}
