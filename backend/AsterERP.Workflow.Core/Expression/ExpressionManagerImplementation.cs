using DynamicExpresso;
using AsterERP.Workflow.Core.Delegate;

namespace AsterERP.Workflow.Core.Expression;

public class ExpressionManagerImplementation : IExpressionManager
{
    private readonly Interpreter _interpreter;

    public ExpressionManagerImplementation()
    {
        _interpreter = new Interpreter();
    }

    public object? Evaluate(string expression, Dictionary<string, object?> variables)
    {
        var resolvedExpression = UelExpressionResolver.ExtractExpression(expression) ?? expression;
        resolvedExpression = NormalizeStringLiterals(resolvedExpression);
        var interpreter = CreateConfiguredInterpreter(variables);
        return interpreter.Eval(resolvedExpression);
    }

    public T? Evaluate<T>(string expression, Dictionary<string, object?> variables)
    {
        var resolvedExpression = UelExpressionResolver.ExtractExpression(expression) ?? expression;
        resolvedExpression = NormalizeStringLiterals(resolvedExpression);
        var interpreter = CreateConfiguredInterpreter(variables);
        return interpreter.Eval<T>(resolvedExpression);
    }

    public async global::System.Threading.Tasks.Task<object?> EvaluateAsync(string expression, Dictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        return await global::System.Threading.Tasks.Task.Run(() => Evaluate(expression, variables), cancellationToken).ConfigureAwait(false);
    }

    public async global::System.Threading.Tasks.Task<T?> EvaluateAsync<T>(string expression, Dictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        return await global::System.Threading.Tasks.Task.Run(() => Evaluate<T>(expression, variables), cancellationToken).ConfigureAwait(false);
    }

    public bool EvaluateBooleanExpression(string expression, Dictionary<string, object?> variables)
    {
        var result = Evaluate(expression, variables);
        if (result is bool boolResult)
        {
            return boolResult;
        }
        throw new AsterERP.Workflow.Common.WorkflowEngineException($"Expression '{expression}' did not evaluate to a boolean value");
    }

    public IWorkflowExpression CreateExpression(string expression)
    {
        return new WorkflowExpressionImplementation(expression, this);
    }

    private Interpreter CreateConfiguredInterpreter(Dictionary<string, object?> variables)
    {
        var interpreter = new Interpreter();
        foreach (var variable in variables)
        {
            if (variable.Value != null)
            {
                interpreter.SetVariable(variable.Key, variable.Value);
            }
        }
        return interpreter;
    }

    private static string NormalizeStringLiterals(string expression)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            expression,
            @"'([^']*)'",
            m => $"\"{m.Groups[1].Value}\"");
    }
}
