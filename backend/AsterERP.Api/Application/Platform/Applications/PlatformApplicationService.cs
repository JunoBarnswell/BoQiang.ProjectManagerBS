using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Platform;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Modules.Platform;
using SqlSugar;

namespace AsterERP.Api.Application.Platform.Applications;

public sealed class PlatformApplicationService(
    ISqlSugarClient db,
    PlatformAccessGuard accessGuard,
    IPlatformApplicationWorkspaceProvisioningService workspaceProvisioningService) : IPlatformApplicationService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemApplicationEntity>, OrderByType, ISugarQueryable<SystemApplicationEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemApplicationEntity>, OrderByType, ISugarQueryable<SystemApplicationEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["appCode"] = (query, order) => query.OrderBy(item => item.AppCode, order),
            ["appName"] = (query, order) => query.OrderBy(item => item.AppName, order),
            ["appType"] = (query, order) => query.OrderBy(item => item.AppType, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["defaultRoutePath"] = (query, order) => query.OrderBy(item => item.AdminDefaultRoutePath, order),
            ["adminDefaultRoutePath"] = (query, order) => query.OrderBy(item => item.AdminDefaultRoutePath, order),
            ["runtimeDefaultRoutePath"] = (query, order) => query.OrderBy(item => item.RuntimeDefaultRoutePath, order),
            ["status"] = (query, order) => query.OrderBy(item => item.Status, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order),
            ["version"] = (query, order) => query.OrderBy(item => item.Version, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemApplicationEntity>, GridFilter, ISugarQueryable<SystemApplicationEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemApplicationEntity>, GridFilter, ISugarQueryable<SystemApplicationEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["appCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.AppCode),
            ["appName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.AppName),
            ["appType"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.AppType),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["defaultRoutePath"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.AdminDefaultRoutePath),
            ["adminDefaultRoutePath"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.AdminDefaultRoutePath),
            ["runtimeDefaultRoutePath"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.RuntimeDefaultRoutePath),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Status),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime),
            ["version"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Version)
        };

    public async Task<GridPageResult<ApplicationListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var keyword = NormalizeOptional(gridQuery.Keyword);
        var status = NormalizeOptional(gridQuery.Status);
        var query = db.Queryable<SystemApplicationEntity>().Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item => item.AppCode.Contains(keyword) || item.AppName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var total = new RefAsync<int>();
        var items = await GridSortApplier
            .Apply(query, gridQuery.Sorts, Sorters, ApplyDefaultSort)
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, total);

        return new GridPageResult<ApplicationListItemResponse>
        {
            Total = total.Value,
            Items = items.Select(Map).ToList()
        };
    }

    public async Task<ApplicationListItemResponse> CreateAsync(ApplicationUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        EnsureRequest(request);
        await EnsureUniqueCodeAsync(request.AppCode, null, cancellationToken);

        var adminDefaultRoutePath = NormalizeOptional(request.AdminDefaultRoutePath) ?? NormalizeOptional(request.DefaultRoutePath);
        var runtimeDefaultRoutePath = NormalizeOptional(request.RuntimeDefaultRoutePath);
        var entity = new SystemApplicationEntity
        {
            AppCode = request.AppCode.Trim().ToUpperInvariant(),
            AppName = request.AppName.Trim(),
            AppType = NormalizeOptional(request.AppType) ?? "Business",
            Icon = NormalizeOptional(request.Icon),
            DefaultRoutePath = adminDefaultRoutePath,
            AdminDefaultRoutePath = adminDefaultRoutePath,
            RuntimeDefaultRoutePath = runtimeDefaultRoutePath,
            Status = NormalizeStatus(request.Status),
            Version = NormalizeOptional(request.Version),
            Remark = NormalizeOptional(request.Remark)
        };

        await db.Ado.BeginTranAsync();
        try
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await workspaceProvisioningService.ProvisionCurrentTenantAsync(
                entity.AppCode,
                entity.AppName,
                cancellationToken);
            await db.Ado.CommitTranAsync();
        }
        catch
        {
            await db.Ado.RollbackTranAsync();
            throw;
        }

        return Map(entity);
    }

    public async Task<ApplicationListItemResponse> UpdateAsync(string id, ApplicationUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        EnsureRequest(request);
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureUniqueCodeAsync(request.AppCode, id, cancellationToken);

        entity.AppCode = request.AppCode.Trim().ToUpperInvariant();
        entity.AppName = request.AppName.Trim();
        entity.AppType = NormalizeOptional(request.AppType) ?? "Business";
        entity.Icon = NormalizeOptional(request.Icon);
        var adminDefaultRoutePath = NormalizeOptional(request.AdminDefaultRoutePath) ?? NormalizeOptional(request.DefaultRoutePath);
        entity.DefaultRoutePath = adminDefaultRoutePath;
        entity.AdminDefaultRoutePath = adminDefaultRoutePath;
        entity.RuntimeDefaultRoutePath = NormalizeOptional(request.RuntimeDefaultRoutePath);
        entity.Status = NormalizeStatus(request.Status);
        entity.Version = NormalizeOptional(request.Version);
        entity.Remark = NormalizeOptional(request.Remark);
        entity.UpdatedTime = DateTime.UtcNow;

        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedTime = entity.DeletedTime;
        await db.Updateable(entity).UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    public async Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var normalizedIds = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var entities = await db.Queryable<SystemApplicationEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (entities.Count != normalizedIds.Count)
        {
            throw new NotFoundException("应用不存在", ErrorCodes.PlatformResourceNotFound);
        }

        var normalizedStatus = NormalizeStatus(status);
        var updatedTime = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.Status = normalizedStatus;
            entity.UpdatedTime = updatedTime;
        }
        await db.Updateable(entities).UpdateColumns(item => new { item.Status, item.UpdatedTime }).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<SystemApplicationEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("应用不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task EnsureUniqueCodeAsync(string appCode, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = appCode.Trim().ToUpperInvariant();
        var exists = await db.Queryable<SystemApplicationEntity>()
            .Where(item => item.AppCode == normalizedCode && item.Id != (currentId ?? string.Empty) && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("应用编码已存在");
        }
    }

    private static void EnsureRequest(ApplicationUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AppCode) || string.IsNullOrWhiteSpace(request.AppName))
        {
            throw new ValidationException("应用编码和名称不能为空");
        }
    }

    private static ApplicationListItemResponse Map(SystemApplicationEntity entity)
    {
        return new ApplicationListItemResponse(
            entity.Id,
            entity.AppCode,
            entity.AppName,
            entity.AppType,
            entity.Icon,
            entity.DefaultRoutePath,
            entity.AdminDefaultRoutePath ?? entity.DefaultRoutePath,
            entity.RuntimeDefaultRoutePath,
            entity.Status,
            entity.Version,
            entity.Remark);
    }

    private static string NormalizeStatus(string status)
    {
        return status.Trim().Equals("Disabled", StringComparison.OrdinalIgnoreCase) ? "Disabled" : "Enabled";
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static ISugarQueryable<SystemApplicationEntity> ApplyDefaultSort(ISugarQueryable<SystemApplicationEntity> query) =>
        query.OrderBy(item => item.CreatedTime, OrderByType.Desc);
}
