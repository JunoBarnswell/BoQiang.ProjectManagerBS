using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public record EventSubscriptionRecord
{
    public string Id { get; init; } = null!;
    public string? EventType { get; init; }
    public string? EventName { get; init; }
    public string? ExecutionId { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? ActivityId { get; init; }
    public string? ProcessDefinitionId { get; init; }
    public string? Configuration { get; init; }
    public string? TenantId { get; init; }
    public DateTime? Created { get; init; }
}

public static class EventSubscriptionQueryProperty
{
    public const string Created = "CREATED_";
    public const string EventName = "EVENT_NAME_";
    public const string EventType = "EVENT_TYPE_";
    public const string ExecutionId = "EXECUTION_ID_";
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string ActivityId = "ACT_ID_";
    public const string TenantId = "TENANT_ID_";
}

public class EventSubscriptionQueryImpl : AbstractQuery<EventSubscriptionQueryImpl, EventSubscriptionRecord>
{
    protected string? eventSubscriptionId;
    protected string? eventName;
    protected string? eventType;
    protected string? executionId;
    protected string? processInstanceId;
    protected string? activityId;
    protected string? processDefinitionId;
    protected string? tenantId;
    protected string? configuration;

    private readonly IEnumerable<EventSubscriptionRecord>? _source;

    public EventSubscriptionQueryImpl() { }

    public EventSubscriptionQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public EventSubscriptionQueryImpl(IEnumerable<EventSubscriptionRecord> source)
    {
        _source = source;
    }

    public EventSubscriptionQueryImpl EventSubscriptionId(string eventSubscriptionId)
    {
        this.eventSubscriptionId = eventSubscriptionId ?? throw new ArgumentNullException(nameof(eventSubscriptionId));
        return this;
    }

    public EventSubscriptionQueryImpl EventName(string eventName)
    {
        this.eventName = eventName ?? throw new ArgumentNullException(nameof(eventName));
        return this;
    }

    public EventSubscriptionQueryImpl EventType(string eventType)
    {
        this.eventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        return this;
    }

    public EventSubscriptionQueryImpl ExecutionId(string executionId)
    {
        this.executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        return this;
    }

    public EventSubscriptionQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
        return this;
    }

    public EventSubscriptionQueryImpl ActivityId(string activityId)
    {
        this.activityId = activityId ?? throw new ArgumentNullException(nameof(activityId));
        return this;
    }

    public EventSubscriptionQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        this.processDefinitionId = processDefinitionId;
        return this;
    }

    public EventSubscriptionQueryImpl TenantId(string tenantId)
    {
        this.tenantId = tenantId;
        return this;
    }

    public EventSubscriptionQueryImpl Configuration(string configuration)
    {
        this.configuration = configuration;
        return this;
    }

    public EventSubscriptionQueryImpl OrderByCreated() => OrderByProperty(EventSubscriptionQueryProperty.Created);
    public EventSubscriptionQueryImpl OrderByEventName() => OrderByProperty(EventSubscriptionQueryProperty.EventName);
    public EventSubscriptionQueryImpl OrderByEventType() => OrderByProperty(EventSubscriptionQueryProperty.EventType);
    public EventSubscriptionQueryImpl OrderByExecutionId() => OrderByProperty(EventSubscriptionQueryProperty.ExecutionId);
    public EventSubscriptionQueryImpl OrderByProcessInstanceId() => OrderByProperty(EventSubscriptionQueryProperty.ProcessInstanceId);

    public override Task<List<EventSubscriptionRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<EventSubscriptionRecord?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    private IEnumerable<EventSubscriptionRecord> ApplyFilters(IEnumerable<EventSubscriptionRecord>? source)
    {
        if (source == null) return Enumerable.Empty<EventSubscriptionRecord>();
        var query = source.AsEnumerable();

        if (eventSubscriptionId != null) query = query.Where(e => e.Id == eventSubscriptionId);
        if (eventName != null) query = query.Where(e => e.EventName == eventName);
        if (eventType != null) query = query.Where(e => e.EventType == eventType);
        if (executionId != null) query = query.Where(e => e.ExecutionId == executionId);
        if (processInstanceId != null) query = query.Where(e => e.ProcessInstanceId == processInstanceId);
        if (activityId != null) query = query.Where(e => e.ActivityId == activityId);
        if (processDefinitionId != null) query = query.Where(e => e.ProcessDefinitionId == processDefinitionId);
        if (tenantId != null) query = query.Where(e => e.TenantId == tenantId);
        if (configuration != null) query = query.Where(e => e.Configuration == configuration);

        return query;
    }

    private IEnumerable<EventSubscriptionRecord> ApplySorting(IEnumerable<EventSubscriptionRecord> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            EventSubscriptionQueryProperty.Created => asc ? query.OrderBy(e => e.Created) : query.OrderByDescending(e => e.Created),
            EventSubscriptionQueryProperty.EventName => asc ? query.OrderBy(e => e.EventName) : query.OrderByDescending(e => e.EventName),
            EventSubscriptionQueryProperty.EventType => asc ? query.OrderBy(e => e.EventType) : query.OrderByDescending(e => e.EventType),
            _ => query
        };
    }
}
