using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;
using DynamicExpresso;

namespace AsterERP.Workflow.Core.Expression;

public interface IVariableScope
{
    Dictionary<string, object?> Variables { get; }
    object? GetVariable(string variableName);
    void SetVariable(string variableName, object? value);
    object? GetVariableLocal(string variableName);
    void SetVariableLocal(string variableName, object? value);
    bool HasVariable(string variableName);
    bool HasVariableLocal(string variableName);
    HashSet<string> GetVariableNames();
    void RemoveVariable(string variableName);
    void RemoveVariableLocal(string variableName);
}

public interface IVariableScopeItemELResolver
{
    bool CanResolve(string property, IVariableScope variableScope);
    object? Resolve(string property, IVariableScope variableScope);
}

public class ExecutionElResolver : IVariableScopeItemELResolver
{
    private const string ExecutionKey = "execution";

    public bool CanResolve(string property, IVariableScope variableScope)
    {
        return ExecutionKey.Equals(property) && variableScope is ExecutionEntity;
    }

    public object? Resolve(string property, IVariableScope variableScope)
    {
        return variableScope;
    }
}

public class TaskElResolver : IVariableScopeItemELResolver
{
    private const string TaskKey = "task";

    public bool CanResolve(string property, IVariableScope variableScope)
    {
        return TaskKey.Equals(property) && variableScope is TaskImplementation;
    }

    public object? Resolve(string property, IVariableScope variableScope)
    {
        return variableScope;
    }
}

public class AuthenticatedUserELResolver : IVariableScopeItemELResolver
{
    private const string AuthenticatedUserKey = "authenticatedUserId";

    public bool CanResolve(string property, IVariableScope variableScope)
    {
        return AuthenticatedUserKey.Equals(property);
    }

    public object? Resolve(string property, IVariableScope variableScope)
    {
        return Authentication.GetAuthenticatedUserId();
    }
}

public class ProcessInitiatorELResolver : IVariableScopeItemELResolver
{
    private const string InitiatorKey = "initiator";

    public bool CanResolve(string property, IVariableScope variableScope)
    {
        return InitiatorKey.Equals(property) && variableScope is ExecutionEntity;
    }

    public object? Resolve(string property, IVariableScope variableScope)
    {
        if (variableScope is ExecutionEntity execution)
        {
            var processInstance = execution;
            return processInstance.Variables.TryGetValue("initiator", out var initiator) ? initiator : null;
        }
        return null;
    }
}

public class VariableElResolver : IVariableScopeItemELResolver
{
    public bool CanResolve(string property, IVariableScope variableScope)
    {
        return variableScope.HasVariable(property);
    }

    public object? Resolve(string property, IVariableScope variableScope)
    {
        var value = variableScope.GetVariable(property);
        if (value is JsonNode jsonNode && jsonNode is JsonArray jsonArray)
        {
            return jsonArray.Select(item => item).ToList();
        }
        return value;
    }
}

public class VariableScopeElResolver
{
    private readonly IVariableScope _variableScope;
    private List<IVariableScopeItemELResolver>? _itemResolvers;

    public VariableScopeElResolver(IVariableScope variableScope)
    {
        _variableScope = variableScope;
    }

    public object? GetValue(string property)
    {
        foreach (var resolver in GetItemResolvers())
        {
            if (resolver.CanResolve(property, _variableScope))
            {
                return resolver.Resolve(property, _variableScope);
            }
        }
        return null;
    }

    public bool IsReadOnly(string property)
    {
        return !_variableScope.HasVariable(property);
    }

    public void SetValue(string property, object? value)
    {
        if (_variableScope.HasVariable(property))
        {
            _variableScope.SetVariable(property, value);
        }
    }

    protected List<IVariableScopeItemELResolver> GetItemResolvers()
    {
        if (_itemResolvers == null)
        {
            _itemResolvers = new List<IVariableScopeItemELResolver>
            {
                new ExecutionElResolver(),
                new TaskElResolver(),
                new AuthenticatedUserELResolver(),
                new ProcessInitiatorELResolver(),
                new VariableElResolver()
            };
        }
        return _itemResolvers;
    }
}

public class DynamicBeanPropertyELResolver
{
    private readonly Type _subject;
    private readonly string _readMethodName;
    private readonly string _writeMethodName;
    private readonly bool _readOnly;

    public DynamicBeanPropertyELResolver(Type subject, string readMethodName, string writeMethodName)
        : this(false, subject, readMethodName, writeMethodName)
    {
    }

    public DynamicBeanPropertyELResolver(bool readOnly, Type subject, string readMethodName, string writeMethodName)
    {
        _readOnly = readOnly;
        _subject = subject;
        _readMethodName = readMethodName;
        _writeMethodName = writeMethodName;
    }

