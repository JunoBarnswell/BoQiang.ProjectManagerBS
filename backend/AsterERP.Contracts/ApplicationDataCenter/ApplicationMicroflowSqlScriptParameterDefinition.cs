using AsterERP.Contracts.Runtime;

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowSqlScriptParameterDefinition
{
    public string DataType { get; set; } = "string";

    public RuntimeValueExpressionDto? Expression { get; set; }

    public string Name { get; set; } = string.Empty;
}
