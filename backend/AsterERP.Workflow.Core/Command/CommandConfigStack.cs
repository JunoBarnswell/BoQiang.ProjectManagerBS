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

public static class CommandConfigStack
{
    private static readonly AsyncLocal<Stack<CommandConfig>> _stack = new();

    public static CommandConfig? Current => _stack.Value?.Count > 0 ? _stack.Value.Peek() : null;

    public static void Push(CommandConfig config)
    {
        _stack.Value ??= new Stack<CommandConfig>();
        _stack.Value.Push(config);
    }

    public static CommandConfig? Pop()
    {
        if (_stack.Value?.Count > 0)
        {
            return _stack.Value.Pop();
        }
        return null;
    }
}

