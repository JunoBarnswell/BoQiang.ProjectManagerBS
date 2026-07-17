namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed record ApplicationDataCenterSqlScriptVariableExpression(
    string Name,
    IReadOnlyList<string> Path) : ApplicationDataCenterSqlScriptExpression;
