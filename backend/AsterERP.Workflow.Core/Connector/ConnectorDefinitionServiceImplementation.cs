using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Connector;

public class ConnectorDefinitionServiceImplementation : IConnectorDefinitionService
{
    private readonly Dictionary<string, ConnectorDefinition> _definitions = new();

    public IReadOnlyList<ConnectorDefinition> GetConnectorDefinitions()
    {
        return _definitions.Values.ToList().AsReadOnly();
    }

    public ConnectorDefinition? GetConnectorDefinition(string connectorName)
    {
        return _definitions.TryGetValue(connectorName, out var definition) ? definition : null;
    }

    public void RegisterConnectorDefinition(ConnectorDefinition definition)
    {
        _definitions[definition.Name] = definition;
    }
}
