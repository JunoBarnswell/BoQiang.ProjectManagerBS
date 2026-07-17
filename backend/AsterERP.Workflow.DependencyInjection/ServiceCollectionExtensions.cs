using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Deployer;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.EventLogger;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.History;
using AsterERP.Workflow.Core.Integration;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Management;
using AsterERP.Workflow.Core.Security;
using AsterERP.Workflow.Core.Service;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence;
using AsterERP.Workflow.DependencyInjection.Persistence;
using AsterERP.Workflow.Persistence.Database;
using AsterERP.Workflow.Processing;
using AsterERP.Workflow.Api.Process.Runtime;
using AsterERP.Workflow.Api.Task.Runtime;
using SqlSugar;

namespace AsterERP.Workflow.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsterERPWorkflow(this IServiceCollection services, Action<ProcessEngineConfiguration>? configure = null)
    {
        EnsureDefaultPersistenceRegistration(services);
        return AddAsterERPWorkflowCoreServices(services, configure);
    }

    public static IServiceCollection AddAsterERPWorkflow(
        this IServiceCollection services,
        string connectionString,
        DbType dbType,
        Action<ProcessEngineConfiguration>? configure = null)
    {
        EnsurePersistenceRegistration(services, connectionString, dbType);
        return AddAsterERPWorkflowCoreServices(services, configure);
    }

    private static IServiceCollection AddAsterERPWorkflowCoreServices(
        IServiceCollection services,
        Action<ProcessEngineConfiguration>? configure)
    {
        var config = ProcessEngineConfiguration.CreateDefault();
        configure?.Invoke(config);

        services.AddSingleton<IProcessEngineConfiguration>(sp =>
        {
            AbpTimeIdProvider.Configure(sp);
            config.ServiceProvider = sp;
            var historicEntityService = new HistoricEntityServiceImplementation { Configuration = config };
            config.HistoryManager = config.IsDbHistoryUsed
                ? new DefaultHistoryManager(config.HistoryLevel, historicEntityService)
                : new DefaultHistoryManager(config.HistoryLevel);

            if (config.JobManager == null)
            {
                config.JobManager = new Persistence.ScopedSqlSugarJobManager(sp.GetRequiredService<IServiceScopeFactory>());
            }

            return config;
        });
        services.AddSingleton<ICommandExecutor>(sp => sp.GetRequiredService<IProcessEngineConfiguration>().CommandExecutor);
        services.AddSingleton<IJobManager>(sp => sp.GetRequiredService<IProcessEngineConfiguration>().JobManager!);
        services.AddSingleton(sp => sp.GetRequiredService<IProcessEngineConfiguration>().EventDispatcher);
        services.AddSingleton<IExpressionManager, ExpressionManagerImplementation>();
        services.AddScoped<IActivityBehaviorFactory>(sp =>
            new DefaultActivityBehaviorFactory(
                sp,
                sp.GetRequiredService<IExpressionManager>()));
        services.AddSingleton<IProcessDefinitionManager, CallActivityProcessDefinitionManager>();
        services.AddSingleton<BpmnDeploymentHelper>();
        services.AddSingleton<EventSubscriptionManager>();
        services.AddSingleton<TimerManager>();
        services.AddScoped<BpmnDeployer>(sp => new BpmnDeployer(
            sp.GetRequiredService<IExpressionManager>(),
            sp.GetRequiredService<BpmnDeploymentHelper>(),
            sp.GetRequiredService<EventSubscriptionManager>(),
            sp.GetRequiredService<TimerManager>(),
            sp.GetService<IActivityBehaviorFactory>()));

        services.AddSingleton<IRepositoryService>(sp => new RepositoryServiceImplementation(
            sp.GetRequiredService<IProcessEngineConfiguration>()));
        services.AddSingleton<IRuntimeService>(sp => new RuntimeServiceImplementation(
            sp.GetRequiredService<IProcessEngineConfiguration>()));
        services.AddSingleton<ITaskService>(sp => new TaskServiceImplementation(
            sp.GetRequiredService<IProcessEngineConfiguration>()));
        services.AddSingleton<IHistoryService>(sp => new HistoryServiceImplementation(
            sp.GetRequiredService<IProcessEngineConfiguration>()));
        services.AddSingleton<IProcessRuntime, ProcessRuntimeImplementation>();
        services.AddSingleton<IProcessAdminRuntime, ProcessAdminRuntimeImplementation>();
        services.AddSingleton<ITaskRuntime, TaskRuntimeImplementation>();
        services.AddSingleton<ITaskAdminRuntime, TaskAdminRuntimeImplementation>();
        services.AddScoped<IApprovalQueryService>(sp =>
        {
            var sqlSugar = (ISqlSugarClient?)sp.GetService<SqlSugarScope>() ?? sp.GetRequiredService<ISqlSugarClient>();
            var persistenceStore = sp.GetRequiredService<IWorkflowPersistenceStore>();
            return new SqlSugarApprovalQueryService(sqlSugar, persistenceStore);
        });
        services.AddSingleton<ManagementServiceImplementation>(sp =>
        {
            var cfg = sp.GetRequiredService<IProcessEngineConfiguration>();
            var repo = sp.GetService<IEventLoggerRepository>();
            return repo == null
                ? new ManagementServiceImplementation(cfg)
                : new ManagementServiceImplementation(cfg, repo);
        });
        services.AddSingleton<IManagementService>(sp => sp.GetRequiredService<ManagementServiceImplementation>());
        services.AddSingleton<AsterERP.Workflow.Core.Job.IAsyncJobExecutor, AsyncJobExecutorImplementation>();

        services.AddSingleton<WorkflowEngineIdentityServiceImplementation>();
        services.AddSingleton<IWorkflowEngineIdentityService>(sp => sp.GetRequiredService<WorkflowEngineIdentityServiceImplementation>());
        services.AddSingleton<IUserLookupService, UserLookupServiceImplementation>();
        services.AddSingleton<IAccessControlService, AccessControlServiceImplementation>();
        services.AddSingleton<IIntegrationContextService, IntegrationContextServiceImpl>();
        services.AddAsterERPWorkflowProcessing();

        services.AddSingleton<IProcessEngine>(sp =>
        {
            var cfg = sp.GetRequiredService<IProcessEngineConfiguration>();
            cfg.ServiceProvider = sp;

            return new ProcessEngineImplementation(
                "default",
                cfg,
                sp.GetRequiredService<IRepositoryService>(),
                sp.GetRequiredService<IRuntimeService>(),
                sp.GetRequiredService<ITaskService>(),
                sp.GetRequiredService<IHistoryService>(),
                sp.GetRequiredService<AsterERP.Workflow.Core.Services.IManagementService>()
            );
        });

        services.AddAsterERPWorkflowHostedJobExecutor();

        return services;
    }

    private static void EnsureDefaultPersistenceRegistration(IServiceCollection services)
    {
        var sqlitePath = Path.Combine(Path.GetTempPath(), $"workflow-{AbpTimeIdProvider.NewGuid()}.db");
        EnsurePersistenceRegistration(services, $"Data Source={sqlitePath};Cache=Shared", DbType.Sqlite);
    }

    private static void EnsurePersistenceRegistration(IServiceCollection services, string connectionString, DbType dbType)
    {
        var hasSugarClient = services.Any(descriptor => descriptor.ServiceType == typeof(ISqlSugarClient));
        var hasSugarScope = services.Any(descriptor => descriptor.ServiceType == typeof(SqlSugarScope));
        if (!hasSugarClient && !hasSugarScope)
        {
            services.AddSqlSugar(connectionString, dbType);
        }

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IWorkflowPersistenceStore)))
        {
            services.AddAsterERPWorkflowPersistence();
        }
    }

    public static IServiceCollection AddAsterERPWorkflowPersistence(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowPersistenceStore, SqlSugarWorkflowPersistenceStore>();
        services.AddScoped<ISessionFactory, SqlSugarCommandContextSessionFactory>();
        return services;
    }

    public static IServiceCollection AddAsterERPWorkflowPersistence(this IServiceCollection services, ISqlSugarClient db)
    {
        services.AddSingleton(db);
        return services.AddAsterERPWorkflowPersistence();
    }

    public static IServiceCollection AddAsterERPWorkflowEventLogger(this IServiceCollection services, Action<EventLoggerConfiguration>? configure = null)
    {
        var eventLoggerConfig = EventLoggerConfiguration.Default;
        configure?.Invoke(eventLoggerConfig);

        services.AddSingleton(eventLoggerConfig);
        services.AddSingleton<IEventLoggerRepository, InMemoryEventLoggerRepository>();
        services.AddSingleton<IEventFlusher>(sp =>
        {
            if (eventLoggerConfig.FlusherType == typeof(DatabaseEventFlusher))
            {
                return new DatabaseEventFlusher(
                    sp.GetRequiredService<IEventLoggerRepository>(),
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<DatabaseEventFlusher>>());
            }
            return new ConsoleEventFlusher(
                sp.GetService<Microsoft.Extensions.Logging.ILogger<ConsoleEventFlusher>>());
        });
        services.AddSingleton<IEventLogger>(sp =>
        {
            var flusher = sp.GetRequiredService<IEventFlusher>();
            var config = sp.GetRequiredService<EventLoggerConfiguration>();
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<EventLogger>>();
            return new EventLogger(flusher, config, logger);
        });
        services.AddSingleton<EventLoggerListener>(sp =>
        {
            var eventLogger = sp.GetRequiredService<IEventLogger>();
            var config = sp.GetRequiredService<EventLoggerConfiguration>();
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<EventLoggerListener>>();
            return new EventLoggerListener(eventLogger, config, logger);
        });

        return services;
    }

    public static IServiceCollection AddAsterERPWorkflowHostedJobExecutor(
        this IServiceCollection services,
        Action<AsyncExecutorOptions>? configure = null)
    {
        var options = new AsyncExecutorOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<HostedAsyncExecutor>(sp => new HostedAsyncExecutor(
            sp.GetRequiredService<IProcessEngineConfiguration>(),
            sp.GetRequiredService<IJobManager>(),
            sp.GetService<IEventDispatcher>(),
            sp.GetRequiredService<AsyncExecutorOptions>()));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, HostedAsyncExecutor>());

        return services;
    }
}
