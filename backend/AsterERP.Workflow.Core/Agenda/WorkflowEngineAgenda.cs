using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Agenda;

public interface IAgenda
{
    void PlanOperation(Func<Task> operation);
    void PlanContinueProcessOperation(ExecutionEntity execution);
    void PlanContinueMultiInstanceOperation(ExecutionEntity execution);
    void PlanTakeOutgoingSequenceFlowsOperation(ExecutionEntity execution, bool evaluateConditions);
    void PlanEndExecutionOperation(ExecutionEntity execution);
    void PlanDestroyScopeOperation(ExecutionEntity execution);
    void PlanTriggerExecutionOperation(ExecutionEntity execution);
    void PlanExecuteInactiveBehaviorsOperation(IEnumerable<ExecutionEntity> involvedExecutions);
    bool IsEmpty { get; }
    Task ExecuteNextAsync(CancellationToken cancellationToken = default);
}

public class WorkflowEngineAgenda : IAgenda
{
    private readonly Queue<Func<CancellationToken, Task>> _operations = new();
    private readonly IProcessEngineConfiguration _engineConfig;

    public bool IsEmpty => _operations.Count == 0;

    public WorkflowEngineAgenda(IProcessEngineConfiguration engineConfig)
    {
        _engineConfig = engineConfig;
    }

    public void PlanOperation(Func<Task> operation)
    {
        _operations.Enqueue(_ => operation());
    }

    public void PlanContinueProcessOperation(ExecutionEntity execution)
    {
        _operations.Enqueue(token => new ContinueProcessOperation(this, execution, _engineConfig).RunAsync(token));
    }

    public void PlanContinueMultiInstanceOperation(ExecutionEntity execution)
    {
        _operations.Enqueue(token => new ContinueMultiInstanceOperation(this, execution, _engineConfig).RunAsync(token));
    }

    public void PlanTakeOutgoingSequenceFlowsOperation(ExecutionEntity execution, bool evaluateConditions)
    {
        _operations.Enqueue(token => new TakeOutgoingSequenceFlowsOperation(this, execution, evaluateConditions, _engineConfig).RunAsync(token));
    }

    public void PlanEndExecutionOperation(ExecutionEntity execution)
    {
        _operations.Enqueue(token => new EndExecutionOperation(this, execution, _engineConfig).RunAsync(token));
    }

    public void PlanDestroyScopeOperation(ExecutionEntity execution)
    {
        _operations.Enqueue(token => new DestroyScopeOperation(this, execution, _engineConfig).RunAsync(token));
    }

    public void PlanTriggerExecutionOperation(ExecutionEntity execution)
    {
        _operations.Enqueue(token => new TriggerExecutionOperation(this, execution, _engineConfig).RunAsync(token));
    }

    public void PlanExecuteInactiveBehaviorsOperation(IEnumerable<ExecutionEntity> involvedExecutions)
    {
        _operations.Enqueue(token => new ExecuteInactiveBehaviorsOperation(this, involvedExecutions, _engineConfig).RunAsync(token));
    }

    public async Task ExecuteNextAsync(CancellationToken cancellationToken = default)
    {
        if (_operations.Count == 0)
            throw new InvalidOperationException("Agenda is empty");

        cancellationToken.ThrowIfCancellationRequested();

        var operation = _operations.Dequeue();
        await operation(cancellationToken);
    }
}

public class WorkflowEngineAgendaFactory
{
    private readonly IProcessEngineConfiguration _engineConfig;

    public WorkflowEngineAgendaFactory(IProcessEngineConfiguration engineConfig)
    {
        _engineConfig = engineConfig;
    }

    public virtual IAgenda CreateAgenda()
    {
        return new WorkflowEngineAgenda(_engineConfig);
    }
}
