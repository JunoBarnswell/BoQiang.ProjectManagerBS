namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed record ApplicationDataCenterSqlScriptFunctionCallExpression(
    string NamespaceName,
    string FunctionName,
    IReadOnlyList<ApplicationDataCenterSqlScriptExpression> Arguments) : ApplicationDataCenterSqlScriptExpression
{
    public string QualifiedName => $"{NamespaceName}.{FunctionName}";
}
