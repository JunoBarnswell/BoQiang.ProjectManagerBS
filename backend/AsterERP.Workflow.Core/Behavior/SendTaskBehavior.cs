using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class SendTaskActivityBehavior : FlowNodeActivityBehavior
{
    private readonly BpmnModelNs.SendTask? _sendTask;

    public SendTaskActivityBehavior()
    {
    }

    public SendTaskActivityBehavior(BpmnModelNs.SendTask sendTask)
    {
        _sendTask = sendTask;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariable("_sendTaskExecuted", true);
        execution.SetVariable("_sendTaskTimestamp", AbpTimeIdProvider.UtcNow);

        if (!string.IsNullOrWhiteSpace(_sendTask?.Id))
        {
            execution.SetVariable("_sendTaskId", _sendTask.Id);
        }

        if (!string.IsNullOrWhiteSpace(_sendTask?.Name))
        {
            execution.SetVariable("_sendTaskName", _sendTask.Name);
        }

        if (!string.IsNullOrWhiteSpace(_sendTask?.Implementation))
        {
            execution.SetVariable("_sendTaskImplementation", _sendTask.Implementation);
        }

        if (!string.IsNullOrWhiteSpace(_sendTask?.Type))
        {
            execution.SetVariable("_sendTaskType", _sendTask.Type);
        }

        if (!string.IsNullOrWhiteSpace(_sendTask?.OperationRef ?? _sendTask?.Operation))
        {
            execution.SetVariable("_sendTaskOperation", _sendTask?.OperationRef ?? _sendTask?.Operation);
        }

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }
}

