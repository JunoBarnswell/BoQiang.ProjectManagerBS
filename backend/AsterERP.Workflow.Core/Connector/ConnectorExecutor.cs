using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Connector;

public class ConnectorExecutor
{
    private readonly IConnectorDiscovery _discovery;

    public ConnectorExecutor(IConnectorDiscovery discovery)
    {
        _discovery = discovery;
    }

    public async global::System.Threading.Tasks.Task<ConnectorResponse> ExecuteAsync(string connectorName, ConnectorRequest request, CancellationToken cancellationToken = default)
    {
        var connector = _discovery.GetConnector(connectorName)
            ?? throw new WorkflowEngineObjectNotFoundException($"Connector '{connectorName}' not found");
        return await connector.ExecuteAsync(request, cancellationToken);
    }
}
