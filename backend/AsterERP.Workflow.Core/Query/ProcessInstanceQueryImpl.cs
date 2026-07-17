using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public static class ProcessInstanceQueryProperty
{
    public const string ProcessInstanceId = "PROC_INST_ID_";
    public const string ProcessDefinitionId = "PROC_DEF_ID_";
    public const string ProcessDefinitionKey = "PROC_DEF_KEY_";
    public const string TenantId = "TENANT_ID_";
    public const string StartTime = "START_TIME_";
}

public class ProcessInstanceQueryImpl : AbstractVariableQuery<ProcessInstanceQueryImpl, ExecutionRecord>
{
    protected string? processInstanceId;
    protected HashSet<string>? processInstanceIds;
    protected string? businessKey;
    protected string? processDefinitionId;
    protected HashSet<string>? processDefinitionIds;
    protected string? processDefinitionCategory;
    protected string? processDefinitionName;
    protected int? processDefinitionVersion;
    protected string? processDefinitionKey;
    protected HashSet<string>? processDefinitionKeys;
    protected string? deploymentId;
    protected List<string>? deploymentIds;
    protected string? superProcessInstanceId;
    protected string? subProcessInstanceId;
    protected bool excludeSubprocesses;
    protected string? involvedUser;
    protected bool isSuspended;
    protected bool isActive;
    protected bool includeProcessVariables;
    protected int? processInstanceVariablesLimit;
    protected bool withJobException;
    protected string? name;
    protected string? nameLike;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;
    protected DateTime? startedBefore;
    protected DateTime? startedAfter;
    protected string? startedBy;
    protected string? rootProcessInstanceId;
    protected List<string>? involvedGroups;

    private readonly IEnumerable<ExecutionRecord>? _source;

    public ProcessInstanceQueryImpl() { }

    public ProcessInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public ProcessInstanceQueryImpl(IEnumerable<ExecutionRecord> source)
    {
        _source = source;
    }

