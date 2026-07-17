using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

/// <summary>
/// Owns the independently managed application API catalog.
/// Microflow publication may register generated entries, but it does not replace this resource boundary.
/// </summary>
public sealed class ApplicationApiServiceService(
    IRepository<ApplicationApiServiceEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService)
    : ApplicationDataCenterObjectService<ApplicationApiServiceEntity>(
        repository,
        databaseAccessor,
        workspaceResolver,
        secretProtector,
        riskGuard,
        referenceService,
        templateCatalog,
        snapshotService)
{
    protected override string ModuleKey => ApplicationDataCenterModuleKey.ApiService;

    protected override void ValidatePublicConfigJson(string normalizedConfigJson) =>
        EnsureSecretRefOnlyPublicConfig(normalizedConfigJson);

    public override async Task<ApplicationDataCenterOperationResponse> CreateAsync(
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        await NormalizeAndValidateAsync(request, null, cancellationToken);
        return await base.CreateAsync(request, cancellationToken);
    }

    public override async Task<ApplicationDataCenterOperationResponse> UpdateAsync(
        string id,
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        await NormalizeAndValidateAsync(request, id, cancellationToken);
        return await base.UpdateAsync(id, request, cancellationToken);
    }

    public override async Task<ApplicationDataCenterActionResultResponse> TestAsync(
        string id,
        ApplicationDataCenterActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        ValidateEntity(entity);
        await EnsureSourceExistsAsync(entity.ObjectType, entity.SourceObjectId, cancellationToken);
        var detailJson = ApplicationDataCenterJson.Serialize(new
        {
            routePath = entity.RoutePath,
            httpMethod = entity.HttpMethod,
            sourceType = entity.ObjectType,
            sourceObjectId = entity.SourceObjectId,
            requiresAuthentication = entity.RequiresAuthentication,
            permissionCode = entity.PermissionCode
        });
        await MarkValidationAsync(id, ApplicationDataCenterObjectStatus.Normal, "API 服务配置校验通过", detailJson, cancellationToken);
        return new ApplicationDataCenterActionResultResponse(
            true,
            ApplicationDataCenterObjectStatus.Normal,
            "API 服务配置校验通过",
            0,
            detailJson,
            TemplateCatalog.BuildNextActions(ModuleKey, id, entity.Status));
    }

    public override async Task<ApplicationDataCenterPreviewResponse> PreviewAsync(
        string id,
        ApplicationDataCenterPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        ValidateEntity(entity);
        IReadOnlyDictionary<string, object?> row = new Dictionary<string, object?>
        {
            ["objectCode"] = entity.ObjectCode,
            ["objectName"] = entity.ObjectName,
            ["routePath"] = entity.RoutePath,
            ["httpMethod"] = entity.HttpMethod,
            ["sourceType"] = entity.ObjectType,
            ["sourceObjectId"] = entity.SourceObjectId,
            ["requiresAuthentication"] = entity.RequiresAuthentication,
            ["permissionCode"] = entity.PermissionCode,
            ["status"] = entity.Status
        };
        var fields = row.Keys
            .Select((key, index) => new ApplicationDataCenterPreviewFieldResponse(key, key, ResolveDataType(row[key]), true, false, index + 1))
            .ToArray();
        return new ApplicationDataCenterPreviewResponse([row], fields, "API 服务路由预览");
    }

    public override async Task<ApplicationDataCenterOperationResponse> PublishAsync(
        string id,
        ApplicationDataCenterPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        ValidateEntity(entity);
        await EnsureRouteUniqueAsync(entity.RoutePath, entity.HttpMethod, entity.Id, cancellationToken);
        return await base.PublishAsync(id, request, cancellationToken);
    }

    protected override void ApplySpecificFields(
        ApplicationApiServiceEntity entity,
        ApplicationDataCenterObjectUpsertRequest request,
        bool isCreate)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        entity.HttpMethod = NormalizeMethod(ReadString(config, "httpMethod") ?? "GET");
        entity.RoutePath = NormalizeRoute(request.Endpoint ?? ReadString(config, "routePath") ?? string.Empty);
        entity.Endpoint = entity.RoutePath;
        entity.SourceObjectId = NormalizeOptional(ReadString(config, "sourceObjectId"));
        entity.PermissionCode = NormalizeOptional(ReadString(config, "permissionCode"), 256);
        entity.RequiresAuthentication = ReadBoolean(config, "requiresAuthentication", true);
    }

    protected override IReadOnlyList<string> GetSpecificChangedFields(
        ApplicationApiServiceEntity entity,
        ApplicationDataCenterObjectUpsertRequest request)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        var route = NormalizeRoute(request.Endpoint ?? ReadString(config, "routePath") ?? string.Empty);
        var method = NormalizeMethod(ReadString(config, "httpMethod") ?? "GET");
        return new[]
        {
            entity.RoutePath == route ? string.Empty : "routePath",
            entity.HttpMethod == method ? string.Empty : "httpMethod",
            entity.PermissionCode == NormalizeOptional(ReadString(config, "permissionCode"), 256) ? string.Empty : "permissionCode"
        }.Where(item => item.Length > 0).ToArray();
    }

    private async Task NormalizeAndValidateAsync(
        ApplicationDataCenterObjectUpsertRequest request,
        string? excludingId,
        CancellationToken cancellationToken)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        var route = NormalizeRoute(request.Endpoint ?? ReadString(config, "routePath") ?? string.Empty);
        request.Endpoint = route;
        config = new Dictionary<string, object?>(config, StringComparer.OrdinalIgnoreCase)
        {
            ["routePath"] = route,
            ["httpMethod"] = NormalizeMethod(ReadString(config, "httpMethod") ?? "GET")
        };
        request.ConfigJson = ApplicationDataCenterJson.Serialize(config);
        var sourceType = ApplicationDataCenterCodePolicy.NormalizeCode(request.ObjectType, "API 服务类型");
        ValidateConfiguration(sourceType, config);
        await EnsureRouteUniqueAsync(route, ReadString(config, "httpMethod")!, excludingId, cancellationToken);
        await EnsureSourceExistsAsync(sourceType, ReadString(config, "sourceObjectId"), cancellationToken);
        await PopulateRuntimeBindingAsync(config, sourceType, ReadString(config, "sourceObjectId"), cancellationToken);
        request.ConfigJson = ApplicationDataCenterJson.Serialize(config);
    }

    private async Task EnsureRouteUniqueAsync(string routePath, string httpMethod, string? excludingId, CancellationToken cancellationToken)
    {
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var exists = await db.Queryable<ApplicationApiServiceEntity>()
            .Where(item =>
                item.ModuleKey == ModuleKey &&
                item.RoutePath == routePath &&
                item.HttpMethod == httpMethod &&
                item.Id != excludingId &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("同一应用中 API 路由与请求方法已存在", ErrorCodes.ApplicationDataCenterDuplicateCode);
        }
    }

    private async Task EnsureSourceExistsAsync(string sourceType, string? sourceObjectId, CancellationToken cancellationToken)
    {
        if (sourceType is ApplicationApiServiceSourceType.Webhook or ApplicationApiServiceSourceType.ExternalProxy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sourceObjectId))
        {
            throw new ValidationException("API 服务必须绑定来源对象", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var workspace = WorkspaceResolver.Resolve();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var found = sourceType switch
        {
            ApplicationApiServiceSourceType.Microflow => await db.Queryable<ApplicationMicroflowEntity>().Where(item => item.Id == sourceObjectId && !item.IsDeleted).AnyAsync(cancellationToken),
            ApplicationApiServiceSourceType.SqlQuery => await db.Queryable<ApplicationDataSourceEntity>().Where(item => item.Id == sourceObjectId && !item.IsDeleted).AnyAsync(cancellationToken),
            _ => false
        };
        if (!found)
        {
            throw new NotFoundException("API 服务来源对象不存在或不属于当前应用工作区", ErrorCodes.ApplicationDataCenterObjectNotFound);
        }
    }

    private async Task PopulateRuntimeBindingAsync(
        Dictionary<string, object?> config,
        string sourceType,
        string? sourceObjectId,
        CancellationToken cancellationToken)
    {
        if (sourceType != ApplicationApiServiceSourceType.Microflow || string.IsNullOrWhiteSpace(sourceObjectId) || !string.IsNullOrWhiteSpace(ReadString(config, "flowCode")))
        {
            return;
        }

        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var flowCode = (await db.Queryable<ApplicationMicroflowEntity>()
            .Where(item => item.Id == sourceObjectId && !item.IsDeleted)
            .Select(item => item.ObjectCode)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(flowCode))
        {
            config["flowCode"] = flowCode;
        }
    }

    private static void ValidateConfiguration(string sourceType, IReadOnlyDictionary<string, object?> config)
    {
        switch (sourceType)
        {
            case ApplicationApiServiceSourceType.Microflow when string.IsNullOrWhiteSpace(ReadString(config, "flowCode")) && string.IsNullOrWhiteSpace(ReadString(config, "sourceObjectId")):
                throw new ValidationException("微流 API 服务缺少 flowCode 或 sourceObjectId", ErrorCodes.ApplicationDataCenterInvalidConfig);
            case ApplicationApiServiceSourceType.SqlQuery when string.IsNullOrWhiteSpace(ReadString(config, "sql")) && string.IsNullOrWhiteSpace(ReadString(config, "tableName")):
                throw new ValidationException("SQL API 服务必须配置 sql 或 tableName", ErrorCodes.ApplicationDataCenterInvalidConfig);
            case ApplicationApiServiceSourceType.ExternalProxy:
                var url = ReadString(config, "baseUrl") ?? ReadString(config, "url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                {
                    throw new ValidationException("外部代理必须配置绝对 HTTP(S) URL", ErrorCodes.ApplicationDataCenterInvalidConfig);
                }
                break;
        }
    }

    private static void ValidateEntity(ApplicationApiServiceEntity entity)
    {
        NormalizeRoute(entity.RoutePath);
        NormalizeMethod(entity.HttpMethod);
        ValidateConfiguration(entity.ObjectType, ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson));
    }

    private static string NormalizeRoute(string routePath)
    {
        var normalized = routePath.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 200 || normalized.Contains("..", StringComparison.Ordinal) || normalized.Contains('?'))
        {
            throw new ValidationException("API 路由不能为空、不能超过 200 个字符且不能包含非法路径段", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
        if (!normalized.StartsWith('/')) normalized = "/" + normalized;
        return normalized.TrimEnd('/');
    }

    private static string NormalizeMethod(string method)
    {
        var normalized = method.Trim().ToUpperInvariant();
        if (normalized is not ("GET" or "POST" or "PUT" or "PATCH" or "DELETE"))
        {
            throw new ValidationException("API 请求方法仅支持 GET、POST、PUT、PATCH、DELETE", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
        return normalized;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static string? NormalizeOptional(string? value, int maxLength = 255) =>
        ApplicationDataCenterCodePolicy.NormalizeOptional(value, maxLength);

    private static bool ReadBoolean(IReadOnlyDictionary<string, object?> config, string key, bool fallback) =>
        config.TryGetValue(key, out var value) && value is not null && bool.TryParse(value.ToString(), out var parsed) ? parsed : fallback;

    private static string ResolveDataType(object? value) => value switch
    {
        bool => "Boolean",
        int or long or decimal or double => "Number",
        _ => "Text"
    };
}
