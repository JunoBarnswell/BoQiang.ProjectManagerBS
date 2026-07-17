using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public static class TaskQueryProperty
{
    public const string TaskId = "TASK_ID_";
    public const string Name = "NAME_";
    public const string Description = "DESCRIPTION_";
    public const string Priority = "PRIORITY_";
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string ExecutionId = "EXECUTION_ID_";
    public const string ProcessDefinitionId = "PROC_DEF_ID_";
    public const string Assignee = "ASSIGNEE_";
    public const string Owner = "OWNER_";
    public const string CreateTime = "CREATE_TIME_";
    public const string DueDate = "DUE_DATE_";
    public const string TaskDefinitionKey = "TASK_DEF_KEY_";
    public const string TenantId = "TENANT_ID_";
}

public class TaskQueryImpl : AbstractVariableQuery<TaskQueryImpl, TaskImplementation>
{
    protected string? taskId;
    protected string? name;
    protected string? nameLike;
    protected string? description;
    protected string? descriptionLike;
    protected int? priority;
    protected int? minPriority;
    protected int? maxPriority;
    protected string? assignee;
    protected string? assigneeLike;
    protected string? owner;
    protected string? ownerLike;
    protected bool unassigned;
    protected string? delegationState;
    protected bool noDelegationState;
    protected string? candidateUser;
    protected string? candidateGroup;
    protected List<string>? candidateGroups;
    protected string? involvedUser;
    protected List<string>? involvedGroups;
    protected string? processInstanceId;
    protected List<string>? processInstanceIds;
    protected string? executionId;
    protected DateTime? createTime;
    protected DateTime? createTimeBefore;
    protected DateTime? createTimeAfter;
    protected string? category;
    protected string? taskDefinitionKey;
    protected string? taskDefinitionKeyLike;
    protected string? processDefinitionKey;
    protected string? processDefinitionKeyLike;
    protected List<string>? processDefinitionKeys;
    protected string? processDefinitionId;
    protected string? processDefinitionName;
    protected string? processDefinitionNameLike;
    protected string? deploymentId;
    protected List<string>? deploymentIds;
    protected string? processInstanceBusinessKey;
    protected string? processInstanceBusinessKeyLike;
    protected DateTime? dueDate;
    protected DateTime? dueBefore;
    protected DateTime? dueAfter;
    protected bool withoutDueDate;
    protected bool isSuspended;
    protected bool isActive;
    protected bool excludeSubtasks;
    protected bool includeTaskLocalVariables;
    protected bool includeProcessVariables;
    protected int? taskVariablesLimit;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;
    protected string? userIdForCandidateAndAssignee;
    protected bool bothCandidateAndAssigned;
    protected string? taskParentTaskId;

    private readonly IEnumerable<TaskImplementation>? _source;

    public TaskQueryImpl() { }

    public TaskQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public TaskQueryImpl(IEnumerable<TaskImplementation> source)
    {
        _source = source;
    }

    public TaskQueryImpl TaskId(string taskId)
    {
        this.taskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
        return this;
    }

