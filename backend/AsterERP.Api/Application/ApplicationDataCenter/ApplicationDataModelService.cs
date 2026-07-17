using System.Diagnostics;
using System.Text.Json;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataModelService(
    IRepository<ApplicationDataModelDesignEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService,
    ApplicationDataPreviewReader previewReader,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ILogger<ApplicationDataModelService> logger)
    : ApplicationDataCenterObjectService<ApplicationDataModelDesignEntity>(
        repository,
        databaseAccessor,
        workspaceResolver,
        secretProtector,
        riskGuard,
        referenceService,
        templateCatalog,
        snapshotService)
{
    protected override string ModuleKey => ApplicationDataCenterModuleKey.DataModel;

    protected override void ApplySpecificFields(
        ApplicationDataModelDesignEntity entity,
        ApplicationDataCenterObjectUpsertRequest request,
        bool isCreate)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        entity.BuildMode = entity.ObjectType;
        entity.SourceDataSourceId = ReadString(config, "sourceDataSourceId") ?? ReadString(config, "dataSourceId");
    }

    public override async Task<ApplicationDataCenterOperationResponse> PublishAsync(
        string id,
        ApplicationDataCenterPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        logger.LogDebug("Application data model publish requested. DesignId={DesignId}", id);
        try
        {
        var workspace = WorkspaceResolver.Resolve();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var fields = await ResolveRuntimeFieldsAsync(db, workspace, entity, config, cancellationToken);
        var keyField = ResolveRuntimeKeyField(config, fields);
        var providerKey = ResolveProviderKey(entity, config);
        var operations = TryReadOperations(entity.ConfigJson);
        ApplicationDataModelOperationPolicy.ValidateForPublish(operations, entity.ObjectCode);
        var runtimeSchema = ApplicationDataCenterJson.Serialize(new
        {
            idGeneration = ReadString(config, "idGeneration") ?? RuntimeModelIdGeneration.Guid,
            source = new
            {
                dataCenterModelId = entity.Id,
                dataSourceId = ReadString(config, "sourceDataSourceId") ?? ReadString(config, "dataSourceId"),
                tableName = ReadString(config, "sourceTable") ?? ReadString(config, "tableName"),
                sql = ReadString(config, "sql"),
                filePath = ReadString(config, "filePath")
            },
            fields,
            operations
        });

        var runtime = (await db.Queryable<SystemDataModelEntity>()
            .Where(item =>
                item.ModelCode == entity.ObjectCode &&
                item.Status == "Published" &&
                !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (runtime is null)
        {
            runtime = new SystemDataModelEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                ModelCode = entity.ObjectCode,
                ModelName = entity.ObjectName,
                ProviderKey = providerKey,
                KeyField = keyField,
                PermissionCode = ReadString(config, "permissionCode"),
                VersionNo = entity.VersionNo + 1,
                Status = "Published",
                SchemaJson = runtimeSchema,
                CreatedBy = workspace.UserId,
                CreatedTime = now,
                IsDeleted = false
            };
            await db.Insertable(runtime).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            runtime.ModelName = entity.ObjectName;
            runtime.ProviderKey = providerKey;
            runtime.KeyField = keyField;
            runtime.PermissionCode = ReadString(config, "permissionCode");
            runtime.VersionNo += 1;
            runtime.SchemaJson = runtimeSchema;
            runtime.UpdatedBy = workspace.UserId;
            runtime.UpdatedTime = now;
            await db.Updateable(runtime).ExecuteCommandAsync(cancellationToken);
        }

        entity.RuntimeModelId = runtime.Id;
        entity.RuntimeModelCode = runtime.ModelCode;
        entity.Status = ApplicationDataCenterObjectStatus.Published;
        entity.VersionNo += 1;
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = now;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await UpsertEntityAndFieldsAsync(db, workspace, entity, config, fields, keyField, cancellationToken);
        var summary = await ReferenceService.RecalculateReferencesAsync(ModuleKey, entity.Id, cancellationToken);
        var detail = await MapDetailAsync(await EnsureEntityAsync(entity.Id, cancellationToken), cancellationToken);
        logger.LogInformation(
            "Application data model published. DesignId={DesignId} ModelCode={ModelCode} ProviderKey={ProviderKey} FieldCount={FieldCount} OperationCount={OperationCount} ElapsedMs={ElapsedMs}",
            entity.Id,
            entity.ObjectCode,
            providerKey,
            fields.Count,
            operations.Count,
            elapsed.ElapsedMilliseconds);
        return new ApplicationDataCenterOperationResponse(detail, summary, detail.NextActions);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Application data model publish rejected. DesignId={DesignId} ElapsedMs={ElapsedMs}",
                id,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Application data model publish failed. DesignId={DesignId} ElapsedMs={ElapsedMs}",
                id,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public override async Task<ApplicationDataCenterActionResultResponse> TestAsync(
        string id,
        ApplicationDataCenterActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = WorkspaceResolver.Resolve();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var fields = await ResolveRuntimeFieldsAsync(db, workspace, entity, ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson), cancellationToken);
        var detailJson = ApplicationDataCenterJson.Serialize(new { fieldCount = fields.Count, fields });
        await MarkValidationAsync(id, ApplicationDataCenterObjectStatus.Normal, "模型校验通过", detailJson, cancellationToken);
        return new ApplicationDataCenterActionResultResponse(true, ApplicationDataCenterObjectStatus.Normal, "模型校验通过", 0, detailJson, TemplateCatalog.BuildNextActions(ModuleKey, id, entity.Status));
    }

    public override async Task<ApplicationDataCenterPreviewResponse> PreviewAsync(
        string id,
        ApplicationDataCenterPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = WorkspaceResolver.Resolve();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var fields = await ResolveRuntimeFieldsAsync(db, workspace, entity, config, cancellationToken);
        ApplicationDataCenterPreviewResponse? sourcePreview = null;
        var sourceDataSourceId = ReadString(config, "sourceDataSourceId") ?? ReadString(config, "dataSourceId");
        if (!string.IsNullOrWhiteSpace(sourceDataSourceId))
        {
            var source = (await db.Queryable<ApplicationDataSourceEntity>()
                .Where(item => item.Id == sourceDataSourceId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault()
                ?? throw new NotFoundException("模型来源数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
            sourcePreview = await PreviewDataSourceAsync(source, config, cancellationToken);
        }

        var previewFields = fields
            .Select(field => new ApplicationDataCenterPreviewFieldResponse(
                field.FieldCode,
                field.FieldName,
                field.DataType,
                true,
                field.FieldCode.Equals(ResolveRuntimeKeyField(config, fields), StringComparison.OrdinalIgnoreCase),
                field.Order))
            .ToArray();
        return new ApplicationDataCenterPreviewResponse(
            sourcePreview?.Rows ?? [],
            previewFields,
            sourcePreview is null ? "模型字段预览" : "模型来源数据预览");
    }

    private async Task<IReadOnlyList<RuntimeDataFieldDefinition>> ResolveRuntimeFieldsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDataModelDesignEntity entity,
        IReadOnlyDictionary<string, object?> config,
        CancellationToken cancellationToken)
    {
        var fieldsFromConfig = TryReadFields(entity.ConfigJson);
        if (fieldsFromConfig.Count > 0)
        {
            return fieldsFromConfig;
        }

        var dataSourceId = ReadString(config, "sourceDataSourceId") ?? ReadString(config, "dataSourceId");
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            throw new ValidationException("模型缺少来源数据源或字段定义", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var dataSource = (await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.Id == dataSourceId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("来源数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);

        var preview = await PreviewDataSourceAsync(dataSource, config, cancellationToken);
        if (preview.Fields.Count == 0)
        {
            throw new ValidationException("无法从来源识别字段", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return preview.Fields.Select(item => new RuntimeDataFieldDefinition
        {
            FieldCode = item.FieldCode,
            FieldName = item.FieldName,
            DataType = NormalizeRuntimeType(item.DataType),
            Binding = item.FieldCode,
            Visible = true,
            Queryable = true,
            Sortable = true,
            Exportable = true,
            Writable = !item.PrimaryKey,
            Order = item.Order
        }).ToArray();
    }

    private async Task<ApplicationDataCenterPreviewResponse> PreviewDataSourceAsync(
        ApplicationDataSourceEntity dataSource,
        IReadOnlyDictionary<string, object?> config,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ApplicationDataSourceConnectionFactory.IsDatabaseType(dataSource.ObjectType))
        {
            using var sourceDb = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
            return await previewReader.PreviewDatabaseAsync(
                sourceDb,
                ReadString(config, "sql"),
                ReadString(config, "sourceTable") ?? ReadString(config, "tableName"),
                5,
                cancellationToken);
        }

        var dataSourceConfig = ApplicationDataCenterJson.DeserializeDictionary(dataSource.ConfigJson);
        if (dataSource.ObjectType == ApplicationDataSourceType.Excel)
        {
            var options = connectionFactory.Resolve(dataSource);
            return previewReader.PreviewExcel(
                options.FilePath ?? throw new ValidationException("Excel 文件路径不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig),
                ReadString(dataSourceConfig, "sheetName"),
                ReadInt(dataSourceConfig, "headerRow") ?? 1,
                ReadInt(dataSourceConfig, "dataStartRow") ?? 2,
                5);
        }

        if (dataSource.ObjectType == ApplicationDataSourceType.Csv)
        {
            var options = connectionFactory.Resolve(dataSource);
            return previewReader.PreviewCsv(
                options.FilePath ?? throw new ValidationException("CSV 文件路径不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig),
                ReadString(dataSourceConfig, "delimiter") ?? ",",
                true,
                1,
                global::System.Text.Encoding.UTF8,
                5);
        }

        throw new ValidationException("当前数据源暂不能自动识别模型字段", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private async Task UpsertEntityAndFieldsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDataModelDesignEntity model,
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyList<RuntimeDataFieldDefinition> fields,
        string keyField,
        CancellationToken cancellationToken)
    {
        var entityCode = $"{model.ObjectCode}.entity";
        var entity = (await db.Queryable<ApplicationDataEntityDefinitionEntity>()
            .Where(item => item.ModelId == model.Id && item.ObjectCode == entityCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new ApplicationDataEntityDefinitionEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                ModuleKey = ApplicationDataCenterModuleKey.EntityField,
                ObjectCode = entityCode,
                ObjectName = $"{model.ObjectName}实体",
                ObjectType = "Entity",
                Status = ApplicationDataCenterObjectStatus.Published,
                ModelId = model.Id,
                SourceTable = ReadString(config, "sourceTable") ?? ReadString(config, "tableName"),
                KeyField = keyField,
                ConfigJson = ApplicationDataCenterJson.Serialize(new { modelId = model.Id, modelCode = model.ObjectCode }),
                CreatedBy = workspace.UserId,
                CreatedTime = now
            };
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity.ObjectName = $"{model.ObjectName}实体";
            entity.Status = ApplicationDataCenterObjectStatus.Published;
            entity.SourceTable = ReadString(config, "sourceTable") ?? ReadString(config, "tableName");
            entity.KeyField = keyField;
            entity.UpdatedBy = workspace.UserId;
            entity.UpdatedTime = now;
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        var oldFields = await db.Queryable<ApplicationDataFieldDefinitionEntity>()
            .Where(item => item.ModelId == model.Id && item.EntityId == entity.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var oldField in oldFields)
        {
            oldField.IsDeleted = true;
            oldField.DeletedBy = workspace.UserId;
            oldField.DeletedTime = now;
        }

        if (oldFields.Count > 0)
        {
            await db.Updateable(oldFields).ExecuteCommandAsync(cancellationToken);
        }

        var inserts = fields.Select(field => new ApplicationDataFieldDefinitionEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ModuleKey = ApplicationDataCenterModuleKey.EntityField,
            ObjectCode = $"{model.ObjectCode}.{field.FieldCode}",
            ObjectName = field.FieldName,
            ObjectType = field.DataType,
            Status = ApplicationDataCenterObjectStatus.Published,
            ModelId = model.Id,
            EntityId = entity.Id,
            FieldCode = field.FieldCode,
            FieldName = field.FieldName,
            DataType = field.DataType,
            Binding = field.Binding,
            IsPrimaryKey = field.FieldCode.Equals(keyField, StringComparison.OrdinalIgnoreCase) || field.Binding.Equals(keyField, StringComparison.OrdinalIgnoreCase),
            IsNullable = true,
            IsQueryable = field.Queryable,
            IsSortable = field.Sortable,
            IsWritable = field.Writable,
            SortOrder = field.Order,
            ConfigJson = ApplicationDataCenterJson.Serialize(field),
            CreatedBy = workspace.UserId,
            CreatedTime = now
        }).ToArray();
        await db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
    }

    private static IReadOnlyList<RuntimeDataFieldDefinition> TryReadFields(string configJson)
    {
        using var document = JsonDocument.Parse(configJson);
        if (!document.RootElement.TryGetProperty("fields", out var fieldsElement) ||
            fieldsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<RuntimeDataFieldDefinition>();
        var order = 1;
        foreach (var fieldElement in fieldsElement.EnumerateArray())
        {
            var fieldCode = ReadString(fieldElement, "fieldCode") ?? ReadString(fieldElement, "code") ?? ReadString(fieldElement, "name");
            if (string.IsNullOrWhiteSpace(fieldCode))
            {
                continue;
            }

            var fieldName = ReadString(fieldElement, "fieldName") ?? ReadString(fieldElement, "label") ?? fieldCode;
            result.Add(new RuntimeDataFieldDefinition
            {
                FieldCode = ApplicationDataCenterCodePolicy.NormalizeCode(fieldCode, "字段编码"),
                FieldName = fieldName,
                DataType = NormalizeRuntimeType(ReadString(fieldElement, "dataType") ?? "Text"),
                Binding = ReadString(fieldElement, "binding") ?? fieldCode,
                Visible = ReadBool(fieldElement, "visible", true),
                Queryable = ReadBool(fieldElement, "queryable", true),
                Sortable = ReadBool(fieldElement, "sortable", true),
                Exportable = ReadBool(fieldElement, "exportable", true),
                Writable = ReadBool(fieldElement, "writable", !ReadBool(fieldElement, "primaryKey", false)),
                Renderer = ReadString(fieldElement, "renderer"),
                DictType = ReadString(fieldElement, "dictType"),
                Required = ReadBool(fieldElement, "required", false),
                Width = ReadString(fieldElement, "width"),
                Fixed = ReadString(fieldElement, "fixed") ?? ReadString(fieldElement, "fixedValue"),
                DisplayHelpers = ReadHelpers(fieldElement, "displayHelpers"),
                WriteHelpers = ReadHelpers(fieldElement, "writeHelpers"),
                QueryHelpers = ReadHelpers(fieldElement, "queryHelpers"),
                Order = ReadInt(fieldElement, "order") ?? order
            });
            order += 1;
        }

        return result;
    }

    private static IReadOnlyList<RuntimeModelOperationDefinitionDto> TryReadOperations(string configJson)
    {
        using var document = JsonDocument.Parse(configJson);
        if (!document.RootElement.TryGetProperty("operations", out var operationsElement) ||
            operationsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<RuntimeModelOperationDefinitionDto>>(
            operationsElement.GetRawText(),
            ApplicationDataCenterJson.Options) ?? [];
    }

    private static List<RuntimeExpressionHelperDto> ReadHelpers(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var helpersElement) ||
            helpersElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<RuntimeExpressionHelperDto>>(
            helpersElement.GetRawText(),
            ApplicationDataCenterJson.Options) ?? [];
    }

    private static string ResolveRuntimeKeyField(
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyList<RuntimeDataFieldDefinition> fields)
    {
        var configuredKeyField = ReadString(config, "keyField");
        if (!string.IsNullOrWhiteSpace(configuredKeyField))
        {
            var canonicalField = fields.FirstOrDefault(item =>
                item.FieldCode.Equals(configuredKeyField, StringComparison.OrdinalIgnoreCase) ||
                item.Binding.Equals(configuredKeyField, StringComparison.OrdinalIgnoreCase));
            if (canonicalField is not null)
            {
                return canonicalField.FieldCode;
            }

            throw new ValidationException($"模型主键字段未找到: {configuredKeyField}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var fallbackIdField = fields.FirstOrDefault(item => item.FieldCode.Equals("id", StringComparison.OrdinalIgnoreCase));
        if (fallbackIdField is not null)
        {
            return fallbackIdField.FieldCode;
        }

        throw new ValidationException("模型必须显式配置主键字段", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static string ResolveProviderKey(ApplicationDataModelDesignEntity entity, IReadOnlyDictionary<string, object?> config) =>
        string.Equals(entity.ObjectType, ApplicationDataModelBuildMode.FromExcel, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entity.ObjectType, ApplicationDataModelBuildMode.FromApiResponse, StringComparison.OrdinalIgnoreCase)
            ? "application-data-center.file"
            : "application-data-center.sql-table";

    private static string NormalizeRuntimeType(string dataType)
    {
        var normalized = dataType.Trim().ToLowerInvariant();
        if (normalized.Contains("int") || normalized.Contains("decimal") || normalized.Contains("number") || normalized.Contains("double"))
        {
            return "number";
        }

        if (normalized.Contains("date") || normalized.Contains("time"))
        {
            return "date";
        }

        if (normalized.Contains("bool"))
        {
            return "boolean";
        }

        return "text";
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static int? ReadInt(IReadOnlyDictionary<string, object?> config, string key) =>
        int.TryParse(ReadString(config, key), out var parsed) ? parsed : null;

    private static string? ReadString(JsonElement element, string key) =>
        element.TryGetProperty(key, out var property) && property.ValueKind != JsonValueKind.Null ? property.ToString() : null;

    private static bool ReadBool(JsonElement element, string key, bool defaultValue) =>
        element.TryGetProperty(key, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : defaultValue;

    private static int? ReadInt(JsonElement element, string key) =>
        element.TryGetProperty(key, out var property) && property.TryGetInt32(out var parsed) ? parsed : null;
}
