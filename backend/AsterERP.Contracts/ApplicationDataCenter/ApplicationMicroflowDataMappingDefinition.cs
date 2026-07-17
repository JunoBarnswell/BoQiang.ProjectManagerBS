using AsterERP.Contracts.Runtime;

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowDataMappingDefinition
{
    public string Target { get; set; } = string.Empty;

    public RuntimeValueExpressionDto? Expression { get; set; }
}
