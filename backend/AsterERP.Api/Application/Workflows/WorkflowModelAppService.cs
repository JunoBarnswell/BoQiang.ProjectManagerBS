using System.Text;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Workflows;
using System.Text.Json;
using AsterERP.Api.Application.Ai.Tools.Workflow;
using AsterERP.Api.Modules.Ai;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Ai;
using AsterERP.Contracts.Workflows;
using AsterERP.Workflow.Approval.Api.Enums.Form;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using NativeModelEntity = AsterERP.Workflow.Persistence.Entities.ModelEntity;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowModelAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IClock clock,
    IGuidGenerator guidGenerator,
    IRepositoryService repositoryService,
    WorkflowWorkspaceRuntimeInitializer workflowRuntimeInitializer,
    WorkflowBpmnDraftMapper aiWorkflowBpmnDraftMapper,
    WorkflowBusinessCanvasDraftMapper aiWorkflowBusinessCanvasDraftMapper,
    WorkflowBusinessModelLatestValidator businessModelValidator) : IWorkflowModelAppService
{
    public async Task<GridPageResult<WorkflowModelListItemResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var appCode = ResolveAppCode(query.AppCode);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<ModelInfo>()
            .Where(item => item.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(appCode), item => item.AppSn == appCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.Name.Contains(query.Keyword!) || item.ModelKey.Contains(query.Keyword!))
            .WhereIF(int.TryParse(query.Status, out var status), item => item.Status == status)
            .OrderBy(item => item.UpdateTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        var modelKeys = items.Select(item => item.ModelKey).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToList();
        var definitions = modelKeys.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<ProcessDefinitionEntity>()
                .Where(item => modelKeys.Contains(item.Key!))
                .ToListAsync(cancellationToken);

        return new GridPageResult<WorkflowModelListItemResponse>
        {
            Total = total.Value,
            Items = items.Select(item => MapListItem(item, definitions)).ToList()
        };
    }

    public async Task<WorkflowModelDetailResponse> GetDetailAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = await GetModelByModelIdAsync(modelId, cancellationToken);
        var extension = await GetExtensionAsync(model.ModelId, cancellationToken);
        var definitions = await databaseAccessor.GetCurrentDb().Queryable<ProcessDefinitionEntity>()
            .Where(item => item.Key == model.ModelKey)
            .ToListAsync(cancellationToken);
        return MapDetail(model, extension?.ExtensionJson, definitions);
    }

    public async Task<WorkflowModelDetailResponse> CreateOrUpdateAsync(WorkflowModelUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var appCode = ResolveAppCode(request.AppCode);
        var model = await FindModelInfoForUpsertAsync(request, cancellationToken);
        var isNew = model is null;
        model ??= new ModelInfo();
        model.Id = string.IsNullOrWhiteSpace(request.Id) ? model.Id : request.Id;
        model.ModelId = string.IsNullOrWhiteSpace(request.ModelId) ? model.ModelId : request.ModelId;
        model.ModelKey = NormalizeRequired(request.ModelKey, "流程 Key 不能为空");
        model.Name = NormalizeRequired(request.Name, "流程名称不能为空");
        model.AppSn = appCode;
        model.CategoryCode = string.IsNullOrWhiteSpace(request.CategoryCode) ? "FLOW_GENERAL" : request.CategoryCode.Trim();
        model.ModelType = request.ModelType ?? ModelInfo.CUSTOM_MODEL_TYPE;
        model.FormType = request.FormType ?? 0;
        model.ModelXml = string.IsNullOrWhiteSpace(model.ModelXml)
            ? WorkflowBpmnTemplate.Create(model.ModelKey, model.Name, currentUser.GetAsterErpUserId())
            : model.ModelXml;

        var saved = await SaveOrUpdateModelInfoAsync(model, isNew, cancellationToken);
        await UpsertNativeModelAsync(saved, cancellationToken);
        return await GetDetailAsync(saved.ModelId, cancellationToken);
    }

    public async Task DeleteDraftAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = await GetModelByModelIdAsync(modelId, cancellationToken);
        if (model.Status != ModelFormStatusEnum.CG.GetStatus() && model.Status != ModelFormStatusEnum.DFB.GetStatus())
        {
            throw new ValidationException("只能删除草稿或待发布流程模型", ErrorCodes.WorkflowActionInvalid);
        }

        model.DelFlag = 0;
        model.UpdateTime = clock.Now;
        model.Updator = currentUser.GetAsterErpUserId();
        await databaseAccessor.GetCurrentDb().Updateable<ModelInfo>()
            .SetColumns(item => item.DelFlag == model.DelFlag)
            .SetColumns(item => item.UpdateTime == model.UpdateTime)
            .SetColumns(item => item.Updator == model.Updator)
            .Where(item => item.ModelId == model.ModelId)
            .ExecuteCommandAsync(cancellationToken);

        await databaseAccessor.GetCurrentDb().Deleteable<NativeModelEntity>().Where(item => item.Id == model.ModelId).ExecuteCommandAsync(cancellationToken);
        await databaseAccessor.GetCurrentDb().Deleteable<WorkflowModelExtensionEntity>().Where(item => item.ModelId == model.ModelId).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<WorkflowModelDetailResponse> SaveXmlAsync(string modelId, WorkflowModelXmlSaveRequest request, CancellationToken cancellationToken = default)
    {
        var model = await GetModelByModelIdAsync(modelId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.BpmnXml))
        {
            throw new ValidationException("BPMN XML 不能为空", ErrorCodes.WorkflowModelInvalid);
        }

        businessModelValidator.ValidatePersisted(request.ExtensionJson);
        model.ModelXml = request.BpmnXml;
        model.Status = ModelFormStatusEnum.DFB.GetStatus();
        model.ExtendStatus = ModelFormStatusEnum.DFB.GetStatus();
        model.UpdateTime = clock.Now;
        model.Updator = currentUser.GetAsterErpUserId();
        await databaseAccessor.GetCurrentDb().Updateable<ModelInfo>()
            .SetColumns(item => item.ModelXml == model.ModelXml)
            .SetColumns(item => item.Status == model.Status)
            .SetColumns(item => item.ExtendStatus == model.ExtendStatus)
            .SetColumns(item => item.UpdateTime == model.UpdateTime)
            .SetColumns(item => item.Updator == model.Updator)
            .Where(item => item.ModelId == model.ModelId)
            .ExecuteCommandAsync(cancellationToken);
        await UpsertExtensionAsync(model, request.ExtensionJson, cancellationToken);
        await UpsertNativeModelAsync(model, cancellationToken);
        return await GetDetailAsync(model.ModelId, cancellationToken);
    }

    public async Task<WorkflowModelDetailResponse> ImportAiDraftAsync(string draftArtifactId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasAsterErpPermission(PermissionCodes.AiToolWorkflowImportDraft))
        {
            throw new ValidationException("无权限导入 AI Workflow 草稿", ErrorCodes.AiWorkflowImportDraftDenied);
        }

        var artifact = await databaseAccessor.GetCurrentDb().Queryable<AiWorkflowDraftArtifactEntity>()
            .FirstAsync(item =>
                item.Id == draftArtifactId &&
                !item.IsDeleted &&
                item.TenantId == currentUser.GetAsterErpTenantId() &&
                item.AppCode == ResolveAppCode(currentUser.GetAsterErpAppCode()) &&
                item.OwnerUserId == currentUser.GetAsterErpUserId(),
                cancellationToken)
            ?? throw new NotFoundException("AI Workflow 草稿不存在", ErrorCodes.AiWorkflowModelNotFound);
        var targetWorkflowModelId = string.IsNullOrWhiteSpace(artifact.ImportedWorkflowModelId)
            ? await FindExistingAiWorkflowModelIdAsync(artifact, cancellationToken)
            : artifact.ImportedWorkflowModelId;
        var isUpdate = !string.IsNullOrWhiteSpace(targetWorkflowModelId);
        var requiredPermission = isUpdate ? PermissionCodes.WorkflowModelEdit : PermissionCodes.WorkflowModelAdd;
        if (!currentUser.HasAsterErpPermission(requiredPermission))
        {
            throw new ValidationException("无权限创建或更新正式 Workflow 草稿", ErrorCodes.AiWorkflowImportDraftDenied);
        }

        var saved = await CreateOrUpdateAsync(new WorkflowModelUpsertRequest(
            null,
            targetWorkflowModelId,
            artifact.WorkflowKey,
            artifact.WorkflowName,
            artifact.AppCode,
            "AI_WORKFLOW",
            null,
            null,
            $"Imported from AI draft {artifact.Id}"), cancellationToken);
        var draft = ParseAiWorkflowDraft(artifact);
        var bpmnXml = string.IsNullOrWhiteSpace(artifact.BpmnXml)
            ? aiWorkflowBpmnDraftMapper.Map(draft)
            : artifact.BpmnXml;
        var canvas = aiWorkflowBusinessCanvasDraftMapper.ResolveDesignerCanvas(artifact.BusinessCanvasJson, draft);
        var extensionJson = JsonSerializer.Serialize(new
        {
            version = "latest",
            kind = "WorkflowBusinessModelLatest",
            businessDesign = canvas.BusinessDesign
        }, WorkflowJsonOptions.Options);
        var detail = await SaveXmlAsync(saved.ModelId, new WorkflowModelXmlSaveRequest(bpmnXml!, extensionJson), cancellationToken);
        artifact.ImportedWorkflowModelId = detail.ModelId;
        artifact.BusinessCanvasJson = canvas.CanvasJson;
        artifact.Status = "ImportedDraft";
        artifact.UpdatedBy = currentUser.GetAsterErpUserId();
        artifact.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(artifact).ExecuteCommandAsync(cancellationToken);
        return detail;
    }

    private async Task<string?> FindExistingAiWorkflowModelIdAsync(
        AiWorkflowDraftArtifactEntity artifact,
        CancellationToken cancellationToken)
    {
        var appCode = ResolveAppCode(artifact.AppCode);
        var existing = await databaseAccessor.GetCurrentDb().Queryable<ModelInfo>()
            .Where(item => item.DelFlag == 1 &&
                           item.AppSn == appCode &&
                           item.ModelKey == artifact.WorkflowKey)
            .OrderBy(item => item.UpdateTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        return existing?.ModelId;
    }

    public async Task<WorkflowModelValidationResponse> ValidateAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = await GetModelByModelIdAsync(modelId, cancellationToken);
        var extension = await GetExtensionAsync(model.ModelId, cancellationToken);
        businessModelValidator.ValidatePersisted(extension?.ExtensionJson);
        var errors = await repositoryService.ValidateProcessAsync(Encoding.UTF8.GetBytes(model.ModelXml ?? string.Empty), cancellationToken);
        return new WorkflowModelValidationResponse(
            errors.Count == 0,
            errors.Select(item => string.IsNullOrWhiteSpace(item.Message) ? item.ToString() ?? string.Empty : item.Message!)
                .ToList());
    }

    public async Task<WorkflowModelPublishResponse> PublishAsync(string modelId, CancellationToken cancellationToken = default)
    {
        await workflowRuntimeInitializer.EnsureInitializedAsync(cancellationToken);
        var model = await GetModelByModelIdAsync(modelId, cancellationToken);
        var validation = await ValidateAsync(modelId, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(string.Join("; ", validation.Errors), ErrorCodes.WorkflowModelInvalid);
        }

        var resourceName = $"{model.ModelKey}.bpmn";
        var deploymentId = await repositoryService.DeployAsync(
            model.Name,
            model.CategoryCode,
            model.AppSn,
            new Dictionary<string, byte[]> { [resourceName] = Encoding.UTF8.GetBytes(model.ModelXml ?? string.Empty) },
            enableDuplicateFiltering: false,
            cancellationToken);

        var processDefinition = await databaseAccessor.GetCurrentDb().Queryable<ProcessDefinitionEntity>()
            .Where(item => item.DeploymentId == deploymentId && item.Key == model.ModelKey)
            .OrderBy(item => item.Version, OrderByType.Desc)
            .FirstAsync(cancellationToken);

        if (processDefinition is null)
        {
            throw new ValidationException("流程已部署但未生成流程定义", ErrorCodes.WorkflowPublishFailed);
        }

        model.Status = ModelFormStatusEnum.YFB.GetStatus();
        model.ExtendStatus = ModelFormStatusEnum.YFB.GetStatus();
        model.UpdateTime = clock.Now;
        model.Updator = currentUser.GetAsterErpUserId();
        await databaseAccessor.GetCurrentDb().Updateable<ModelInfo>()
            .SetColumns(item => item.Status == model.Status)
            .SetColumns(item => item.ExtendStatus == model.ExtendStatus)
            .SetColumns(item => item.UpdateTime == model.UpdateTime)
            .SetColumns(item => item.Updator == model.Updator)
            .Where(item => item.ModelId == model.ModelId)
            .ExecuteCommandAsync(cancellationToken);
        await databaseAccessor.GetCurrentDb().Updateable<NativeModelEntity>()
            .SetColumns(item => item.DeploymentId == deploymentId)
            .Where(item => item.Id == model.ModelId)
            .ExecuteCommandAsync(cancellationToken);

        return new WorkflowModelPublishResponse(model.ModelId, deploymentId, processDefinition.Id, processDefinition.Version);
    }

    public async Task SuspendAsync(string processDefinitionId, CancellationToken cancellationToken = default)
    {
        await repositoryService.SuspendProcessDefinitionByIdAsync(processDefinitionId, true, null, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowModelVersionResponse>> GetVersionsAsync(string modelKey, CancellationToken cancellationToken = default)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<ProcessDefinitionEntity>()
            .Where(item => item.Key == modelKey)
            .OrderBy(item => item.Version, OrderByType.Desc)
            .Select(item => new WorkflowModelVersionResponse(
                item.Id,
                item.DeploymentId,
                item.Key,
                item.Name,
                item.Version,
                item.SuspensionState != 1,
                item.TenantId))
            .ToListAsync(cancellationToken);
    }

    private async Task<ModelInfo> GetModelByModelIdAsync(string modelId, CancellationToken cancellationToken)
    {
        var model = await databaseAccessor.GetCurrentDb().Queryable<ModelInfo>()
            .FirstAsync(item => item.ModelId == modelId && item.DelFlag == 1, cancellationToken);
        return model ?? throw new NotFoundException("流程模型不存在", ErrorCodes.WorkflowModelNotFound);
    }

    private async Task<ModelInfo?> FindModelInfoForUpsertAsync(
        WorkflowModelUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        if (!string.IsNullOrWhiteSpace(request.ModelId))
        {
            return await db.Queryable<ModelInfo>()
                .FirstAsync(item => item.ModelId == request.ModelId && item.DelFlag == 1, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            return await db.Queryable<ModelInfo>()
                .FirstAsync(item => item.Id == request.Id && item.DelFlag == 1, cancellationToken);
        }

        return null;
    }

    private async Task<ModelInfo> SaveOrUpdateModelInfoAsync(
        ModelInfo model,
        bool isNew,
        CancellationToken cancellationToken)
    {
        NormalizeModelInfo(model);
        var now = clock.Now;
        if (isNew)
        {
            model.Id = string.IsNullOrWhiteSpace(model.Id) ? guidGenerator.Create().ToString("N") : model.Id;
            model.ModelId = string.IsNullOrWhiteSpace(model.ModelId) ? $"model-{guidGenerator.Create():N}" : model.ModelId;
            model.CreateTime = now;
            model.Creator = currentUser.GetAsterErpUserId();
            model.UpdateTime = now;
            model.Updator = currentUser.GetAsterErpUserId();
            model.Status = ModelFormStatusEnum.CG.GetStatus();
            model.ExtendStatus = ModelFormStatusEnum.CG.GetStatus();
            await databaseAccessor.GetCurrentDb().Insertable(model).ExecuteCommandAsync(cancellationToken);
            return model;
        }

        model.UpdateTime = now;
        model.Updator = currentUser.GetAsterErpUserId();
        model.ExtendStatus = ModelFormStatusEnum.DFB.GetStatus();
        await databaseAccessor.GetCurrentDb().Updateable(model).ExecuteCommandAsync(cancellationToken);
        return model;
    }

    private void NormalizeModelInfo(ModelInfo model)
    {
        var now = clock.Now;
        model.ModelId = string.IsNullOrWhiteSpace(model.ModelId) ? $"model-{guidGenerator.Create():N}" : model.ModelId;
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.ModelKey = model.ModelKey?.Trim() ?? string.Empty;
        model.ModelType ??= ModelInfo.CUSTOM_MODEL_TYPE;
        model.FormType ??= 0;
        model.AppSn = string.IsNullOrWhiteSpace(model.AppSn) ? ResolveAppCode(null) : model.AppSn.Trim().ToUpperInvariant();
        model.CategoryCode = string.IsNullOrWhiteSpace(model.CategoryCode) ? "FLOW_GENERAL" : model.CategoryCode.Trim();
        model.OwnDeptId ??= string.Empty;
        model.OwnDeptName ??= string.Empty;
        model.FlowOwnerNo ??= string.Empty;
        model.FlowOwnerName ??= string.Empty;
        model.ProcessDockingNo ??= string.Empty;
        model.ProcessDockingName ??= string.Empty;
        model.ApplyCompanies ??= string.Empty;
        model.ShowStatus ??= string.Empty;
        model.AppliedRange ??= 0;
        model.AuthPointList ??= string.Empty;
        model.Superuser ??= string.Empty;
        model.BusinessUrl ??= string.Empty;
        model.SkipSet ??= 0;
        model.ModelIcon ??= string.Empty;
        model.OrderNo ??= 0;
        model.ModelXml ??= string.Empty;
        model.Creator = string.IsNullOrWhiteSpace(model.Creator) ? currentUser.GetAsterErpUserId() : model.Creator;
        model.Updator = currentUser.GetAsterErpUserId();
        model.CreateTime ??= now;
        model.UpdateTime = now;
        model.DelFlag ??= 1;
        model.Keyword = $"{model.Name} {model.ModelKey}".Trim();
    }

    private async Task UpsertNativeModelAsync(ModelInfo model, CancellationToken cancellationToken)
    {
        var sourceId = $"model-source-{model.ModelId}";
        await databaseAccessor.GetCurrentDb().Storageable(new ByteArrayEntity
        {
            Id = sourceId,
            Revision = 1,
            Name = $"{model.ModelKey}.bpmn",
            DeploymentId = null,
            Bytes = Encoding.UTF8.GetBytes(model.ModelXml ?? string.Empty),
            Generated = false
        }).ExecuteCommandAsync(cancellationToken);

        await databaseAccessor.GetCurrentDb().Storageable(new NativeModelEntity
        {
            Id = model.ModelId,
            Revision = 1,
            Name = model.Name,
            Key = model.ModelKey,
            Category = model.CategoryCode,
            CreateTime = model.CreateTime,
            LastUpdateTime = model.UpdateTime,
            Version = model.Version ?? 1,
            MetaInfo = WorkflowJson.Serialize(new { appCode = model.AppSn, categoryCode = model.CategoryCode }),
            DeploymentId = null,
            EditorSourceValueId = sourceId,
            EditorSourceExtraValueId = null,
            TenantId = model.AppSn
        }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task UpsertExtensionAsync(ModelInfo model, string? extensionJson, CancellationToken cancellationToken)
    {
        var existing = await GetExtensionAsync(model.ModelId, cancellationToken);
        if (existing is null)
        {
            await databaseAccessor.GetCurrentDb().Insertable(new WorkflowModelExtensionEntity
            {
                ModelId = model.ModelId,
                ModelKey = model.ModelKey,
                TenantId = currentUser.GetAsterErpTenantId(),
                AppCode = ResolveAppCode(model.AppSn),
                ExtensionJson = extensionJson
            }).ExecuteCommandAsync(cancellationToken);
            return;
        }

        existing.ModelKey = model.ModelKey;
        existing.AppCode = ResolveAppCode(model.AppSn);
        existing.ExtensionJson = extensionJson;
        existing.UpdatedBy = currentUser.GetAsterErpUserId();
        existing.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(existing).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<WorkflowModelExtensionEntity?> GetExtensionAsync(string modelId, CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowModelExtensionEntity>()
            .FirstAsync(item => item.ModelId == modelId && !item.IsDeleted, cancellationToken);
    }

    private WorkflowModelListItemResponse MapListItem(ModelInfo model, IReadOnlyCollection<ProcessDefinitionEntity> definitions)
    {
        var latest = definitions
            .Where(item => string.Equals(item.Key, model.ModelKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();

        return new WorkflowModelListItemResponse(
            model.Id,
            model.ModelId,
            model.ModelKey,
            model.Name,
            model.AppSn,
            model.CategoryCode,
            model.Status,
            model.ExtendStatus,
            latest?.Version,
            latest?.Id,
            model.CreateTime,
            model.UpdateTime);
    }

    private WorkflowModelDetailResponse MapDetail(ModelInfo model, string? extensionJson, IReadOnlyCollection<ProcessDefinitionEntity> definitions)
    {
        if (!string.IsNullOrWhiteSpace(extensionJson))
        {
            businessModelValidator.ValidatePersisted(extensionJson);
        }

        var latest = definitions
            .Where(item => string.Equals(item.Key, model.ModelKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();

        return new WorkflowModelDetailResponse(
            model.Id,
            model.ModelId,
            model.ModelKey,
            model.Name,
            model.AppSn,
            model.CategoryCode,
            model.Status,
            model.ExtendStatus,
            latest?.Version,
            latest?.Id,
            model.ModelXml ?? string.Empty,
            extensionJson,
            model.CreateTime,
            model.UpdateTime);
    }

    private string ResolveAppCode(string? appCode)
    {
        return (string.IsNullOrWhiteSpace(appCode) ? currentUser.GetAsterErpAppCode() : appCode) ?? "SYSTEM";
    }

    private static string NormalizeRequired(string value, string message)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(message, ErrorCodes.WorkflowModelInvalid);
        }

        return normalized;
    }

    private static AiWorkflowDraftDto ParseAiWorkflowDraft(AiWorkflowDraftArtifactEntity artifact)
    {
        try
        {
            return JsonSerializer.Deserialize<AiWorkflowDraftDto>(artifact.DraftDslJson, WorkflowJsonOptions.Options)
                   ?? throw new JsonException("empty draft");
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"AI Workflow 草稿解析失败：{ex.Message}", ErrorCodes.AiWorkflowDraftParseFailed);
        }
    }
}

