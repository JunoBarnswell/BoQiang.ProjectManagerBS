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

public class TransactionCommandContextCloseListener : ICommandContextCloseListener
{
    private readonly TransactionScope? _transactionScope;

    public TransactionCommandContextCloseListener(TransactionScope? transactionScope)
    {
        _transactionScope = transactionScope;
    }

    public void Closing(ICommandContext commandContext)
    {
    }

    public void AfterSessionsFlush(ICommandContext commandContext)
    {
        _transactionScope?.Complete();
    }

    public void Closed(ICommandContext commandContext)
    {
        _transactionScope?.Dispose();
    }

    public void CloseFailure(ICommandContext commandContext)
    {
        _transactionScope?.Dispose();
    }
}

