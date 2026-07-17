using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Service;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class GetIdentityLinksForTaskCmd : ICommand<List<IdentityLinkEntity>>
{
    private readonly string _taskId;

    public GetIdentityLinksForTaskCmd(string taskId)
    {
        _taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
    }


    private List<IdentityLinkEntity> BuildIdentityLinks(TaskImplementation task)
    {
        var links = new List<IdentityLinkEntity>();

        if (!string.IsNullOrEmpty(task.Assignee))
        {
            links.Add(CreateLink(task, IdentityLinkType.ASSIGNEE, task.Assignee, null));
        }

        if (!string.IsNullOrEmpty(task.Owner))
        {
            links.Add(CreateLink(task, IdentityLinkType.OWNER, task.Owner, null));
        }

        foreach (var userId in task.CandidateUsers ?? new List<string>())
        {
            links.Add(CreateLink(task, IdentityLinkType.CANDIDATE, userId, null));
        }

        foreach (var groupId in task.CandidateGroups ?? new List<string>())
        {
            links.Add(CreateLink(task, IdentityLinkType.CANDIDATE, null, groupId));
        }

        return links;
    }

    private static IdentityLinkEntity CreateLink(TaskImplementation task, string type, string? userId, string? groupId)
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

    public async Task<List<IdentityLinkEntity>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_taskId))
            throw new WorkflowEngineArgumentException("taskId is null");

        var task = (await context.FindTasksAsync(candidate => candidate.Id == _taskId, cancellationToken)).FirstOrDefault();
        return task == null ? new List<IdentityLinkEntity>() : BuildIdentityLinks(task);
    }
}
