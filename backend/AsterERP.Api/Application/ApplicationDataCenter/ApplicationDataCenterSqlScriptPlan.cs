namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterSqlScriptPlan
{
    public IReadOnlySet<string> DeclaredVariableNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string OriginalScript { get; init; } = string.Empty;

    public string ReturnKind { get; init; } = "select";

    public string ReturnJson { get; init; } = string.Empty;

    public string ReturnSql { get; init; } = string.Empty;

    public string ReturnVariableName { get; init; } = string.Empty;

    public IReadOnlyList<string> SqlStatements { get; init; } = [];
}
