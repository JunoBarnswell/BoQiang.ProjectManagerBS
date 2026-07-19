using System.Threading.RateLimiting;
using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Abp;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Errors;
using AsterERP.Api.Infrastructure.Logging;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.ApplicationDataCenter;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Volo.Abp.Hangfire;

namespace AsterERP.Api.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAsterErpInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddAsterErpAbpInfrastructure(configuration);
        services.AddSingleton<GlobalExceptionHandler>();
        services.AddScoped<AuthSessionCookieWriter>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddSingleton<DataPermissionRequestClassifier>();
        services.AddScoped<IDataPermissionFilterRegistrar, DataPermissionFilterRegistrar>();
        services.AddScoped<IDataScopeDepartmentResolver, DataScopeDepartmentResolver>();
        services.AddScoped<IDataPermissionDescriptor<SystemUserEntity>, SystemUserDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<SystemRoleEntity>, SystemRoleDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<SystemMenuEntity>, SystemMenuDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataSourceEntity>, ApplicationDataSourceDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationQueryDatasetEntity>, ApplicationQueryDatasetDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationApiServiceEntity>, ApplicationApiServiceDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationConnectionCheckTaskEntity>, ApplicationConnectionCheckTaskDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationConnectionCheckRunEntity>, ApplicationConnectionCheckRunDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataCenterDictionaryEntity>, ApplicationDataCenterDictionaryDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataEntityDefinitionEntity>, ApplicationDataEntityDefinitionDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataFieldDefinitionEntity>, ApplicationDataFieldDefinitionDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataImportBatchEntity>, ApplicationDataImportBatchDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataModelDesignEntity>, ApplicationDataModelDesignDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataObjectReferenceEntity>, ApplicationDataObjectReferenceDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationIntegrationTaskEntity>, ApplicationIntegrationTaskDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationIntegrationTaskRunEntity>, ApplicationIntegrationTaskRunDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationMicroflowEntity>, ApplicationMicroflowDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationSqlScriptAuditEntity>, ApplicationSqlScriptAuditDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataSourceCatalogSnapshotEntity>, ApplicationDataSourceCatalogSnapshotDataPermissionDescriptor>();
        services.AddScoped<IDataPermissionDescriptor<ApplicationDataSourceSchemaChangePlanEntity>, ApplicationDataSourceSchemaChangePlanDataPermissionDescriptor>();
        var passwordHashPolicy = new PasswordHashPolicyOptions();
        configuration.GetSection(PasswordHashPolicyOptions.SectionName).Bind(passwordHashPolicy);
        services.AddSingleton(passwordHashPolicy);
        services.AddSingleton<PasswordFormatInventoryService>();
        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        AuthenticationRateLimitPolicy.Register(services, configuration);
        services.AddScoped<DbInitializer>();
        services.AddScoped<DbMigrationService>();
        services.AddScoped<DbSeedService>();
        services.AddAsterErpSqlSugar(configuration);
        services.AddScoped<IWorkspaceDatabaseAccessor, WorkspaceDatabaseAccessor>();
        services.AddScoped(typeof(IRepository<>), typeof(WorkspaceSqlSugarRepository<>));
        services.AddScoped<IUnitOfWork, WorkspaceUnitOfWork>();
        services.AddScoped<IOperationLogWriter, OperationLogWriter>();
        services.AddSingleton<IOperationLogQueue, OperationLogQueue>();
        services.AddHostedService<OperationLogWorker>();
        services.AddHttpClient();
        services.AddHttpClient("scheduled-job-callback")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        ProductionSecurityConfigurationValidator.Validate(configuration, environment);
        ConfigureDataProtection(services, configuration, environment);
        var hangfireStoragePath = configuration["Scheduler:HangfireStoragePath"] ?? "./data/astererp-hangfire.db";
        EnsureHangfireDirectory(hangfireStoragePath);
        JobStorage.Current = new SQLiteStorage(hangfireStoragePath, CreateSQLiteStorageOptions());

        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSQLiteStorage(hangfireStoragePath, CreateSQLiteStorageOptions());
        });
        services.Configure<AbpHangfireOptions>(options =>
        {
            options.ServerOptions = new BackgroundJobServerOptions
            {
                WorkerCount = Math.Max(1, configuration.GetValue("Scheduler:WorkerCount", 2)),
                Queues = ["scheduled-jobs", "default"]
            };
        });

        return services;
    }

    private static SQLiteStorageOptions CreateSQLiteStorageOptions() =>
        new()
        {
            Prefix = "Hangfire",
            QueuePollInterval = TimeSpan.FromSeconds(15),
            JobExpirationCheckInterval = TimeSpan.FromHours(1)
        };

    private static void EnsureHangfireDirectory(string hangfireStoragePath)
    {
        var hangfireDirectory = Path.GetDirectoryName(hangfireStoragePath);
        if (!string.IsNullOrWhiteSpace(hangfireDirectory))
        {
            Directory.CreateDirectory(hangfireDirectory);
        }
    }

    private static void ConfigureDataProtection(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredKeysPath = configuration["DataProtection:KeysPath"];
        var keysPath = string.IsNullOrWhiteSpace(configuredKeysPath)
            ? Path.Combine(environment.ContentRootPath, "data", "data-protection-keys")
            : Path.GetFullPath(configuredKeysPath, environment.ContentRootPath);
        var applicationName = configuration["DataProtection:ApplicationName"] ?? "AsterERP";
        Directory.CreateDirectory(keysPath);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName(applicationName);
    }
}

