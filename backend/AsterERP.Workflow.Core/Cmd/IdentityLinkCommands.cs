using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class IdentityLinkEntity
{
    public string Id { get; set; } = AbpTimeIdProvider.NewGuid("N");
    public string? Type { get; set; }
    public string? UserId { get; set; }
    public string? GroupId { get; set; }
    public string? TaskId { get; set; }
    public string? ProcessInstanceId { get; set; }
    public string? ProcessDefinitionId { get; set; }
}

public class AddIdentityLinkForProcessInstanceCmd : ICommand<object?>
{
    private readonly string _processInstanceId;
    private readonly string? _userId;
    private readonly string? _groupId;
    private readonly string _type;
    private readonly byte[]? _details;

    public AddIdentityLinkForProcessInstanceCmd(string processInstanceId, string? userId, string? groupId, string type)
        : this(processInstanceId, userId, groupId, type, null)
    {
    }

    public AddIdentityLinkForProcessInstanceCmd(string processInstanceId, string? userId, string? groupId, string type, byte[]? details)
    {
        ValidateParams(processInstanceId, userId, groupId, type);
        _processInstanceId = processInstanceId;
        _userId = userId;
        _groupId = groupId;
        _type = type;
        _details = details;
    }

    private static void ValidateParams(string processInstanceId, string? userId, string? groupId, string type)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");
        if (string.IsNullOrEmpty(type))
            throw new WorkflowEngineArgumentException("type is required when adding a new process instance identity link");
        if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(groupId))
            throw new WorkflowEngineArgumentException("userId and groupId cannot both be null");
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("AddIdentityLinkForProcessInstanceCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken) == null)
            throw new WorkflowEngineObjectNotFoundException($"Cannot find process instance with id {_processInstanceId}", typeof(Execution.ExecutionEntity));
        var dispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (dispatcher.IsEnabled)
            await dispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_CREATED, new { ProcessInstanceId = _processInstanceId, UserId = _userId, GroupId = _groupId, Type = _type }), cancellationToken);
        return null;
    }
}

public class AddIdentityLinkForProcessDefinitionCmd : ICommand<object?>
{
    private readonly string _processDefinitionId;
    private readonly string? _userId;
    private readonly string? _groupId;
    private readonly string _type;

    public AddIdentityLinkForProcessDefinitionCmd(string processDefinitionId, string? userId, string? groupId, string type)
    {
        if (string.IsNullOrEmpty(processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");
        if (string.IsNullOrEmpty(type))
            throw new WorkflowEngineArgumentException("type is required when adding a new process definition identity link");
        if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(groupId))
            throw new WorkflowEngineArgumentException("userId and groupId cannot both be null");

        _processDefinitionId = processDefinitionId;
        _userId = userId;
        _groupId = groupId;
        _type = type;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("AddIdentityLinkForProcessDefinitionCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (dispatcher.IsEnabled)
            await dispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_CREATED, new { ProcessDefinitionId = _processDefinitionId, UserId = _userId, GroupId = _groupId, Type = _type }), cancellationToken);
        return null;
    }
}

public class DeleteIdentityLinkForProcessInstanceCmd : ICommand<object?>
{
    private readonly string _processInstanceId;
    private readonly string? _userId;
    private readonly string? _groupId;
    private readonly string _type;

    public DeleteIdentityLinkForProcessInstanceCmd(string processInstanceId, string? userId, string? groupId, string type)
    {
        if (string.IsNullOrEmpty(processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");
        if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(groupId))
            throw new WorkflowEngineArgumentException("userId and groupId cannot both be null");

        _processInstanceId = processInstanceId;
        _userId = userId;
        _groupId = groupId;
        _type = type;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("DeleteIdentityLinkForProcessInstanceCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken) == null)
            throw new WorkflowEngineObjectNotFoundException($"Cannot find process instance with id {_processInstanceId}", typeof(Execution.ExecutionEntity));
        var dispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (dispatcher.IsEnabled)
            await dispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_DELETED, new { ProcessInstanceId = _processInstanceId, UserId = _userId, GroupId = _groupId, Type = _type }), cancellationToken);
        return null;
    }
}

public class DeleteIdentityLinkForProcessDefinitionCmd : ICommand<object?>
{
    private readonly string _processDefinitionId;
    private readonly string? _userId;
    private readonly string? _groupId;
    private readonly string _type;

    public DeleteIdentityLinkForProcessDefinitionCmd(string processDefinitionId, string? userId, string? groupId, string type)
    {
        if (string.IsNullOrEmpty(processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");
        if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(groupId))
            throw new WorkflowEngineArgumentException("userId and groupId cannot both be null");

        _processDefinitionId = processDefinitionId;
        _userId = userId;
        _groupId = groupId;
        _type = type;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("DeleteIdentityLinkForProcessDefinitionCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (dispatcher.IsEnabled)
            await dispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_DELETED, new { ProcessDefinitionId = _processDefinitionId, UserId = _userId, GroupId = _groupId, Type = _type }), cancellationToken);
        return null;
    }
}

