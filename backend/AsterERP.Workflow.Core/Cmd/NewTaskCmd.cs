using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class NewTaskCmd : ICommand<TaskImplementation>
{
    private readonly string? _assignee;
    private readonly string? _name;
    private readonly string? _description;
    private readonly TaskImplementation? _taskTemplate;

    public NewTaskCmd() { }

    public NewTaskCmd(string? assignee, string? name = null, string? description = null)
    {
        _assignee = assignee;
        _name = name;
        _description = description;
    }

    public NewTaskCmd(TaskImplementation taskTemplate)
    {
        _taskTemplate = taskTemplate;
    }

    public TaskImplementation Execute(ICommandContext context)
    {
        if (_taskTemplate != null)
        {
            return _taskTemplate with
            {
                Id = _taskTemplate.Id ?? AbpTimeIdProvider.NewGuid("N"),
                CreateTime = _taskTemplate.CreateTime ?? AbpTimeIdProvider.UtcNow
            };
        }

        return new TaskImplementation
        {
            Id = AbpTimeIdProvider.NewGuid("N"),
            Name = _name,
            Assignee = _assignee,
            Description = _description,
            CreateTime = AbpTimeIdProvider.UtcNow,
            DelegationState = null
        };
    }

    public Task<TaskImplementation> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

