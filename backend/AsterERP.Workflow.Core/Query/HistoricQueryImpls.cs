using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.History;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public static class HistoricActivityInstanceQueryProperty
{
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string ActivityId = "ACT_ID_";
    public const string ActivityType = "ACT_TYPE_";
    public const string StartTime = "START_TIME_";
    public const string EndTime = "END_TIME_";
    public const string Duration = "DURATION_";
}

public static class HistoricProcessInstanceQueryProperty
{
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string ProcessDefinitionId = "PROC_DEF_ID_";
    public const string ProcessDefinitionKey = "PROC_DEF_KEY_";
    public const string StartTime = "START_TIME_";
    public const string EndTime = "END_TIME_";
    public const string Duration = "DURATION_";
    public const string TenantId = "TENANT_ID_";
}

public static class HistoricTaskInstanceQueryProperty
{
    public const string TaskId = "ID_";
    public const string TaskName = "NAME_";
    public const string TaskAssignee = "ASSIGNEE_";
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string StartTime = "START_TIME_";
    public const string EndTime = "END_TIME_";
    public const string Duration = "DURATION_";
    public const string TaskDefinitionKey = "TASK_DEF_KEY_";
    public const string TenantId = "TENANT_ID_";
}

public static class HistoricVariableInstanceQueryProperty
{
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string VariableName = "NAME_";
    public const string VariableType = "TYPE_";
    public const string CreateTime = "CREATE_TIME_";
}

public static class HistoricDetailQueryProperty
{
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string VariableName = "NAME_";
    public const string VariableType = "TYPE_";
    public const string Time = "TIME_";
}

public class HistoricActivityInstanceQueryImpl : AbstractQuery<HistoricActivityInstanceQueryImpl, HistoricActivityInstance>, IHistoricActivityInstanceQuery
{
    protected string? activityInstanceId;
    protected string? processInstanceId;
    protected string? executionId;
    protected string? processDefinitionId;
    protected string? activityId;
    protected string? activityName;
    protected string? activityType;
    protected string? assignee;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;
    protected bool finished;
    protected bool unfinished;
    protected string? deleteReason;
    protected string? deleteReasonLike;

    private readonly IEnumerable<HistoricActivityInstance>? _source;

    public HistoricActivityInstanceQueryImpl() { }
    public HistoricActivityInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public HistoricActivityInstanceQueryImpl(IEnumerable<HistoricActivityInstance> source) { _source = source; }

