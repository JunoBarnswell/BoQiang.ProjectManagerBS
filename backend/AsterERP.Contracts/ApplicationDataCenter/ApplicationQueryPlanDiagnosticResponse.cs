namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanDiagnosticResponse(
    bool IsValid,
    string? Provider,
    string? Sql,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string? AuditId);
