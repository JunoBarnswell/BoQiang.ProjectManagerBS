namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class InfrastructureSettingsUpdateRequest
{
    public EmailInfrastructureSettingsUpdate? Email { get; set; }

    public SmsInfrastructureSettingsUpdate? Sms { get; set; }

    public ObjectStorageInfrastructureSettingsUpdate? ObjectStorage { get; set; }

    public CacheInfrastructureSettingsUpdate? Cache { get; set; }

    public JobsInfrastructureSettingsUpdate? Jobs { get; set; }

    public AuditInfrastructureSettingsUpdate? Audit { get; set; }
}