    public HistoricActivityInstanceQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId;
        return this;
    }

    public HistoricActivityInstanceQueryImpl ExecutionId(string executionId)
    {
        this.executionId = executionId;
        return this;
    }

    public HistoricActivityInstanceQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        this.processDefinitionId = processDefinitionId;
        return this;
    }

    public HistoricActivityInstanceQueryImpl ActivityId(string activityId)
    {
        this.activityId = activityId;
        return this;
    }

    public HistoricActivityInstanceQueryImpl ActivityName(string activityName)
    {
        this.activityName = activityName;
        return this;
    }

    public HistoricActivityInstanceQueryImpl ActivityType(string activityType)
    {
        this.activityType = activityType;
        return this;
    }

    public HistoricActivityInstanceQueryImpl ActivityInstanceId(string activityInstanceId)
    {
        this.activityInstanceId = activityInstanceId;
        return this;
    }

    public HistoricActivityInstanceQueryImpl Assignee(string assignee)
    {
        this.assignee = assignee;
        return this;
    }

    public HistoricActivityInstanceQueryImpl Finished()
    {
        finished = true;
        unfinished = false;
        return this;
    }

    public HistoricActivityInstanceQueryImpl Unfinished()
    {
        unfinished = true;
        finished = false;
        return this;
    }

    public HistoricActivityInstanceQueryImpl DeleteReason(string deleteReason)
    {
        this.deleteReason = deleteReason;
        return this;
    }

    public HistoricActivityInstanceQueryImpl DeleteReasonLike(string deleteReasonLike)
    {
        this.deleteReasonLike = deleteReasonLike;
        return this;
    }

    public HistoricActivityInstanceQueryImpl OrderByProcessInstanceId() => OrderByProperty(HistoricActivityInstanceQueryProperty.ProcessInstanceId);
    public HistoricActivityInstanceQueryImpl OrderByActivityId() => OrderByProperty(HistoricActivityInstanceQueryProperty.ActivityId);
    public HistoricActivityInstanceQueryImpl OrderByActivityType() => OrderByProperty(HistoricActivityInstanceQueryProperty.ActivityType);
    public HistoricActivityInstanceQueryImpl OrderByStartTime() => OrderByProperty(HistoricActivityInstanceQueryProperty.StartTime);
    public HistoricActivityInstanceQueryImpl OrderByEndTime() => OrderByProperty(HistoricActivityInstanceQueryProperty.EndTime);

    public override Task<List<HistoricActivityInstance>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<HistoricActivityInstance?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    IHistoricActivityInstanceQuery IHistoricActivityInstanceQuery.ProcessInstanceId(string processInstanceId) => ProcessInstanceId(processInstanceId);
    IHistoricActivityInstanceQuery IHistoricActivityInstanceQuery.ActivityId(string activityId) => ActivityId(activityId);
    IHistoricActivityInstanceQuery IHistoricActivityInstanceQuery.ActivityType(string activityType) => ActivityType(activityType);
    IHistoricActivityInstanceQuery IHistoricActivityInstanceQuery.ProcessDefinitionId(string processDefinitionId) => ProcessDefinitionId(processDefinitionId);
    Task<List<HistoricActivityInstance>> IHistoricActivityInstanceQuery.ListAsync(CancellationToken cancellationToken) => ToListAsync(cancellationToken);
    async Task<long> IHistoricActivityInstanceQuery.CountAsync(CancellationToken cancellationToken) => await CountAsync(cancellationToken);

    private IEnumerable<HistoricActivityInstance> ApplyFilters(IEnumerable<HistoricActivityInstance>? source)
    {
        if (source == null) return Enumerable.Empty<HistoricActivityInstance>();
        var query = source.AsEnumerable();
        if (processInstanceId != null) query = query.Where(a => a.ProcessInstanceId == processInstanceId);
        if (executionId != null) query = query.Where(a => a.ExecutionId == executionId);
        if (processDefinitionId != null) query = query.Where(a => a.ProcessDefinitionId == processDefinitionId);
        if (activityId != null) query = query.Where(a => a.ActivityId == activityId);
        if (activityName != null) query = query.Where(a => a.ActivityName == activityName);
        if (activityType != null) query = query.Where(a => a.ActivityType == activityType);
        if (assignee != null) query = query.Where(a => a.Assignee == assignee);
        if (finished) query = query.Where(a => a.EndTime.HasValue);
        if (unfinished) query = query.Where(a => !a.EndTime.HasValue);
        return query;
    }

    private IEnumerable<HistoricActivityInstance> ApplySorting(IEnumerable<HistoricActivityInstance> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            HistoricActivityInstanceQueryProperty.StartTime => asc ? query.OrderBy(a => a.StartTime) : query.OrderByDescending(a => a.StartTime),
            HistoricActivityInstanceQueryProperty.EndTime => asc ? query.OrderBy(a => a.EndTime) : query.OrderByDescending(a => a.EndTime),
            _ => query
        };
    }
}

public class HistoricProcessInstanceQueryImpl : AbstractVariableQuery<HistoricProcessInstanceQueryImpl, HistoricProcessInstance>, IHistoricProcessInstanceQuery
{
    protected string? processInstanceId;
    protected string? processDefinitionId;
    protected string? businessKey;
    protected string? deploymentId;
    protected List<string>? deploymentIds;
    protected bool finished;
    protected bool unfinished;
    protected bool deleted;
    protected bool notDeleted;
    protected string? startedBy;
    protected string? superProcessInstanceId;
    protected bool excludeSubprocesses;
    protected List<string>? processDefinitionKeyIn;
    protected List<string>? processKeyNotIn;
    protected DateTime? startedBefore;
    protected DateTime? startedAfter;
    protected DateTime? finishedBefore;
    protected DateTime? finishedAfter;
    protected string? processDefinitionKey;
    protected string? processDefinitionCategory;
    protected string? processDefinitionName;
    protected int? processDefinitionVersion;
    protected HashSet<string>? processInstanceIds;
    protected string? involvedUser;
    protected bool includeProcessVariables;
    protected int? processInstanceVariablesLimit;
    protected bool withJobException;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;
    protected string? name;
    protected string? nameLike;
    protected List<string>? involvedGroups;

