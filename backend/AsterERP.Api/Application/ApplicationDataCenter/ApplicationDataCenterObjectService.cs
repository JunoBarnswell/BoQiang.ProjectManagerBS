using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public abstract class ApplicationDataCenterObjectService<TEntity>(
    IRepository<TEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService)
    where TEntity : ApplicationDataCenterObjectEntity, new()
{
    protected abstract string ModuleKey { get; }

    public async Task<GridPageResult<ApplicationDataCenterObjectListItemResponse>> GetPageAsync(
        ApplicationDataCenterObjectListQuery query,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dbQuery = ScopedQuery(workspace);
        var keyword = query.Keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            dbQuery = dbQuery.Where(item =>
                item.ObjectCode.Contains(keyword) ||
                item.ObjectName.Contains(keyword) ||
                (item.Endpoint != null && item.Endpoint.Contains(keyword)) ||
                (item.Remark != null && item.Remark.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.ObjectType))
        {
            var objectType = query.ObjectType.Trim();
            dbQuery = dbQuery.Where(item => item.ObjectType == objectType);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.OwnerUserId))
        {
            var ownerUserId = query.OwnerUserId.Trim();
            dbQuery = dbQuery.Where(item => item.OwnerUserId == ownerUserId);
        }

        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 200);
        var total = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GridPageResult<ApplicationDataCenterObjectListItemResponse>
        {
            Total = total,
            Items = items.Select(MapListItem).ToList()
        };
    }

    public async Task<ApplicationDataCenterObjectDetailResponse> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public virtual async Task<ApplicationDataCenterOperationResponse> CreateAsync(
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var objectCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.ObjectCode, "对象编码");
        await EnsureUniqueCodeAsync(workspace, objectCode, null, cancellationToken);

        var entity = CreateEntity();
        entity.TenantId = workspace.TenantId;
        entity.AppCode = workspace.AppCode;
        entity.ModuleKey = ModuleKey;
        entity.ObjectCode = objectCode;
        entity.ObjectName = ApplicationDataCenterCodePolicy.NormalizeName(request.ObjectName, "对象名称");
        entity.ObjectType = ApplicationDataCenterCodePolicy.NormalizeCode(request.ObjectType, "对象类型");
        entity.Status = ApplicationDataCenterObjectStatus.Draft;
        entity.OwnerUserId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.OwnerUserId);
        entity.Environment = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Environment);
        entity.Endpoint = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Endpoint, 1000);
        entity.ConfigJson = ApplicationDataCenterJson.NormalizeObjectJson(request.ConfigJson, "配置");
        ValidatePublicConfigJson(entity.ConfigJson);
        entity.SecretConfigCipherText = ProtectSecret(request.SecretConfigJson);
        entity.SecretRef = entity.SecretConfigCipherText is null ? null : $"{entity.Id}:secret";
        entity.PublicConfigJson = secretProtector.BuildPublicSecretSummary(entity.SecretConfigCipherText, entity.SecretRef ?? string.Empty, entity.SecretConfigCipherText is null ? null : DateTime.UtcNow);
        entity.Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000);
        entity.CreatedBy = workspace.UserId;
        entity.CreatedTime = DateTime.UtcNow;
        entity.IsDeleted = false;
        ApplySpecificFields(entity, request, isCreate: true);

        await repository.InsertAsync(entity, cancellationToken);
        await AfterSavedAsync(db, workspace, entity, request, cancellationToken);
        var summary = await referenceService.RecalculateReferencesAsync(ModuleKey, entity.Id, cancellationToken);
        var detail = await MapDetailAsync(await EnsureEntityAsync(entity.Id, cancellationToken), cancellationToken);
        return new ApplicationDataCenterOperationResponse(detail, summary, detail.NextActions);
    }

    public virtual async Task<ApplicationDataCenterOperationResponse> UpdateAsync(
        string id,
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var objectCode = ApplicationDataCenterCodePolicy.NormalizeCode(request.ObjectCode, "对象编码");
        await EnsureUniqueCodeAsync(workspace, objectCode, id, cancellationToken);

        var normalizedConfig = ApplicationDataCenterJson.NormalizeObjectJson(request.ConfigJson, "配置");
        ValidatePublicConfigJson(normalizedConfig);
        var changedFields = BuildChangedFields(entity, objectCode, request, normalizedConfig);
        riskGuard.EnsureConfirmed(entity, changedFields, request.ConfirmedRiskFields);

        entity.ObjectCode = objectCode;
        entity.ObjectName = ApplicationDataCenterCodePolicy.NormalizeName(request.ObjectName, "对象名称");
        entity.ObjectType = ApplicationDataCenterCodePolicy.NormalizeCode(request.ObjectType, "对象类型");
        entity.OwnerUserId = ApplicationDataCenterCodePolicy.NormalizeOptional(request.OwnerUserId);
        entity.Environment = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Environment);
        entity.Endpoint = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Endpoint, 1000);
        entity.ConfigJson = normalizedConfig;
        if (request.SecretConfigJson is not null)
        {
            entity.SecretConfigCipherText = ProtectSecret(request.SecretConfigJson);
            entity.SecretRef = entity.SecretConfigCipherText is null
                ? null
                : entity.SecretRef ?? $"{entity.Id}:secret";
            entity.PublicConfigJson = secretProtector.BuildPublicSecretSummary(
                entity.SecretConfigCipherText,
                entity.SecretRef ?? string.Empty,
                entity.SecretConfigCipherText is null ? null : DateTime.UtcNow);
        }

        entity.Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000);
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        entity.VersionNo += 1;
        if (string.Equals(entity.Status, ApplicationDataCenterObjectStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            entity.Status = ApplicationDataCenterObjectStatus.Draft;
        }
        ApplySpecificFields(entity, request, isCreate: false);

        await repository.UpdateAsync(entity, cancellationToken);
        await AfterSavedAsync(db, workspace, entity, request, cancellationToken);
        var summary = await referenceService.RecalculateReferencesAsync(ModuleKey, entity.Id, cancellationToken);
        var detail = await MapDetailAsync(await EnsureEntityAsync(entity.Id, cancellationToken), cancellationToken);
        return new ApplicationDataCenterOperationResponse(detail, summary, detail.NextActions);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var summary = await referenceService.RecalculateReferencesAsync(ModuleKey, entity.Id, cancellationToken);
        if (summary.Total > 0)
        {
            throw new ValidationException("当前对象仍被引用，删除前请先解除引用关系", ErrorCodes.ApplicationDataCenterReferenceBlocked);
        }

        entity.IsDeleted = true;
        entity.DeletedBy = workspace.UserId;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await repository.UpdateAsync(entity, cancellationToken);
        return true;
    }

    public Task<ApplicationDataCenterOperationResponse> EnableAsync(string id, CancellationToken cancellationToken = default) =>
        SetStatusAsync(id, ApplicationDataCenterObjectStatus.Normal, cancellationToken);

    public Task<ApplicationDataCenterOperationResponse> DisableAsync(string id, CancellationToken cancellationToken = default) =>
        SetStatusAsync(id, ApplicationDataCenterObjectStatus.Disabled, cancellationToken);

    public virtual async Task<ApplicationDataCenterActionResultResponse> TestAsync(
        string id,
        ApplicationDataCenterActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        ApplicationDataCenterJson.NormalizeObjectJson(entity.ConfigJson, "配置");
        await MarkValidationAsync(entity.Id, "Normal", "配置校验通过", null, cancellationToken);
        return new ApplicationDataCenterActionResultResponse(
            true,
            "Normal",
            "配置校验通过",
            0,
            "{}",
            templateCatalog.BuildNextActions(ModuleKey, id, entity.Status));
    }

    public virtual async Task<ApplicationDataCenterPreviewResponse> PreviewAsync(
        string id,
        ApplicationDataCenterPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            config.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase)
        };
        var fields = config.Keys
            .Select((key, index) => new ApplicationDataCenterPreviewFieldResponse(key, key, "Text", true, false, index + 1))
            .ToArray();
        return new ApplicationDataCenterPreviewResponse(rows, fields, "配置预览");
    }

    public virtual async Task<ApplicationDataCenterOperationResponse> PublishAsync(
        string id,
        ApplicationDataCenterPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        riskGuard.EnsureConfirmed(entity, ["status"], request.ConfirmedRiskFields);
        await ValidateForPublishAsync(entity, cancellationToken);
        entity.Status = ApplicationDataCenterObjectStatus.Published;
        entity.VersionNo += 1;
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await repository.UpdateAsync(entity, cancellationToken);
        await snapshotService.CreateAsync(entity, BuildSnapshotBinding(entity), cancellationToken);
        var summary = await referenceService.RecalculateReferencesAsync(ModuleKey, entity.Id, cancellationToken);
        var detail = await MapDetailAsync(await EnsureEntityAsync(entity.Id, cancellationToken), cancellationToken);
        return new ApplicationDataCenterOperationResponse(detail, summary, detail.NextActions);
    }

    public Task<ApplicationDataCenterReferenceSummaryResponse> GetReferencesAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        referenceService.RecalculateReferencesAsync(ModuleKey, id, cancellationToken);

    protected virtual TEntity CreateEntity() => new();

    protected virtual void ApplySpecificFields(TEntity entity, ApplicationDataCenterObjectUpsertRequest request, bool isCreate)
    {
    }

    protected virtual void ValidatePublicConfigJson(string normalizedConfigJson)
    {
    }

    protected static void EnsureSecretRefOnlyPublicConfig(string normalizedConfigJson)
    {
        using var document = global::System.Text.Json.JsonDocument.Parse(normalizedConfigJson);
        var sensitiveProperty = FindSensitiveProperty(document.RootElement, null);
        if (sensitiveProperty is not null)
        {
            throw new ValidationException(
                $"ConfigJson contains sensitive property '{sensitiveProperty}'. Store the value in SecretConfigJson so it is persisted through SecretRef.",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    protected virtual IReadOnlyList<string> GetSpecificChangedFields(
        TEntity entity,
        ApplicationDataCenterObjectUpsertRequest request) => [];

    protected virtual Task AfterSavedAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        TEntity entity,
        ApplicationDataCenterObjectUpsertRequest request,
        CancellationToken cancellationToken) => Task.CompletedTask;

    protected async Task<TEntity> EnsureEntityAsync(string id, CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = (await ScopedQuery(workspace)
            .Where(item => item.Id == id)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity ?? throw new NotFoundException("数据中心对象不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    protected async Task MarkValidationAsync(
        string id,
        string status,
        string message,
        string? detailJson,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = (await ScopedQuery(workspace)
            .Where(item => item.Id == id)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("数据中心对象不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);

        entity.LastValidationStatus = status;
        entity.LastValidationMessage = message;
        entity.LastValidatedAt = DateTime.UtcNow;
        if (detailJson is not null)
        {
            entity.PublicConfigJson = detailJson;
        }

        await repository.UpdateAsync(entity, cancellationToken);
    }

    protected async Task<string?> UnprotectSecretAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        return string.IsNullOrWhiteSpace(entity.SecretConfigCipherText)
            ? null
            : secretProtector.Unprotect(entity.SecretConfigCipherText);
    }

    protected async Task<ApplicationDataCenterObjectDetailResponse> MapDetailAsync(
        TEntity entity,
        CancellationToken cancellationToken)
    {
        var summary = await referenceService.GetSummaryAsync(ModuleKey, entity.Id, cancellationToken);
        var nextActions = templateCatalog.BuildNextActions(ModuleKey, entity.Id, entity.Status);
        return new ApplicationDataCenterObjectDetailResponse(
            entity.Id,
            ModuleKey,
            entity.ObjectCode,
            entity.ObjectName,
            entity.ObjectType,
            entity.Status,
            entity.VersionNo,
            entity.OwnerUserId,
            entity.OwnerName,
            entity.Environment,
            entity.Endpoint,
            BuildPublicConfigJson(entity.ConfigJson),
            entity.PublicConfigJson,
            entity.LastValidationStatus,
            entity.LastValidationMessage,
            entity.LastValidatedAt,
            summary,
            nextActions,
            entity.CreatedTime,
            entity.UpdatedTime,
            entity.Remark);
    }

    protected ISugarQueryable<TEntity> ScopedQuery(ApplicationDataCenterWorkspace workspace) =>
        repository.Query()
            .Where(item =>
                item.ModuleKey == ModuleKey &&
                !item.IsDeleted);

    protected IWorkspaceDatabaseAccessor DatabaseAccessor => databaseAccessor;

    protected IRepository<TEntity> Repository => repository;

    protected ApplicationDataCenterWorkspaceResolver WorkspaceResolver => workspaceResolver;

    protected IApplicationDataSecretProtector SecretProtector => secretProtector;

    protected ApplicationObjectReferenceService ReferenceService => referenceService;

    protected ApplicationDataCenterTemplateCatalog TemplateCatalog => templateCatalog;

    protected ApplicationDataCenterPublishedSnapshotService SnapshotService => snapshotService;

    private static string BuildPublicConfigJson(string configJson)
    {
        using var document = global::System.Text.Json.JsonDocument.Parse(configJson);
        return global::System.Text.Json.JsonSerializer.Serialize(RedactPublicValue(document.RootElement, null));
    }

    private static object? RedactPublicValue(global::System.Text.Json.JsonElement value, string? propertyName)
    {
        if (propertyName is not null && IsSensitiveProperty(propertyName))
            return null;

        return value.ValueKind switch
        {
            global::System.Text.Json.JsonValueKind.Object => value.EnumerateObject()
                .Where(property => !IsSensitiveProperty(property.Name))
                .ToDictionary(property => property.Name, property => RedactPublicValue(property.Value, property.Name)),
            global::System.Text.Json.JsonValueKind.Array => value.EnumerateArray().Select(item => RedactPublicValue(item, null)).ToArray(),
            global::System.Text.Json.JsonValueKind.Null => null,
            _ => value.Clone()
        };
    }

    private static string? FindSensitiveProperty(global::System.Text.Json.JsonElement value, string? path)
    {
        if (value.ValueKind == global::System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = string.IsNullOrWhiteSpace(path) ? property.Name : $"{path}.{property.Name}";
                if (IsSensitiveProperty(property.Name))
                    return propertyPath;

                var nested = FindSensitiveProperty(property.Value, propertyPath);
                if (nested is not null)
                    return nested;
            }
        }
        else if (value.ValueKind == global::System.Text.Json.JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                var nested = FindSensitiveProperty(item, $"{path}[{index}]");
                if (nested is not null)
                    return nested;
                index++;
            }
        }

        return null;
    }

    private static bool IsSensitiveProperty(string propertyName) =>
        propertyName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("connectionstring", StringComparison.OrdinalIgnoreCase);

    protected virtual Task ValidateForPublishAsync(TEntity entity, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected virtual IReadOnlyDictionary<string, object?> BuildSnapshotBinding(TEntity entity) =>
        new Dictionary<string, object?>
        {
            ["objectCode"] = entity.ObjectCode,
            ["objectType"] = entity.ObjectType,
            ["endpoint"] = entity.Endpoint
        };

    private async Task<ApplicationDataCenterOperationResponse> SetStatusAsync(
        string id,
        string status,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        entity.Status = status;
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await repository.UpdateAsync(entity, cancellationToken);
        var summary = await referenceService.RecalculateReferencesAsync(ModuleKey, id, cancellationToken);
        var detail = await MapDetailAsync(await EnsureEntityAsync(id, cancellationToken), cancellationToken);
        return new ApplicationDataCenterOperationResponse(detail, summary, detail.NextActions);
    }

    private async Task EnsureUniqueCodeAsync(
        ApplicationDataCenterWorkspace workspace,
        string objectCode,
        string? excludingId,
        CancellationToken cancellationToken)
    {
        var exists = await ScopedQuery(workspace)
            .Where(item => item.ObjectCode == objectCode && item.Id != excludingId)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("对象编码已存在", ErrorCodes.ApplicationDataCenterDuplicateCode);
        }
    }

    private string? ProtectSecret(string? secretJson)
    {
        if (string.IsNullOrWhiteSpace(secretJson))
        {
            return null;
        }

        var normalized = ApplicationDataCenterJson.NormalizeObjectJson(secretJson, "凭据配置");
        return secretProtector.Protect(normalized);
    }

    private IReadOnlyList<string> BuildChangedFields(
        TEntity entity,
        string objectCode,
        ApplicationDataCenterObjectUpsertRequest request,
        string normalizedConfig)
    {
        var result = new List<string>();
        AddIfChanged(result, "objectCode", entity.ObjectCode, objectCode);
        AddIfChanged(result, "objectName", entity.ObjectName, request.ObjectName);
        AddIfChanged(result, "objectType", entity.ObjectType, request.ObjectType);
        AddIfChanged(result, "endpoint", entity.Endpoint, request.Endpoint);
        AddIfChanged(result, "configJson", entity.ConfigJson, normalizedConfig);
        result.AddRange(GetSpecificChangedFields(entity, request));
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddIfChanged(List<string> result, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue?.Trim(), newValue?.Trim(), StringComparison.Ordinal))
        {
            result.Add(field);
        }
    }

    private ApplicationDataCenterObjectListItemResponse MapListItem(TEntity entity) =>
        new(
            entity.Id,
            ModuleKey,
            entity.ObjectCode,
            entity.ObjectName,
            entity.ObjectType,
            entity.Status,
            entity.VersionNo,
            entity.OwnerUserId,
            entity.OwnerName,
            entity.Environment,
            entity.Endpoint,
            entity.LastValidationStatus,
            entity.LastValidationMessage,
            entity.LastValidatedAt,
            entity.ReferenceCount,
            entity.CreatedTime,
            entity.UpdatedTime,
            entity.Remark);
}
