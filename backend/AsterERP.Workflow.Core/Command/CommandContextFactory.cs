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

public class CommandContextFactory
{
    public IProcessEngineConfiguration? ProcessEngineConfiguration { get; set; }

    public CommandContextFactory()
    {
    }

    public CommandContextFactory(IProcessEngineConfiguration processEngineConfiguration)
    {
        ProcessEngineConfiguration = processEngineConfiguration;
    }

    public virtual ICommandContext CreateCommandContext<T>(ICommand<T> command)
    {
        var configuration = ProcessEngineConfiguration ?? AsterERP.Workflow.Core.Context.Context.GetProcessEngineConfiguration();
        if (configuration == null)
        {
            throw new InvalidOperationException("ProcessEngineConfiguration is required to create a command context.");
        }

        return new CommandContextImplementation(configuration);
    }
}

