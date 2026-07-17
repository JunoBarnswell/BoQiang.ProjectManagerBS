using System.Collections.Generic;

namespace AsterERP.Workflow.Core.Connector;

public interface IConnectorDiscovery
{
    void DiscoverConnectors();
    IReadOnlyList<IConnector> GetDiscoveredConnectors();
    IConnector? GetConnector(string name);
    void RegisterConnector(IConnector connector);
    void UnregisterConnector(string name);
}
