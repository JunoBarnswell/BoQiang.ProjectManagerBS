namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed record ApplicationDataCenterSqlScriptIfElseBlock(
    int Index,
    int Length,
    string Condition,
    string ThenBlock,
    string ElseBlock);
