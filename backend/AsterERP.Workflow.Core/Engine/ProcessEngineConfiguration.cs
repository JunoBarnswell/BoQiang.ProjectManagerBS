using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Deploy;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.History;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Engine;

public interface IProcessEngineConfiguration
{
    string DatabaseSchemaUpdate { get; set; }
    string? DatabaseType { get; set; }
    string? DataSource { get; set; }
    bool IsDbHistoryUsed { get; set; }
    string HistoryLevel { get; set; }
    bool IsAsyncExecutorEnabled { get; set; }
    ICommandExecutor CommandExecutor { get; }
    ILoggerFactory LoggerFactory { get; }
    IExpressionManager ExpressionManager { get; }
    IEventDispatcher EventDispatcher { get; }
    IHistoryManager HistoryManager { get; }
    IJobManager? JobManager { get; set; }
    DeploymentManager? DeploymentManager { get; set; }
    System.IServiceProvider? ServiceProvider { get; set; }
}

public class ProcessEngineConfiguration : IProcessEngineConfiguration
{
    public string DatabaseSchemaUpdate { get; set; } = "true";
    public string? DatabaseType { get; set; }
    public string? DataSource { get; set; }
    public bool IsDbHistoryUsed { get; set; } = true;
    public string HistoryLevel { get; set; } = "audit";
    public bool IsAsyncExecutorEnabled { get; set; } = false;

    public ICommandExecutor CommandExecutor { get; internal set; } = null!;
    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
    public IExpressionManager ExpressionManager { get; set; } = new ExpressionManagerImplementation();
    public IEventDispatcher EventDispatcher { get; set; } = new EventDispatcherImplementation();
    public IHistoryManager HistoryManager { get; set; } = new DefaultHistoryManager();
    public IJobManager? JobManager { get; set; }
    public DeploymentManager? DeploymentManager { get; set; }
    public System.IServiceProvider? ServiceProvider { get; set; }

    public static ProcessEngineConfiguration CreateDefault()
    {
        var config = new ProcessEngineConfiguration();
        var historicEntityService = new HistoricEntityServiceImplementation { Configuration = config };
        config.CommandExecutor = CreateDefaultCommandExecutor(config);
        config.HistoryManager = new DefaultHistoryManager(History.HistoryLevel.Audit, historicEntityService);
        return config;
    }

    private static ICommandExecutor CreateDefaultCommandExecutor(ProcessEngineConfiguration config)
    {
        var commandContextFactory = new CommandContextFactory(config);
        var commandContextInterceptor = new CommandContextInterceptor(commandContextFactory, config);
        var transactionContextInterceptor = new TransactionContextInterceptor();
        var retryInterceptor = new RetryInterceptor();
        var logInterceptor = new LogInterceptor();

        return new CommandExecutorImplementation(new ICommandInterceptor[]
        {
            logInterceptor,
            retryInterceptor,
            transactionContextInterceptor,
            commandContextInterceptor
        });
    }
}
