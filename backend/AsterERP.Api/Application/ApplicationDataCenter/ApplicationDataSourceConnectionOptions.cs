namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed record ApplicationDataSourceConnectionOptions(
    string Type,
    string? ConnectionString,
    string? FilePath,
    string? BaseUrl,
    string? Host,
    int? Port,
    string? Database,
    string? UserName,
    string? Password,
    string? Token,
    string? AuthType,
    string? SslMode = null,
    bool? Encrypt = null,
    bool? TrustServerCertificate = null,
    int? TimeoutSeconds = null,
    int? PoolSize = null,
    string? Charset = null);
