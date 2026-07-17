namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record InfrastructureSettingsResponse(
    EmailInfrastructureSettings Email,
    SmsInfrastructureSettings Sms,
    ObjectStorageInfrastructureSettings ObjectStorage,
    CacheInfrastructureSettings Cache,
    JobsInfrastructureSettings Jobs,
    AuditInfrastructureSettings Audit);