    public ProcessInstanceQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessInstanceIds(HashSet<string> processInstanceIds)
    {
        this.processInstanceIds = processInstanceIds ?? throw new ArgumentNullException(nameof(processInstanceIds));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessInstanceBusinessKey(string businessKey)
    {
        this.businessKey = businessKey ?? throw new ArgumentNullException(nameof(businessKey));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessInstanceBusinessKey(string businessKey, string processDefinitionKey)
    {
        this.businessKey = businessKey ?? throw new ArgumentNullException(nameof(businessKey));
        this.processDefinitionKey = processDefinitionKey;
        return this;
    }

    public ProcessInstanceQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        this.processDefinitionId = processDefinitionId ?? throw new ArgumentNullException(nameof(processDefinitionId));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessDefinitionIds(HashSet<string> processDefinitionIds)
    {
        this.processDefinitionIds = processDefinitionIds ?? throw new ArgumentNullException(nameof(processDefinitionIds));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessDefinitionCategory(string processDefinitionCategory)
    {
        this.processDefinitionCategory = processDefinitionCategory ?? throw new ArgumentNullException(nameof(processDefinitionCategory));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessDefinitionName(string processDefinitionName)
    {
        this.processDefinitionName = processDefinitionName ?? throw new ArgumentNullException(nameof(processDefinitionName));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessDefinitionVersion(int? processDefinitionVersion)
    {
        this.processDefinitionVersion = processDefinitionVersion ?? throw new ArgumentNullException(nameof(processDefinitionVersion));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessDefinitionKey(string processDefinitionKey)
    {
        this.processDefinitionKey = processDefinitionKey ?? throw new ArgumentNullException(nameof(processDefinitionKey));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessDefinitionKeys(HashSet<string> processDefinitionKeys)
    {
        this.processDefinitionKeys = processDefinitionKeys ?? throw new ArgumentNullException(nameof(processDefinitionKeys));
        return this;
    }

    public ProcessInstanceQueryImpl DeploymentId(string deploymentId)
    {
        this.deploymentId = deploymentId;
        return this;
    }

    public ProcessInstanceQueryImpl DeploymentIdIn(List<string> deploymentIds)
    {
        this.deploymentIds = deploymentIds;
        return this;
    }

    public ProcessInstanceQueryImpl SuperProcessInstanceId(string superProcessInstanceId)
    {
        this.superProcessInstanceId = superProcessInstanceId;
        return this;
    }

    public ProcessInstanceQueryImpl SubProcessInstanceId(string subProcessInstanceId)
    {
        this.subProcessInstanceId = subProcessInstanceId;
        return this;
    }

    public ProcessInstanceQueryImpl ExcludeSubprocesses(bool excludeSubprocesses)
    {
        this.excludeSubprocesses = excludeSubprocesses;
        return this;
    }

    public ProcessInstanceQueryImpl InvolvedUser(string involvedUser)
    {
        this.involvedUser = involvedUser ?? throw new ArgumentNullException(nameof(involvedUser));
        return this;
    }

    public ProcessInstanceQueryImpl InvolvedGroupsIn(List<string> involvedGroups)
    {
        this.involvedGroups = involvedGroups ?? throw new ArgumentNullException(nameof(involvedGroups));
        return this;
    }

    public ProcessInstanceQueryImpl Active()
    {
        isActive = true;
        isSuspended = false;
        return this;
    }

    public ProcessInstanceQueryImpl Suspended()
    {
        isSuspended = true;
        isActive = false;
        return this;
    }

    public ProcessInstanceQueryImpl IncludeProcessVariables()
    {
        includeProcessVariables = true;
        return this;
    }

    public ProcessInstanceQueryImpl LimitProcessInstanceVariables(int? limit)
    {
        processInstanceVariablesLimit = limit;
        return this;
    }

    public ProcessInstanceQueryImpl WithJobException()
    {
        withJobException = true;
        return this;
    }

    public ProcessInstanceQueryImpl ProcessInstanceName(string name)
    {
        this.name = name;
        return this;
    }

    public ProcessInstanceQueryImpl ProcessInstanceNameLike(string nameLike)
    {
        this.nameLike = nameLike;
        return this;
    }

    public ProcessInstanceQueryImpl ProcessInstanceTenantId(string tenantId)
    {
        this.tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessInstanceTenantIdLike(string tenantIdLike)
    {
        this.tenantIdLike = tenantIdLike ?? throw new ArgumentNullException(nameof(tenantIdLike));
        return this;
    }

    public ProcessInstanceQueryImpl ProcessInstanceWithoutTenantId()
    {
        withoutTenantId = true;
        return this;
    }

    public ProcessInstanceQueryImpl StartedBefore(DateTime? beforeTime)
    {
        startedBefore = beforeTime;
        return this;
    }

    public ProcessInstanceQueryImpl StartedAfter(DateTime? afterTime)
    {
        startedAfter = afterTime;
        return this;
    }

    public ProcessInstanceQueryImpl StartedBy(string userId)
    {
        startedBy = userId ?? throw new ArgumentNullException(nameof(userId));
        return this;
    }

    public ProcessInstanceQueryImpl OrderByProcessInstanceId() => OrderByProperty(ProcessInstanceQueryProperty.ProcessInstanceId);
    public ProcessInstanceQueryImpl OrderByProcessDefinitionId() => OrderByProperty(ProcessInstanceQueryProperty.ProcessDefinitionId);
    public ProcessInstanceQueryImpl OrderByProcessDefinitionKey() => OrderByProperty(ProcessInstanceQueryProperty.ProcessDefinitionKey);
    public ProcessInstanceQueryImpl OrderByTenantId() => OrderByProperty(ProcessInstanceQueryProperty.TenantId);
    public ProcessInstanceQueryImpl OrderByStartTime() => OrderByProperty(ProcessInstanceQueryProperty.StartTime);

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

        if (processInstanceId != null) query = query.Where(e => e.Id == processInstanceId);
        if (processInstanceIds != null) query = query.Where(e => processInstanceIds.Contains(e.Id));
        if (businessKey != null) query = query.Where(e => e.BusinessKey == businessKey);
        if (processDefinitionId != null) query = query.Where(e => e.ProcessDefinitionId == processDefinitionId);
        if (processDefinitionIds != null) query = query.Where(e => e.ProcessDefinitionId != null && processDefinitionIds.Contains(e.ProcessDefinitionId));
        if (processDefinitionKey != null) query = query.Where(e => e.ProcessDefinitionId != null && e.ProcessDefinitionId.Contains(processDefinitionKey));
        if (processDefinitionName != null) query = query.Where(e => e.ProcessDefinitionId != null);
        if (processDefinitionVersion.HasValue) query = query.Where(e => e.ProcessDefinitionId != null);
        if (deploymentId != null) query = query.Where(e => e.ProcessDefinitionId != null);
        if (isActive) query = query.Where(e => e.IsActive);
        if (isSuspended) query = query.Where(e => !e.IsActive);
        if (startedBy != null) query = query.Where(e => true);
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
            ProcessInstanceQueryProperty.ProcessInstanceId => asc ? query.OrderBy(e => e.Id) : query.OrderByDescending(e => e.Id),
            ProcessInstanceQueryProperty.ProcessDefinitionId => asc ? query.OrderBy(e => e.ProcessDefinitionId) : query.OrderByDescending(e => e.ProcessDefinitionId),
            _ => query
        };
    }
}