public class GetIdentityLinksForProcessInstanceCmd : ICommand<List<IdentityLinkEntity>>
{
    private readonly string _processInstanceId;

    public GetIdentityLinksForProcessInstanceCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }


    public async Task<List<IdentityLinkEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    { if (string.IsNullOrEmpty(_processInstanceId)) throw new WorkflowEngineArgumentException("processInstanceId is null"); var store = IdentityLinkQuerySupport.ResolveStore(context, "Runtime identity link store is not available."); return await store.GetIdentityLinksForProcessInstanceAsync(_processInstanceId, cancellationToken); }

    private static IdentityLinkEntity CreateIdentityLink(
        TaskImplementation task,
        string type,
        string? userId,
        string? groupId)
    {
        return new IdentityLinkEntity
        {
            TaskId = task.Id,
            ProcessInstanceId = task.ProcessInstanceId,
            ProcessDefinitionId = task.ProcessDefinitionId,
            Type = type,
            UserId = userId,
            GroupId = groupId
        };
    }
}

public class GetIdentityLinksForProcessDefinitionCmd : ICommand<List<IdentityLinkEntity>>
{
    private readonly string _processDefinitionId;

    public GetIdentityLinksForProcessDefinitionCmd(string processDefinitionId)
    {
        _processDefinitionId = processDefinitionId;
    }


    public async Task<List<IdentityLinkEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    { if (string.IsNullOrEmpty(_processDefinitionId)) throw new WorkflowEngineArgumentException("processDefinitionId is null"); var store = IdentityLinkQuerySupport.ResolveStore(context, "Process-definition identity link store is not available."); return await store.GetIdentityLinksForProcessDefinitionAsync(_processDefinitionId, cancellationToken); }

    private static IdentityLinkEntity CreateIdentityLink(
        TaskImplementation task,
        string type,
        string? userId,
        string? groupId)
    {
        return new IdentityLinkEntity
        {
            TaskId = task.Id,
            ProcessInstanceId = task.ProcessInstanceId,
            ProcessDefinitionId = task.ProcessDefinitionId,
            Type = type,
            UserId = userId,
            GroupId = groupId
        };
    }
}

public class GetHistoricIdentityLinksForTaskCmd : ICommand<List<IdentityLinkEntity>>
{
    private readonly string _taskId;

    public GetHistoricIdentityLinksForTaskCmd(string taskId)
    {
        _taskId = taskId;
    }


    public async Task<List<IdentityLinkEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    { if (string.IsNullOrEmpty(_taskId)) throw new WorkflowEngineArgumentException("taskId is null"); var store = IdentityLinkQuerySupport.ResolveStore(context, "Historic identity link store is not available."); return await store.GetHistoricIdentityLinksForTaskAsync(_taskId, cancellationToken); }
}

public static class IdentityLinkType
{
    public const string ASSIGNEE = "assignee";
    public const string OWNER = "owner";
    public const string CANDIDATE = "candidate";
    public const string STARTER = "starter";
    public const string PARTICIPANT = "participant";
}

public class GetHistoricIdentityLinksForProcessInstanceCmd : ICommand<List<IdentityLinkEntity>>
{
    private readonly string _processInstanceId;

    public GetHistoricIdentityLinksForProcessInstanceCmd(string processInstanceId)
    {
        _processInstanceId = processInstanceId;
    }


    public async Task<List<IdentityLinkEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    { if (string.IsNullOrEmpty(_processInstanceId)) throw new WorkflowEngineArgumentException("processInstanceId is null"); var store = IdentityLinkQuerySupport.ResolveStore(context, "Historic identity link store is not available."); return await store.GetHistoricIdentityLinksForProcessInstanceAsync(_processInstanceId, cancellationToken); }

}

internal static class IdentityLinkQuerySupport
{
    public static IWorkflowPersistenceStore ResolveStore(ICommandContext context, string errorMessage)
    {
        var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(context.ProcessEngineConfiguration);
        if (store == null || !store.IsEnabled) throw new WorkflowEngineException(errorMessage);
        return store;
    }

    public static List<IdentityLinkEntity> MapHistoricLinks(IEnumerable<HistoricIdentityLink> links)
    {
        return links.Select(link => new IdentityLinkEntity
        {
            Id = link.Id ?? string.Empty, ProcessInstanceId = link.ProcessInstanceId, TaskId = link.TaskId,
            Type = link.Type, UserId = link.UserId, GroupId = link.GroupId
        }).ToList();
    }
}

