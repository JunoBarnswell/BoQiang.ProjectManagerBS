using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Expression;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Util;

public static class ConditionUtil
{
    public static bool HasTrueCondition(BpmnModelNs.SequenceFlow sequenceFlow, IDelegateExecution execution, IExpressionManager expressionManager)
    {
        var conditionExpression = sequenceFlow.ConditionExpression;
        if (!string.IsNullOrEmpty(conditionExpression))
        {
            var expression = expressionManager.CreateExpression(conditionExpression);
            var condition = new UelExpressionCondition(expression);
            return condition.Evaluate(sequenceFlow.Id!, execution);
        }
        return true;
    }
}
