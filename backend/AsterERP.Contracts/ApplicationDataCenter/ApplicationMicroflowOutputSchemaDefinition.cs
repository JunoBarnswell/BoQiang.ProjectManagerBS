using AsterERP.Contracts.Runtime;

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowOutputSchemaDefinition
{
    public string VariableCode { get; set; } = string.Empty;

    public string VariableName { get; set; } = string.Empty;

    public string ValueType { get; set; } = "object";

    public string SourceMode { get; set; } = "fields";

    public RuntimeValueExpressionDto? ArrayExpression { get; set; }

    public ApplicationMicroflowSqlScriptDefinition? SqlScript { get; set; }

    public List<ApplicationMicroflowFieldDefinition> Fields { get; set; } = [];
}
