using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public static class ExecutionQueryProperty
{
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string ProcessDefinitionId = "PROC_DEF_ID_";
    public const string ProcessDefinitionKey = "PROC_DEF_KEY_";
    public const string TenantId = "TENANT_ID_";
}

public class EventSubscriptionQueryValue
{
    public string EventName { get; }
    public string EventType { get; }

    public EventSubscriptionQueryValue(string eventName, string eventType)
    {
        EventName = eventName;
        EventType = eventType;
    }
}

public class ExecutionQueryImpl : AbstractVariableQuery<ExecutionQueryImpl, ExecutionRecord>
{
    protected string? processDefinitionId;
    protected string? processDefinitionKey;
    protected string? processDefinitionCategory;
    protected string? processDefinitionName;
    protected int? processDefinitionVersion;
    protected string? activityId;
    protected string? executionId;
    protected string? parentId;
    protected bool onlyChildExecutions;
    protected bool onlySubProcessExecutions;
    protected bool onlyProcessInstanceExecutions;
    protected string? processInstanceId;
    protected string? rootProcessInstanceId;
    protected List<EventSubscriptionQueryValue>? eventSubscriptions;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;
    protected DateTime? startedBefore;
    protected DateTime? startedAfter;
    protected string? startedBy;
    protected string? businessKey;
    protected HashSet<string>? processDefinitionKeys;
    protected HashSet<string>? processDefinitionIds;
    protected List<string>? involvedGroups;
    protected string? involvedUser;
    protected string? name;
    protected string? nameLike;
    protected string? deploymentId;
    protected List<string>? deploymentIds;
    protected bool isActive;

    private readonly IEnumerable<ExecutionRecord>? _source;

    public ExecutionQueryImpl() { }

    public ExecutionQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public ExecutionQueryImpl(IEnumerable<ExecutionRecord> source)
    {
        _source = source;
    }

