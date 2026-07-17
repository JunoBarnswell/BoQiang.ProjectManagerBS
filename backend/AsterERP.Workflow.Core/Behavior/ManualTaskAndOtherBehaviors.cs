using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class ManualTaskActivityBehavior : TaskActivityBehavior
{
    public BpmnModelNs.ManualTask? ManualTask { get; set; }

    public ManualTaskActivityBehavior() { }

    public ManualTaskActivityBehavior(BpmnModelNs.ManualTask manualTask)
    {
        ManualTask = manualTask;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }
}
