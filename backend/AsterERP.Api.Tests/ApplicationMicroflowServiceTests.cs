using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationMicroflowServiceTests : IDisposable
{
    private const string TenantId = "tenant-a";
    private const string AppCode = "MES";

    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-microflow-service-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task PublishAsync_RegistersAndUpdatesMicroflowApiEndpoint()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<
            ApplicationMicroflowEntity,
            ApplicationApiServiceEntity,
            ApplicationDataObjectReferenceEntity,
            ApplicationDataEntityDefinitionEntity,
            ApplicationDataFieldDefinitionEntity>();
        db.CodeFirst.InitTables<ApplicationDataModelDesignEntity>();
        db.CodeFirst.InitTables<
            ApplicationQueryDatasetEntity,
            ApplicationIntegrationTaskEntity>();
        var microflowId = Guid.NewGuid().ToString("N");
        await InsertMicroflowAsync(
            db,
            microflowId,
            "order_save_flow",
            Definition("/app/orders/save", "POST", "app.order.save", true));
        var service = CreateService(db);

        await service.PublishAsync(microflowId, new ApplicationDataCenterPublishRequest(), CancellationToken.None);

        var endpoint = await LoadEndpointAsync(db, "order_save_flow_save");
        Assert.NotNull(endpoint);
        Assert.Equal(ApplicationApiServiceSourceType.Microflow, endpoint.ObjectType);
        Assert.Equal(ApplicationDataCenterObjectStatus.Published, endpoint.Status);
        Assert.Equal(microflowId, endpoint.SourceObjectId);
        Assert.Equal("/app/orders/save", endpoint.RoutePath);
        Assert.Equal("POST", endpoint.HttpMethod);
        Assert.Equal("app.order.save", endpoint.PermissionCode);
        Assert.True(endpoint.RequiresAuthentication);
        Assert.Contains("\"flowCode\":\"order_save_flow\"", endpoint.ConfigJson, StringComparison.Ordinal);
        Assert.Contains("\"startNodeId\":\"createOrder\"", endpoint.ConfigJson, StringComparison.Ordinal);

        var firstEndpointId = endpoint.Id;
        await UpdateMicroflowDefinitionAsync(
            db,
            microflowId,
            Definition("app/orders/save-v2", "patch", "app.order.edit", false));

        await service.PublishAsync(microflowId, new ApplicationDataCenterPublishRequest(), CancellationToken.None);

        var updatedEndpoint = await LoadEndpointAsync(db, "order_save_flow_save");
        Assert.NotNull(updatedEndpoint);
        Assert.Equal(firstEndpointId, updatedEndpoint.Id);
        Assert.Equal("/app/orders/save-v2", updatedEndpoint.RoutePath);
        Assert.Equal("PATCH", updatedEndpoint.HttpMethod);
        Assert.Equal("app.order.edit", updatedEndpoint.PermissionCode);
        Assert.False(updatedEndpoint.RequiresAuthentication);
        Assert.Equal(2, updatedEndpoint.VersionNo);
    }

    [Fact]
    public async Task PublishAsync_DisablesRemovedMicroflowApiEndpoint()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<
            ApplicationMicroflowEntity,
            ApplicationApiServiceEntity,
            ApplicationDataObjectReferenceEntity,
            ApplicationDataEntityDefinitionEntity,
            ApplicationDataFieldDefinitionEntity>();
        db.CodeFirst.InitTables<ApplicationDataModelDesignEntity>();
        db.CodeFirst.InitTables<
            ApplicationQueryDatasetEntity,
            ApplicationIntegrationTaskEntity>();
        var microflowId = Guid.NewGuid().ToString("N");
        var definition = Definition("/app/orders/save", "POST", "app.order.save", true);
        definition.ApiEndpoints.Add(new ApplicationMicroflowApiEndpointDefinition
        {
            EndpointCode = "legacy",
            EndpointName = "旧接口",
            HttpMethod = "POST",
            RoutePath = "/app/orders/legacy",
            StartNodeId = "createOrder",
            PermissionCode = "app.order.legacy",
            RequiresAuthentication = true
        });
        await InsertMicroflowAsync(db, microflowId, "order_save_flow", definition);
        var service = CreateService(db);

        await service.PublishAsync(microflowId, new ApplicationDataCenterPublishRequest(), CancellationToken.None);
        Assert.NotNull(await LoadEndpointAsync(db, "order_save_flow_legacy"));

        definition.ApiEndpoints.RemoveAll(item => item.EndpointCode == "legacy");
        await UpdateMicroflowDefinitionAsync(db, microflowId, definition);
        await service.PublishAsync(microflowId, new ApplicationDataCenterPublishRequest(), CancellationToken.None);

        var staleEndpoint = await LoadEndpointIncludingDeletedAsync(db, "order_save_flow_legacy");
        Assert.NotNull(staleEndpoint);
        Assert.True(staleEndpoint.IsDeleted);
        Assert.Equal(ApplicationDataCenterObjectStatus.Disabled, staleEndpoint.Status);
    }

    [Fact]
    public async Task ExecutePublishedAsync_RunsSelectedMicroflowWithoutPageContext()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<
            ApplicationMicroflowEntity,
            ApplicationApiServiceEntity,
            ApplicationDataObjectReferenceEntity,
            ApplicationDataEntityDefinitionEntity,
            ApplicationDataFieldDefinitionEntity>();
        db.CodeFirst.InitTables<ApplicationDataModelDesignEntity>();
        db.CodeFirst.InitTables<
            ApplicationQueryDatasetEntity,
            ApplicationIntegrationTaskEntity>();
        var microflowId = Guid.NewGuid().ToString("N");
        await InsertMicroflowAsync(
            db,
            microflowId,
            "order_save_flow",
            Definition("/app/orders/save", "POST", "app.order.save", true),
            ApplicationDataCenterObjectStatus.Published);
        var runtimeService = new CapturingMicroflowRuntimeService();
        var service = CreateService(db, runtimeService);
        var request = new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?>
        {
            ["keyword"] = "  abc  "
        });

        var response = await service.ExecutePublishedAsync(microflowId, request, CancellationToken.None);

        Assert.Equal("order_save_flow", runtimeService.FlowCode);
        Assert.Same(request, runtimeService.Request);
        Assert.Null(runtimeService.Request?.PageCode);
        Assert.Equal("order_save_flow", response.FlowCode);
        Assert.Equal("ok", response.Result);
    }

    [Fact]
    public async Task ExecutePublishedAsync_RejectsDraftMicroflow()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<
            ApplicationMicroflowEntity,
            ApplicationApiServiceEntity,
            ApplicationDataObjectReferenceEntity,
            ApplicationDataEntityDefinitionEntity,
            ApplicationDataFieldDefinitionEntity>();
        db.CodeFirst.InitTables<ApplicationDataModelDesignEntity>();
        db.CodeFirst.InitTables<
            ApplicationQueryDatasetEntity,
            ApplicationIntegrationTaskEntity>();
        var microflowId = Guid.NewGuid().ToString("N");
        await InsertMicroflowAsync(
            db,
            microflowId,
            "order_save_flow",
            Definition("/app/orders/save", "POST", "app.order.save", true),
            ApplicationDataCenterObjectStatus.Normal);
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ExecutePublishedAsync(microflowId, new ApplicationMicroflowExecuteRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task PreviewRunAsync_RunsDraftDefinitionWithoutPersisting()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<
            ApplicationMicroflowEntity,
            ApplicationApiServiceEntity,
            ApplicationDataObjectReferenceEntity,
            ApplicationDataEntityDefinitionEntity,
            ApplicationDataFieldDefinitionEntity>();
        db.CodeFirst.InitTables<ApplicationDataModelDesignEntity>();
        db.CodeFirst.InitTables<
            ApplicationQueryDatasetEntity,
            ApplicationIntegrationTaskEntity>();
        var microflowId = Guid.NewGuid().ToString("N");
        await InsertMicroflowAsync(
            db,
            microflowId,
            "order_save_flow",
            Definition("/app/orders/save", "POST", "app.order.save", true),
            ApplicationDataCenterObjectStatus.Normal);
        var draftDefinition = Definition("/app/orders/query", "GET", "app.order.list", true);
        var runtimeService = new CapturingMicroflowRuntimeService
        {
            Result = new[]
            {
                new Dictionary<string, object?> { ["id"] = "order-1", ["name"] = "Order 1" }
            },
            VariablesResult = new Dictionary<string, object?>
            {
                ["items"] = new[]
                {
                    new Dictionary<string, object?> { ["id"] = "order-1", ["name"] = "Order 1" }
                }
            }
        };
        var service = CreateService(db, runtimeService);

        var response = await service.PreviewRunAsync(
            microflowId,
            new ApplicationMicroflowPreviewRequest(
                "draft",
                new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?> { ["keyword"] = "order" }),
                ApplicationDataCenterJson.Serialize(draftDefinition),
                10),
            CancellationToken.None);

        Assert.Equal("draft", response.Mode);
        Assert.Equal("order_save_flow", runtimeService.FlowCode);
        Assert.NotNull(runtimeService.Definition);
        Assert.Equal("/app/orders/query", runtimeService.Definition.ApiEndpoints.Single().RoutePath);
        Assert.Contains(response.Datasets, item => item.Key == "result");
        Assert.Contains(response.Datasets, item => item.Key == "variables.items");
        var entity = await db.Queryable<ApplicationMicroflowEntity>().SingleAsync(item => item.Id == microflowId);
        Assert.Equal(ApplicationDataCenterObjectStatus.Normal, entity.Status);
        Assert.Contains("/app/orders/save", entity.ConfigJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewRunAsync_RunsPublishedModeThroughPublishedRuntime()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<
            ApplicationMicroflowEntity,
            ApplicationApiServiceEntity,
            ApplicationDataObjectReferenceEntity,
            ApplicationDataEntityDefinitionEntity,
            ApplicationDataFieldDefinitionEntity>();
        db.CodeFirst.InitTables<ApplicationDataModelDesignEntity>();
        db.CodeFirst.InitTables<
            ApplicationQueryDatasetEntity,
            ApplicationIntegrationTaskEntity>();
        var microflowId = Guid.NewGuid().ToString("N");
        await InsertMicroflowAsync(
            db,
            microflowId,
            "order_save_flow",
            Definition("/app/orders/save", "POST", "app.order.save", true),
            ApplicationDataCenterObjectStatus.Published);
        var runtimeService = new CapturingMicroflowRuntimeService();
        var service = CreateService(db, runtimeService);

        var response = await service.PreviewRunAsync(
            microflowId,
            new ApplicationMicroflowPreviewRequest("published", new ApplicationMicroflowExecuteRequest()),
            CancellationToken.None);

        Assert.Equal("published", response.Mode);
        Assert.Equal("order_save_flow", runtimeService.FlowCode);
        Assert.Null(runtimeService.Definition);
        Assert.Equal("order_save_flow", response.FlowCode);
    }

    [Fact]
    public async Task PreviewRunAsync_RejectsInvalidMode()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<
            ApplicationMicroflowEntity,
            ApplicationApiServiceEntity,
            ApplicationDataObjectReferenceEntity,
            ApplicationDataEntityDefinitionEntity,
            ApplicationDataFieldDefinitionEntity>();
        db.CodeFirst.InitTables<ApplicationDataModelDesignEntity>();
        db.CodeFirst.InitTables<
            ApplicationQueryDatasetEntity,
            ApplicationIntegrationTaskEntity>();
        var microflowId = Guid.NewGuid().ToString("N");
        await InsertMicroflowAsync(
            db,
            microflowId,
            "order_save_flow",
            Definition("/app/orders/save", "POST", "app.order.save", true));
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.PreviewRunAsync(
                microflowId,
                new ApplicationMicroflowPreviewRequest("unknown"),
                CancellationToken.None));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private ApplicationMicroflowService CreateService(
        ISqlSugarClient db,
        IApplicationMicroflowRuntimeService? runtimeService = null)
    {
        var currentUser = CreateCurrentUser();
        var accessor = new FixedWorkspaceDatabaseAccessor(db);
        var resolver = new ApplicationDataCenterWorkspaceResolver(currentUser);
        var expressionEvaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());
        return new ApplicationMicroflowService(
            new WorkspaceSqlSugarRepository<ApplicationMicroflowEntity>(accessor, currentUser),
            accessor,
            resolver,
            new NoopApplicationDataSecretProtector(),
            new ApplicationDataCenterRiskGuard(),
            new ApplicationObjectReferenceService(accessor, resolver),
            new ApplicationDataCenterTemplateCatalog(),
            new ApplicationDataCenterPublishedSnapshotService(accessor, resolver),
            new ApplicationMicroflowDefinitionValidator(),
            new ApplicationMicroflowOutputSchemaSynchronizer(),
            runtimeService ?? new CapturingMicroflowRuntimeService(),
            new ApplicationMicroflowPreviewResultBuilder(),
            CreateSqlScriptEngine(accessor, resolver, currentUser, expressionEvaluator),
            new ApplicationMicroflowRevisionService(accessor, resolver));
    }

    private ApplicationDataCenterSqlScriptEngine CreateSqlScriptEngine(
        IWorkspaceDatabaseAccessor accessor,
        ApplicationDataCenterWorkspaceResolver resolver,
        ICurrentUser currentUser,
        RuntimeValueExpressionEvaluator expressionEvaluator)
    {
        var hostRoot = Path.GetDirectoryName(databasePath) ?? Path.GetTempPath();
        var connectionFactory = new ApplicationDataSourceConnectionFactory(
            new TestHostEnvironment(hostRoot),
            new NoopApplicationDataSecretProtector(),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance));
        var parser = new ApplicationDataCenterSqlScriptParser();
        var functionCatalog = new RuntimeExpressionFunctionCatalog();
        var tokenizer = new ApplicationDataCenterSqlScriptExpressionTokenizer();
        var expressionParser = new ApplicationDataCenterSqlScriptExpressionParser(tokenizer);
        var scriptExpressionEvaluator = new ApplicationDataCenterSqlScriptExpressionEvaluator(
            expressionParser,
            functionCatalog,
            new RuntimeExpressionHelperCatalog(),
            new ApplicationDataCenterSqlRbacFunctionEvaluator(currentUser));
        var functionParameterizer = new ApplicationDataCenterSqlScriptFunctionParameterizer(
            expressionParser,
            scriptExpressionEvaluator);
        return new ApplicationDataCenterSqlScriptEngine(
            accessor,
            resolver,
            expressionEvaluator,
            connectionFactory,
            new ApplicationDataCenterSqlBuiltInVariableProvider(currentUser),
            parser,
            new ApplicationDataCenterSqlScriptValidator(parser, functionParameterizer),
            scriptExpressionEvaluator,
            functionParameterizer,
            new ApplicationDataCenterSqlScriptIfElseBlockReader(),
            new ApplicationDataCenterSqlScriptResultProjector(),
            new ApplicationDataCenterSqlScriptAuditWriter(
                accessor,
                resolver,
                currentUser,
                NullLogger<ApplicationDataCenterSqlScriptAuditWriter>.Instance),
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "test-microflow-service-sql-run"
                }
            },
            NullLogger<ApplicationDataCenterSqlScriptEngine>.Instance);
    }

    private SqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<ApplicationDataCenterPublishedSnapshot>();
        return db;
    }

    private static ApplicationMicroflowDefinition Definition(
        string routePath,
        string httpMethod,
        string permissionCode,
        bool requiresAuthentication) =>
        new()
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "createOrder",
                    Name = "Create Order",
                    Type = "create"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = new Dictionary<string, object?>
                    {
                        ["outputSchema"] = MinimalReturnSchema()
                    }
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "start-create",
                    SourceNodeId = "start",
                    TargetNodeId = "createOrder"
                },
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "create-return",
                    SourceNodeId = "createOrder",
                    TargetNodeId = "return"
                }
            ],
            ApiEndpoints =
            [
                new ApplicationMicroflowApiEndpointDefinition
                {
                    EndpointCode = "save",
                    EndpointName = "保存订单",
                    HttpMethod = httpMethod,
                    RoutePath = routePath,
                    StartNodeId = "createOrder",
                    PermissionCode = permissionCode,
                    RequiresAuthentication = requiresAuthentication
                }
            ]
        };

    private static ApplicationMicroflowOutputSchemaDefinition MinimalReturnSchema() =>
        new()
        {
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "string",
                    FieldCode = "status",
                    FieldName = "状态",
                    Expression = new RuntimeValueExpressionDto
                    {
                        DataType = "string",
                        Kind = "literal",
                        Value = "ok"
                    },
                    Visible = true,
                    Writable = false
                }
            ],
            ValueType = "object",
            VariableCode = "result",
            VariableName = "返回结果"
        };

    private static async Task InsertMicroflowAsync(
        ISqlSugarClient db,
        string id,
        string flowCode,
        ApplicationMicroflowDefinition definition,
        string status = ApplicationDataCenterObjectStatus.Normal)
    {
        await db.Insertable(new ApplicationMicroflowEntity
        {
            Id = id,
            TenantId = TenantId,
            AppCode = AppCode,
            ModuleKey = ApplicationDataCenterModuleKey.Microflow,
            ObjectCode = flowCode,
            ObjectName = "订单保存微流",
            ObjectType = "Microflow",
            Status = status,
            VersionNo = 1,
            ConfigJson = ApplicationDataCenterJson.Serialize(definition),
            CreatedBy = "admin",
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
    }

    private static async Task UpdateMicroflowDefinitionAsync(
        ISqlSugarClient db,
        string id,
        ApplicationMicroflowDefinition definition)
    {
        await db.Updateable<ApplicationMicroflowEntity>()
            .SetColumns(item => item.ConfigJson == ApplicationDataCenterJson.Serialize(definition))
            .Where(item => item.Id == id)
            .ExecuteCommandAsync();
    }

    private static async Task<ApplicationApiServiceEntity?> LoadEndpointAsync(ISqlSugarClient db, string objectCode)
    {
        var items = await db.Queryable<ApplicationApiServiceEntity>()
            .Where(item =>
                item.TenantId == TenantId &&
                item.AppCode == AppCode &&
                item.ObjectCode == objectCode &&
                !item.IsDeleted)
            .Take(1)
            .ToListAsync();
        return items.FirstOrDefault();
    }

    private static async Task<ApplicationApiServiceEntity?> LoadEndpointIncludingDeletedAsync(ISqlSugarClient db, string objectCode)
    {
        var items = await db.Queryable<ApplicationApiServiceEntity>()
            .Where(item =>
                item.TenantId == TenantId &&
                item.AppCode == AppCode &&
                item.ObjectCode == objectCode)
            .Take(1)
            .ToListAsync();
        return items.FirstOrDefault();
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            TenantId,
            "客户A",
            AppCode,
            "客户A MES",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            ["*"],
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var currentUser = new FixedAsterErpCurrentUser(principal);
        Assert.True(currentUser.IsAsterErpAuthenticated());
        return currentUser;
    }

    private sealed class FixedWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;

        public ISqlSugarClient GetCurrentDb() => db;

        public ISqlSugarClient RequireApplicationDb() => db;

        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);

        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class NoopApplicationDataSecretProtector : IApplicationDataSecretProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;

        public string BuildPublicSecretSummary(string? cipherText) =>
            string.IsNullOrWhiteSpace(cipherText) ? "{}" : "{\"configured\":true}";

        public string BuildPublicSecretSummary(string? cipherText, string secretRef, DateTime? updatedAt) =>
            BuildPublicSecretSummary(cipherText);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "AsterERP.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingMicroflowRuntimeService : IApplicationMicroflowRuntimeService
    {
        public string? FlowCode { get; private set; }

        public ApplicationMicroflowExecuteRequest? Request { get; private set; }

        public ApplicationMicroflowDefinition? Definition { get; private set; }

        public object? Result { get; set; } = "ok";

        public Dictionary<string, object?>? VariablesResult { get; set; }

        public Task<ApplicationMicroflowExecuteResponse> ExecuteAsync(
            string flowCode,
            ApplicationMicroflowExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            FlowCode = flowCode;
            Request = request;
            return Task.FromResult(new ApplicationMicroflowExecuteResponse(
                flowCode,
                Result,
                VariablesResult ?? request.Variables ?? [],
                ["captured"]));
        }

        public Task<ApplicationMicroflowExecuteResponse> ExecuteDefinitionAsync(
            string flowCode,
            ApplicationMicroflowDefinition definition,
            ApplicationMicroflowExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            FlowCode = flowCode;
            Definition = definition;
            Request = request;
            return Task.FromResult(new ApplicationMicroflowExecuteResponse(
                flowCode,
                Result,
                VariablesResult ?? request.Variables ?? [],
                ["draft:start"]));
        }
    }
}
