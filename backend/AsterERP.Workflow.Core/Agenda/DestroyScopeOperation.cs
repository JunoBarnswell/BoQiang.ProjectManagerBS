using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;
using BpmnModel = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Agenda;

public class DestroyScopeOperation
{
    private readonly IAgenda _agenda;
    private readonly ExecutionEntity _execution;
    private readonly IProcessEngineConfiguration _engineConfig;

    public DestroyScopeOperation(IAgenda agenda, ExecutionEntity execution, IProcessEngineConfiguration engineConfig)
    {
        _agenda = agenda;
        _execution = execution;
        _engineConfig = engineConfig;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scopeExecution = _execution.IsScope ? _execution : FindFirstParentScopeExecution(_execution);
        if (scopeExecution == null)
        {
            return;
        }

        DeleteAllChildExecutions(scopeExecution);

        DeleteAllScopeTasks(scopeExecution);

        await DeleteAllScopeJobsAsync(scopeExecution, cancellationToken);

        RemoveAllVariablesFromScope(scopeExecution);

        _engineConfig.HistoryManager.RecordActivityEnd(scopeExecution, scopeExecution.GetVariable("_deleteReason") as string);

        scopeExecution.IsActive = false;
        scopeExecution.IsEnded = true;

        if (scopeExecution.CurrentFlowElement is BpmnModel.FlowNode flowNode && flowNode.OutgoingFlows != null && flowNode.OutgoingFlows.Count > 0)
        {
            var outgoingFlow = flowNode.OutgoingFlows[0];
            scopeExecution.CurrentFlowElement = outgoingFlow;
            scopeExecution.CurrentFlowElementId = outgoingFlow.Id;
            scopeExecution.ActivityId = outgoingFlow.Id;
            scopeExecution.IsScope = false;
            scopeExecution.IsActive = true;
            scopeExecution.IsEnded = false;
            _agenda.PlanContinueProcessOperation(scopeExecution);
        }
        else if (scopeExecution.Parent != null)
        {
            var parent = scopeExecution.Parent;
            var allChildrenEnded = parent.ChildExecutions.All(child => child.IsEnded);

            if (allChildrenEnded)
            {
                if (parent.IsProcessInstanceType || parent.ParentId == null)
                {
                    _agenda.PlanEndExecutionOperation(parent);
                }
                else if (parent.IsScope)
                {
                    _agenda.PlanDestroyScopeOperation(parent);
                }
                else
                {
                    _agenda.PlanEndExecutionOperation(parent);
                }
            }
        }

        await Task.CompletedTask;
    }

    private void DeleteAllChildExecutions(ExecutionEntity scopeExecution)
    {
        foreach (var child in scopeExecution.ChildExecutions.ToList())
        {
            DeleteAllChildExecutions(child);
            child.IsActive = false;
            child.IsEnded = true;
            child.Variables.Clear();
        }
        scopeExecution.ChildExecutions.Clear();
    }

    private void DeleteAllScopeTasks(ExecutionEntity scopeExecution)
    {
        scopeExecution.TaskEntities.Clear();

        foreach (var child in scopeExecution.ChildExecutions)
        {
            DeleteAllScopeTasks(child);
        }
    }

    private async Task DeleteAllScopeJobsAsync(ExecutionEntity scopeExecution, CancellationToken cancellationToken)
    {
        var jobManager = _engineConfig.JobManager;
        if (jobManager != null)
        {
            await jobManager.CancelTimerJobAsync(scopeExecution.Id, scopeExecution.CurrentActivityId ?? "", cancellationToken);
        }

        foreach (var child in scopeExecution.ChildExecutions)
        {
            await DeleteAllScopeJobsAsync(child, cancellationToken);
        }
    }

    private void RemoveAllVariablesFromScope(ExecutionEntity scopeExecution)
    {
        scopeExecution.Variables.Clear();
    }

    private ExecutionEntity? FindFirstParentScopeExecution(ExecutionEntity executionEntity)
    {
        var current = executionEntity.Parent;
        while (current != null)
        {
            if (current.IsScope)
                return current;
            current = current.Parent;
        }
        return null;
    }
}