    private readonly IEnumerable<HistoricProcessInstance>? _source;

    public HistoricProcessInstanceQueryImpl() { }
    public HistoricProcessInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public HistoricProcessInstanceQueryImpl(IEnumerable<HistoricProcessInstance> source) { _source = source; }

    public HistoricProcessInstanceQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId;
        return this;
    }

    public HistoricProcessInstanceQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        this.processDefinitionId = processDefinitionId;
        return this;
    }

    public HistoricProcessInstanceQueryImpl BusinessKey(string businessKey)
    {
        this.businessKey = businessKey;
        return this;
    }

    public HistoricProcessInstanceQueryImpl Finished()
    {
        finished = true;
        unfinished = false;
        return this;
    }

    public HistoricProcessInstanceQueryImpl Unfinished()
    {
        unfinished = true;
        finished = false;
        return this;
    }

    public HistoricProcessInstanceQueryImpl StartedBy(string startedBy)
    {
        this.startedBy = startedBy;
        return this;
    }

    public HistoricProcessInstanceQueryImpl StartedBefore(DateTime? before)
    {
        startedBefore = before;
        return this;
    }

    public HistoricProcessInstanceQueryImpl StartedAfter(DateTime? after)
    {
        startedAfter = after;
        return this;
    }

    public HistoricProcessInstanceQueryImpl FinishedBefore(DateTime? before)
    {
        finishedBefore = before;
        return this;
    }

    public HistoricProcessInstanceQueryImpl FinishedAfter(DateTime? after)
    {
        finishedAfter = after;
        return this;
    }

    public HistoricProcessInstanceQueryImpl ProcessDefinitionKey(string processDefinitionKey)
    {
        this.processDefinitionKey = processDefinitionKey;
        return this;
    }

    public HistoricProcessInstanceQueryImpl ProcessDefinitionKeyIn(List<string> processDefinitionKeyIn)
    {
        this.processDefinitionKeyIn = processDefinitionKeyIn;
        return this;
    }

    public HistoricProcessInstanceQueryImpl ProcessDefinitionCategory(string processDefinitionCategory)
    {
        this.processDefinitionCategory = processDefinitionCategory;
        return this;
    }

    public HistoricProcessInstanceQueryImpl ProcessDefinitionName(string processDefinitionName)
    {
        this.processDefinitionName = processDefinitionName;
        return this;
    }

    public HistoricProcessInstanceQueryImpl ProcessDefinitionVersion(int? processDefinitionVersion)
    {
        this.processDefinitionVersion = processDefinitionVersion;
        return this;
    }

    public HistoricProcessInstanceQueryImpl InvolvedUser(string involvedUser)
    {
        this.involvedUser = involvedUser;
        return this;
    }

    public HistoricProcessInstanceQueryImpl IncludeProcessVariables()
    {
        includeProcessVariables = true;
        return this;
    }

    public HistoricProcessInstanceQueryImpl WithJobException()
    {
        withJobException = true;
        return this;
    }

    public HistoricProcessInstanceQueryImpl OrderByProcessInstanceId() => OrderByProperty(HistoricProcessInstanceQueryProperty.ProcessInstanceId);
    public HistoricProcessInstanceQueryImpl OrderByProcessDefinitionId() => OrderByProperty(HistoricProcessInstanceQueryProperty.ProcessDefinitionId);
    public HistoricProcessInstanceQueryImpl OrderByStartTime() => OrderByProperty(HistoricProcessInstanceQueryProperty.StartTime);
    public HistoricProcessInstanceQueryImpl OrderByEndTime() => OrderByProperty(HistoricProcessInstanceQueryProperty.EndTime);
    public HistoricProcessInstanceQueryImpl OrderByTenantId() => OrderByProperty(HistoricProcessInstanceQueryProperty.TenantId);

    public override Task<List<HistoricProcessInstance>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<HistoricProcessInstance?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    IHistoricProcessInstanceQuery IHistoricProcessInstanceQuery.ProcessDefinitionId(string processDefinitionId) => ProcessDefinitionId(processDefinitionId);
    IHistoricProcessInstanceQuery IHistoricProcessInstanceQuery.ProcessDefinitionKey(string processDefinitionKey) => ProcessDefinitionKey(processDefinitionKey);
    IHistoricProcessInstanceQuery IHistoricProcessInstanceQuery.ProcessInstanceId(string processInstanceId) => ProcessInstanceId(processInstanceId);
    IHistoricProcessInstanceQuery IHistoricProcessInstanceQuery.Unfinished() => Unfinished();
    IHistoricProcessInstanceQuery IHistoricProcessInstanceQuery.Finished() => Finished();
    Task<List<HistoricProcessInstance>> IHistoricProcessInstanceQuery.ListAsync(CancellationToken cancellationToken) => ToListAsync(cancellationToken);
    async Task<long> IHistoricProcessInstanceQuery.CountAsync(CancellationToken cancellationToken) => await CountAsync(cancellationToken);

    private IEnumerable<HistoricProcessInstance> ApplyFilters(IEnumerable<HistoricProcessInstance>? source)
    {
        if (source == null) return Enumerable.Empty<HistoricProcessInstance>();
        var query = source.AsEnumerable();
        if (processInstanceId != null) query = query.Where(p => p.Id == processInstanceId);
        if (processDefinitionId != null) query = query.Where(p => p.ProcessDefinitionId == processDefinitionId);
        if (businessKey != null) query = query.Where(p => p.BusinessKey == businessKey);
        if (finished) query = query.Where(p => p.EndTime.HasValue);
        if (unfinished) query = query.Where(p => !p.EndTime.HasValue);
        if (startedBy != null) query = query.Where(p => true);
        if (startedBefore.HasValue) query = query.Where(p => p.StartTime < startedBefore);
        if (startedAfter.HasValue) query = query.Where(p => p.StartTime > startedAfter);
        if (finishedBefore.HasValue) query = query.Where(p => p.EndTime < finishedBefore);
        if (finishedAfter.HasValue) query = query.Where(p => p.EndTime > finishedAfter);
        return query;
    }

    private IEnumerable<HistoricProcessInstance> ApplySorting(IEnumerable<HistoricProcessInstance> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            HistoricProcessInstanceQueryProperty.StartTime => asc ? query.OrderBy(p => p.StartTime) : query.OrderByDescending(p => p.StartTime),
            HistoricProcessInstanceQueryProperty.EndTime => asc ? query.OrderBy(p => p.EndTime) : query.OrderByDescending(p => p.EndTime),
            _ => query
        };
    }
}

