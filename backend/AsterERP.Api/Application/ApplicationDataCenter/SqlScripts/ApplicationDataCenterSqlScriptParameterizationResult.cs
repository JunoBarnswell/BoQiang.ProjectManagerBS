namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed record ApplicationDataCenterSqlScriptParameterizationResult(
    string Script,
    IReadOnlyDictionary<string, object?> GeneratedVariables);