    public ExecutionQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        this.processDefinitionId = processDefinitionId ?? throw new ArgumentNullException(nameof(processDefinitionId));
        return this;
    }

    public ExecutionQueryImpl ProcessDefinitionKey(string processDefinitionKey)
    {
        this.processDefinitionKey = processDefinitionKey ?? throw new ArgumentNullException(nameof(processDefinitionKey));
        return this;
    }

    public ExecutionQueryImpl ProcessDefinitionCategory(string processDefinitionCategory)
    {
        this.processDefinitionCategory = processDefinitionCategory ?? throw new ArgumentNullException(nameof(processDefinitionCategory));
        return this;
    }

    public ExecutionQueryImpl ProcessDefinitionName(string processDefinitionName)
    {
        this.processDefinitionName = processDefinitionName ?? throw new ArgumentNullException(nameof(processDefinitionName));
        return this;
    }

    public ExecutionQueryImpl ProcessDefinitionVersion(int? processDefinitionVersion)
    {
        this.processDefinitionVersion = processDefinitionVersion ?? throw new ArgumentNullException(nameof(processDefinitionVersion));
        return this;
    }

    public ExecutionQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
        return this;
    }

    public ExecutionQueryImpl RootProcessInstanceId(string rootProcessInstanceId)
    {
        this.rootProcessInstanceId = rootProcessInstanceId ?? throw new ArgumentNullException(nameof(rootProcessInstanceId));
        return this;
    }

    public ExecutionQueryImpl ProcessInstanceBusinessKey(string businessKey)
    {
        this.businessKey = businessKey ?? throw new ArgumentNullException(nameof(businessKey));
        return this;
    }

    public ExecutionQueryImpl ProcessDefinitionKeys(HashSet<string> processDefinitionKeys)
    {
        this.processDefinitionKeys = processDefinitionKeys ?? throw new ArgumentNullException(nameof(processDefinitionKeys));
        return this;
    }

    public ExecutionQueryImpl ExecutionId(string executionId)
    {
        this.executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        return this;
    }

    public ExecutionQueryImpl ActivityId(string activityId)
    {
        this.activityId = activityId;
        if (activityId != null) isActive = true;
        return this;
    }

    public ExecutionQueryImpl ParentId(string parentId)
    {
        this.parentId = parentId ?? throw new ArgumentNullException(nameof(parentId));
        return this;
    }

    public ExecutionQueryImpl OnlyChildExecutions()
    {
        onlyChildExecutions = true;
        return this;
    }

    public ExecutionQueryImpl OnlySubProcessExecutions()
    {
        onlySubProcessExecutions = true;
        return this;
    }

    public ExecutionQueryImpl OnlyProcessInstanceExecutions()
    {
        onlyProcessInstanceExecutions = true;
        return this;
    }

    public ExecutionQueryImpl ExecutionTenantId(string tenantId)
    {
        this.tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        return this;
    }

    public ExecutionQueryImpl ExecutionTenantIdLike(string tenantIdLike)
    {
        this.tenantIdLike = tenantIdLike ?? throw new ArgumentNullException(nameof(tenantIdLike));
        return this;
    }

    public ExecutionQueryImpl ExecutionWithoutTenantId()
    {
        withoutTenantId = true;
        return this;
    }

    public ExecutionQueryImpl SignalEventSubscription(string signalName)
    {
        return EventSubscription("signal", signalName);
    }

    public ExecutionQueryImpl MessageEventSubscriptionName(string messageName)
    {
        return EventSubscription("message", messageName);
    }

    public ExecutionQueryImpl EventSubscription(string eventType, string eventName)
    {
        eventSubscriptions ??= new List<EventSubscriptionQueryValue>();
        eventSubscriptions.Add(new EventSubscriptionQueryValue(eventName, eventType));
        return this;
    }

    public ExecutionQueryImpl ProcessVariableValueEquals(string name, object? value)
    {
        return VariableValueEquals(name, value, false);
    }

    public ExecutionQueryImpl ProcessVariableValueNotEquals(string name, object? value)
    {
        return VariableValueNotEquals(name, value, false);
    }

    public ExecutionQueryImpl ProcessVariableValueLike(string name, string? value)
    {
        return VariableValueLike(name, value, false);
    }

    public ExecutionQueryImpl StartedBefore(DateTime? beforeTime)
    {
        startedBefore = beforeTime ?? throw new ArgumentNullException(nameof(beforeTime));
        return this;
    }

    public ExecutionQueryImpl StartedAfter(DateTime? afterTime)
    {
        startedAfter = afterTime ?? throw new ArgumentNullException(nameof(afterTime));
        return this;
    }

    public ExecutionQueryImpl StartedBy(string userId)
    {
        startedBy = userId ?? throw new ArgumentNullException(nameof(userId));
        return this;
    }

    public ExecutionQueryImpl OrderByProcessInstanceId() => OrderByProperty(ExecutionQueryProperty.ProcessInstanceId);
    public ExecutionQueryImpl OrderByProcessDefinitionId() => OrderByProperty(ExecutionQueryProperty.ProcessDefinitionId);
    public ExecutionQueryImpl OrderByProcessDefinitionKey() => OrderByProperty(ExecutionQueryProperty.ProcessDefinitionKey);
    public ExecutionQueryImpl OrderByTenantId() => OrderByProperty(ExecutionQueryProperty.TenantId);

    public override Task<List<ExecutionRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<ExecutionRecord?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    private IEnumerable<ExecutionRecord> ApplyFilters(IEnumerable<ExecutionRecord>? source)
    {
        if (source == null) return Enumerable.Empty<ExecutionRecord>();
        var query = source.AsEnumerable();

        if (processDefinitionId != null) query = query.Where(e => e.ProcessDefinitionId == processDefinitionId);
        if (processDefinitionKey != null) query = query.Where(e => e.ProcessDefinitionId != null && e.ProcessDefinitionId.Contains(processDefinitionKey));
        if (processInstanceId != null) query = query.Where(e => e.ProcessInstanceId == processInstanceId);
        if (executionId != null) query = query.Where(e => e.Id == executionId);
        if (parentId != null) query = query.Where(e => e.ParentId == parentId);
        if (activityId != null) query = query.Where(e => e.CurrentActivityId == activityId);
        if (businessKey != null) query = query.Where(e => e.BusinessKey == businessKey);
        if (onlyProcessInstanceExecutions) query = query.Where(e => e.ParentId == null);
        if (onlyChildExecutions) query = query.Where(e => e.ParentId != null);
        if (tenantId != null) query = query.Where(e => false);
        if (withoutTenantId) query = query.Where(e => true);

        return query;
    }

    private IEnumerable<ExecutionRecord> ApplySorting(IEnumerable<ExecutionRecord> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            ExecutionQueryProperty.ProcessInstanceId => asc ? query.OrderBy(e => e.ProcessInstanceId) : query.OrderByDescending(e => e.ProcessInstanceId),
            ExecutionQueryProperty.ProcessDefinitionId => asc ? query.OrderBy(e => e.ProcessDefinitionId) : query.OrderByDescending(e => e.ProcessDefinitionId),
            _ => query
        };
    }
}
