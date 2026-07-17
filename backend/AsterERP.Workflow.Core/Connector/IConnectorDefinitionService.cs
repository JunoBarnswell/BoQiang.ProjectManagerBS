using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Connector;

public interface IConnectorDefinitionService
{
    IReadOnlyList<ConnectorDefinition> GetConnectorDefinitions();
    ConnectorDefinition? GetConnectorDefinition(string connectorName);
    void RegisterConnectorDefinition(ConnectorDefinition definition);
}

public class ConnectorDefinition
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Implementation { get; set; }
    public string? ImplementationType { get; set; }
    public List<ConnectorActionDefinition> Actions { get; set; } = new();
}

public class ConnectorActionDefinition
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public List<ConnectorParameterDefinition> InputParameters { get; set; } = new();
    public List<ConnectorParameterDefinition> OutputParameters { get; set; } = new();
}

public class ConnectorParameterDefinition
{
    public string Name { get; set; } = null!;
    public string? Type { get; set; }
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public object? DefaultValue { get; set; }
}
