using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public static class JobQueryProperty
{
    public const string JobId = "ID_";
    public const string DueDate = "DUEDATE_";
    public const string ExecutionId = "EXECUTION_ID_";
    public const string ProcessInstanceId = "PROCESS_INST_ID_";
    public const string ProcessDefinitionId = "PROC_DEF_ID_";
    public const string Retries = "RETRIES_";
    public const string TenantId = "TENANT_ID_";
    public const string CreateTime = "CREATE_TIME_";
    public const string HandlerType = "HANDLER_TYPE_";
}

public class JobQueryImpl : AbstractQuery<JobQueryImpl, JobRecord>
{
    protected string? id;
    protected string? processInstanceId;
    protected string? executionId;
    protected string? processDefinitionId;
    protected bool retriesLeft;
    protected bool executable;
    protected bool onlyTimers;
    protected bool onlyMessages;
    protected DateTime? duedateHigherThan;
    protected DateTime? duedateLowerThan;
    protected DateTime? duedateHigherThanOrEqual;
    protected DateTime? duedateLowerThanOrEqual;
    protected bool withException;
    protected string? exceptionMessage;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;
    protected bool noRetriesLeft;
    protected bool onlyLocked;
    protected bool onlyUnlocked;

    private readonly IEnumerable<JobRecord>? _source;

    public JobQueryImpl() { }

    public JobQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public JobQueryImpl(IEnumerable<JobRecord> source)
    {
        _source = source;
    }

    public JobQueryImpl JobId(string jobId)
    {
        id = jobId ?? throw new ArgumentNullException(nameof(jobId));
        return this;
    }

    public JobQueryImpl ProcessInstanceId(string processInstanceId)
    {
        this.processInstanceId = processInstanceId ?? throw new ArgumentNullException(nameof(processInstanceId));
        return this;
    }

