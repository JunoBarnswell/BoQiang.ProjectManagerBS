using System.Collections.Generic;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Helper;

public class TaskUpdater
{
    private string? _name;
    private string? _description;
    private string? _assignee;
    private string? _owner;
    private int? _priority;
    private DateTime? _dueDate;
    private string? _category;
    private string? _formKey;
    private List<string>? _candidateUsers;
    private List<string>? _candidateGroups;

    public TaskUpdater SetName(string? name)
    {
        _name = name;
        return this;
    }

    public TaskUpdater SetDescription(string? description)
    {
        _description = description;
        return this;
    }

    public TaskUpdater SetAssignee(string? assignee)
    {
        _assignee = assignee;
        return this;
    }

    public TaskUpdater SetOwner(string? owner)
    {
        _owner = owner;
        return this;
    }

    public TaskUpdater SetPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    public TaskUpdater SetDueDate(DateTime? dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public TaskUpdater SetCategory(string? category)
    {
        _category = category;
        return this;
    }

    public TaskUpdater SetFormKey(string? formKey)
    {
        _formKey = formKey;
        return this;
    }

    public TaskUpdater SetCandidateUsers(IEnumerable<string>? candidateUsers)
    {
        _candidateUsers = candidateUsers != null ? new List<string>(candidateUsers) : null;
        return this;
    }

    public TaskUpdater SetCandidateGroups(IEnumerable<string>? candidateGroups)
    {
        _candidateGroups = candidateGroups != null ? new List<string>(candidateGroups) : null;
        return this;
    }

    public TaskImplementation Apply(TaskImplementation task)
    {
        return task with
        {
            Name = _name ?? task.Name,
            Description = _description ?? task.Description,
            Assignee = _assignee ?? task.Assignee,
            Owner = _owner ?? task.Owner,
            Priority = _priority ?? task.Priority,
            DueDate = _dueDate ?? task.DueDate,
            Category = _category ?? task.Category,
            FormKey = _formKey ?? task.FormKey,
            CandidateUsers = _candidateUsers ?? task.CandidateUsers,
            CandidateGroups = _candidateGroups ?? task.CandidateGroups
        };
    }
}
