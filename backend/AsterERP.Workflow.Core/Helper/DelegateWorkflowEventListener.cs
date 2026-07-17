using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Helper;

public class DelegateWorkflowEventListener : BaseDelegateEventListener
{
    private readonly IServiceProvider? _serviceProvider;
    private IWorkflowEventListener? _delegateInstance;
    private bool _failOnException;

    public string ClassName { get; }

    public DelegateWorkflowEventListener(string className, Type? entityType = null, IServiceProvider? serviceProvider = null)
    {
        ClassName = className;
        EntityType = entityType;
        _serviceProvider = serviceProvider;
    }

    public override bool IsFailOnException => _delegateInstance?.IsFailOnException ?? _failOnException;

    public override void OnEvent(IWorkflowEvent @event)
    {
        if (IsValidEvent(@event))
            GetDelegateInstance().OnEvent(@event);
    }

    private IWorkflowEventListener GetDelegateInstance()
    {
        if (_delegateInstance != null)
            return _delegateInstance;

        var instance = ClassDelegateUtil.Instantiate(ClassName, _serviceProvider);
        if (instance is IWorkflowEventListener listener)
        {
            _delegateInstance = listener;
            return listener;
        }

        _failOnException = true;
        throw new WorkflowEngineArgumentException(
            $"Class '{ClassName}' does not implement {typeof(IWorkflowEventListener).FullName}");
    }
}