    public JobQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        this.processDefinitionId = processDefinitionId ?? throw new ArgumentNullException(nameof(processDefinitionId));
        return this;
    }

    public JobQueryImpl ExecutionId(string executionId)
    {
        this.executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        return this;
    }

    public JobQueryImpl WithRetriesLeft()
    {
        retriesLeft = true;
        return this;
    }

    public JobQueryImpl Executable()
    {
        executable = true;
        return this;
    }

    public JobQueryImpl Timers()
    {
        if (onlyMessages) throw new InvalidOperationException("Cannot combine onlyTimers() with onlyMessages()");
        onlyTimers = true;
        return this;
    }

    public JobQueryImpl Messages()
    {
        if (onlyTimers) throw new InvalidOperationException("Cannot combine onlyTimers() with onlyMessages()");
        onlyMessages = true;
        return this;
    }

    public JobQueryImpl DueDateHigherThan(DateTime? date)
    {
        duedateHigherThan = date ?? throw new ArgumentNullException(nameof(date));
        return this;
    }

    public JobQueryImpl DueDateLowerThan(DateTime? date)
    {
        duedateLowerThan = date ?? throw new ArgumentNullException(nameof(date));
        return this;
    }

    public JobQueryImpl DueDateHigherThanOrEqual(DateTime? date)
    {
        duedateHigherThanOrEqual = date ?? throw new ArgumentNullException(nameof(date));
        return this;
    }

    public JobQueryImpl DueDateLowerThanOrEqual(DateTime? date)
    {
        duedateLowerThanOrEqual = date ?? throw new ArgumentNullException(nameof(date));
        return this;
    }

    public JobQueryImpl NoRetriesLeft()
    {
        noRetriesLeft = true;
        return this;
    }

    public JobQueryImpl WithException()
    {
        withException = true;
        return this;
    }

    public JobQueryImpl ExceptionMessage(string exceptionMessage)
    {
        this.exceptionMessage = exceptionMessage ?? throw new ArgumentNullException(nameof(exceptionMessage));
        return this;
    }

    public JobQueryImpl JobTenantId(string tenantId)
    {
        this.tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        return this;
    }

    public JobQueryImpl JobTenantIdLike(string tenantIdLike)
    {
        this.tenantIdLike = tenantIdLike ?? throw new ArgumentNullException(nameof(tenantIdLike));
        return this;
    }

    public JobQueryImpl JobWithoutTenantId()
    {
        withoutTenantId = true;
        return this;
    }

    public JobQueryImpl Locked()
    {
        onlyLocked = true;
        return this;
    }

    public JobQueryImpl Unlocked()
    {
        onlyUnlocked = true;
        return this;
    }

    public JobQueryImpl OrderByJobDueDate() => OrderByProperty(JobQueryProperty.DueDate);
    public JobQueryImpl OrderByExecutionId() => OrderByProperty(JobQueryProperty.ExecutionId);
    public JobQueryImpl OrderByJobId() => OrderByProperty(JobQueryProperty.JobId);
    public JobQueryImpl OrderByProcessInstanceId() => OrderByProperty(JobQueryProperty.ProcessInstanceId);
    public JobQueryImpl OrderByJobRetries() => OrderByProperty(JobQueryProperty.Retries);
    public JobQueryImpl OrderByTenantId() => OrderByProperty(JobQueryProperty.TenantId);

    public override Task<List<JobRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<JobRecord?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    protected IEnumerable<JobRecord> ApplyFilters(IEnumerable<JobRecord>? source)
    {
        if (source == null) return Enumerable.Empty<JobRecord>();
        var query = source.AsEnumerable();

        if (id != null) query = query.Where(j => j.Id == id);
        if (processInstanceId != null) query = query.Where(j => j.ProcessInstanceId == processInstanceId);
        if (executionId != null) query = query.Where(j => j.ExecutionId == executionId);
        if (processDefinitionId != null) query = query.Where(j => j.ProcessDefinitionId == processDefinitionId);
        if (retriesLeft) query = query.Where(j => j.Retries > 0);
        if (noRetriesLeft) query = query.Where(j => j.Retries == 0);
        if (withException) query = query.Where(j => j.ExceptionMessage != null);
        if (exceptionMessage != null) query = query.Where(j => j.ExceptionMessage == exceptionMessage);
        if (duedateHigherThan.HasValue) query = query.Where(j => j.DueDate > duedateHigherThan);
        if (duedateLowerThan.HasValue) query = query.Where(j => j.DueDate < duedateLowerThan);
        if (duedateHigherThanOrEqual.HasValue) query = query.Where(j => j.DueDate >= duedateHigherThanOrEqual);
        if (duedateLowerThanOrEqual.HasValue) query = query.Where(j => j.DueDate <= duedateLowerThanOrEqual);
        if (tenantId != null) query = query.Where(j => j.TenantId == tenantId);
        if (tenantIdLike != null) query = query.Where(j => j.TenantId != null && j.TenantId.Contains(tenantIdLike.Replace("%", "")));
        if (withoutTenantId) query = query.Where(j => j.TenantId == null);

        return query;
    }

    protected IEnumerable<JobRecord> ApplySorting(IEnumerable<JobRecord> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            JobQueryProperty.JobId => asc ? query.OrderBy(j => j.Id) : query.OrderByDescending(j => j.Id),
            JobQueryProperty.DueDate => asc ? query.OrderBy(j => j.DueDate) : query.OrderByDescending(j => j.DueDate),
            JobQueryProperty.ExecutionId => asc ? query.OrderBy(j => j.ExecutionId) : query.OrderByDescending(j => j.ExecutionId),
            JobQueryProperty.ProcessInstanceId => asc ? query.OrderBy(j => j.ProcessInstanceId) : query.OrderByDescending(j => j.ProcessInstanceId),
            JobQueryProperty.Retries => asc ? query.OrderBy(j => j.Retries) : query.OrderByDescending(j => j.Retries),
            _ => query
        };
    }
}

public class TimerJobQueryImpl : JobQueryImpl
{
    public TimerJobQueryImpl() { }
    public TimerJobQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public TimerJobQueryImpl(IEnumerable<JobRecord> source) : base(source) { }
}

public class SuspendedJobQueryImpl : JobQueryImpl
{
    public SuspendedJobQueryImpl() { }
    public SuspendedJobQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public SuspendedJobQueryImpl(IEnumerable<JobRecord> source) : base(source) { }
}

public class DeadLetterJobQueryImpl : JobQueryImpl
{
    public DeadLetterJobQueryImpl() { }
    public DeadLetterJobQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public DeadLetterJobQueryImpl(IEnumerable<JobRecord> source) : base(source) { }
}
