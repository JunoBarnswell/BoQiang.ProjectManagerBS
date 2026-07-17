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

public class CommandConfig
{
    public bool ContextReusePossible { get; private set; }
    public TransactionPropagation TransactionPropagation { get; private set; }

    public CommandConfig()
    {
        ContextReusePossible = true;
        TransactionPropagation = TransactionPropagation.Required;
    }

    public CommandConfig(bool contextReusePossible)
    {
        ContextReusePossible = contextReusePossible;
        TransactionPropagation = TransactionPropagation.Required;
    }

    public CommandConfig(bool contextReusePossible, TransactionPropagation transactionPropagation)
    {
        ContextReusePossible = contextReusePossible;
        TransactionPropagation = transactionPropagation;
    }

    private CommandConfig(CommandConfig source)
    {
        ContextReusePossible = source.ContextReusePossible;
        TransactionPropagation = source.TransactionPropagation;
    }

    public CommandConfig SetContextReusePossible(bool contextReusePossible)
    {
        var config = new CommandConfig(this);
        config.ContextReusePossible = contextReusePossible;
        return config;
    }

    public CommandConfig TransactionRequired()
    {
        var config = new CommandConfig(this);
        config.TransactionPropagation = TransactionPropagation.Required;
        return config;
    }

    public CommandConfig TransactionRequiresNew()
    {
        var config = new CommandConfig();
        config.ContextReusePossible = false;
        config.TransactionPropagation = TransactionPropagation.RequiresNew;
        return config;
    }

    public CommandConfig TransactionNotSupported()
    {
        var config = new CommandConfig();
        config.ContextReusePossible = false;
        config.TransactionPropagation = TransactionPropagation.NotSupported;
        return config;
    }
}

