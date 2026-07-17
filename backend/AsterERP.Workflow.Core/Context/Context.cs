using System.Collections.Generic;
using System.Threading;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Context;

public static class Context
{
    private static readonly AsyncLocal<Stack<ICommandContext>?> _commandContextStack = new();
    private static readonly AsyncLocal<Stack<IProcessEngineConfiguration>?> _processEngineConfigurationStack = new();
    private static readonly AsyncLocal<Stack<ITransactionContext>?> _transactionContextStack = new();
    private static readonly AsyncLocal<ExecutionContext?> _executionContext = new();

    public static ICommandContext? GetCommandContext()
    {
        var stack = GetStack(_commandContextStack);
        return stack.Count > 0 ? stack.Peek() : null;
    }

    public static void SetCommandContext(ICommandContext commandContext)
    {
        GetStack(_commandContextStack).Push(commandContext);
    }

    public static void RemoveCommandContext()
    {
        var stack = GetStack(_commandContextStack);
        if (stack.Count > 0)
            stack.Pop();
    }

    public static IProcessEngineConfiguration? GetProcessEngineConfiguration()
    {
        var stack = GetStack(_processEngineConfigurationStack);
        return stack.Count > 0 ? stack.Peek() : null;
    }

    public static void SetProcessEngineConfiguration(IProcessEngineConfiguration processEngineConfiguration)
    {
        GetStack(_processEngineConfigurationStack).Push(processEngineConfiguration);
    }

    public static void RemoveProcessEngineConfiguration()
    {
        var stack = GetStack(_processEngineConfigurationStack);
        if (stack.Count > 0)
            stack.Pop();
    }

    public static ITransactionContext? GetTransactionContext()
    {
        var stack = GetStack(_transactionContextStack);
        return stack.Count > 0 ? stack.Peek() : null;
    }

    public static void SetTransactionContext(ITransactionContext transactionContext)
    {
        GetStack(_transactionContextStack).Push(transactionContext);
    }

    public static void RemoveTransactionContext()
    {
        var stack = GetStack(_transactionContextStack);
        if (stack.Count > 0)
            stack.Pop();
    }

    public static ExecutionContext? GetExecutionContext()
    {
        return _executionContext.Value;
    }

    public static void SetExecutionContext(ExecutionContext? executionContext)
    {
        _executionContext.Value = executionContext;
    }

    private static Stack<T> GetStack<T>(AsyncLocal<Stack<T>?> asyncLocal)
    {
        if (asyncLocal.Value == null)
        {
            asyncLocal.Value = new Stack<T>();
        }
        return asyncLocal.Value;
    }
}

public interface ITransactionContext
{
    void Commit();
    void Rollback();
}

public class ExecutionContext
{
    public ExecutionEntity Execution { get; }
    public BpmnModelNs.FlowElement? CurrentFlowElement { get; }

    public ExecutionContext(ExecutionEntity execution, BpmnModelNs.FlowElement? currentFlowElement)
    {
        Execution = execution;
        CurrentFlowElement = currentFlowElement;
    }
}
