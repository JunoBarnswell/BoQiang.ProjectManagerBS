namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed record ApplicationDataCenterSqlScriptExpressionToken(
    ApplicationDataCenterSqlScriptExpressionTokenType Type,
    string Text,
    int Start,
    int End);
