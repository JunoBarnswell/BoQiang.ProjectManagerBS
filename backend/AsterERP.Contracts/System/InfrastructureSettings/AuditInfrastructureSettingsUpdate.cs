namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class AuditInfrastructureSettingsUpdate
{
    public bool? OperationLogEnabled { get; set; }

    public bool? CaptureQueryString { get; set; }

    public int? QueueCapacity { get; set; }
}
