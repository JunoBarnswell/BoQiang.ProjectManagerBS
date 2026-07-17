namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class UpdateDataSourceDefinitionCommand : ApplicationDataCenterObjectUpsertRequest
{
    public string? DiagnosticFingerprint { get; set; }
}
