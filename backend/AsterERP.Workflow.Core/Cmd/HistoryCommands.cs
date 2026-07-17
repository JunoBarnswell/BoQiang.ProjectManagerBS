using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.EventLogger;
using AsterERP.Workflow.Core.History;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class DeleteHistoricTaskInstanceCmd : ICommand<object?>
{
    private readonly string _taskId;

    public DeleteHistoricTaskInstanceCmd(string taskId)
    {
        _taskId = taskId;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId)) throw new WorkflowEngineArgumentException("taskId is null");
        await HistoricStoreResolver.Resolve(context).DeleteHistoricTaskInstanceAsync(_taskId, cancellationToken);
        return null;
    }
}

public class DeleteHistoricProcessInstanceCmd : ICommand<object?>
{
    private readonly string _processInstanceId;

    public DeleteHistoricProcessInstanceCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_processInstanceId)) throw new WorkflowEngineArgumentException("processInstanceId is null");
        await HistoricStoreResolver.Resolve(context).DeleteHistoricProcessInstanceAsync(_processInstanceId, cancellationToken);
        return null;
    }
}

public class GetEventLogEntriesCmd : ICommand<List<EventLogEntry>>
{
    private readonly long _startId;
    private readonly int _maxResults;

    public GetEventLogEntriesCmd(long startId, int maxResults)
    {
        _startId = startId;
        _maxResults = maxResults;
    }

    public List<EventLogEntry> Execute(ICommandContext context)
    {
        if (_startId < 0)
            throw new WorkflowEngineArgumentException("startId must be 0 or larger");
        if (_maxResults <= 0)
            throw new WorkflowEngineArgumentException("maxResults must be larger than 0");

        throw new WorkflowEngineException("Event log repository is not available in command context.");
    }

    public Task<List<EventLogEntry>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

public class GetHistoricTaskInstancesCmd : ICommand<List<HistoricTaskInstance>>
{

    public Task<List<HistoricTaskInstance>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default) => HistoricStoreResolver.Resolve(context).GetHistoricTaskInstancesAsync(cancellationToken);
}

public class GetHistoricDetailsCmd : ICommand<List<HistoricDetail>>
{
    private readonly string _processInstanceId;

    public GetHistoricDetailsCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }


    public async Task<List<HistoricDetail>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    { if (string.IsNullOrEmpty(_processInstanceId)) throw new WorkflowEngineArgumentException("processInstanceId is null"); return await HistoricStoreResolver.Resolve(context).GetHistoricDetailsAsync(_processInstanceId, cancellationToken); }
}

public class GetHistoricActivityInstancesCmd : ICommand<List<HistoricActivityInstance>>
{

    public Task<List<HistoricActivityInstance>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default) => HistoricStoreResolver.Resolve(context).GetHistoricActivityInstancesAsync(cancellationToken);
}

public class GetHistoricVariableInstancesCmd : ICommand<List<HistoricVariableInstance>>
{

    public Task<List<HistoricVariableInstance>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default) => HistoricStoreResolver.Resolve(context).GetHistoricVariableInstancesAsync(cancellationToken);
}

public class GetHistoricProcessInstancesCmd : ICommand<List<HistoricProcessInstance>>
{

    public Task<List<HistoricProcessInstance>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default) => HistoricStoreResolver.Resolve(context).GetHistoricProcessInstancesAsync(cancellationToken);
}

public class GetHistoricProcessInstanceCmd : ICommand<HistoricProcessInstance?>
{
    private readonly string _processInstanceId;

    public GetHistoricProcessInstanceCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }


    public async Task<HistoricProcessInstance?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    { if (string.IsNullOrEmpty(_processInstanceId)) throw new WorkflowEngineArgumentException("processInstanceId is null"); return await HistoricStoreResolver.Resolve(context).GetHistoricProcessInstanceAsync(_processInstanceId, cancellationToken); }
}

public class GetHistoricIdentityLinksCmd : ICommand<List<HistoricIdentityLink>>
{
    private readonly string _processInstanceId;

    public GetHistoricIdentityLinksCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }


    public async Task<List<HistoricIdentityLink>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    { if (string.IsNullOrEmpty(_processInstanceId)) throw new WorkflowEngineArgumentException("processInstanceId is null"); return await HistoricStoreResolver.Resolve(context).GetHistoricIdentityLinksAsync(_processInstanceId, cancellationToken); }
}

public class DeleteEventLogEntryCmd : ICommand<object?>
{
    private readonly string _eventLogEntryId;

    public DeleteEventLogEntryCmd(string eventLogEntryId)
    {
        _eventLogEntryId = eventLogEntryId;
    }

    public object? Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_eventLogEntryId))
            throw new WorkflowEngineArgumentException("eventLogEntryId is null");

        throw new WorkflowEngineException("Event log repository is not available in command context.");
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

internal static class HistoricStoreResolver
{
    public static IWorkflowPersistenceStore Resolve(ICommandContext context)
    {
        var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(context.ProcessEngineConfiguration);
        if (store == null)
        {
            throw new WorkflowEngineException("Historic store is not available in command context.");
        }

        return store;
    }
}
