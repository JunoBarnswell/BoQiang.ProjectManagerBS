using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Cmd;

public class CompleteTaskCmd : AbstractCompleteTaskCmd
{
    private readonly Dictionary<string, object?>? _variables;
    private readonly bool _localScope;

    public CompleteTaskCmd(string taskId, Dictionary<string, object?>? variables) : base(taskId)
    {
        _variables = variables;
    }

    public CompleteTaskCmd(string taskId, Dictionary<string, object?>? variables, bool localScope) : base(taskId)
    {
        _variables = variables;
        _localScope = localScope;
    }

    public override object? Execute(ICommandContext context) =>
        throw new NotSupportedException("CompleteTaskCmd is async-only. Use ExecuteAsync.");

    protected override object? Execute(ICommandContext context, TaskImplementation task)
    {
        throw new NotSupportedException("CompleteTaskCmd is async-only. Use ExecuteAsync.");
    }

    protected override async Task<object?> ExecuteAsync(ICommandContext context, TaskImplementation task, CancellationToken cancellationToken)
    {
        var execution = await context.FindExecutionByTaskIdAsync(task.Id, cancellationToken);

        if (_variables != null)
        {
            await SetVariablesOnTaskAsync(context, task, _variables, cancellationToken);
        }

        await ExecuteTaskCompleteAsync(context, task, _variables, _localScope, cancellationToken);

        if (execution != null)
        {
            execution.TaskEntities.RemoveAll(existingTask => existingTask.Id == task.Id);

            var agenda = new WorkflowEngineAgenda(context.ProcessEngineConfiguration);
            if (!await TryContinueMultiInstanceAsync(execution, agenda, cancellationToken))
            {
                agenda.PlanTakeOutgoingSequenceFlowsOperation(execution, true);
            }

            while (!agenda.IsEmpty)
            {
                await agenda.ExecuteNextAsync(cancellationToken);
            }
        }

        return null;
    }

    private async Task SetVariablesOnTaskAsync(
        ICommandContext context,
        TaskImplementation task,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        if (_localScope || string.IsNullOrEmpty(task.ProcessInstanceId))
        {
            return;
        }

        try
        {
            var taskExecution = !string.IsNullOrEmpty(task.Id)
                ? await context.FindExecutionByTaskIdAsync(task.Id, cancellationToken)
                : await context.GetCurrentExecutionAsync(task.ProcessInstanceId, cancellationToken);
            if (taskExecution == null)
                return;

            var processInstanceExecution = await context.GetCurrentExecutionAsync(task.ProcessInstanceId, cancellationToken);
            var variableScopeExecution = processInstanceExecution ?? taskExecution;

            foreach (var kvp in variables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                variableScopeExecution.SetVariable(kvp.Key, kvp.Value);
            }
        }
        catch (System.ArgumentException)
        {
        }
    }

    protected override string GetSuspendedTaskException() => "Cannot complete a suspended task";

    private static async Task<bool> TryContinueMultiInstanceAsync(
        ExecutionEntity execution,
        WorkflowEngineAgenda agenda,
        CancellationToken cancellationToken)
    {
        if (execution.CurrentFlowElement is not BpmnModelNs.FlowNode flowNode ||
            flowNode.Behavior is not MultiInstanceActivityBehavior multiInstanceBehavior)
        {
            return false;
        }

        multiInstanceBehavior.Agenda = agenda;

        switch (multiInstanceBehavior)
        {
            case SequentialMultiInstanceActivityBehavior sequentialBehavior when execution.Parent != null:
                await sequentialBehavior.LeaveInstanceAsync(execution, cancellationToken);
                return true;

            case ParallelMultiInstanceActivityBehavior parallelBehavior:
                await parallelBehavior.LeaveInstanceAsync(execution, cancellationToken);
                return true;

            default:
                return false;
        }
    }
}