internal static class AuthenticationRateLimitPolicy
{
    public const string Name = "authentication-login";
    public const string ProjectManagementExternalApiName = "project-management-external-api";

    public static void Register(IServiceCollection services, IConfiguration configuration)
    {
        var settings = ResolveSettings(configuration);
        var externalApiSettings = ResolveProjectManagementExternalApiSettings(configuration);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = OnRejectedAsync;
            options.AddPolicy(Name, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));
            options.AddPolicy(ProjectManagementExternalApiName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveExternalApiPartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = externalApiSettings.PermitLimit,
                        Window = TimeSpan.FromSeconds(externalApiSettings.WindowSeconds),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));
        });
    }

    internal static AuthenticationRateLimitSettings ResolveSettings(IConfiguration configuration) =>
        new(
            Math.Clamp(configuration.GetValue("Auth:LoginRateLimitPermitCount", 10), 1, 100),
            Math.Clamp(configuration.GetValue("Auth:LoginRateLimitWindowSeconds", 60), 10, 3600));

    internal static AuthenticationRateLimitSettings ResolveProjectManagementExternalApiSettings(IConfiguration configuration) =>
        new(
            Math.Clamp(configuration.GetValue("ProjectManagement:ExternalApiRateLimitPermitCount", 60), 1, 600),
            Math.Clamp(configuration.GetValue("ProjectManagement:ExternalApiRateLimitWindowSeconds", 60), 10, 3600));

    internal static string ResolvePartitionKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address?.IsIPv4MappedToIPv6 == true)
        {
            address = address.MapToIPv4();
        }

        return address?.ToString() ?? "unknown-client";
    }

    internal static string ResolveExternalApiPartitionKey(HttpContext context)
    {
        var userId = context.User.FindFirst(AsterErpClaimTypes.UserId)?.Value;
        return string.IsNullOrWhiteSpace(userId)
            ? $"anonymous:{ResolvePartitionKey(context)}"
            : $"user:{userId.Trim()}";
    }

    private static async ValueTask OnRejectedAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResultFactory.Fail<object?>(
                "登录尝试过于频繁，请稍后重试",
                context.HttpContext.TraceIdentifier,
                ErrorCodes.AuthenticationRequired),
            cancellationToken);
    }
}

internal readonly record struct AuthenticationRateLimitSettings(
    int PermitLimit,
    int WindowSeconds);