public class HistoricTaskInstanceQueryImpl : AbstractVariableQuery<HistoricTaskInstanceQueryImpl, HistoricTaskInstance>, IHistoricTaskInstanceQuery
{
    protected string? processDefinitionId;
    protected string? processDefinitionKey;
    protected string? processDefinitionKeyLike;
    protected List<string>? processDefinitionKeys;
    protected string? processDefinitionName;
    protected string? processDefinitionNameLike;
    protected string? deploymentId;
    protected List<string>? deploymentIds;
    protected string? processInstanceId;
    protected List<string>? processInstanceIds;
    protected string? processInstanceBusinessKey;
    protected string? processInstanceBusinessKeyLike;
    protected string? executionId;
    protected string? taskId;
    protected string? taskName;
    protected string? taskNameLike;
    protected string? taskDescription;
    protected string? taskDescriptionLike;
    protected string? taskDeleteReason;
    protected string? taskDeleteReasonLike;
    protected string? taskOwner;
    protected string? taskOwnerLike;
    protected string? taskAssignee;
    protected string? taskAssigneeLike;
    protected List<string>? taskAssigneeIds;
    protected string? taskDefinitionKey;
    protected string? taskDefinitionKeyLike;
    protected string? candidateUser;
    protected string? candidateGroup;
    protected List<string>? candidateGroups;
    protected string? involvedUser;
    protected List<string>? involvedGroups;
    protected int? taskPriority;
    protected int? taskMinPriority;
    protected int? taskMaxPriority;
    protected bool finished;
    protected bool unfinished;
    protected bool processFinished;
    protected bool processUnfinished;
    protected DateTime? dueDate;
    protected DateTime? dueAfter;
    protected DateTime? dueBefore;
    protected bool withoutDueDate;
    protected DateTime? creationDate;
    protected DateTime? creationAfterDate;
    protected DateTime? creationBeforeDate;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;

