namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowSqlScriptRunRequest(
    string NodeId,
    ApplicationMicroflowDefinition Definition,
    ApplicationMicroflowSqlScriptDefinition SqlScript,
    string? ValueType = null,
    ApplicationMicroflowExecuteRequest? ExecuteRequest = null,
    int? PageIndex = null,
    int? PageSize = null);
