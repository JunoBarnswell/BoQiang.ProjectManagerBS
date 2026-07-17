using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Helper;

public class ClassDelegate : FlowNodeActivityBehavior, IExecutionListener, ITaskListener
{
    private readonly IServiceProvider? _serviceProvider;
    private IWorkflowDelegate? _activityDelegate;
    private IExecutionListener? _executionListener;
    private ITaskListener? _taskListener;

    public string? ServiceTaskId { get; }
    public string ClassName { get; }
    public List<FieldDeclaration> FieldDeclarations { get; }
    public string? SkipExpression { get; }
    public List<BpmnModelNs.MapExceptionEntry> MapExceptions { get; }

    public ClassDelegate(
        string className,
        List<FieldDeclaration>? fieldDeclarations = null,
        IServiceProvider? serviceProvider = null)
        : this(null, className, fieldDeclarations, null, null, serviceProvider)
    {
    }

    public ClassDelegate(
        string? id,
        string className,
        List<FieldDeclaration>? fieldDeclarations,
        string? skipExpression,
        List<BpmnModelNs.MapExceptionEntry>? mapExceptions,
        IServiceProvider? serviceProvider = null)
    {
        ServiceTaskId = id;
        ClassName = className;
        FieldDeclarations = fieldDeclarations ?? new List<FieldDeclaration>();
        SkipExpression = skipExpression;
        MapExceptions = mapExceptions ?? new List<BpmnModelNs.MapExceptionEntry>();
        _serviceProvider = serviceProvider;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        _activityDelegate ??= CreateDelegate<IWorkflowDelegate>();
        await _activityDelegate.ExecuteAsync(new DelegateExecution(execution));
        await LeaveAsync(execution, cancellationToken);
    }

    public async Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        _executionListener ??= ResolveExecutionListener();
        await _executionListener.NotifyAsync(execution, cancellationToken);
    }

    public async Task NotifyAsync(IDelegateTask delegateTask, CancellationToken cancellationToken = default)
    {
        _taskListener ??= CreateDelegate<ITaskListener>();
        await _taskListener.NotifyAsync(delegateTask, cancellationToken);
    }

    private IExecutionListener ResolveExecutionListener()
    {
        var instance = CreateInstance();
        if (instance is IExecutionListener executionListener)
            return executionListener;

        if (instance is IWorkflowDelegate workflowDelegate)
            return new WorkflowDelegateExecutionListener(workflowDelegate);

        throw new WorkflowEngineArgumentException(
            $"Class '{ClassName}' does not implement {typeof(IExecutionListener).FullName} or {typeof(IWorkflowDelegate).FullName}");
    }

    private T CreateDelegate<T>() where T : class
    {
        var instance = CreateInstance();
        if (instance is T typed)
            return typed;

        throw new WorkflowEngineArgumentException(
            $"Class '{ClassName}' does not implement {typeof(T).FullName}");
    }

    private object CreateInstance()
    {
        var instance = ClassDelegateUtil.Instantiate(ClassName, _serviceProvider);
        ApplyFieldDeclarations(instance);
        return instance;
    }

    private void ApplyFieldDeclarations(object instance)
    {
        foreach (var declaration in FieldDeclarations)
        {
            var property = instance.GetType().GetProperty(declaration.Name);
            if (property?.CanWrite == true)
            {
                property.SetValue(instance, declaration.Value);
                continue;
            }

            var field = instance.GetType().GetField(declaration.Name);
            field?.SetValue(instance, declaration.Value);
        }
    }

    private sealed class WorkflowDelegateExecutionListener : IExecutionListener
    {
        private readonly IWorkflowDelegate _delegate;

        public WorkflowDelegateExecutionListener(IWorkflowDelegate @delegate)
        {
            _delegate = @delegate;
        }

        public Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
        {
            return _delegate.ExecuteAsync(execution);
        }
    }
}
