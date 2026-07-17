using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Connector;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class ConnectorActivityBehavior : FlowNodeActivityBehavior
{
    private readonly IConnector _connector;

    public ConnectorActivityBehavior(IConnector connector)
    {
        _connector = connector;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var request = new ConnectorRequest
        {
            Action = _connector.Name,
            Parameters = execution.Variables
        };
        await _connector.ExecuteAsync(request, cancellationToken);
        await LeaveAsync(execution, cancellationToken);
    }
}
