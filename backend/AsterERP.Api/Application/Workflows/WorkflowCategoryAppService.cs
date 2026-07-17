using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Contracts.Workflows;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowCategoryAppService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser, IClock clock) : IWorkflowCategoryAppService
{
    public async Task<GridPageResult<WorkflowCategoryResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(query.TenantId);
        var appCode = ResolveApp(query.AppCode);
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var entities = await databaseAccessor.GetCurrentDb().Queryable<WorkflowCategoryEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.AppCode == appCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.CategoryCode.Contains(query.Keyword!) ||
                item.CategoryName.Contains(query.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status), item => item.IsEnabled == (query.Status == "Enabled"))
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CategoryCode, OrderByType.Asc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return new GridPageResult<WorkflowCategoryResponse>
        {
            Total = total.Value,
            Items = entities.Select(Map).ToList()
        };
    }

    public async Task<WorkflowCategoryResponse> SaveAsync(WorkflowCategoryUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(request.TenantId);
        var appCode = ResolveApp(request.AppCode);
        var categoryCode = NormalizeCode(request.CategoryCode, "分类编码不能为空");
        var categoryName = NormalizeRequired(request.CategoryName, "分类名称不能为空");
        var parentCode = NormalizeOptional(request.ParentCode)?.ToUpperInvariant();

        var duplicate = await databaseAccessor.GetCurrentDb().Queryable<WorkflowCategoryEntity>()
            .AnyAsync(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.CategoryCode == categoryCode &&
                    item.Id != (request.Id ?? string.Empty),
                cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("流程分类编码已存在", ErrorCodes.WorkflowCategoryDuplicate);
        }

        WorkflowCategoryEntity entity;
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            entity = new WorkflowCategoryEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                CategoryCode = categoryCode,
                CategoryName = categoryName,
                ParentCode = parentCode,
                SortOrder = request.SortOrder ?? 0,
                IsEnabled = request.IsEnabled ?? true,
                Remark = request.Remark
            };
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity = await GetRequiredAsync(request.Id, cancellationToken);
            entity.CategoryCode = categoryCode;
            entity.CategoryName = categoryName;
            entity.ParentCode = parentCode;
            entity.SortOrder = request.SortOrder ?? entity.SortOrder;
            entity.IsEnabled = request.IsEnabled ?? entity.IsEnabled;
            entity.Remark = request.Remark;
            entity.UpdatedBy = currentUser.GetAsterErpUserId();
            entity.UpdatedTime = clock.Now;
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        return Map(entity);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        var inUse = await databaseAccessor.GetCurrentDb().Queryable<ModelInfo>()
            .AnyAsync(item => item.DelFlag == 1 && item.AppSn == entity.AppCode && item.CategoryCode == entity.CategoryCode, cancellationToken);
        if (inUse)
        {
            throw new ValidationException("流程分类已被审批模板使用，不能删除", ErrorCodes.WorkflowActionInvalid);
        }

        entity.IsDeleted = true;
        entity.DeletedBy = currentUser.GetAsterErpUserId();
        entity.DeletedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<WorkflowCategoryEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowCategoryEntity>()
            .FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("流程分类不存在", ErrorCodes.WorkflowCategoryNotFound);
    }

    private string ResolveTenant(string? tenantId) =>
        NormalizeRequired(tenantId, currentUser.GetAsterErpTenantId(), "租户不能为空");

    private string ResolveApp(string? appCode) =>
        NormalizeRequired(appCode, currentUser.GetAsterErpAppCode(), "应用不能为空").ToUpperInvariant();

    private static string NormalizeRequired(string? value, string message) =>
        NormalizeRequired(value, null, message);

    private static string NormalizeRequired(string? value, string? fallback, string message)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static string NormalizeCode(string value, string message) =>
        NormalizeRequired(value, message).ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static WorkflowCategoryResponse Map(WorkflowCategoryEntity entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.CategoryCode,
            entity.CategoryName,
            entity.ParentCode,
            entity.SortOrder,
            entity.IsEnabled,
            entity.Remark,
            entity.CreatedTime,
            entity.UpdatedTime);
}