    public TaskQueryImpl TaskName(string name)
    {
        this.name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    public TaskQueryImpl TaskNameLike(string nameLike)
    {
        this.nameLike = nameLike ?? throw new ArgumentNullException(nameof(nameLike));
        return this;
    }

    public TaskQueryImpl TaskDescription(string description)
    {
        this.description = description ?? throw new ArgumentNullException(nameof(description));
        return this;
    }

    public TaskQueryImpl TaskDescriptionLike(string descriptionLike)
    {
        this.descriptionLike = descriptionLike ?? throw new ArgumentNullException(nameof(descriptionLike));
        return this;
    }

    public TaskQueryImpl TaskPriority(int? priority)
    {
        this.priority = priority ?? throw new ArgumentNullException(nameof(priority));
        return this;
    }

    public TaskQueryImpl TaskMinPriority(int? minPriority)
    {
        this.minPriority = minPriority ?? throw new ArgumentNullException(nameof(minPriority));
        return this;
    }

    public TaskQueryImpl TaskMaxPriority(int? maxPriority)
    {
        this.maxPriority = maxPriority ?? throw new ArgumentNullException(nameof(maxPriority));
        return this;
    }

    public TaskQueryImpl TaskAssignee(string assignee)
    {
        this.assignee = assignee ?? throw new ArgumentNullException(nameof(assignee));
        return this;
    }

    public TaskQueryImpl TaskAssigneeLike(string assigneeLike)
    {
        this.assigneeLike = assigneeLike ?? throw new ArgumentNullException(nameof(assigneeLike));
        return this;
    }

    public TaskQueryImpl TaskOwner(string owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        return this;
    }

    public TaskQueryImpl TaskOwnerLike(string ownerLike)
    {
        this.ownerLike = ownerLike ?? throw new ArgumentNullException(nameof(ownerLike));
        return this;
    }

    public TaskQueryImpl TaskUnassigned()
    {
        unassigned = true;
        return this;
    }

    public TaskQueryImpl TaskDelegationState(string? state)
    {
        if (state == null)
            noDelegationState = true;
        else
            delegationState = state;
        return this;
    }

    public TaskQueryImpl TaskCandidateUser(string candidateUser)
    {
        this.candidateUser = candidateUser ?? throw new ArgumentNullException(nameof(candidateUser));
        return this;
    }

    public TaskQueryImpl TaskCandidateUser(string candidateUser, List<string> usersGroups)
    {
        this.candidateUser = candidateUser ?? throw new ArgumentNullException(nameof(candidateUser));
        candidateGroups = usersGroups;
        return this;
    }

    public TaskQueryImpl TaskCandidateGroup(string candidateGroup)
    {
        if (candidateGroups != null)
            throw new InvalidOperationException("Cannot set both candidateGroup and candidateGroupIn");
        this.candidateGroup = candidateGroup ?? throw new ArgumentNullException(nameof(candidateGroup));
        return this;
    }

    public TaskQueryImpl TaskCandidateGroupIn(List<string> groups)
    {
        if (groups == null) throw new ArgumentNullException(nameof(groups));
        if (groups.Count == 0) throw new ArgumentException("Candidate group list is empty");
        if (candidateGroup != null)
            throw new InvalidOperationException("Cannot set both candidateGroupIn and candidateGroup");
        candidateGroups = groups;
        return this;
    }

    public TaskQueryImpl TaskInvolvedUser(string involvedUser)
    {
        this.involvedUser = involvedUser ?? throw new ArgumentNullException(nameof(involvedUser));
        return this;
    }

    public TaskQueryImpl TaskCandidateOrAssigned(string userId)
    {
        if (candidateGroup != null) throw new InvalidOperationException("Cannot set candidateGroup");
        if (candidateUser != null) throw new InvalidOperationException("Cannot set both candidateGroup and candidateUser");
        bothCandidateAndAssigned = true;
        userIdForCandidateAndAssignee = userId;
        return this;
    }

    public TaskQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId;
        return this;
    }

    public TaskQueryImpl ProcessInstanceIdIn(List<string> processInstanceIds)
    {
        this.processInstanceIds = processInstanceIds ?? throw new ArgumentNullException(nameof(processInstanceIds));
        return this;
    }

    public TaskQueryImpl ProcessInstanceBusinessKey(string businessKey)
    {
        processInstanceBusinessKey = businessKey;
        return this;
    }

    public TaskQueryImpl ProcessInstanceBusinessKeyLike(string businessKeyLike)
    {
        processInstanceBusinessKeyLike = businessKeyLike;
        return this;
    }

    public TaskQueryImpl ExecutionId(string executionId)
    {
        this.executionId = executionId;
        return this;
    }

    public TaskQueryImpl TaskCreatedOn(DateTime? createTime)
    {
        this.createTime = createTime;
        return this;
    }

