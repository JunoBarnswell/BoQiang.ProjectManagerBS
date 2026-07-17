namespace AsterERP.Workflow.Core.Expression;

public interface IExpressionManager
{
    object? Evaluate(string expression, Dictionary<string, object?> variables);
    T? Evaluate<T>(string expression, Dictionary<string, object?> variables);
    global::System.Threading.Tasks.Task<object?> EvaluateAsync(string expression, Dictionary<string, object?> variables, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<T?> EvaluateAsync<T>(string expression, Dictionary<string, object?> variables, CancellationToken cancellationToken = default);
    bool EvaluateBooleanExpression(string expression, Dictionary<string, object?> variables);
    IWorkflowExpression CreateExpression(string expression);
}
