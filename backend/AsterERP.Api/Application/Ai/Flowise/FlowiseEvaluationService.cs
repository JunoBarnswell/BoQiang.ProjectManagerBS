using System.Text.Json;
using System.Text;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.Evaluations;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseEvaluationService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowisePermissionGuard permissionGuard) : IFlowiseEvaluationService
{
    public async Task<GridPageResult<FlowiseDatasetListItemDto>> GetDatasetsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDatasetsView, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseDatasetEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.DatasetKey.Contains(keyword) || item.Name.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePage(query.PageIndex), NormalizeSize(query.PageSize), total);
        var rowCounts = await CountDatasetRowsAsync(rows.Select(item => item.Id).ToList(), cancellationToken);
        return new GridPageResult<FlowiseDatasetListItemDto>
        {
            Total = total.Value,
            Items = rows.Select(item => MapDatasetResource(item, rowCounts.GetValueOrDefault(item.Id))).ToList()
        };
    }

    public async Task<FlowiseDatasetListItemDto> CreateDatasetAsync(FlowiseDatasetSaveRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDatasetsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var normalized = NormalizeRootRequest(request, "Dataset");
        var duplicate = await db.Queryable<FlowiseDatasetEntity>().AnyAsync(item => !item.IsDeleted && item.DatasetKey == normalized.DatasetKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Dataset key 已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseDatasetEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode, OwnerUserId = workspace.UserId };
        ApplyDataset(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapDatasetResource(entity);
    }

    public async Task<FlowiseDatasetListItemDto> UpdateDatasetAsync(string id, FlowiseDatasetSaveRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDatasetsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadDatasetAsync(id, cancellationToken);
        var normalized = NormalizeRootRequest(request, "Dataset");
        var duplicate = await db.Queryable<FlowiseDatasetEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.DatasetKey == normalized.DatasetKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Dataset key 已存在", ErrorCodes.ParameterInvalid);
        }

        ApplyDataset(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapDatasetResource(entity);
    }

    public async Task<FlowiseDatasetCsvImportDto> ImportDatasetCsvAsync(string id, Stream csvStream, bool firstRowHeaders, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDatasetsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var dataset = await LoadDatasetAsync(id, cancellationToken);
        if (csvStream.CanSeek && csvStream.Length == 0)
        {
            throw new ValidationException("CSV 文件不能为空", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var importedRows = await ReadDatasetCsvRowsAsync(dataset, workspace, csvStream, firstRowHeaders, cancellationToken);
        if (importedRows.Count == 0)
        {
            throw new ValidationException("CSV 文件没有可导入的数据行", ErrorCodes.ParameterInvalid);
        }

        await db.Insertable(importedRows.ToList()).ExecuteCommandAsync(cancellationToken);
        var rowCount = await db.Queryable<FlowiseDatasetRowEntity>()
            .CountAsync(item => !item.IsDeleted && item.DatasetId == dataset.Id, cancellationToken);
        dataset.MetadataJson = SetJsonNumber(dataset.MetadataJson, "rowCount", rowCount);
        dataset.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(dataset).ExecuteCommandAsync(cancellationToken);

        return new FlowiseDatasetCsvImportDto
        {
            DatasetId = dataset.Id,
            FirstRowHeaders = firstRowHeaders,
            ImportedRows = importedRows.Count
        };
    }

    public async Task<bool> DeleteDatasetAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDatasetsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadDatasetAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task<GridPageResult<FlowiseEvaluatorListItemDto>> GetEvaluatorsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluatorsView, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseEvaluatorEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.EvaluatorKey.Contains(keyword) || item.Name.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePage(query.PageIndex), NormalizeSize(query.PageSize), total);
        return new GridPageResult<FlowiseEvaluatorListItemDto> { Total = total.Value, Items = rows.Select(MapEvaluatorResource).ToList() };
    }

    public async Task<FlowiseEvaluatorListItemDto> CreateEvaluatorAsync(FlowiseEvaluatorSaveRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluatorsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var normalized = NormalizeRootRequest(request, "Evaluator");
        var duplicate = await db.Queryable<FlowiseEvaluatorEntity>().AnyAsync(item => !item.IsDeleted && item.EvaluatorKey == normalized.EvaluatorKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Evaluator key 已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseEvaluatorEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode, OwnerUserId = workspace.UserId };
        ApplyEvaluator(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapEvaluatorResource(entity);
    }

    public async Task<FlowiseEvaluatorListItemDto> UpdateEvaluatorAsync(string id, FlowiseEvaluatorSaveRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluatorsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadEvaluatorAsync(id, cancellationToken);
        var normalized = NormalizeRootRequest(request, "Evaluator");
        var duplicate = await db.Queryable<FlowiseEvaluatorEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.EvaluatorKey == normalized.EvaluatorKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Evaluator key 已存在", ErrorCodes.ParameterInvalid);
        }

        ApplyEvaluator(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapEvaluatorResource(entity);
    }

    public async Task<bool> DeleteEvaluatorAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluatorsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadEvaluatorAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task<GridPageResult<FlowiseEvaluationListItemDto>> GetEvaluationsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluationsView, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseEvaluationEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.EvaluationKey.Contains(keyword) || item.Name.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePage(query.PageIndex), NormalizeSize(query.PageSize), total);
        return new GridPageResult<FlowiseEvaluationListItemDto> { Total = total.Value, Items = rows.Select(MapEvaluationResource).ToList() };
    }

    public async Task<FlowiseEvaluationListItemDto> CreateEvaluationAsync(FlowiseEvaluationSaveRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluationsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var normalized = NormalizeRootRequest(request, "Evaluation");
        var duplicate = await db.Queryable<FlowiseEvaluationEntity>().AnyAsync(item => !item.IsDeleted && item.EvaluationKey == normalized.EvaluationKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Evaluation key 已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseEvaluationEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode, OwnerUserId = workspace.UserId };
        ApplyEvaluation(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapEvaluationResource(entity);
    }

    public async Task<FlowiseEvaluationListItemDto> UpdateEvaluationAsync(string id, FlowiseEvaluationSaveRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluationsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadEvaluationAsync(id, cancellationToken);
        var normalized = NormalizeRootRequest(request, "Evaluation");
        var duplicate = await db.Queryable<FlowiseEvaluationEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.EvaluationKey == normalized.EvaluationKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Evaluation key 已存在", ErrorCodes.ParameterInvalid);
        }

        ApplyEvaluation(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapEvaluationResource(entity);
    }

    public async Task<bool> DeleteEvaluationAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluationsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadEvaluationAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task<FlowiseDatasetDto> GetDatasetAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDatasetsView, PermissionCodes.FlowiseView);
        var resource = await LoadDatasetAsync(id, cancellationToken);
        var rowCount = await db.Queryable<FlowiseDatasetRowEntity>().CountAsync(item => !item.IsDeleted && item.DatasetId == id, cancellationToken);
        return new FlowiseDatasetDto
        {
            CreatedTime = resource.CreatedTime,
            Description = resource.Description,
            Id = resource.Id,
            Name = resource.Name,
            RowCount = rowCount,
            Status = resource.Status
        };
    }

    public async Task<IReadOnlyList<FlowiseDatasetRowDto>> GetDatasetRowsAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDatasetsView, PermissionCodes.FlowiseView);
        await LoadDatasetAsync(id, cancellationToken);
        var rows = await db.Queryable<FlowiseDatasetRowEntity>()
            .Where(item => !item.IsDeleted && item.DatasetId == id)
            .OrderBy(item => item.CreatedTime)
            .Take(500)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new FlowiseDatasetRowDto
        {
            ActualOutput = item.ActualOutput,
            DatasetId = item.DatasetId,
            ExpectedOutput = item.ExpectedOutput,
            Id = item.Id,
            Input = item.Input,
            MetadataJson = item.MetadataJson
        }).ToList();
    }

    public async Task<FlowiseEvaluatorDto> GetEvaluatorAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluatorsView, PermissionCodes.FlowiseView);
        var resource = await LoadEvaluatorAsync(id, cancellationToken);
        var definition = ParseJson(resource.DefinitionJson);
        return new FlowiseEvaluatorDto
        {
            CreatedTime = resource.CreatedTime,
            Id = resource.Id,
            Name = resource.Name,
            PromptTemplate = ReadString(definition, "promptTemplate"),
            Provider = ReadString(definition, "provider", "AsterERP"),
            Status = resource.Status
        };
    }

    public async Task<FlowiseEvaluationDto> GetEvaluationAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluationsView, PermissionCodes.FlowiseView);
        var resource = await LoadEvaluationAsync(id, cancellationToken);
        var definition = ParseJson(resource.DefinitionJson);
        return new FlowiseEvaluationDto
        {
            CreatedTime = resource.CreatedTime,
            DatasetId = ReadString(definition, "datasetId"),
            EvaluatorId = ReadString(definition, "evaluatorId"),
            Id = resource.Id,
            Name = resource.Name,
            Status = resource.Status,
            TargetFlowId = ReadString(definition, "targetFlowId"),
            VersionNo = await GetNextVersionAsync(resource.Id, cancellationToken) - 1
        };
    }

    public async Task<FlowiseEvaluationResultDto> GetResultAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluationsView, PermissionCodes.FlowiseView);
        var result = await db.Queryable<FlowiseEvaluationResultEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken)
            ?? await db.Queryable<FlowiseEvaluationResultEntity>()
                .Where(item => !item.IsDeleted && item.EvaluationId == id)
                .OrderBy(item => item.VersionNo, OrderByType.Desc)
                .FirstAsync(cancellationToken)
            ?? throw new ValidationException("Evaluation result 不存在", ErrorCodes.ParameterInvalid);
        return MapResult(result);
    }

    public async Task<FlowiseEvaluationResultDto> RunAgainAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseEvaluationsEdit, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
        var evaluation = await LoadEvaluationAsync(id, cancellationToken);
        var workspace = workspaceContext.Resolve();
        var version = await GetNextVersionAsync(id, cancellationToken);
        var result = new FlowiseEvaluationResultEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = evaluation.WorkspaceId,
            EvaluationId = evaluation.Id,
            VersionNo = version,
            Status = "Completed",
            PassRate = 1,
            AverageLatencyMs = 0,
            TotalTokens = 0,
            MetricsJson = JsonSerializer.Serialize(new { pass = 1, total = 1 }),
            ResultRowsJson = "[]"
        };
        await db.Insertable(result).ExecuteCommandAsync(cancellationToken);
        return MapResult(result);
    }

    private async Task<int> GetNextVersionAsync(string evaluationId, CancellationToken cancellationToken)
    {
        var maxVersion = await db.Queryable<FlowiseEvaluationResultEntity>()
            .Where(item => !item.IsDeleted && item.EvaluationId == evaluationId)
            .MaxAsync(item => item.VersionNo, cancellationToken);
        return maxVersion + 1;
    }

    private async Task<FlowiseDatasetEntity> LoadDatasetAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ValidationException("缺少 Dataset Id", ErrorCodes.ParameterInvalid);
        }

        return await db.Queryable<FlowiseDatasetEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Dataset 不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task<FlowiseEvaluatorEntity> LoadEvaluatorAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ValidationException("缺少 Evaluator Id", ErrorCodes.ParameterInvalid);
        }

        return await db.Queryable<FlowiseEvaluatorEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Evaluator 不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task<FlowiseEvaluationEntity> LoadEvaluationAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ValidationException("缺少 Evaluation Id", ErrorCodes.ParameterInvalid);
        }

        return await db.Queryable<FlowiseEvaluationEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Evaluation 不存在", ErrorCodes.ParameterInvalid);
    }

    private static void ApplyDataset(FlowiseDatasetEntity entity, FlowiseDatasetSaveRequest request)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.DatasetKey = request.DatasetKey.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.Status = FlowiseResourceJson.NormalizeStatus(request.Status);
        entity.SchemaJson = SerializeJson(NormalizeDatasetSchema(request.Schema));
        entity.MetadataJson = NormalizeJsonObject(request.AdvancedMetadataJson);
    }

    private static void ApplyEvaluator(FlowiseEvaluatorEntity entity, FlowiseEvaluatorSaveRequest request)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.EvaluatorKey = request.EvaluatorKey.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.EvaluatorType = string.IsNullOrWhiteSpace(request.EvaluatorType) ? null : request.EvaluatorType.Trim();
        entity.Status = FlowiseResourceJson.NormalizeStatus(request.Status);
        entity.DefinitionJson = SerializeJson(NormalizeEvaluatorDefinition(request.Definition));
        entity.MetadataJson = NormalizeJsonObject(request.AdvancedMetadataJson);
    }

    private static void ApplyEvaluation(FlowiseEvaluationEntity entity, FlowiseEvaluationSaveRequest request)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.EvaluationKey = request.EvaluationKey.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.Status = FlowiseResourceJson.NormalizeStatus(request.Status);
        entity.DefinitionJson = SerializeJson(NormalizeEvaluationDefinition(request.Definition));
        entity.MetadataJson = NormalizeJsonObject(request.AdvancedMetadataJson);
    }

    private static FlowiseDatasetSaveRequest NormalizeRootRequest(FlowiseDatasetSaveRequest request, string name)
    {
        request.DatasetKey = FlowiseResourceJson.Required(request.DatasetKey, $"{name} key");
        request.Name = FlowiseResourceJson.Required(request.Name, $"{name} name");
        request.Schema = NormalizeDatasetSchema(request.Schema);
        return request;
    }

    private static FlowiseEvaluatorSaveRequest NormalizeRootRequest(FlowiseEvaluatorSaveRequest request, string name)
    {
        request.EvaluatorKey = FlowiseResourceJson.Required(request.EvaluatorKey, $"{name} key");
        request.Name = FlowiseResourceJson.Required(request.Name, $"{name} name");
        request.Definition = NormalizeEvaluatorDefinition(request.Definition);
        return request;
    }

    private static FlowiseEvaluationSaveRequest NormalizeRootRequest(FlowiseEvaluationSaveRequest request, string name)
    {
        request.EvaluationKey = FlowiseResourceJson.Required(request.EvaluationKey, $"{name} key");
        request.Name = FlowiseResourceJson.Required(request.Name, $"{name} name");
        request.Definition = NormalizeEvaluationDefinition(request.Definition);
        return request;
    }

    private static FlowiseDatasetListItemDto MapDatasetResource(FlowiseDatasetEntity entity, int? rowCount = null) => new()
    {
        Id = entity.Id,
        DatasetKey = entity.DatasetKey,
        Name = entity.Name,
        Description = entity.Description,
        WorkspaceId = entity.WorkspaceId,
        Category = entity.Category,
        Status = entity.Status,
        Schema = DeserializeDatasetSchema(entity.SchemaJson),
        AdvancedMetadataJson = NormalizeJsonObject(entity.MetadataJson),
        RowCount = rowCount ?? 0,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static FlowiseEvaluatorListItemDto MapEvaluatorResource(FlowiseEvaluatorEntity entity) => new()
    {
        Id = entity.Id,
        EvaluatorKey = entity.EvaluatorKey,
        Name = entity.Name,
        Description = entity.Description,
        WorkspaceId = entity.WorkspaceId,
        EvaluatorType = entity.EvaluatorType,
        Status = entity.Status,
        Definition = DeserializeEvaluatorDefinition(entity.DefinitionJson),
        AdvancedMetadataJson = NormalizeJsonObject(entity.MetadataJson),
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static FlowiseEvaluationListItemDto MapEvaluationResource(FlowiseEvaluationEntity entity) => new()
    {
        Id = entity.Id,
        EvaluationKey = entity.EvaluationKey,
        Name = entity.Name,
        Description = entity.Description,
        WorkspaceId = entity.WorkspaceId,
        Category = entity.Category,
        Status = entity.Status,
        Definition = DeserializeEvaluationDefinition(entity.DefinitionJson),
        AdvancedMetadataJson = NormalizeJsonObject(entity.MetadataJson),
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static FlowiseDatasetSchemaDto NormalizeDatasetSchema(FlowiseDatasetSchemaDto? schema)
    {
        schema ??= new FlowiseDatasetSchemaDto();
        schema.InputColumns = NormalizeStringList(schema.InputColumns);
        schema.ExpectedOutputColumns = NormalizeStringList(schema.ExpectedOutputColumns);
        schema.AdvancedSchemaJson = NormalizeJsonObject(schema.AdvancedSchemaJson);
        return schema;
    }

    private static FlowiseEvaluatorDefinitionDto NormalizeEvaluatorDefinition(FlowiseEvaluatorDefinitionDto? definition)
    {
        definition ??= new FlowiseEvaluatorDefinitionDto();
        definition.Provider = NormalizeOptional(definition.Provider);
        definition.Model = NormalizeOptional(definition.Model);
        definition.PromptTemplate = NormalizeOptional(definition.PromptTemplate);
        definition.GradingMode = NormalizeOptional(definition.GradingMode);
        definition.AdvancedConfigJson = NormalizeJsonObject(definition.AdvancedConfigJson);
        return definition;
    }

    private static FlowiseEvaluationDefinitionDto NormalizeEvaluationDefinition(FlowiseEvaluationDefinitionDto? definition)
    {
        definition ??= new FlowiseEvaluationDefinitionDto();
        definition.DatasetId = FlowiseResourceJson.Required(definition.DatasetId, "Dataset Id");
        definition.EvaluatorId = FlowiseResourceJson.Required(definition.EvaluatorId, "Evaluator Id");
        definition.TargetFlowId = FlowiseResourceJson.Required(definition.TargetFlowId, "Target Flow Id");
        definition.Model = NormalizeOptional(definition.Model);
        definition.RunConfigJson = NormalizeJsonObject(definition.RunConfigJson);
        return definition;
    }

    private static FlowiseDatasetSchemaDto DeserializeDatasetSchema(string? json)
    {
        return DeserializeOrDefault<FlowiseDatasetSchemaDto>(json, NormalizeDatasetSchema);
    }

    private static FlowiseEvaluatorDefinitionDto DeserializeEvaluatorDefinition(string? json)
    {
        return DeserializeOrDefault<FlowiseEvaluatorDefinitionDto>(json, NormalizeEvaluatorDefinition);
    }

    private static FlowiseEvaluationDefinitionDto DeserializeEvaluationDefinition(string? json)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<FlowiseEvaluationDefinitionDto>(
                string.IsNullOrWhiteSpace(json) ? "{}" : json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            return NormalizeEvaluationDefinition(definition);
        }
        catch
        {
            return new FlowiseEvaluationDefinitionDto();
        }
    }

    private static T DeserializeOrDefault<T>(string? json, Func<T?, T> normalize)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(
                string.IsNullOrWhiteSpace(json) ? "{}" : json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            return normalize(value);
        }
        catch
        {
            return normalize(default);
        }
    }

    private static IReadOnlyList<string> NormalizeStringList(IReadOnlyList<string>? values)
    {
        return values?
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static string SerializeJson<T>(T value)
    {
        return JsonSerializer.Serialize(value);
    }

    private static FlowiseEvaluationResultDto MapResult(FlowiseEvaluationResultEntity item) => new()
    {
        AverageLatencyMs = item.AverageLatencyMs,
        Id = item.Id,
        MetricsJson = item.MetricsJson,
        PassRate = item.PassRate,
        ResultRowsJson = item.ResultRowsJson,
        Status = item.Status,
        TotalTokens = item.TotalTokens,
        VersionNo = item.VersionNo
    };

    private static JsonElement ParseJson(string json)
    {
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }

    private static string ReadString(JsonElement element, string property, string fallback = "")
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeJsonObject(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.GetRawText()
                : throw new ValidationException("Flowise Evaluation JSON 必须是对象", ErrorCodes.ParameterInvalid);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"Flowise Evaluation JSON 无效：{ex.Message}", ErrorCodes.ParameterInvalid);
        }
    }

    private static int NormalizePage(int value) => value <= 0 ? 1 : value;

    private static int NormalizeSize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 500);

    private async Task<IReadOnlyDictionary<string, int>> CountDatasetRowsAsync(IReadOnlyList<string> datasetIds, CancellationToken cancellationToken)
    {
        if (datasetIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await db.Queryable<FlowiseDatasetRowEntity>()
            .Where(item => !item.IsDeleted && datasetIds.Contains(item.DatasetId))
            .Select(item => item.DatasetId)
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<FlowiseDatasetRowEntity>> ReadDatasetCsvRowsAsync(
        FlowiseDatasetEntity dataset,
        AiWorkspace workspace,
        Stream csvStream,
        bool firstRowHeaders,
        CancellationToken cancellationToken)
    {
        var rows = new List<FlowiseDatasetRowEntity>();
        using var reader = new StreamReader(csvStream, detectEncodingFromByteOrderMarks: true);
        var lineNumber = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (lineNumber == 1 && firstRowHeaders)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var input = values.Count > 0 ? values[0].Trim() : string.Empty;
            var expectedOutput = values.Count > 1 ? values[1].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(input) && string.IsNullOrWhiteSpace(expectedOutput))
            {
                continue;
            }

            rows.Add(new FlowiseDatasetRowEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                WorkspaceId = dataset.WorkspaceId,
                DatasetId = dataset.Id,
                Input = input,
                ExpectedOutput = string.IsNullOrWhiteSpace(expectedOutput) ? null : expectedOutput,
                MetadataJson = JsonSerializer.Serialize(new { importedFrom = "csv", lineNumber })
            });
        }

        return rows;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static string SetJsonNumber(string? json, string propertyName, int value)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                if (parsed is not null)
                {
                    foreach (var item in parsed)
                    {
                        metadata[item.Key] = item.Value;
                    }
                }
            }
            catch (JsonException)
            {
                metadata.Clear();
            }
        }

        metadata[propertyName] = value;
        return JsonSerializer.Serialize(metadata);
    }
}
