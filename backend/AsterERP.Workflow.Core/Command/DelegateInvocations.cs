using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Command;

public class ActivityBehaviorInvocation : DelegateInvocation
{
    private readonly IBpmnActivityBehavior _activityBehavior;
    private readonly ExecutionEntity _execution;
    private readonly CancellationToken _cancellationToken;

    public ActivityBehaviorInvocation(IBpmnActivityBehavior activityBehavior, ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        _activityBehavior = activityBehavior ?? throw new ArgumentNullException(nameof(activityBehavior));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _cancellationToken = cancellationToken;
    }

    public override void Proceed()
    {
        throw new NotSupportedException("ActivityBehaviorInvocation is async-only. Use ProceedAsync.");
    }

    public override async Task ProceedAsync()
    {
        await _activityBehavior.ExecuteAsync(_execution, _cancellationToken);
        Result = _execution;
    }
}

public abstract class ExpressionInvocation : DelegateInvocation
{
    protected readonly IExpression _expression;
    protected readonly IVariableScope _variableScope;

    public ExpressionInvocation(IExpression expression, IVariableScope variableScope)
    {
        _expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _variableScope = variableScope ?? throw new ArgumentNullException(nameof(variableScope));
    }
}

public class ExpressionGetInvocation : ExpressionInvocation
{
    public ExpressionGetInvocation(IExpression expression, IVariableScope variableScope) : base(expression, variableScope)
    {
    }

    public override void Proceed()
    {
        Result = _expression.GetValue(_variableScope);
    }

    public override Task ProceedAsync()
    {
        Result = _expression.GetValue(_variableScope);
        return Task.CompletedTask;
    }
}

public class ExpressionSetInvocation : ExpressionInvocation
{
    private readonly object? _value;

    public ExpressionSetInvocation(IExpression expression, IVariableScope variableScope, object? value) : base(expression, variableScope)
    {
        _value = value;
    }

    public override void Proceed()
    {
        _expression.SetValue(_variableScope, _value);
        Result = _value;
    }

    public override Task ProceedAsync()
    {
        _expression.SetValue(_variableScope, _value);
        Result = _value;
        return Task.CompletedTask;
    }
}

public class JavaDelegateInvocation : DelegateInvocation
{
    private readonly IWorkflowDelegate _delegate;
    private readonly IDelegateExecution _execution;
    private readonly CancellationToken _cancellationToken;

    public JavaDelegateInvocation(IWorkflowDelegate @delegate, IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        _delegate = @delegate ?? throw new ArgumentNullException(nameof(@delegate));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _cancellationToken = cancellationToken;
    }

    public override void Proceed()
    {
        throw new NotSupportedException("JavaDelegateInvocation is async-only. Use ProceedAsync.");
    }

    public override async Task ProceedAsync()
    {
        await _delegate.ExecuteAsync(_execution);
        Result = _execution;
    }
}

public class ExecutionListenerInvocation : DelegateInvocation
{
    private readonly IExecutionListener _executionListener;
    private readonly IDelegateExecution _execution;
    private readonly CancellationToken _cancellationToken;

    public ExecutionListenerInvocation(IExecutionListener executionListener, IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        _executionListener = executionListener ?? throw new ArgumentNullException(nameof(executionListener));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _cancellationToken = cancellationToken;
    }

    public override void Proceed()
    {
        throw new NotSupportedException("ExecutionListenerInvocation is async-only. Use ProceedAsync.");
    }

    public override async Task ProceedAsync()
    {
        await _executionListener.NotifyAsync(_execution, _cancellationToken);
        Result = _execution;
    }
}

public class TaskListenerInvocation : DelegateInvocation
{
    private readonly ITaskListener _taskListener;
    private readonly IDelegateTask _delegateTask;
    private readonly CancellationToken _cancellationToken;

    public TaskListenerInvocation(ITaskListener taskListener, IDelegateTask delegateTask, CancellationToken cancellationToken = default)
    {
        _taskListener = taskListener ?? throw new ArgumentNullException(nameof(taskListener));
        _delegateTask = delegateTask ?? throw new ArgumentNullException(nameof(delegateTask));
        _cancellationToken = cancellationToken;
    }

    public override void Proceed()
    {
        throw new NotSupportedException("TaskListenerInvocation is async-only. Use ProceedAsync.");
    }

    public override async Task ProceedAsync()
    {
        await _taskListener.NotifyAsync(_delegateTask, _cancellationToken);
        Result = _delegateTask;
    }
}

public class ThrowMessageDelegateInvocation : DelegateInvocation
{
    private readonly IThrowMessageDelegate _throwMessageDelegate;
    private readonly IDelegateExecution _execution;
    private readonly ThrowMessage _message;
    private readonly CancellationToken _cancellationToken;

    public ThrowMessageDelegateInvocation(
        IThrowMessageDelegate throwMessageDelegate,
        IDelegateExecution execution,
        ThrowMessage message,
        CancellationToken cancellationToken = default)
    {
        _throwMessageDelegate = throwMessageDelegate ?? throw new ArgumentNullException(nameof(throwMessageDelegate));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _cancellationToken = cancellationToken;
    }

    public override void Proceed()
    {
        throw new NotSupportedException("ThrowMessageDelegateInvocation is async-only. Use ProceedAsync.");
    }

    public override async Task ProceedAsync()
    {
        Result = await _throwMessageDelegate.SendAsync(_execution, _message, _cancellationToken);
    }
}
