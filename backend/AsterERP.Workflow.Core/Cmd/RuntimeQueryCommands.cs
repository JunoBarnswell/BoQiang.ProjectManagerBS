using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class GetExecutionByIdCmd : ICommand<ExecutionRecord?>
{
    private readonly string _executionId;

    public GetExecutionByIdCmd(string executionId)
    {
        _executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
    }


    public async Task<ExecutionRecord?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_executionId)) throw new WorkflowEngineArgumentException("executionId is null");
        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        if (execution != null) return RuntimeQueryRecordMapper.ToExecutionRecord(execution);
        var store = RuntimeQueryStoreResolver.Resolve(context);
        if (store?.IsEnabled != true) return null;
        var record = await store.GetExecutionAsync(_executionId, cancellationToken);
        return record is { IsEnded: true } ? null : record;
    }
}

public class GetAllExecutionsCmd : ICommand<List<ExecutionRecord>>
{

    public async Task<List<ExecutionRecord>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var localExecutions = await context.FindExecutionsAsync(cancellationToken: cancellationToken);
        if (localExecutions.Count > 0) return localExecutions.GroupBy(execution => execution.Id).Select(group => RuntimeQueryRecordMapper.ToExecutionRecord(group.First())).ToList();
        var store = RuntimeQueryStoreResolver.Resolve(context);
        if (store?.IsEnabled == true) return await store.GetExecutionsAsync(cancellationToken);
        var executions = await context.FindExecutionsAsync(cancellationToken: cancellationToken);
        return executions.GroupBy(execution => execution.Id).Select(group => RuntimeQueryRecordMapper.ToExecutionRecord(group.First())).ToList();
    }

    private static ExecutionRecord ToExecutionRecord(ExecutionEntity execution)
    {
        return new ExecutionRecord
        {
            Id = execution.Id,
            ProcessInstanceId = execution.ProcessInstanceId,
            ProcessDefinitionId = execution.ProcessDefinitionId,
            ParentId = execution.ParentId,
            CurrentActivityId = execution.CurrentFlowElementId ?? execution.ActivityId ?? execution.CurrentActivityId,
            CurrentActivityName = execution.CurrentFlowElement?.Name ?? execution.CurrentActivityName,
            IsActive = execution.IsActive,
            IsEnded = execution.IsEnded,
            BusinessKey = execution.BusinessKey
        };
    }
}

public class GetVariableInstancesByExecutionCmd : ICommand<List<VariableInstanceRecord>>
{
    private readonly string? _executionId;

    public GetVariableInstancesByExecutionCmd(string? executionId)
    {
        _executionId = executionId;
    }


    public async Task<List<VariableInstanceRecord>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var localExecutions = await context.FindExecutionsAsync(cancellationToken: cancellationToken);
        if (_executionId == null && localExecutions.Count > 0) return ToVariableInstanceRecords(localExecutions);
        if (_executionId is { Length: > 0 })
        {
            var localExecution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
            if (localExecution != null) return ToVariableInstanceRecords(new[] { localExecution });
        }
        var store = RuntimeQueryStoreResolver.Resolve(context);
        if (store?.IsEnabled == true) return await store.GetExecutionVariableInstancesAsync(_executionId, cancellationToken);
        if (_executionId == null) return ToVariableInstanceRecords(localExecutions);
        if (_executionId.Length == 0) return new List<VariableInstanceRecord>();
        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
        return execution == null ? new List<VariableInstanceRecord>() : ToVariableInstanceRecords(new[] { execution });
    }

    private static List<VariableInstanceRecord> ToVariableInstanceRecords(IEnumerable<ExecutionEntity> executions)
    {
        var variableInstances = new List<VariableInstanceRecord>();

        foreach (var execution in executions.GroupBy(item => item.Id).Select(group => group.First()))
        {
            foreach (var variable in execution.Variables)
            {
                variableInstances.Add(RuntimeQueryRecordMapper.ToVariableInstanceRecord(execution, variable.Key, variable.Value));
            }
        }

        return variableInstances;
    }

}

internal static class RuntimeQueryStoreResolver
{
    public static IWorkflowPersistenceStore? Resolve(ICommandContext context)
    {
        return ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(context.ProcessEngineConfiguration);
    }
}