    public bool CanResolve(object baseObject)
    {
        return baseObject != null && _subject.IsAssignableFrom(baseObject.GetType());
    }

    public object? GetValue(object baseObject, string propertyName)
    {
        if (!CanResolve(baseObject)) return null;

        var method = _subject.GetMethod(_readMethodName, new[] { typeof(string) });
        if (method == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException($"Method '{_readMethodName}' not found on type '{_subject.Name}'");

        return method.Invoke(baseObject, new object[] { propertyName });
    }

    public void SetValue(object baseObject, string propertyName, object? value)
    {
        if (!CanResolve(baseObject)) return;
        if (_readOnly) return;

        var method = _subject.GetMethod(_writeMethodName, new[] { typeof(string), typeof(object) });
        if (method == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException($"Method '{_writeMethodName}' not found on type '{_subject.Name}'");

        method.Invoke(baseObject, new object?[] { propertyName, value });
    }
}

public class CustomMapperJsonNodeELResolver
{
    public object? GetValue(JsonNode? jsonNode, string propertyName)
    {
        if (jsonNode == null) return null;

        if (jsonNode is JsonObject jsonObject)
        {
            if (jsonObject.TryGetPropertyValue(propertyName, out var value))
            {
                return value;
            }
        }

        return null;
    }

    public void SetValue(JsonNode jsonNode, string propertyName, object? value)
    {
        if (jsonNode is JsonObject jsonObject)
        {
            jsonObject[propertyName] = value != null ? JsonValue.Create(value) : null;
        }
    }
}

public class ParsingElContext
{
    private readonly Interpreter _interpreter;

    public ParsingElContext()
    {
        _interpreter = new Interpreter();
    }

    public Interpreter Interpreter => _interpreter;

    public void SetVariable(string name, object? value)
    {
        if (value != null)
        {
            _interpreter.SetVariable(name, value);
        }
    }

    public object? Eval(string expression)
    {
        return _interpreter.Eval(expression);
    }

    public T? Eval<T>(string expression)
    {
        return _interpreter.Eval<T>(expression);
    }
}

public class NoExecutionVariableScope : IVariableScope
{
    private static readonly NoExecutionVariableScope _instance = new();

    public static NoExecutionVariableScope SharedInstance => _instance;

    public Dictionary<string, object?> Variables => new();
    public bool HasVariable(string variableName) => false;
    public bool HasVariableLocal(string variableName) => false;
    public HashSet<string> GetVariableNames() => new();

    public object? GetVariable(string variableName) => null;
    public object? GetVariableLocal(string variableName) => null;

    public void SetVariable(string variableName, object? value)
    {
        throw new WorkflowEngineException("No execution active, no variables can be set");
    }

    public void SetVariableLocal(string variableName, object? value)
    {
        throw new WorkflowEngineException("No execution active, no variables can be set");
    }

    public void RemoveVariable(string variableName)
    {
        throw new WorkflowEngineException("No execution active, no variables can be removed");
    }

    public void RemoveVariableLocal(string variableName)
    {
        throw new WorkflowEngineException("No execution active, no variables can be removed");
    }
}

public class DynamicExpressoExpression : IWorkflowExpression
{
    private readonly string _expressionText;
    private readonly IExpressionManager _expressionManager;

    public DynamicExpressoExpression(string expressionText, IExpressionManager expressionManager)
    {
        _expressionText = expressionText;
        _expressionManager = expressionManager;
    }

    public string ExpressionText => _expressionText;

    public object? GetValue(IDelegateExecution execution)
    {
        try
        {
            return _expressionManager.Evaluate(_expressionText, execution.Variables);
        }
        catch (Exception ex)
        {
            throw new AsterERP.Workflow.Common.WorkflowEngineException($"Error while evaluating expression: {_expressionText}", ex);
        }
    }

    public void SetValue(IDelegateExecution execution, object? value)
    {
        throw new AsterERP.Workflow.Common.WorkflowEngineException("Cannot set value via DynamicExpresso expression");
    }
}

public class FixedValue : IWorkflowExpression
{
    private readonly object? _value;

    public FixedValue(object? value)
    {
        _value = value;
    }

    public string ExpressionText => _value?.ToString() ?? string.Empty;

    public object? GetValue(IDelegateExecution execution)
    {
        return _value;
    }

    public void SetValue(IDelegateExecution execution, object? value)
    {
        throw new AsterERP.Workflow.Common.WorkflowEngineException("Cannot change fixed value");
    }
}

public static class Authentication
{
    private static readonly AsyncLocal<string?> _authenticatedUserId = new();

    public static string? GetAuthenticatedUserId()
    {
        return _authenticatedUserId.Value;
    }

    public static void SetAuthenticatedUserId(string? userId)
    {
        _authenticatedUserId.Value = userId;
    }
}
