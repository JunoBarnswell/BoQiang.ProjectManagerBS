using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowRequestDraftAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IClock clock,
    IWorkflowInstanceAppService instanceService) : IWorkflowRequestDraftAppService
{
    public async Task<GridPageResult<WorkflowRequestDraftResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(query.TenantId);
        var appCode = ResolveApp(query.AppCode);
        var ownerUserId = currentUser.GetAsterErpUserId();
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var entities = await databaseAccessor.GetCurrentDb().Queryable<WorkflowRequestDraftEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.AppCode == appCode && item.OwnerUserId == ownerUserId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.Title.Contains(query.Keyword!) ||
                item.FormResourceCode.Contains(query.Keyword!) ||
                item.BusinessType.Contains(query.Keyword!) ||
                (item.BusinessKey != null && item.BusinessKey.Contains(query.Keyword!)))
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status), item => item.Status == query.Status)
            .OrderBy(item => item.LastSavedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        var ownerNames = await LoadUserNamesAsync(entities.Select(item => item.OwnerUserId), cancellationToken);
        return new GridPageResult<WorkflowRequestDraftResponse>
        {
            Total = total.Value,
            Items = entities.Select(item => Map(item, ownerNames)).ToList()
        };
    }

    public async Task<WorkflowRequestDraftResponse> SaveAsync(WorkflowRequestDraftUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(request.TenantId);
        var appCode = ResolveApp(request.AppCode);
        var draftJson = NormalizeJson(request.DraftJson);
        var ownerUserId = currentUser.GetAsterErpUserId();

        WorkflowRequestDraftEntity entity;
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            entity = new WorkflowRequestDraftEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                OwnerUserId = ownerUserId,
                FormResourceCode = NormalizeRequired(request.FormResourceCode, "表单资源不能为空"),
                MenuCode = NormalizeRequired(request.MenuCode, "菜单编码不能为空"),
                BusinessType = NormalizeRequired(request.BusinessType, "业务类型不能为空"),
                BusinessKey = NormalizeOptional(request.BusinessKey),
                Title = NormalizeRequired(request.Title, "草稿标题不能为空"),
                DraftJson = draftJson,
                LastSavedAt = clock.Now
            };
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity = await GetRequiredOwnedAsync(request.Id, cancellationToken);
            if (entity.Status == "Submitted")
            {
                throw new ValidationException("已提交草稿不能再次编辑", ErrorCodes.WorkflowActionInvalid);
            }

            entity.FormResourceCode = NormalizeRequired(request.FormResourceCode, "表单资源不能为空");
            entity.MenuCode = NormalizeRequired(request.MenuCode, "菜单编码不能为空");
            entity.BusinessType = NormalizeRequired(request.BusinessType, "业务类型不能为空");
            entity.BusinessKey = NormalizeOptional(request.BusinessKey);
            entity.Title = NormalizeRequired(request.Title, "草稿标题不能为空");
            entity.DraftJson = draftJson;
            entity.LastSavedAt = clock.Now;
            entity.UpdatedBy = ownerUserId;
            entity.UpdatedTime = clock.Now;
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        var ownerNames = await LoadUserNamesAsync([entity.OwnerUserId], cancellationToken);
        return Map(entity, ownerNames);
    }

    public async Task<WorkflowInstanceResponse> SubmitAsync(
        string id,
        WorkflowRequestDraftSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredOwnedAsync(id, cancellationToken);
        if (entity.Status == "Submitted")
        {
            throw new ValidationException("草稿已经提交", ErrorCodes.WorkflowActionInvalid);
        }

        if (string.IsNullOrWhiteSpace(entity.BusinessKey))
        {
            throw new ValidationException("草稿缺少业务主键，不能发起审批", ErrorCodes.ParameterInvalid);
        }

        var variables = DeserializeVariables(entity.DraftJson);
        if (request.Variables is not null)
        {
            foreach (var pair in request.Variables)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        var response = await instanceService.StartAsync(new WorkflowStartInstanceRequest(
            entity.TenantId,
            entity.AppCode,
            entity.MenuCode,
            entity.BusinessType,
            entity.BusinessKey,
            string.IsNullOrWhiteSpace(request.Comment) ? entity.Title : request.Comment,
            variables), cancellationToken);

        entity.Status = "Submitted";
        entity.SubmittedAt = clock.Now;
        entity.ProcessInstanceId = response.ProcessInstanceId;
        entity.UpdatedBy = currentUser.GetAsterErpUserId();
        entity.UpdatedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return response;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredOwnedAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedBy = currentUser.GetAsterErpUserId();
        entity.DeletedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<WorkflowRequestDraftEntity> GetRequiredOwnedAsync(string id, CancellationToken cancellationToken)
    {
        var ownerUserId = currentUser.GetAsterErpUserId();
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowRequestDraftEntity>()
            .FirstAsync(item => item.Id == id && item.OwnerUserId == ownerUserId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("审批草稿不存在", ErrorCodes.WorkflowDraftNotFound);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadUserNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => ids.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        return users.ToDictionary(item => item.Id, item => string.IsNullOrWhiteSpace(item.DisplayName) ? item.UserName : item.DisplayName, StringComparer.OrdinalIgnoreCase);
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeJson(string value)
    {
        var json = string.IsNullOrWhiteSpace(value) ? "{}" : value.Trim();
        try
        {
            using var _ = JsonDocument.Parse(json);
            return json;
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"草稿 JSON 格式不正确：{ex.Message}", ErrorCodes.ParameterInvalid);
        }
    }

    private static Dictionary<string, object?> DeserializeVariables(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
    }

    private static WorkflowRequestDraftResponse Map(
        WorkflowRequestDraftEntity entity,
        IReadOnlyDictionary<string, string> ownerNames) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.OwnerUserId,
            ownerNames.GetValueOrDefault(entity.OwnerUserId),
            entity.FormResourceCode,
            entity.MenuCode,
            entity.BusinessType,
            entity.BusinessKey,
            entity.Title,
            entity.DraftJson,
            entity.Status,
            entity.LastSavedAt,
            entity.SubmittedAt,
            entity.ProcessInstanceId,
            entity.CreatedTime,
            entity.UpdatedTime);
}