    private readonly IEnumerable<HistoricTaskInstance>? _source;

    public HistoricTaskInstanceQueryImpl() { }
    public HistoricTaskInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public HistoricTaskInstanceQueryImpl(IEnumerable<HistoricTaskInstance> source) { _source = source; }

    public HistoricTaskInstanceQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        this.processDefinitionId = processDefinitionId;
        return this;
    }

    public HistoricTaskInstanceQueryImpl ProcessDefinitionKey(string processDefinitionKey)
    {
        this.processDefinitionKey = processDefinitionKey;
        return this;
    }

    public HistoricTaskInstanceQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskId(string taskId)
    {
        this.taskId = taskId;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskName(string taskName)
    {
        this.taskName = taskName;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskNameLike(string taskNameLike)
    {
        this.taskNameLike = taskNameLike;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskAssignee(string taskAssignee)
    {
        this.taskAssignee = taskAssignee;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskAssigneeLike(string taskAssigneeLike)
    {
        this.taskAssigneeLike = taskAssigneeLike;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskOwner(string taskOwner)
    {
        this.taskOwner = taskOwner;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskDefinitionKey(string taskDefinitionKey)
    {
        this.taskDefinitionKey = taskDefinitionKey;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskDefinitionKeyLike(string taskDefinitionKeyLike)
    {
        this.taskDefinitionKeyLike = taskDefinitionKeyLike;
        return this;
    }

    public HistoricTaskInstanceQueryImpl CandidateUser(string candidateUser)
    {
        this.candidateUser = candidateUser;
        return this;
    }

    public HistoricTaskInstanceQueryImpl CandidateGroup(string candidateGroup)
    {
        this.candidateGroup = candidateGroup;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskPriority(int? taskPriority)
    {
        this.taskPriority = taskPriority;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskMinPriority(int? taskMinPriority)
    {
        this.taskMinPriority = taskMinPriority;
        return this;
    }

    public HistoricTaskInstanceQueryImpl TaskMaxPriority(int? taskMaxPriority)
    {
        this.taskMaxPriority = taskMaxPriority;
        return this;
    }

    public HistoricTaskInstanceQueryImpl Finished()
    {
        finished = true;
        unfinished = false;
        return this;
    }

    public HistoricTaskInstanceQueryImpl Unfinished()
    {
        unfinished = true;
        finished = false;
        return this;
    }

    public HistoricTaskInstanceQueryImpl ProcessFinished()
    {
        processFinished = true;
        processUnfinished = false;
        return this;
    }

    public HistoricTaskInstanceQueryImpl ProcessUnfinished()
    {
        processUnfinished = true;
        processFinished = false;
        return this;
    }

    public HistoricTaskInstanceQueryImpl OrderByTaskId() => OrderByProperty(HistoricTaskInstanceQueryProperty.TaskId);
    public HistoricTaskInstanceQueryImpl OrderByTaskName() => OrderByProperty(HistoricTaskInstanceQueryProperty.TaskName);
    public HistoricTaskInstanceQueryImpl OrderByTaskAssignee() => OrderByProperty(HistoricTaskInstanceQueryProperty.TaskAssignee);
    public HistoricTaskInstanceQueryImpl OrderByProcessInstanceId() => OrderByProperty(HistoricTaskInstanceQueryProperty.ProcessInstanceId);
    public HistoricTaskInstanceQueryImpl OrderByStartTime() => OrderByProperty(HistoricTaskInstanceQueryProperty.StartTime);
    public HistoricTaskInstanceQueryImpl OrderByEndTime() => OrderByProperty(HistoricTaskInstanceQueryProperty.EndTime);
    public HistoricTaskInstanceQueryImpl OrderByTaskDefinitionKey() => OrderByProperty(HistoricTaskInstanceQueryProperty.TaskDefinitionKey);

    public override Task<List<HistoricTaskInstance>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<HistoricTaskInstance?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    IHistoricTaskInstanceQuery IHistoricTaskInstanceQuery.ProcessDefinitionId(string processDefinitionId) => ProcessDefinitionId(processDefinitionId);
    IHistoricTaskInstanceQuery IHistoricTaskInstanceQuery.TaskAssignee(string assignee) => TaskAssignee(assignee);
    IHistoricTaskInstanceQuery IHistoricTaskInstanceQuery.ProcessInstanceId(string processInstanceId) => ProcessInstanceId(processInstanceId);
    IHistoricTaskInstanceQuery IHistoricTaskInstanceQuery.TaskDefinitionKey(string taskDefinitionKey) => TaskDefinitionKey(taskDefinitionKey);
    IHistoricTaskInstanceQuery IHistoricTaskInstanceQuery.Unfinished() => Unfinished();
    IHistoricTaskInstanceQuery IHistoricTaskInstanceQuery.Finished() => Finished();
    Task<List<HistoricTaskInstance>> IHistoricTaskInstanceQuery.ListAsync(CancellationToken cancellationToken) => ToListAsync(cancellationToken);
    async Task<long> IHistoricTaskInstanceQuery.CountAsync(CancellationToken cancellationToken) => await CountAsync(cancellationToken);

    private IEnumerable<HistoricTaskInstance> ApplyFilters(IEnumerable<HistoricTaskInstance>? source)
    {
        if (source == null) return Enumerable.Empty<HistoricTaskInstance>();
        var query = source.AsEnumerable();
        if (processDefinitionId != null) query = query.Where(t => t.ProcessDefinitionId == processDefinitionId);
        if (processInstanceId != null) query = query.Where(t => t.ProcessInstanceId == processInstanceId);
        if (taskId != null) query = query.Where(t => t.Id == taskId);
        if (taskName != null) query = query.Where(t => t.Name == taskName);
        if (taskNameLike != null) query = query.Where(t => t.Name != null && t.Name.Contains(taskNameLike.Replace("%", "")));
        if (taskAssignee != null) query = query.Where(t => t.Assignee == taskAssignee);
        if (taskAssigneeLike != null) query = query.Where(t => t.Assignee != null && t.Assignee.Contains(taskAssigneeLike.Replace("%", "")));
        if (taskOwner != null) query = query.Where(t => t.Owner == taskOwner);
        if (taskDefinitionKey != null) query = query.Where(t => t.TaskDefinitionKey == taskDefinitionKey);
        if (taskPriority.HasValue) query = query.Where(t => true);
        if (finished) query = query.Where(t => t.EndTime.HasValue);
        if (unfinished) query = query.Where(t => !t.EndTime.HasValue);
        return query;
    }

    private IEnumerable<HistoricTaskInstance> ApplySorting(IEnumerable<HistoricTaskInstance> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            HistoricTaskInstanceQueryProperty.StartTime => asc ? query.OrderBy(t => t.StartTime) : query.OrderByDescending(t => t.StartTime),
            HistoricTaskInstanceQueryProperty.EndTime => asc ? query.OrderBy(t => t.EndTime) : query.OrderByDescending(t => t.EndTime),
            _ => query
        };
    }
}

public class HistoricVariableInstanceQueryImpl : AbstractQuery<HistoricVariableInstanceQueryImpl, HistoricVariableInstance>, IHistoricVariableInstanceQuery
{
    protected string? id;
    protected string? taskId;
    protected HashSet<string>? taskIds;
    protected string? executionId;
    protected HashSet<string>? executionIds;
    protected string? processInstanceId;
    protected string? activityInstanceId;
    protected string? variableName;
    protected string? variableNameLike;
    protected bool excludeTaskRelated;
    protected QueryVariableValue? queryVariableValue;

    private readonly IEnumerable<HistoricVariableInstance>? _source;

    public HistoricVariableInstanceQueryImpl() { }
    public HistoricVariableInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public HistoricVariableInstanceQueryImpl(IEnumerable<HistoricVariableInstance> source) { _source = source; }

    public HistoricVariableInstanceQueryImpl Id(string id)
    {
        this.id = id;
        return this;
    }

    public HistoricVariableInstanceQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
        return this;
    }

    public HistoricVariableInstanceQueryImpl ExecutionId(string executionId)
    {
        this.executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        return this;
    }

    public HistoricVariableInstanceQueryImpl TaskId(string taskId)
    {
        this.taskId = taskId;
        return this;
    }

    public HistoricVariableInstanceQueryImpl TaskIds(HashSet<string> taskIds)
    {
        this.taskIds = taskIds;
        return this;
    }

    public HistoricVariableInstanceQueryImpl VariableName(string variableName)
    {
        this.variableName = variableName;
        return this;
    }

    public HistoricVariableInstanceQueryImpl VariableNameLike(string variableNameLike)
    {
        this.variableNameLike = variableNameLike;
        return this;
    }

    public HistoricVariableInstanceQueryImpl ExcludeTaskRelated()
    {
        excludeTaskRelated = true;
        return this;
    }

    public HistoricVariableInstanceQueryImpl OrderByProcessInstanceId() => OrderByProperty(HistoricVariableInstanceQueryProperty.ProcessInstanceId);
    public HistoricVariableInstanceQueryImpl OrderByVariableName() => OrderByProperty(HistoricVariableInstanceQueryProperty.VariableName);
    public HistoricVariableInstanceQueryImpl OrderByCreateTime() => OrderByProperty(HistoricVariableInstanceQueryProperty.CreateTime);

    public override Task<List<HistoricVariableInstance>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<HistoricVariableInstance?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    IHistoricVariableInstanceQuery IHistoricVariableInstanceQuery.ProcessInstanceId(string processInstanceId) => ProcessInstanceId(processInstanceId);
    IHistoricVariableInstanceQuery IHistoricVariableInstanceQuery.VariableName(string variableName) => VariableName(variableName);
    IHistoricVariableInstanceQuery IHistoricVariableInstanceQuery.TaskId(string taskId) => TaskId(taskId);
    Task<List<HistoricVariableInstance>> IHistoricVariableInstanceQuery.ListAsync(CancellationToken cancellationToken) => ToListAsync(cancellationToken);
    async Task<long> IHistoricVariableInstanceQuery.CountAsync(CancellationToken cancellationToken) => await CountAsync(cancellationToken);

    private IEnumerable<HistoricVariableInstance> ApplyFilters(IEnumerable<HistoricVariableInstance>? source)
    {
        if (source == null) return Enumerable.Empty<HistoricVariableInstance>();
        var query = source.AsEnumerable();
        if (id != null) query = query.Where(v => v.Id == id);
        if (processInstanceId != null) query = query.Where(v => v.ProcessInstanceId == processInstanceId);
        if (taskId != null) query = query.Where(v => v.TaskId == taskId);
        if (taskIds != null) query = query.Where(v => v.TaskId != null && taskIds.Contains(v.TaskId));
        if (executionId != null) query = query.Where(v => true);
        if (variableName != null) query = query.Where(v => v.Name == variableName);
        if (variableNameLike != null) query = query.Where(v => v.Name != null && v.Name.Contains(variableNameLike.Replace("%", "")));
        if (excludeTaskRelated) query = query.Where(v => v.TaskId == null);
        return query;
    }

    private IEnumerable<HistoricVariableInstance> ApplySorting(IEnumerable<HistoricVariableInstance> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            HistoricVariableInstanceQueryProperty.CreateTime => asc ? query.OrderBy(v => v.CreateTime) : query.OrderByDescending(v => v.CreateTime),
            _ => query
        };
    }
}

public class HistoricDetailQueryImpl : AbstractQuery<HistoricDetailQueryImpl, HistoricDetail>, IHistoricDetailQuery
{
    protected string? id;
    protected string? taskId;
    protected string? processInstanceId;
    protected string? variableName;
    protected string? executionId;
    protected string? activityId;
    protected string? activityInstanceId;
    protected string? type;
    protected bool excludeTaskRelated;
    protected bool variableUpdates;
    protected bool formProperties;

    private readonly IEnumerable<HistoricDetail>? _source;

    public HistoricDetailQueryImpl() { }
    public HistoricDetailQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public HistoricDetailQueryImpl(IEnumerable<HistoricDetail> source) { _source = source; }

    public HistoricDetailQueryImpl Id(string id)
    {
        this.id = id;
        return this;
    }

    public HistoricDetailQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId;
        return this;
    }

    public HistoricDetailQueryImpl ExecutionId(string executionId)
    {
        this.executionId = executionId;
        return this;
    }

    public HistoricDetailQueryImpl ActivityId(string activityId)
    {
        this.activityId = activityId;
        return this;
    }

    public HistoricDetailQueryImpl ActivityInstanceId(string activityInstanceId)
    {
        this.activityInstanceId = activityInstanceId;
        return this;
    }

    public HistoricDetailQueryImpl TaskId(string taskId)
    {
        this.taskId = taskId;
        return this;
    }

    public HistoricDetailQueryImpl VariableName(string variableName)
    {
        this.variableName = variableName;
        return this;
    }

    public HistoricDetailQueryImpl VariableUpdates()
    {
        variableUpdates = true;
        type = "VariableUpdate";
        return this;
    }

    public HistoricDetailQueryImpl FormProperties()
    {
        formProperties = true;
        type = "FormProperty";
        return this;
    }

    public HistoricDetailQueryImpl ExcludeTaskRelated()
    {
        excludeTaskRelated = true;
        return this;
    }

    public HistoricDetailQueryImpl OrderByProcessInstanceId() => OrderByProperty(HistoricDetailQueryProperty.ProcessInstanceId);
    public HistoricDetailQueryImpl OrderByVariableName() => OrderByProperty(HistoricDetailQueryProperty.VariableName);
    public HistoricDetailQueryImpl OrderByTime() => OrderByProperty(HistoricDetailQueryProperty.Time);

    public override Task<List<HistoricDetail>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<HistoricDetail?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    IHistoricDetailQuery IHistoricDetailQuery.ProcessInstanceId(string processInstanceId) => ProcessInstanceId(processInstanceId);
    IHistoricDetailQuery IHistoricDetailQuery.VariableName(string variableName) => VariableName(variableName);
    IHistoricDetailQuery IHistoricDetailQuery.TaskId(string taskId) => TaskId(taskId);
    Task<List<HistoricDetail>> IHistoricDetailQuery.ListAsync(CancellationToken cancellationToken) => ToListAsync(cancellationToken);
    async Task<long> IHistoricDetailQuery.CountAsync(CancellationToken cancellationToken) => await CountAsync(cancellationToken);

    private IEnumerable<HistoricDetail> ApplyFilters(IEnumerable<HistoricDetail>? source)
    {
        if (source == null) return Enumerable.Empty<HistoricDetail>();
        var query = source.AsEnumerable();
        if (id != null) query = query.Where(d => d.Id == id);
        if (processInstanceId != null) query = query.Where(d => d.ProcessInstanceId == processInstanceId);
        if (taskId != null) query = query.Where(d => d.TaskId == taskId);
        if (variableName != null) query = query.Where(d => d.VariableName == variableName);
        if (activityId != null) query = query.Where(d => d.ActivityId == activityId);
        if (type != null) query = query.Where(d => d.Type == type);
        if (excludeTaskRelated) query = query.Where(d => d.TaskId == null);
        return query;
    }

    private IEnumerable<HistoricDetail> ApplySorting(IEnumerable<HistoricDetail> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            HistoricDetailQueryProperty.Time => asc ? query.OrderBy(d => d.Time) : query.OrderByDescending(d => d.Time),
            _ => query
        };
    }
}
