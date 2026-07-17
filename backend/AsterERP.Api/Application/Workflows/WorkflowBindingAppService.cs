using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Application.Workflows.Callbacks;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowBindingAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IClock clock,
    IWorkflowFormResourceAppService formResourceService,
    WorkflowCallbackConfigParser callbackConfigParser,
    WorkflowCallbackConfigValidator callbackConfigValidator) : IWorkflowBindingAppService
{
    public async Task<GridPageResult<WorkflowBindingResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = string.IsNullOrWhiteSpace(query.TenantId) ? currentUser.GetAsterErpTenantId() : query.TenantId;
        var appCode = string.IsNullOrWhiteSpace(query.AppCode) ? currentUser.GetAsterErpAppCode() : query.AppCode;
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>()
            .Where(item => !item.IsDeleted)
            .WhereIF(!string.IsNullOrWhiteSpace(tenantId), item => item.TenantId == tenantId)
            .WhereIF(!string.IsNullOrWhiteSpace(appCode), item => item.AppCode == appCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item => item.MenuCode.Contains(query.Keyword!) || item.BusinessType.Contains(query.Keyword!) || item.ProcessDefinitionKey.Contains(query.Keyword!))
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return new GridPageResult<WorkflowBindingResponse>
        {
            Total = total.Value,
            Items = items.Select(Map).ToList()
        };
    }

    public async Task<WorkflowBindingResponse> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("审批流绑定不存在", ErrorCodes.WorkflowBindingNotFound);
        return Map(entity);
    }

    public async Task<WorkflowBindingResponse> SaveAsync(WorkflowBindingUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = Normalize(request.TenantId, "租户不能为空");
        var appCode = Normalize(request.AppCode, "应用不能为空").ToUpperInvariant();
        var processDefinitionKey = Normalize(request.ProcessDefinitionKey, "流程定义 Key 不能为空");
        var formResource = await formResourceService.ValidateBindingResourceAsync(request, tenantId, appCode, cancellationToken);
        var menuCode = formResource?.MenuCode ?? Normalize(request.MenuCode, "菜单编码不能为空");
        var businessType = formResource?.BusinessType ?? Normalize(request.BusinessType, "业务类型不能为空");
        await EnsureProcessDefinitionAsync(processDefinitionKey, request.ProcessDefinitionId, cancellationToken);
        var callbackConfig = request.CallbackConfig;
        await callbackConfigValidator.ValidateAsync(
            callbackConfig,
            formResource?.ModelCode ?? NormalizeOptional(request.ModelCode),
            cancellationToken);

        var existing = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>()
            .FirstAsync(item =>
                !item.IsDeleted &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.MenuCode == menuCode &&
                item.BusinessType == businessType,
                cancellationToken);

        if (existing is null)
        {
            existing = new WorkflowBindingEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                MenuCode = menuCode,
                BusinessType = businessType,
                CreatedBy = currentUser.GetAsterErpUserId(),
                CreatedTime = clock.Now
            };
            Apply(existing, request, processDefinitionKey, formResource, callbackConfig);
            await databaseAccessor.GetCurrentDb().Insertable(existing).ExecuteCommandAsync(cancellationToken);
            return Map(existing);
        }

        Apply(existing, request, processDefinitionKey, formResource, callbackConfig);
        existing.UpdatedBy = currentUser.GetAsterErpUserId();
        existing.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(existing).ExecuteCommandAsync(cancellationToken);
        return Map(existing);
    }

    public async Task<WorkflowBindingStatusResponse> GetStatusAsync(
        WorkflowBindingStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        var pageCode = NormalizeOptional(request.PageCode);
        var modelCode = NormalizeOptional(request.ModelCode);
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            throw new ValidationException("请先选择租户应用工作区", ErrorCodes.PermissionDenied);
        }

        appCode = appCode.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(pageCode) || string.IsNullOrWhiteSpace(modelCode))
        {
            return new WorkflowBindingStatusResponse(pageCode, modelCode, null, []);
        }

        var binding = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.IsEnabled &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.PageCode == pageCode &&
                item.ModelCode == modelCode)
            .OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        if (binding is null)
        {
            return new WorkflowBindingStatusResponse(pageCode, modelCode, null, []);
        }

        var keys = (request.BusinessKeys ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();
        var instances = keys.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>()
                .Where(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.MenuCode == binding.MenuCode &&
                    item.BusinessType == binding.BusinessType &&
                    keys.Contains(item.BusinessKey))
                .OrderBy(item => item.StartedAt, OrderByType.Desc)
                .ToListAsync(cancellationToken);
        var latestByKey = instances
            .GroupBy(item => item.BusinessKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var statuses = keys
            .Select(key => latestByKey.TryGetValue(key, out var instance)
                ? new WorkflowBusinessApprovalStatusResponse(
                    key,
                    true,
                    instance.Status,
                    instance.ProcessInstanceId,
                    instance.ProcessDefinitionKey,
                    instance.StartedAt,
                    instance.FinishedAt)
                : new WorkflowBusinessApprovalStatusResponse(key, false, null, null, binding.ProcessDefinitionKey, null, null))
            .ToList();

        return new WorkflowBindingStatusResponse(pageCode, modelCode, Map(binding), statuses);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("审批流绑定不存在", ErrorCodes.WorkflowBindingNotFound);
        entity.IsDeleted = true;
        entity.DeletedBy = currentUser.GetAsterErpUserId();
        entity.DeletedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private void Apply(
        WorkflowBindingEntity entity,
        WorkflowBindingUpsertRequest request,
        string processDefinitionKey,
        WorkflowFormResourceResponse? formResource,
        WorkflowCallbackConfigDto? callbackConfig)
    {
        entity.ProcessDefinitionKey = processDefinitionKey;
        entity.ProcessDefinitionId = string.IsNullOrWhiteSpace(request.ProcessDefinitionId) ? null : request.ProcessDefinitionId.Trim();
        entity.ModelId = string.IsNullOrWhiteSpace(request.ModelId) ? null : request.ModelId.Trim();
        entity.ModelKey = string.IsNullOrWhiteSpace(request.ModelKey) ? null : request.ModelKey.Trim();
        if (formResource is not null)
        {
            entity.MenuCode = formResource.MenuCode;
            entity.BusinessType = formResource.BusinessType;
            entity.FormResourceCode = formResource.ResourceCode;
            entity.PageCode = formResource.PageCode;
            entity.ModelCode = formResource.ModelCode;
            entity.KeyField = formResource.KeyField;
            entity.DetailRoute = formResource.RoutePath;
        }
        else
        {
            entity.FormResourceCode = NormalizeOptional(request.FormResourceCode);
            entity.PageCode = NormalizeOptional(request.PageCode);
            entity.ModelCode = NormalizeOptional(request.ModelCode);
            entity.KeyField = NormalizeOptional(request.KeyField);
            entity.DetailRoute = NormalizeOptional(request.DetailRoute);
        }

        entity.TitleTemplate = NormalizeOptional(request.TitleTemplate);
        entity.IsEnabled = request.IsEnabled;
        entity.StartFormJson = request.StartFormJson;
        entity.StatusField = null;
        entity.BindingConfigJson = callbackConfigParser.Serialize(callbackConfig);
        entity.Remark = request.Remark;
    }

    private WorkflowBindingResponse Map(WorkflowBindingEntity entity)
    {
        return new WorkflowBindingResponse(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.MenuCode,
            entity.BusinessType,
            entity.ProcessDefinitionKey,
            entity.ProcessDefinitionId,
            entity.ModelId,
            entity.ModelKey,
            entity.FormResourceCode,
            entity.PageCode,
            entity.ModelCode,
            entity.KeyField,
            entity.DetailRoute,
            entity.TitleTemplate,
            entity.IsEnabled,
            entity.StartFormJson,
            callbackConfigParser.ResolveEffectiveConfig(entity),
            entity.Remark);
    }

    private async Task EnsureProcessDefinitionAsync(
        string processDefinitionKey,
        string? processDefinitionId,
        CancellationToken cancellationToken)
    {
        var query = databaseAccessor.GetCurrentDb().Queryable<ProcessDefinitionEntity>()
            .Where(item => item.SuspensionState != 2);
        var exists = string.IsNullOrWhiteSpace(processDefinitionId)
            ? await query.AnyAsync(item => item.Key == processDefinitionKey, cancellationToken)
            : await query.AnyAsync(
                item =>
                    item.Id == processDefinitionId.Trim() &&
                    item.Key == processDefinitionKey,
                cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("流程定义不存在或已挂起", ErrorCodes.WorkflowProcessDefinitionNotFound);
        }
    }

    private static string Normalize(string? value, string message)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

