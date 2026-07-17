using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Service;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class IdentityLinkRecord
{
    public string Id { get; init; } = null!;
    public string? Type { get; init; }
    public string? UserId { get; init; }
    public string? GroupId { get; init; }
    public string? TaskId { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ProcessDefinitionId { get; init; }
}

public class AddIdentityLinkCmd : ICommand<object?>
{
    public const string IDENTITY_USER = "user";
    public const string IDENTITY_GROUP = "group";

    private readonly string _taskId;
    private readonly string? _userId;
    private readonly string? _groupId;
    private readonly string _type;

    public AddIdentityLinkCmd(string taskId, string? userId, string? groupId, string type)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        _userId = userId;
        _groupId = groupId;
        _type = type ?? throw new ArgumentNullException(nameof(type));
    }

    public object? Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        if (string.IsNullOrEmpty(_type))
            throw new WorkflowEngineArgumentException("type is required when adding a new task identity link");

        if (!IsAssignmentType(_type) && string.IsNullOrEmpty(_userId) && string.IsNullOrEmpty(_groupId))
            throw new WorkflowEngineArgumentException("userId and groupId cannot both be null");

        if (IsAssignmentType(_type) && !string.IsNullOrEmpty(_groupId))
            throw new WorkflowEngineArgumentException($"Incompatible usage: cannot use type '{_type}' together with a groupId");

        TaskCommandHelper.UpdateTask(context, _taskId, ApplyIdentityLink);

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_CREATED,
                    new IdentityLinkRecord
                    {
                        Id = AbpTimeIdProvider.NewGuid("N"),
                        TaskId = _taskId,
                        UserId = _userId,
                        GroupId = _groupId,
                        Type = _type
                    }));
        }

        return null;
    }

    private TaskImplementation ApplyIdentityLink(TaskImplementation task)
    {
        return _type switch
        {
            IdentityLinkType.ASSIGNEE => task with { Assignee = _userId },
            IdentityLinkType.OWNER => task with { Owner = _userId },
            IdentityLinkType.CANDIDATE when !string.IsNullOrEmpty(_userId) => task with
            {
                CandidateUsers = AddUnique(task.CandidateUsers, _userId)
            },
            IdentityLinkType.CANDIDATE when !string.IsNullOrEmpty(_groupId) => task with
            {
                CandidateGroups = AddUnique(task.CandidateGroups, _groupId)
            },
            _ => task
        };
    }

    private static bool IsAssignmentType(string type)
    {
        return type == IdentityLinkType.ASSIGNEE || type == IdentityLinkType.OWNER;
    }

    private static List<string> AddUnique(List<string>? source, string value)
    {
        var result = source == null ? new List<string>() : new List<string>(source);
        if (!result.Contains(value))
        {
            result.Add(value);
        }

        return result;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

