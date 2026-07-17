namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record AuditInfrastructureSettings(
    bool OperationLogEnabled,
    bool CaptureQueryString,
    int QueueCapacity);
