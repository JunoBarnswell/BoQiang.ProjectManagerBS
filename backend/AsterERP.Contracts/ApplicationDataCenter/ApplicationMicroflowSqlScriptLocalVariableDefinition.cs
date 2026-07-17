using AsterERP.Contracts.Runtime;

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowSqlScriptLocalVariableDefinition
{
    public string DataType { get; set; } = "string";

    public RuntimeValueExpressionDto? Initializer { get; set; }

    public string Name { get; set; } = string.Empty;
}
