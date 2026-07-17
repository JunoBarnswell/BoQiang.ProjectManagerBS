using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Helper;

public class DelegateExpressionWorkflowEventListener : BaseDelegateEventListener
{
    private readonly IExpressionManager? _expressionManager;
    private readonly IServiceProvider? _serviceProvider;
    private bool _failOnException;

    public string DelegateExpression { get; }

    public DelegateExpressionWorkflowEventListener(
        string delegateExpression,
        Type? entityType = null,
        IExpressionManager? expressionManager = null,
        IServiceProvider? serviceProvider = null)
    {
        DelegateExpression = delegateExpression;
        EntityType = entityType;
        _expressionManager = expressionManager;
        _serviceProvider = serviceProvider;
    }

    public override bool IsFailOnException => _failOnException;

    public override void OnEvent(IWorkflowEvent @event)
    {
        if (!IsValidEvent(@event))
            return;

        var listener = DelegateExpressionUtil.ResolveDelegateExpression<IWorkflowEventListener>(
            DelegateExpression,
            _expressionManager,
            serviceProvider: _serviceProvider);

        _failOnException = listener.IsFailOnException;
        listener.OnEvent(@event);
    }
}
