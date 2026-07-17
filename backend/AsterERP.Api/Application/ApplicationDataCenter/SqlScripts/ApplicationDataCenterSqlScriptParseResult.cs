namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed record ApplicationDataCenterSqlScriptParseResult(
    ApplicationDataCenterSqlScriptExpression Expression,
    int ConsumedLength);
