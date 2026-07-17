using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowService : ApplicationDataCenterObjectService<ApplicationMicroflowEntity>
{
    private readonly ApplicationMicroflowDefinitionValidator validator;
    private readonly ApplicationMicroflowOutputSchemaSynchronizer outputSchemaSynchronizer;
    private readonly IApplicationMicroflowRuntimeService runtimeService;
    private readonly ApplicationMicroflowPreviewResultBuilder previewResultBuilder;
    private readonly ApplicationDataCenterSqlScriptEngine sqlScriptEngine;
    private readonly ApplicationMicroflowRevisionService revisionService;

    public ApplicationMicroflowService(
        IRepository<ApplicationMicroflowEntity> repository,
        IWorkspaceDatabaseAccessor databaseAccessor,
        ApplicationDataCenterWorkspaceResolver workspaceResolver,
        IApplicationDataSecretProtector secretProtector,
        ApplicationDataCenterRiskGuard riskGuard,
        ApplicationObjectReferenceService referenceService,
        ApplicationDataCenterTemplateCatalog templateCatalog,
        ApplicationDataCenterPublishedSnapshotService snapshotService,
        ApplicationMicroflowDefinitionValidator validator,
        ApplicationMicroflowOutputSchemaSynchronizer outputSchemaSynchronizer,
        IApplicationMicroflowRuntimeService runtimeService,
        ApplicationMicroflowPreviewResultBuilder previewResultBuilder,
        ApplicationDataCenterSqlScriptEngine sqlScriptEngine,
        ApplicationMicroflowRevisionService revisionService)
        : base(
        repository,
        databaseAccessor,
        workspaceResolver,
        secretProtector,
        riskGuard,
        referenceService,
        templateCatalog,
        snapshotService)
    {
        this.validator = validator;
        this.outputSchemaSynchronizer = outputSchemaSynchronizer;
        this.runtimeService = runtimeService;
        this.previewResultBuilder = previewResultBuilder;
        this.sqlScriptEngine = sqlScriptEngine;
        this.revisionService = revisionService;
    }

    protected override string ModuleKey => ApplicationDataCenterModuleKey.Microflow;

    public override async Task<ApplicationDataCenterOperationResponse> CreateAsync(
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await base.CreateAsync(request, cancellationToken);
        await revisionService.CreateForCurrentAsync(await EnsureEntityAsync(response.Object.Id, cancellationToken), cancellationToken);
        return response;
    }

    public override async Task<ApplicationDataCenterOperationResponse> UpdateAsync(
        string id,
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await base.UpdateAsync(id, request, cancellationToken);
        await revisionService.CreateForCurrentAsync(await EnsureEntityAsync(id, cancellationToken), cancellationToken);
        return response;
    }

    public async Task<IReadOnlyList<ApplicationMicroflowRevisionResponse>> ListVersionsAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        await revisionService.ListAsync(await EnsureEntityAsync(id, cancellationToken), cancellationToken);

    public async Task<ApplicationDataCenterActionResultResponse> ValidateRevisionAsync(
        string id,
        ApplicationMicroflowValidateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var revision = await revisionService.RequireCurrentAsync(entity, request.RevisionId, cancellationToken);
        var result = await TestAsync(id, new ApplicationDataCenterActionRequest(), cancellationToken);
        await revisionService.RecordValidationAsync(revision, result.Success, result.Message, cancellationToken);
        return result;
    }

    public async Task<ApplicationDataCenterOperationResponse> RestoreRevisionAsync(
        string id,
        ApplicationMicroflowRestoreRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var revision = await revisionService.RequireAsync(id, request.RevisionId, cancellationToken);
        return await UpdateAsync(id, new ApplicationDataCenterObjectUpsertRequest
        {
            ConfigJson = revision.ConfigJson,
            Endpoint = entity.Endpoint,
            Environment = entity.Environment,
            ObjectCode = entity.ObjectCode,
            ObjectName = entity.ObjectName,
            ObjectType = entity.ObjectType,
            OwnerUserId = entity.OwnerUserId,
            Remark = entity.Remark
        }, cancellationToken);
    }

    public async Task<ApplicationDataCenterOperationResponse> PublishRevisionAsync(
        string id,
        ApplicationMicroflowPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var revision = await revisionService.RequireCurrentAsync(entity, request.RevisionId, cancellationToken);
        if (!string.Equals(revision.ValidationStatus, "Passed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("当前版本尚未校验通过，请先完成校验。", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var response = await PublishAsync(id, new ApplicationDataCenterPublishRequest
        {
            ConfirmedRiskFields = request.ConfirmedRiskFields ?? []
        }, cancellationToken);
        var snapshot = await SnapshotService.GetLatestAsync(ModuleKey, id, cancellationToken);
        await revisionService.MarkPublishedAsync(revision, snapshot.Id, cancellationToken);
        return response;
    }

    protected override void ApplySpecificFields(
        ApplicationMicroflowEntity entity,
        ApplicationDataCenterObjectUpsertRequest request,
        bool isCreate)
    {
        var definition = outputSchemaSynchronizer.Synchronize(ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson));
        entity.ConfigJson = ApplicationDataCenterJson.Serialize(definition);
        entity.EndpointCount = definition.ApiEndpoints.Count;
        entity.DefaultEndpointPath = definition.ApiEndpoints.FirstOrDefault()?.RoutePath;
        entity.Endpoint = entity.DefaultEndpointPath;
    }

    public override async Task<ApplicationDataCenterActionResultResponse> TestAsync(
        string id,
        ApplicationDataCenterActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var definition = outputSchemaSynchronizer.Synchronize(ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson));
        var errors = validator.Validate(definition);
        var success = errors.Count == 0;
        var detail = ApplicationDataCenterJson.Serialize(new
        {
            domainObjectCount = definition.DomainObjects.Count,
            nodeCount = definition.Nodes.Count,
            edgeCount = definition.Edges.Count,
            endpointCount = definition.ApiEndpoints.Count,
            errors
        });
        await MarkValidationAsync(
            id,
            success ? ApplicationDataCenterObjectStatus.Normal : ApplicationDataCenterObjectStatus.Error,
            success ? "微流校验通过" : string.Join("；", errors),
            detail,
            cancellationToken);
        return new ApplicationDataCenterActionResultResponse(
            success,
            success ? ApplicationDataCenterObjectStatus.Normal : ApplicationDataCenterObjectStatus.Error,
            success ? "微流校验通过" : "微流校验失败",
            0,
            detail,
            TemplateCatalog.BuildNextActions(ModuleKey, id, entity.Status));
    }

    public override async Task<ApplicationDataCenterPreviewResponse> PreviewAsync(
        string id,
        ApplicationDataCenterPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var definition = outputSchemaSynchronizer.Synchronize(ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson));
        var rows = definition.Nodes
            .Select(node => new Dictionary<string, object?>
            {
                ["nodeId"] = node.Id,
                ["nodeName"] = node.Name,
                ["nodeType"] = node.Type,
                ["x"] = node.X,
                ["y"] = node.Y
            })
            .Cast<IReadOnlyDictionary<string, object?>>()
            .ToArray();
        ApplicationDataCenterPreviewFieldResponse[] fields =
        [
            new("nodeId", "节点ID", "Text", false, true, 1),
            new("nodeName", "节点名称", "Text", true, false, 2),
            new("nodeType", "节点类型", "Text", true, false, 3),
            new("x", "X", "Number", true, false, 4),
            new("y", "Y", "Number", true, false, 5)
        ];
        return new ApplicationDataCenterPreviewResponse(rows, fields, "微流节点预览");
    }

    public async Task<ApplicationMicroflowExecuteResponse> ExecutePublishedAsync(
        string id,
        ApplicationMicroflowExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        if (!string.Equals(entity.Status, ApplicationDataCenterObjectStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("微流未发布，无法运行已发布版本", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return await runtimeService.ExecuteAsync(entity.ObjectCode, request, cancellationToken);
    }

    public async Task<ApplicationMicroflowPreviewResponse> PreviewRunAsync(
        string id,
        ApplicationMicroflowPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var mode = NormalizePreviewMode(request.Mode);
        var executeRequest = request.ExecuteRequest ?? new ApplicationMicroflowExecuteRequest();
        if (string.Equals(mode, "published", StringComparison.OrdinalIgnoreCase))
        {
            var definition = ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson);
            definition = outputSchemaSynchronizer.Synchronize(definition);
            var execution = await ExecutePublishedAsync(id, executeRequest, cancellationToken);
            return previewResultBuilder.Build(mode, definition, execution, request.MaxRows, request.PreferredResultPath);
        }

        var draftConfigJson = string.IsNullOrWhiteSpace(request.DraftConfigJson)
            ? entity.ConfigJson
            : request.DraftConfigJson;
        var draftDefinition = outputSchemaSynchronizer.Synchronize(ApplicationMicroflowDefinitionReader.Read(draftConfigJson));
        validator.EnsureValid(draftDefinition);
        var draftExecution = await runtimeService.ExecuteDefinitionAsync(entity.ObjectCode, draftDefinition, executeRequest, cancellationToken);
        return previewResultBuilder.Build(mode, draftDefinition, draftExecution, request.MaxRows, request.PreferredResultPath);
    }

    public async Task<ApplicationDataCenterPreviewResponse> RunSqlScriptAsync(
        string id,
        ApplicationMicroflowSqlScriptRunRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureEntityAsync(id, cancellationToken);
        var definition = request.Definition;
        var node = definition.Nodes.FirstOrDefault(item => string.Equals(item.Id, request.NodeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("SQL Run 节点不存在", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var variables = new Dictionary<string, object?>(request.ExecuteRequest?.Variables ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var variable in definition.Variables.Concat(definition.Inputs).Concat(definition.Outputs))
        {
            variables.TryAdd(variable.VariableCode, variable.DefaultValue);
        }

        foreach (var variable in ApplicationMicroflowGlobalVariableNodeReader.ReadVariables(definition))
        {
            if (!string.IsNullOrWhiteSpace(variable.VariableCode))
            {
                variables.TryAdd(variable.VariableCode, variable.DefaultValue);
            }
        }

        var sources = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase)
        {
            ["inputs"] = variables,
            ["input"] = variables,
            ["variables"] = variables,
            ["vars"] = variables
        };
        var execution = await sqlScriptEngine.ExecuteAsync(
            new ApplicationDataCenterSqlScriptExecutionRequest
            {
                ContextDataSourceId = ResolveContextDataSourceId(definition),
                ExpressionContext = new RuntimeExpressionEvaluationContext(sources),
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                SqlScript = request.SqlScript,
                SourceId = node.Id,
                SourceKind = "MicroflowSqlRun",
                SourceName = node.Name
            },
            cancellationToken);
        return execution.Preview;
    }

    public override async Task<ApplicationDataCenterOperationResponse> PublishAsync(
        string id,
        ApplicationDataCenterPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = WorkspaceResolver.Resolve();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var definition = outputSchemaSynchronizer.Synchronize(ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson));
        validator.EnsureValid(definition);
        entity.ConfigJson = ApplicationDataCenterJson.Serialize(definition);
        await Repository.UpdateAsync(entity, cancellationToken);
        await UpsertApiEndpointsAsync(db, workspace, entity, definition, cancellationToken);
        return await base.PublishAsync(id, request, cancellationToken);
    }

    private static string? ResolveContextDataSourceId(ApplicationMicroflowDefinition definition) =>
        definition.DataMappings
            .FirstOrDefault(item => string.Equals(item.Target, "dataSourceId", StringComparison.OrdinalIgnoreCase))
            ?.Expression?.Value?.ToString();

    private static async Task UpsertApiEndpointsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationMicroflowEntity microflow,
        ApplicationMicroflowDefinition definition,
        CancellationToken cancellationToken)
    {
        var activeObjectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in definition.ApiEndpoints)
        {
            var routePath = NormalizeRoutePath(endpoint.RoutePath);
            var httpMethod = NormalizeMethod(endpoint.HttpMethod);
            var objectCode = ApplicationDataCenterCodePolicy.NormalizeCode($"{microflow.ObjectCode}_{endpoint.EndpointCode}", "接口编码");
            activeObjectCodes.Add(objectCode);
            var existing = (await db.Queryable<ApplicationApiServiceEntity>()
                .Where(item =>
                    item.ModuleKey == ApplicationDataCenterModuleKey.ApiService &&
                    item.ObjectCode == objectCode &&
                    !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();
            var configJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
            {
                ["microflowId"] = microflow.Id,
                ["flowCode"] = microflow.ObjectCode,
                ["startNodeId"] = endpoint.StartNodeId,
                ["endpointCode"] = endpoint.EndpointCode,
                ["httpMethod"] = httpMethod,
                ["routePath"] = routePath,
                ["permissionCode"] = endpoint.PermissionCode,
                ["requiresAuthentication"] = endpoint.RequiresAuthentication
            });
            var now = DateTime.UtcNow;
            if (existing is null)
            {
                existing = new ApplicationApiServiceEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    TenantId = workspace.TenantId,
                    AppCode = workspace.AppCode,
                    ModuleKey = ApplicationDataCenterModuleKey.ApiService,
                    ObjectCode = objectCode,
                    ObjectName = string.IsNullOrWhiteSpace(endpoint.EndpointName) ? $"{microflow.ObjectName}接口" : endpoint.EndpointName.Trim(),
                    ObjectType = ApplicationApiServiceSourceType.Microflow,
                    Status = ApplicationDataCenterObjectStatus.Published,
                    SourceObjectId = microflow.Id,
                    RoutePath = routePath,
                    HttpMethod = httpMethod,
                    Endpoint = routePath,
                    PermissionCode = endpoint.PermissionCode,
                    RequiresAuthentication = endpoint.RequiresAuthentication,
                    ConfigJson = configJson,
                    CreatedBy = workspace.UserId,
                    CreatedTime = now
                };
                await db.Insertable(existing).ExecuteCommandAsync(cancellationToken);
                continue;
            }

            existing.ObjectName = string.IsNullOrWhiteSpace(endpoint.EndpointName) ? $"{microflow.ObjectName}接口" : endpoint.EndpointName.Trim();
            existing.ObjectType = ApplicationApiServiceSourceType.Microflow;
            existing.Status = ApplicationDataCenterObjectStatus.Published;
            existing.SourceObjectId = microflow.Id;
            existing.RoutePath = routePath;
            existing.HttpMethod = httpMethod;
            existing.Endpoint = routePath;
            existing.PermissionCode = endpoint.PermissionCode;
            existing.RequiresAuthentication = endpoint.RequiresAuthentication;
            existing.ConfigJson = configJson;
            existing.UpdatedBy = workspace.UserId;
            existing.UpdatedTime = now;
            existing.VersionNo += 1;
            await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }

        await DisableRemovedApiEndpointsAsync(db, workspace, microflow, activeObjectCodes, cancellationToken);
    }

    private static async Task DisableRemovedApiEndpointsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationMicroflowEntity microflow,
        IReadOnlySet<string> activeObjectCodes,
        CancellationToken cancellationToken)
    {
        var staleEndpoints = await db.Queryable<ApplicationApiServiceEntity>()
            .Where(item =>
                item.ModuleKey == ApplicationDataCenterModuleKey.ApiService &&
                item.ObjectType == ApplicationApiServiceSourceType.Microflow &&
                item.SourceObjectId == microflow.Id &&
                !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (staleEndpoints.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var endpoint in staleEndpoints.Where(endpoint => !activeObjectCodes.Contains(endpoint.ObjectCode)))
        {
            endpoint.IsDeleted = true;
            endpoint.Status = ApplicationDataCenterObjectStatus.Disabled;
            endpoint.UpdatedBy = workspace.UserId;
            endpoint.UpdatedTime = now;
            endpoint.VersionNo += 1;
            await db.Updateable(endpoint).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static string NormalizeMethod(string method)
    {
        var normalized = method.Trim().ToUpperInvariant();
        if (normalized is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE"))
        {
            throw new ValidationException("微流接口请求方式仅支持 GET、POST、PUT、PATCH、DELETE", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return normalized;
    }

    private static string NormalizeRoutePath(string routePath)
    {
        var normalized = routePath.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        if (normalized.Length > 200 || normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new ValidationException("微流接口路径不合法", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return normalized;
    }

    private static string NormalizePreviewMode(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "draft" : mode.Trim().ToLowerInvariant();
        if (normalized is not ("draft" or "published"))
        {
            throw new ValidationException("微流预览模式仅支持 draft 或 published", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }
}
