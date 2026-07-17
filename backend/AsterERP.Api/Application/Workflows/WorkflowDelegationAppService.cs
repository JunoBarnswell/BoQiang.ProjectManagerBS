using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowDelegationAppService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser, IClock clock) : IWorkflowDelegationAppService
{
    public async Task<GridPageResult<WorkflowDelegationRuleResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(query.TenantId);
        var appCode = ResolveApp(query.AppCode);
        var ownerUserId = currentUser.GetAsterErpUserId();
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var entities = await databaseAccessor.GetCurrentDb().Queryable<WorkflowDelegationRuleEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.AppCode == appCode && item.OwnerUserId == ownerUserId)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.DelegateUserId.Contains(query.Keyword!) ||
                (item.ProcessDefinitionKey != null && item.ProcessDefinitionKey.Contains(query.Keyword!)))
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status), item => item.IsEnabled == (query.Status == "Enabled"))
            .OrderBy(item => item.StartAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        var userNames = await LoadUserNamesAsync(entities.SelectMany(item => new[] { item.OwnerUserId, item.DelegateUserId }), cancellationToken);
        return new GridPageResult<WorkflowDelegationRuleResponse>
        {
            Total = total.Value,
            Items = entities.Select(item => Map(item, userNames)).ToList()
        };
    }

    public async Task<WorkflowDelegationRuleResponse> SaveAsync(
        WorkflowDelegationRuleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(request.TenantId);
        var appCode = ResolveApp(request.AppCode);
        var ownerUserId = currentUser.GetAsterErpUserId();
        var delegateUserId = NormalizeRequired(request.DelegateUserId, "代理人不能为空");
        if (string.Equals(ownerUserId, delegateUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("审批委托不能委托给自己", ErrorCodes.ParameterInvalid);
        }

        if (request.StartAt >= request.EndAt)
        {
            throw new ValidationException("委托结束时间必须晚于开始时间", ErrorCodes.ParameterInvalid);
        }

        var scopeType = NormalizeOptional(request.ScopeType) ?? "All";
        var processDefinitionKey = NormalizeOptional(request.ProcessDefinitionKey);
        var isEnabled = request.IsEnabled ?? true;
        if (isEnabled)
        {
            await EnsureNoOverlapAsync(tenantId, appCode, ownerUserId, request.Id, request.StartAt, request.EndAt, cancellationToken);
        }

        WorkflowDelegationRuleEntity entity;
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            entity = new WorkflowDelegationRuleEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                OwnerUserId = ownerUserId,
                DelegateUserId = delegateUserId,
                ScopeType = scopeType,
                ProcessDefinitionKey = processDefinitionKey,
                StartAt = request.StartAt,
                EndAt = request.EndAt,
                IsEnabled = isEnabled,
                Remark = request.Reason
            };
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity = await GetRequiredOwnedAsync(request.Id, cancellationToken);
            entity.DelegateUserId = delegateUserId;
            entity.ScopeType = scopeType;
            entity.ProcessDefinitionKey = processDefinitionKey;
            entity.StartAt = request.StartAt;
            entity.EndAt = request.EndAt;
            entity.IsEnabled = isEnabled;
            entity.Remark = request.Reason;
            entity.UpdatedBy = ownerUserId;
            entity.UpdatedTime = clock.Now;
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        var userNames = await LoadUserNamesAsync([entity.OwnerUserId, entity.DelegateUserId], cancellationToken);
        return Map(entity, userNames);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredOwnedAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedBy = currentUser.GetAsterErpUserId();
        entity.DeletedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private async Task EnsureNoOverlapAsync(
        string tenantId,
        string appCode,
        string ownerUserId,
        string? currentId,
        DateTime startAt,
        DateTime endAt,
        CancellationToken cancellationToken)
    {
        var exists = await databaseAccessor.GetCurrentDb().Queryable<WorkflowDelegationRuleEntity>()
            .AnyAsync(item =>
                    !item.IsDeleted &&
                    item.IsEnabled &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.OwnerUserId == ownerUserId &&
                    item.Id != (currentId ?? string.Empty) &&
                    item.StartAt < endAt &&
                    item.EndAt > startAt,
                cancellationToken);
        if (exists)
        {
            throw new ValidationException("同一时间段已存在审批委托规则", ErrorCodes.WorkflowDelegationOverlap);
        }
    }

    private async Task<WorkflowDelegationRuleEntity> GetRequiredOwnedAsync(string id, CancellationToken cancellationToken)
    {
        var ownerUserId = currentUser.GetAsterErpUserId();
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowDelegationRuleEntity>()
            .FirstAsync(item => item.Id == id && item.OwnerUserId == ownerUserId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("审批委托不存在", ErrorCodes.WorkflowDelegationNotFound);
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

    private static WorkflowDelegationRuleResponse Map(
        WorkflowDelegationRuleEntity entity,
        IReadOnlyDictionary<string, string> userNames) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.OwnerUserId,
            userNames.GetValueOrDefault(entity.OwnerUserId),
            entity.DelegateUserId,
            userNames.GetValueOrDefault(entity.DelegateUserId),
            entity.ScopeType,
            entity.ProcessDefinitionKey,
            entity.StartAt,
            entity.EndAt,
            entity.IsEnabled,
            entity.Remark,
            entity.CreatedTime,
            entity.UpdatedTime);
}

