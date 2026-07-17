using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Behavior;

public class ConditionalBoundaryEventActivityBehavior : BoundaryEventActivityBehavior
{
    public string? ConditionExpression { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }

    public ConditionalBoundaryEventActivityBehavior() { }

    public ConditionalBoundaryEventActivityBehavior(
        string? conditionExpression,
        bool interrupting,
        IExpressionManager? expressionManager = null)
        : base(interrupting)
    {
        ConditionExpression = conditionExpression;
        ExpressionManager = expressionManager;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_conditionalBoundarySubscription", true);
        execution.SetVariableLocal("_conditionalBoundaryActive", true);
        if (!string.IsNullOrEmpty(ConditionExpression))
        {
            execution.SetVariableLocal("_conditionalExpression", ConditionExpression);
        }

        if (IsConditionSatisfied(execution))
        {
            await EvaluateConditionAsync(execution, cancellationToken);
        }
    }

    public virtual async Task EvaluateConditionAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (IsConditionSatisfied(execution))
        {
            execution.SetVariableLocal("_conditionalBoundaryActive", false);
            execution.SetVariable("_conditionSatisfied", true);

            if (Interrupting)
            {
                await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
            }
            else
            {
                await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
            }
        }
    }

    protected bool IsConditionSatisfied(ExecutionEntity execution)
    {
        if (string.IsNullOrEmpty(ConditionExpression) || ExpressionManager == null)
        {
            return false;
        }

        var result = ExpressionManager.Evaluate(ConditionExpression, execution.Variables);
        if (result is bool boolValue) return boolValue;

        return false;
    }
}

public class ConditionalIntermediateCatchEventActivityBehavior : FlowNodeActivityBehavior
{
    public string? ConditionExpression { get; set; }
    protected IExpressionManager? ExpressionManager { get; set; }

    public ConditionalIntermediateCatchEventActivityBehavior() { }

    public ConditionalIntermediateCatchEventActivityBehavior(
        string? conditionExpression,
        IExpressionManager? expressionManager = null)
    {
        ConditionExpression = conditionExpression;
        ExpressionManager = expressionManager;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_conditionalCatchWaiting", true);
        if (!string.IsNullOrEmpty(ConditionExpression))
        {
            execution.SetVariableLocal("_conditionalExpression", ConditionExpression);
        }

        if (IsConditionSatisfied(execution))
        {
            await EvaluateConditionAsync(execution, cancellationToken);
        }
        else
        {
            execution.IsActive = false;
        }
    }

    public virtual async Task EvaluateConditionAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (IsConditionSatisfied(execution))
        {
            execution.SetVariableLocal("_conditionalCatchWaiting", false);
            execution.SetVariable("_conditionSatisfied", true);
            await LeaveAsync(execution, cancellationToken);
        }
    }

    protected bool IsConditionSatisfied(ExecutionEntity execution)
    {
        if (string.IsNullOrEmpty(ConditionExpression) || ExpressionManager == null)
        {
            return false;
        }

        var result = ExpressionManager.Evaluate(ConditionExpression, execution.Variables);
        if (result is bool boolValue) return boolValue;

        return false;
    }
}
