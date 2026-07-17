using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AsterERP.Workflow.Core.Command;

public static class CommandContextStack
{
    private static readonly AsyncLocal<Stack<ICommandContext>> _stack = new();

    public static ICommandContext? Current => _stack.Value?.Count > 0 ? _stack.Value.Peek() : null;
    public static bool HasContext => _stack.Value?.Count > 0;

    public static void Push(ICommandContext context)
    {
        _stack.Value ??= new Stack<ICommandContext>();
        _stack.Value.Push(context);
    }

    public static ICommandContext? Pop()
    {
        if (_stack.Value?.Count > 0)
        {
            return _stack.Value.Pop();
        }
        return null;
    }
}

