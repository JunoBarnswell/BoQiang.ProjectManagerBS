using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Connector;

public class ConnectorDiscoveryImplementation : IConnectorDiscovery
{
    private readonly Dictionary<string, IConnector> _connectors = new();

    public void DiscoverConnectors()
    {
    }

    public IReadOnlyList<IConnector> GetDiscoveredConnectors()
    {
        return _connectors.Values.ToList().AsReadOnly();
    }

    public IConnector? GetConnector(string name)
    {
        return _connectors.TryGetValue(name, out var connector) ? connector : null;
    }

    public void RegisterConnector(IConnector connector)
    {
        _connectors[connector.Name] = connector;
    }

    public void UnregisterConnector(string name)
    {
        _connectors.Remove(name);
    }
}
