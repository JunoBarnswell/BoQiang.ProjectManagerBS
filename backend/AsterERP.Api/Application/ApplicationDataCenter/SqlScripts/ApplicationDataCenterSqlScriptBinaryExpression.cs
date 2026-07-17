namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed record ApplicationDataCenterSqlScriptBinaryExpression(
    ApplicationDataCenterSqlScriptExpression Left,
    string Operator,
    ApplicationDataCenterSqlScriptExpression Right) : ApplicationDataCenterSqlScriptExpression;
