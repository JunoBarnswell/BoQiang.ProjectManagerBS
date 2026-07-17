namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed record ApplicationDevelopmentEnvironmentCheckResponse(
    bool Passed,
    IReadOnlyList<ApplicationDevelopmentEnvironmentDiagnostic> Diagnostics);

public sealed record ApplicationDevelopmentEnvironmentDiagnostic(
    string Code,
    string Category,
    string Severity,
    string Message,
    string? Path = null,
    string? FlowCode = null,
    string? DataSourceId = null,
    string? TableName = null,
    string? FixHint = null);