    public TaskQueryImpl TaskCreatedBefore(DateTime? before)
    {
        createTimeBefore = before;
        return this;
    }

    public TaskQueryImpl TaskCreatedAfter(DateTime? after)
    {
        createTimeAfter = after;
        return this;
    }

    public TaskQueryImpl TaskCategory(string category)
    {
        this.category = category;
        return this;
    }

    public TaskQueryImpl TaskDefinitionKey(string key)
    {
        taskDefinitionKey = key;
        return this;
    }

    public TaskQueryImpl TaskDefinitionKeyLike(string keyLike)
    {
        taskDefinitionKeyLike = keyLike;
        return this;
    }

    public TaskQueryImpl ProcessDefinitionKey(string processDefinitionKey)
    {
        this.processDefinitionKey = processDefinitionKey;
        return this;
    }

    public TaskQueryImpl ProcessDefinitionKeyLike(string processDefinitionKeyLike)
    {
        this.processDefinitionKeyLike = processDefinitionKeyLike;
        return this;
    }

    public TaskQueryImpl ProcessDefinitionKeyIn(List<string> processDefinitionKeys)
    {
        this.processDefinitionKeys = processDefinitionKeys;
        return this;
    }

    public TaskQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        this.processDefinitionId = processDefinitionId;
        return this;
    }

    public TaskQueryImpl ProcessDefinitionName(string processDefinitionName)
    {
        this.processDefinitionName = processDefinitionName;
        return this;
    }

    public TaskQueryImpl ProcessDefinitionNameLike(string processDefinitionNameLike)
    {
        this.processDefinitionNameLike = processDefinitionNameLike;
        return this;
    }

    public TaskQueryImpl DeploymentId(string deploymentId)
    {
        this.deploymentId = deploymentId;
        return this;
    }

    public TaskQueryImpl DeploymentIdIn(List<string> deploymentIds)
    {
        this.deploymentIds = deploymentIds;
        return this;
    }

    public TaskQueryImpl DueDate(DateTime? dueDate)
    {
        this.dueDate = dueDate;
        withoutDueDate = false;
        return this;
    }

    public TaskQueryImpl DueBefore(DateTime? dueBefore)
    {
        this.dueBefore = dueBefore;
        withoutDueDate = false;
        return this;
    }

    public TaskQueryImpl DueAfter(DateTime? dueAfter)
    {
        this.dueAfter = dueAfter;
        withoutDueDate = false;
        return this;
    }

    public TaskQueryImpl WithoutDueDate()
    {
        withoutDueDate = true;
        return this;
    }

    public TaskQueryImpl ExcludeSubtasks()
    {
        excludeSubtasks = true;
        return this;
    }

    public TaskQueryImpl Suspended()
    {
        isSuspended = true;
        isActive = false;
        return this;
    }

    public TaskQueryImpl Active()
    {
        isActive = true;
        isSuspended = false;
        return this;
    }

    public TaskQueryImpl IncludeTaskLocalVariables()
    {
        includeTaskLocalVariables = true;
        return this;
    }

    public TaskQueryImpl IncludeProcessVariables()
    {
        includeProcessVariables = true;
        return this;
    }

    public TaskQueryImpl LimitTaskVariables(int? limit)
    {
        taskVariablesLimit = limit;
        return this;
    }

    public TaskQueryImpl TaskTenantId(string tenantId)
    {
        this.tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        return this;
    }

    public TaskQueryImpl TaskTenantIdLike(string tenantIdLike)
    {
        this.tenantIdLike = tenantIdLike ?? throw new ArgumentNullException(nameof(tenantIdLike));
        return this;
    }

    public TaskQueryImpl TaskWithoutTenantId()
    {
        withoutTenantId = true;
        return this;
    }

    public TaskQueryImpl TaskParentTaskId(string parentTaskId)
    {
        taskParentTaskId = parentTaskId;
        return this;
    }

    public TaskQueryImpl TaskVariableValueEquals(string name, object? value)
    {
        VariableValueEquals(name, value, true);
        return this;
    }

    public TaskQueryImpl TaskVariableValueNotEquals(string name, object? value)
    {
        VariableValueNotEquals(name, value, true);
        return this;
    }

    public TaskQueryImpl TaskVariableValueLike(string name, string? value)
    {
        VariableValueLike(name, value, true);
        return this;
    }

    public TaskQueryImpl ProcessVariableValueEquals(string name, object? value)
    {
        VariableValueEquals(name, value, false);
        return this;
    }

    public TaskQueryImpl ProcessVariableValueNotEquals(string name, object? value)
    {
        VariableValueNotEquals(name, value, false);
        return this;
    }

    public TaskQueryImpl ProcessVariableValueLike(string name, string? value)
    {
        VariableValueLike(name, value, false);
        return this;
    }

    public TaskQueryImpl OrderByTaskId() => OrderByProperty(TaskQueryProperty.TaskId);
    public TaskQueryImpl OrderByTaskName() => OrderByProperty(TaskQueryProperty.Name);
    public TaskQueryImpl OrderByTaskDescription() => OrderByProperty(TaskQueryProperty.Description);
    public TaskQueryImpl OrderByTaskPriority() => OrderByProperty(TaskQueryProperty.Priority);
    public TaskQueryImpl OrderByProcessInstanceId() => OrderByProperty(TaskQueryProperty.ProcessInstanceId);
    public TaskQueryImpl OrderByExecutionId() => OrderByProperty(TaskQueryProperty.ExecutionId);
    public TaskQueryImpl OrderByProcessDefinitionId() => OrderByProperty(TaskQueryProperty.ProcessDefinitionId);
    public TaskQueryImpl OrderByTaskAssignee() => OrderByProperty(TaskQueryProperty.Assignee);
    public TaskQueryImpl OrderByTaskOwner() => OrderByProperty(TaskQueryProperty.Owner);
    public TaskQueryImpl OrderByTaskCreateTime() => OrderByProperty(TaskQueryProperty.CreateTime);
    public TaskQueryImpl OrderByDueDate() => OrderByProperty(TaskQueryProperty.DueDate);
    public TaskQueryImpl OrderByTaskDefinitionKey() => OrderByProperty(TaskQueryProperty.TaskDefinitionKey);
    public TaskQueryImpl OrderByTenantId() => OrderByProperty(TaskQueryProperty.TenantId);

    public override Task<List<TaskImplementation>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue)
            result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue)
            result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<TaskImplementation?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source).FirstOrDefault();
        return Task.FromResult(result);
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    private IEnumerable<TaskImplementation> ApplyFilters(IEnumerable<TaskImplementation>? source)
    {
        if (source == null) return Enumerable.Empty<TaskImplementation>();
        var query = source.AsEnumerable();

        if (taskId != null) query = query.Where(t => t.Id == taskId);
        if (name != null) query = query.Where(t => t.Name == name);
        if (nameLike != null) query = query.Where(t => t.Name != null && t.Name.Contains(nameLike.Replace("%", "")));
        if (description != null) query = query.Where(t => t.Description == description);
        if (descriptionLike != null) query = query.Where(t => t.Description != null && t.Description.Contains(descriptionLike.Replace("%", "")));
        if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);
        if (minPriority.HasValue) query = query.Where(t => t.Priority >= minPriority.Value);
        if (maxPriority.HasValue) query = query.Where(t => t.Priority <= maxPriority.Value);
        if (assignee != null) query = query.Where(t => t.Assignee == assignee);
        if (assigneeLike != null) query = query.Where(t => t.Assignee != null && t.Assignee.Contains(assigneeLike.Replace("%", "")));
        if (owner != null) query = query.Where(t => t.Owner == owner);
        if (ownerLike != null) query = query.Where(t => t.Owner != null && t.Owner.Contains(ownerLike.Replace("%", "")));
        if (unassigned) query = query.Where(t => t.Assignee == null);
        if (delegationState != null) query = query.Where(t => t.DelegationState == delegationState);
        if (candidateUser != null) query = query.Where(t => t.CandidateUsers != null && t.CandidateUsers.Contains(candidateUser));
        if (candidateGroup != null) query = query.Where(t => t.CandidateGroups != null && t.CandidateGroups.Contains(candidateGroup));
        if (candidateGroups != null) query = query.Where(t => t.CandidateGroups != null && t.CandidateGroups.Any(g => candidateGroups.Contains(g)));
        if (processInstanceId != null) query = query.Where(t => t.ProcessInstanceId == processInstanceId);
        if (processInstanceIds != null) query = query.Where(t => t.ProcessInstanceId != null && processInstanceIds.Contains(t.ProcessInstanceId));
        if (executionId != null) query = query.Where(t => t.ProcessInstanceId == executionId);
        if (createTime.HasValue) query = query.Where(t => t.CreateTime == createTime);
        if (createTimeBefore.HasValue) query = query.Where(t => t.CreateTime < createTimeBefore);
        if (createTimeAfter.HasValue) query = query.Where(t => t.CreateTime > createTimeAfter);
        if (category != null) query = query.Where(t => t.Category == category);
        if (taskDefinitionKey != null) query = query.Where(t => t.TaskDefinitionKey == taskDefinitionKey);
        if (taskDefinitionKeyLike != null) query = query.Where(t => t.TaskDefinitionKey != null && t.TaskDefinitionKey.Contains(taskDefinitionKeyLike.Replace("%", "")));
        if (processDefinitionId != null) query = query.Where(t => t.ProcessDefinitionId == processDefinitionId);
        if (processDefinitionKey != null) query = query.Where(t => t.ProcessDefinitionId != null && t.ProcessDefinitionId.Contains(processDefinitionKey));
        if (processDefinitionName != null) query = query.Where(t => t.ProcessDefinitionId != null);
        if (dueDate.HasValue) query = query.Where(t => t.DueDate == dueDate);
        if (dueBefore.HasValue) query = query.Where(t => t.DueDate < dueBefore);
        if (dueAfter.HasValue) query = query.Where(t => t.DueDate > dueAfter);
        if (withoutDueDate) query = query.Where(t => t.DueDate == null);
        if (tenantId != null) query = query.Where(t => false);
        if (withoutTenantId) query = query.Where(t => true);
        if (bothCandidateAndAssigned && userIdForCandidateAndAssignee != null)
            query = query.Where(t => t.Assignee == userIdForCandidateAndAssignee ||
                                     (t.CandidateUsers != null && t.CandidateUsers.Contains(userIdForCandidateAndAssignee)));

        return query;
    }

    private IEnumerable<TaskImplementation> ApplySorting(IEnumerable<TaskImplementation> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;

        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            TaskQueryProperty.TaskId => asc ? query.OrderBy(t => t.Id) : query.OrderByDescending(t => t.Id),
            TaskQueryProperty.Name => asc ? query.OrderBy(t => t.Name) : query.OrderByDescending(t => t.Name),
            TaskQueryProperty.Priority => asc ? query.OrderBy(t => t.Priority) : query.OrderByDescending(t => t.Priority),
            TaskQueryProperty.Assignee => asc ? query.OrderBy(t => t.Assignee) : query.OrderByDescending(t => t.Assignee),
            TaskQueryProperty.Owner => asc ? query.OrderBy(t => t.Owner) : query.OrderByDescending(t => t.Owner),
            TaskQueryProperty.CreateTime => asc ? query.OrderBy(t => t.CreateTime) : query.OrderByDescending(t => t.CreateTime),
            TaskQueryProperty.DueDate => asc ? query.OrderBy(t => t.DueDate) : query.OrderByDescending(t => t.DueDate),
            TaskQueryProperty.ProcessInstanceId => asc ? query.OrderBy(t => t.ProcessInstanceId) : query.OrderByDescending(t => t.ProcessInstanceId),
            _ => query
        };
    }
}
