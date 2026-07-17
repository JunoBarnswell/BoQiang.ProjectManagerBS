using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public class NativeTaskQueryImpl : AbstractNativeQuery<NativeTaskQueryImpl, TaskImplementation>
{
    private readonly IEnumerable<TaskImplementation>? _source;

    public NativeTaskQueryImpl() { }
    public NativeTaskQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeTaskQueryImpl(IEnumerable<TaskImplementation> source) { _source = source; }

    public override Task<List<TaskImplementation>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<TaskImplementation>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<TaskImplementation?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<TaskImplementation?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeProcessInstanceQueryImpl : AbstractNativeQuery<NativeProcessInstanceQueryImpl, ExecutionRecord>
{
    private readonly IEnumerable<ExecutionRecord>? _source;

    public NativeProcessInstanceQueryImpl() { }
    public NativeProcessInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeProcessInstanceQueryImpl(IEnumerable<ExecutionRecord> source) { _source = source; }

    public override Task<List<ExecutionRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<ExecutionRecord>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<ExecutionRecord?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<ExecutionRecord?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeProcessDefinitionQueryImpl : AbstractNativeQuery<NativeProcessDefinitionQueryImpl, ProcessDefinitionRecord>
{
    private readonly IEnumerable<ProcessDefinitionRecord>? _source;

    public NativeProcessDefinitionQueryImpl() { }
    public NativeProcessDefinitionQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeProcessDefinitionQueryImpl(IEnumerable<ProcessDefinitionRecord> source) { _source = source; }

    public override Task<List<ProcessDefinitionRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<ProcessDefinitionRecord>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<ProcessDefinitionRecord?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<ProcessDefinitionRecord?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeDeploymentQueryImpl : AbstractNativeQuery<NativeDeploymentQueryImpl, DeploymentRecord>
{
    private readonly IEnumerable<DeploymentRecord>? _source;

    public NativeDeploymentQueryImpl() { }
    public NativeDeploymentQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeDeploymentQueryImpl(IEnumerable<DeploymentRecord> source) { _source = source; }

    public override Task<List<DeploymentRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<DeploymentRecord>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<DeploymentRecord?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<DeploymentRecord?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeModelQueryImpl : AbstractNativeQuery<NativeModelQueryImpl, ModelRecord>
{
    private readonly IEnumerable<ModelRecord>? _source;

    public NativeModelQueryImpl() { }
    public NativeModelQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeModelQueryImpl(IEnumerable<ModelRecord> source) { _source = source; }

    public override Task<List<ModelRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<ModelRecord>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<ModelRecord?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<ModelRecord?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeExecutionQueryImpl : AbstractNativeQuery<NativeExecutionQueryImpl, ExecutionRecord>
{
    private readonly IEnumerable<ExecutionRecord>? _source;

    public NativeExecutionQueryImpl() { }
    public NativeExecutionQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeExecutionQueryImpl(IEnumerable<ExecutionRecord> source) { _source = source; }

    public override Task<List<ExecutionRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<ExecutionRecord>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<ExecutionRecord?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<ExecutionRecord?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeHistoricProcessInstanceQueryImpl : AbstractNativeQuery<NativeHistoricProcessInstanceQueryImpl, HistoricProcessInstance>
{
    private readonly IEnumerable<HistoricProcessInstance>? _source;

    public NativeHistoricProcessInstanceQueryImpl() { }
    public NativeHistoricProcessInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeHistoricProcessInstanceQueryImpl(IEnumerable<HistoricProcessInstance> source) { _source = source; }

    public override Task<List<HistoricProcessInstance>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<HistoricProcessInstance>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<HistoricProcessInstance?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<HistoricProcessInstance?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeHistoricTaskInstanceQueryImpl : AbstractNativeQuery<NativeHistoricTaskInstanceQueryImpl, HistoricTaskInstance>
{
    private readonly IEnumerable<HistoricTaskInstance>? _source;

    public NativeHistoricTaskInstanceQueryImpl() { }
    public NativeHistoricTaskInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeHistoricTaskInstanceQueryImpl(IEnumerable<HistoricTaskInstance> source) { _source = source; }

    public override Task<List<HistoricTaskInstance>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<HistoricTaskInstance>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<HistoricTaskInstance?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<HistoricTaskInstance?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeHistoricActivityInstanceQueryImpl : AbstractNativeQuery<NativeHistoricActivityInstanceQueryImpl, HistoricActivityInstance>
{
    private readonly IEnumerable<HistoricActivityInstance>? _source;

    public NativeHistoricActivityInstanceQueryImpl() { }
    public NativeHistoricActivityInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeHistoricActivityInstanceQueryImpl(IEnumerable<HistoricActivityInstance> source) { _source = source; }

    public override Task<List<HistoricActivityInstance>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<HistoricActivityInstance>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<HistoricActivityInstance?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<HistoricActivityInstance?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeHistoricVariableInstanceQueryImpl : AbstractNativeQuery<NativeHistoricVariableInstanceQueryImpl, HistoricVariableInstance>
{
    private readonly IEnumerable<HistoricVariableInstance>? _source;

    public NativeHistoricVariableInstanceQueryImpl() { }
    public NativeHistoricVariableInstanceQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeHistoricVariableInstanceQueryImpl(IEnumerable<HistoricVariableInstance> source) { _source = source; }

    public override Task<List<HistoricVariableInstance>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<HistoricVariableInstance>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<HistoricVariableInstance?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<HistoricVariableInstance?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}

public class NativeHistoricDetailQueryImpl : AbstractNativeQuery<NativeHistoricDetailQueryImpl, HistoricDetail>
{
    private readonly IEnumerable<HistoricDetail>? _source;

    public NativeHistoricDetailQueryImpl() { }
    public NativeHistoricDetailQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }
    public NativeHistoricDetailQueryImpl(IEnumerable<HistoricDetail> source) { _source = source; }

    public override Task<List<HistoricDetail>> ToListAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(new List<HistoricDetail>());
        var result = _source.ToList();
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value).ToList();
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value).ToList();
        return Task.FromResult(result);
    }

    public override Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult(0L);
        return Task.FromResult((long)_source.Count());
    }

    public override Task<HistoricDetail?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_source == null || SqlStatement == null) return Task.FromResult<HistoricDetail?>(null);
        return Task.FromResult(_source.FirstOrDefault());
    }
}
