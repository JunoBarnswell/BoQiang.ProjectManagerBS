using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.Text.Json;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationObjectReferenceService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver)
{
    private static readonly DesignerDocumentReferenceExtractor DesignerDocumentReferences = new();

    public async Task<ApplicationDataCenterReferenceSummaryResponse> GetSummaryAsync(
        string moduleKey,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var items = await db.Queryable<ApplicationDataObjectReferenceEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TargetModule == moduleKey &&
                item.TargetObjectId == objectId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);

        return BuildSummary(moduleKey, objectId, items);
    }

    public async Task<ApplicationDataCenterReferenceSummaryResponse> RecalculateReferencesAsync(
        string moduleKey,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var target = await GetObjectIdentityAsync(db, workspace, moduleKey, objectId, cancellationToken)
            ?? throw new NotFoundException("数据中心对象不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);

        var existing = await db.Queryable<ApplicationDataObjectReferenceEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TargetModule == moduleKey &&
                item.TargetObjectId == objectId)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var item in existing)
        {
            item.IsDeleted = true;
            item.DeletedTime = now;
            item.UpdatedTime = now;
        }

        if (existing.Count > 0)
        {
            await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
        }

        var references = new List<ApplicationDataObjectReferenceEntity>();
        await ScanSourceAsync<ApplicationDataEntityDefinitionEntity>(db, workspace, target, ApplicationDataCenterModuleKey.EntityField, references, cancellationToken);
        await ScanSourceAsync<ApplicationDataFieldDefinitionEntity>(db, workspace, target, ApplicationDataCenterModuleKey.EntityField, references, cancellationToken);
        await ScanSourceAsync<ApplicationQueryDatasetEntity>(db, workspace, target, ApplicationDataCenterModuleKey.QueryDataset, references, cancellationToken);
        await ScanSourceAsync<ApplicationIntegrationTaskEntity>(db, workspace, target, ApplicationDataCenterModuleKey.IntegrationTask, references, cancellationToken);
        await ScanSourceAsync<ApplicationMicroflowEntity>(db, workspace, target, ApplicationDataCenterModuleKey.Microflow, references, cancellationToken);
        await ScanMappingCachesAsync(db, workspace, target, references, cancellationToken);
        await ScanLatestPageBindingsAsync(db, workspace, target, references, cancellationToken);
        try
        {
            if (db.DbMaintenance.IsAnyTable("app_data_model_designs", false))
            {
                await ScanSourceAsync<ApplicationDataModelDesignEntity>(db, workspace, target, ApplicationDataCenterModuleKey.DataModel, references, cancellationToken);
            }
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1 && exception.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)) { }

        try
        {
            if (db.DbMaintenance.IsAnyTable("app_api_services", false))
            {
                await ScanSourceAsync<ApplicationApiServiceEntity>(db, workspace, target, ApplicationDataCenterModuleKey.ApiService, references, cancellationToken);
            }
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1 && exception.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)) { }

        if (references.Count > 0)
        {
            await db.Insertable(references).ExecuteCommandAsync(cancellationToken);
        }

        await UpdateReferenceCountAsync(db, workspace, moduleKey, objectId, references.Count, cancellationToken);
        return BuildSummary(moduleKey, objectId, references);
    }

    private static ApplicationDataCenterReferenceSummaryResponse BuildSummary(
        string moduleKey,
        string objectId,
        IReadOnlyList<ApplicationDataObjectReferenceEntity> items)
    {
        return new ApplicationDataCenterReferenceSummaryResponse(
            moduleKey,
            objectId,
            items.Count,
            items.Count(item => item.SourceModule == ApplicationDataCenterModuleKey.Microflow),
            items.Count(item => item.SourceModule == ApplicationDataCenterModuleKey.QueryDataset),
            items.Count(item => item.SourceModule == ApplicationDataCenterModuleKey.IntegrationTask),
            items.Count(item => item.SourceModule == "page"),
            items.Select(MapReference).ToArray());
    }

    private static ApplicationDataCenterReferenceItemResponse MapReference(ApplicationDataObjectReferenceEntity item) =>
        new(
            item.Id,
            item.SourceModule,
            item.SourceObjectId,
            item.SourceObjectCode,
            item.SourceObjectName,
            item.TargetModule,
            item.TargetObjectId,
            item.ReferenceKind,
            item.Status,
            item.OwnerUserId,
            item.CreatedTime);

    private static async Task ScanSourceAsync<TEntity>(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDataObjectIdentity target,
        string sourceModule,
        List<ApplicationDataObjectReferenceEntity> references,
        CancellationToken cancellationToken)
        where TEntity : ApplicationDataCenterObjectEntity, new()
    {
        var sourceObjects = await db.Queryable<TEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var source in sourceObjects)
        {
            if (source.Id == target.ObjectId)
            {
                continue;
            }

            if (!ReferencesTarget(source, target.ObjectId))
            {
                continue;
            }

            references.Add(new ApplicationDataObjectReferenceEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                SourceModule = sourceModule,
                SourceObjectId = source.Id,
                SourceObjectCode = source.ObjectCode,
                SourceObjectName = source.ObjectName,
                TargetModule = target.ModuleKey,
                TargetObjectId = target.ObjectId,
                ReferenceKind = ResolveReferenceKind(sourceModule, target.ModuleKey),
                Status = source.Status,
                OwnerUserId = source.OwnerUserId,
                CreatedTime = DateTime.UtcNow,
                IsDeleted = false
            });
        }
    }

    private static bool ReferencesTarget(ApplicationDataCenterObjectEntity source, string targetObjectId)
    {
        if (string.IsNullOrWhiteSpace(targetObjectId))
        {
            return false;
        }

        if (ContainsJsonValue(source.ConfigJson, targetObjectId) ||
            (source.PublicConfigJson is not null && ContainsJsonValue(source.PublicConfigJson, targetObjectId)) ||
            string.Equals(source.Endpoint, targetObjectId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return source switch
        {
            ApplicationDataEntityDefinitionEntity entity => entity.ModelId == targetObjectId,
            ApplicationDataFieldDefinitionEntity field => field.ModelId == targetObjectId || field.EntityId == targetObjectId,
            ApplicationQueryDatasetEntity dataset => dataset.SourceObjectId == targetObjectId || dataset.RuntimeViewId == targetObjectId,
            ApplicationIntegrationTaskEntity task => task.SourceObjectId == targetObjectId || task.TargetObjectId == targetObjectId,
            _ => false
        };
    }

    private static bool ContainsJsonValue(string json, string targetObjectId)
    {
        try
        {
            using var document = global::System.Text.Json.JsonDocument.Parse(json);
            return ContainsJsonValue(document.RootElement, targetObjectId);
        }
        catch (global::System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool ContainsJsonValue(global::System.Text.Json.JsonElement element, string targetObjectId)
    {
        if (element.ValueKind == global::System.Text.Json.JsonValueKind.String)
            return string.Equals(element.GetString(), targetObjectId, StringComparison.OrdinalIgnoreCase);
        if (element.ValueKind == global::System.Text.Json.JsonValueKind.Array)
            return element.EnumerateArray().Any(item => ContainsJsonValue(item, targetObjectId));
        if (element.ValueKind == global::System.Text.Json.JsonValueKind.Object)
            return element.EnumerateObject().Any(item => ContainsJsonValue(item.Value, targetObjectId));
        return false;
    }

    private static string ResolveReferenceKind(string sourceModule, string targetModule)
    {
        if (sourceModule == ApplicationDataCenterModuleKey.Microflow)
        {
            return "microflow-reference";
        }

        if (sourceModule == ApplicationDataCenterModuleKey.QueryDataset)
        {
            return "dataset-source";
        }

        if (sourceModule == ApplicationDataCenterModuleKey.IntegrationTask)
        {
            return "sync-source-or-target";
        }

        return sourceModule == targetModule ? "module-link" : "config-reference";
    }

    private static async Task<ApplicationDataObjectIdentity?> GetObjectIdentityAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string moduleKey,
        string objectId,
        CancellationToken cancellationToken)
    {
        return moduleKey switch
        {
            ApplicationDataCenterModuleKey.DataSource => await GetIdentityAsync<ApplicationDataSourceEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.ConnectionTest => await GetIdentityAsync<ApplicationConnectionCheckTaskEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.Microflow => await GetIdentityAsync<ApplicationMicroflowEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.DataModel => await GetIdentityAsync<ApplicationDataModelDesignEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.ApiService => await GetIdentityAsync<ApplicationApiServiceEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.EntityField => await GetIdentityAsync<ApplicationDataEntityDefinitionEntity>(db, workspace, moduleKey, objectId, cancellationToken)
                ?? await GetIdentityAsync<ApplicationDataFieldDefinitionEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.DictionaryCode => await GetIdentityAsync<ApplicationDataCenterDictionaryEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.MappingCache => await GetMappingCacheIdentityAsync(db, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.QueryDataset => await GetIdentityAsync<ApplicationQueryDatasetEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            ApplicationDataCenterModuleKey.IntegrationTask => await GetIdentityAsync<ApplicationIntegrationTaskEntity>(db, workspace, moduleKey, objectId, cancellationToken),
            _ => null
        };
    }

    private static async Task<ApplicationDataObjectIdentity?> GetIdentityAsync<TEntity>(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string moduleKey,
        string objectId,
        CancellationToken cancellationToken)
        where TEntity : ApplicationDataCenterObjectEntity, new()
    {
        var entity = (await db.Queryable<TEntity>()
            .Where(item =>
                item.Id == objectId &&
                !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity is null
            ? null
            : new ApplicationDataObjectIdentity(moduleKey, entity.Id, entity.ObjectCode, entity.ObjectName, entity.OwnerUserId);
    }

    private static Task UpdateReferenceCountAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string moduleKey,
        string objectId,
        int referenceCount,
        CancellationToken cancellationToken)
    {
        return moduleKey switch
        {
            ApplicationDataCenterModuleKey.DataSource => UpdateReferenceCountAsync<ApplicationDataSourceEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            ApplicationDataCenterModuleKey.ConnectionTest => UpdateReferenceCountAsync<ApplicationConnectionCheckTaskEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            ApplicationDataCenterModuleKey.Microflow => UpdateReferenceCountAsync<ApplicationMicroflowEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            ApplicationDataCenterModuleKey.DataModel => UpdateReferenceCountAsync<ApplicationDataModelDesignEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            ApplicationDataCenterModuleKey.ApiService => UpdateReferenceCountAsync<ApplicationApiServiceEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            ApplicationDataCenterModuleKey.EntityField => UpdateReferenceCountAsync<ApplicationDataEntityDefinitionEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            ApplicationDataCenterModuleKey.DictionaryCode => UpdateReferenceCountAsync<ApplicationDataCenterDictionaryEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            ApplicationDataCenterModuleKey.QueryDataset => UpdateReferenceCountAsync<ApplicationQueryDatasetEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            ApplicationDataCenterModuleKey.IntegrationTask => UpdateReferenceCountAsync<ApplicationIntegrationTaskEntity>(db, workspace, objectId, referenceCount, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    private static async Task ScanMappingCachesAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDataObjectIdentity target,
        List<ApplicationDataObjectReferenceEntity> references,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("app_mapping_caches", false))
        {
            return;
        }

        var caches = await db.Queryable<ApplicationMappingCacheEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var cacheIds = caches.Select(item => item.Id).ToArray();
        var columns = cacheIds.Length == 0 || !db.DbMaintenance.IsAnyTable("app_mapping_cache_columns", false)
            ? []
            : await db.Queryable<ApplicationMappingCacheColumnEntity>().Where(item => cacheIds.Contains(item.CacheId) && !item.IsDeleted).ToListAsync(cancellationToken);
        var parameters = cacheIds.Length == 0 || !db.DbMaintenance.IsAnyTable("app_mapping_cache_parameters", false)
            ? []
            : await db.Queryable<ApplicationMappingCacheParameterEntity>().Where(item => cacheIds.Contains(item.CacheId) && !item.IsDeleted).ToListAsync(cancellationToken);
        foreach (var cache in caches)
        {
            if (cache.Id == target.ObjectId || cache.SourceResourceId == target.ObjectId || cache.DataSourceId == target.ObjectId)
            {
                references.Add(CreateReference(workspace, "mapping-cache", cache.Id, cache.CacheKey, cache.CacheName, target, "mapping-cache-source", cache.Status, cache.CreatedBy));
            }

            foreach (var column in columns.Where(item => item.CacheId == cache.Id && item.SourceResourceId == target.ObjectId))
            {
                references.Add(CreateReference(workspace, "mapping-cache", column.SourceResourceId, column.TargetName, column.TargetName, target, "mapping-cache-field", cache.Status, null));
            }

            foreach (var parameter in parameters.Where(item => item.CacheId == cache.Id && (item.ResourceId == target.ObjectId || item.ColumnResourceId == target.ObjectId)))
            {
                references.Add(CreateReference(workspace, "mapping-cache", parameter.ResourceId, parameter.Name, parameter.Name, target, "mapping-cache-parameter", cache.Status, null));
            }
        }
    }

    private static async Task ScanLatestPageBindingsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDataObjectIdentity target,
        List<ApplicationDataObjectReferenceEntity> references,
        CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("app_designer_documents", false))
        {
            return;
        }

        var documents = await db.Queryable<ApplicationDesignerDocumentEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        foreach (var document in documents.GroupBy(item => item.PageId, StringComparer.OrdinalIgnoreCase).Select(item => item.First()))
        {
            if (!DesignerDocumentReferences.References(document.DocumentJson, target.ObjectId))
            {
                continue;
            }

            references.Add(CreateReference(
                workspace,
                "page",
                document.PageId,
                document.PageId,
                document.PageId,
                target,
                "page-binding",
                document.Status,
                document.CreatedBy));
        }
    }

    private static ApplicationDataObjectReferenceEntity CreateReference(
        ApplicationDataCenterWorkspace workspace,
        string sourceModule,
        string sourceObjectId,
        string sourceObjectCode,
        string sourceObjectName,
        ApplicationDataObjectIdentity target,
        string referenceKind,
        string status,
        string? ownerUserId) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            SourceModule = sourceModule,
            SourceObjectId = sourceObjectId,
            SourceObjectCode = sourceObjectCode,
            SourceObjectName = sourceObjectName,
            TargetModule = target.ModuleKey,
            TargetObjectId = target.ObjectId,
            ReferenceKind = referenceKind,
            Status = status,
            OwnerUserId = ownerUserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };

    private static async Task<ApplicationDataObjectIdentity?> GetMappingCacheIdentityAsync(
        ISqlSugarClient db,
        string moduleKey,
        string objectId,
        CancellationToken cancellationToken)
    {
        var entity = await db.Queryable<ApplicationMappingCacheEntity>()
            .Where(item => item.Id == objectId && !item.IsDeleted)
            .FirstAsync(cancellationToken);
        return entity is null ? null : new(moduleKey, entity.Id, entity.CacheKey, entity.CacheName, entity.CreatedBy);
    }

    private static Task UpdateReferenceCountAsync<TEntity>(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string objectId,
        int referenceCount,
        CancellationToken cancellationToken)
        where TEntity : ApplicationDataCenterObjectEntity, new()
    {
        return db.Updateable<TEntity>()
            .SetColumns(item => item.ReferenceCount == referenceCount)
            .SetColumns(item => item.UpdatedTime == DateTime.UtcNow)
            .Where(item =>
                item.Id == objectId &&
                !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);
    }
}
