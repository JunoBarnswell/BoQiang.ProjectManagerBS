using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Approval.Core.Expressions;

public class ExpressionService : IExpressionService
{
    private readonly ILogger<ExpressionService> _logger;

    public ExpressionService(ILogger<ExpressionService> logger)
    {
        _logger = logger;
    }

    public string? GetStrValue(string processInstanceId, string exp)
    {
        return GetValue<string>(processInstanceId, exp);
    }

    public object? GetValue(string processInstanceId, string exp)
    {
        return EvaluateExpression(exp, new Dictionary<string, object>());
    }

    public T? GetValue<T>(string processInstanceId, string exp)
    {
        var value = GetValue(processInstanceId, exp);
        if (value == null) return default;
        return (T)Convert.ChangeType(value, typeof(T));
    }

    public Dictionary<string, string> GetStrValues(string processInstanceId, List<string> exps)
    {
        var datas = new Dictionary<string, string>();
        foreach (var exp in exps)
        {
            var value = GetStrValue(processInstanceId, exp);
            datas[exp] = value ?? "";
        }
        return datas;
    }

    public bool GetBooleanValue(Dictionary<string, object> parameters, string exp, bool flag)
    {
        try
        {
            return GetValue<bool>(parameters, exp);
        }
        catch (Exception)
        {
            return flag;
        }
    }

    public string? GetStrValue(Dictionary<string, object> parameters, string exp)
    {
        try
        {
            return GetValue<string>(parameters, exp);
        }
        catch (Exception)
        {
            return "";
        }
    }

    public T? GetValue<T>(Dictionary<string, object> parameters, string exp)
    {
        try
        {
            var interpreter = new DynamicExpresso.Interpreter();
            foreach (var kvp in parameters)
            {
                if (kvp.Value is Dictionary<string, object> dict)
                {
                    interpreter.SetVariable(kvp.Key, dict);
                }
                else
                {
                    interpreter.SetVariable(kvp.Key, kvp.Value);
                }
            }
            return interpreter.Eval<T>(exp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "流程变量的属性找不到，请确认!");
            throw;
        }
    }

    private static object? EvaluateExpression(string exp, Dictionary<string, object> parameters)
    {
        var interpreter = new DynamicExpresso.Interpreter();
        foreach (var kvp in parameters)
        {
            interpreter.SetVariable(kvp.Key, kvp.Value);
        }
        return interpreter.Eval(exp);
    }
}
