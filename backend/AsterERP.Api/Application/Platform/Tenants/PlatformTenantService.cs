using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Platform;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Modules.Platform;
using SqlSugar;

namespace AsterERP.Api.Application.Platform.Tenants;

public sealed class PlatformTenantService(ISqlSugarClient db, PlatformAccessGuard accessGuard) : IPlatformTenantService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemTenantEntity>, OrderByType, ISugarQueryable<SystemTenantEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemTenantEntity>, OrderByType, ISugarQueryable<SystemTenantEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["contactName"] = (query, order) => query.OrderBy(item => item.ContactName, order),
            ["contactPhone"] = (query, order) => query.OrderBy(item => item.ContactPhone, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["expiredAt"] = (query, order) => query.OrderBy(item => item.ExpiredAt, order),
            ["remark"] = (query, order) => query.OrderBy(item => item.Remark, order),
            ["shortName"] = (query, order) => query.OrderBy(item => item.ShortName, order),
            ["status"] = (query, order) => query.OrderBy(item => item.Status, order),
            ["tenantCode"] = (query, order) => query.OrderBy(item => item.TenantCode, order),
            ["tenantName"] = (query, order) => query.OrderBy(item => item.TenantName, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemTenantEntity>, GridFilter, ISugarQueryable<SystemTenantEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemTenantEntity>, GridFilter, ISugarQueryable<SystemTenantEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["contactName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ContactName),
            ["contactPhone"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ContactPhone),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["remark"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Remark),
            ["shortName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ShortName),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Status),
            ["tenantCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TenantCode),
            ["tenantName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TenantName),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    public async Task<GridPageResult<TenantListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        var keyword = NormalizeOptional(gridQuery.Keyword);
        var status = NormalizeOptional(gridQuery.Status);
        var query = db.Queryable<SystemTenantEntity>().Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.TenantCode.Contains(keyword) ||
                item.TenantName.Contains(keyword) ||
                (item.ShortName != null && item.ShortName.Contains(keyword)));
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

        return new GridPageResult<TenantListItemResponse>
        {
            Total = total.Value,
            Items = items.Select(Map).ToList()
        };
    }

    public async Task<TenantListItemResponse> CreateAsync(TenantUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        EnsureRequest(request);
        await EnsureUniqueCodeAsync(request.TenantCode, null, cancellationToken);

        var entity = new SystemTenantEntity
        {
            TenantCode = request.TenantCode.Trim(),
            TenantName = request.TenantName.Trim(),
            ShortName = NormalizeOptional(request.ShortName),
            Status = NormalizeStatus(request.Status),
            ExpiredAt = request.ExpiredAt,
            ContactName = NormalizeOptional(request.ContactName),
            ContactPhone = NormalizeOptional(request.ContactPhone),
            ConfigJson = NormalizeOptional(request.ConfigJson),
            Remark = NormalizeOptional(request.Remark)
        };

        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<TenantListItemResponse> UpdateAsync(string id, TenantUpsertRequest request, CancellationToken cancellationToken = default)
    {
        accessGuard.EnsurePlatformAdmin();
        EnsureRequest(request);
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureUniqueCodeAsync(request.TenantCode, id, cancellationToken);

        entity.TenantCode = request.TenantCode.Trim();
        entity.TenantName = request.TenantName.Trim();
        entity.ShortName = NormalizeOptional(request.ShortName);
        entity.Status = NormalizeStatus(request.Status);
        entity.ExpiredAt = request.ExpiredAt;
        entity.ContactName = NormalizeOptional(request.ContactName);
        entity.ContactPhone = NormalizeOptional(request.ContactPhone);
        entity.ConfigJson = NormalizeOptional(request.ConfigJson);
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
        var normalizedIds = NormalizeIds(ids);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var entities = await db.Queryable<SystemTenantEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (entities.Count != normalizedIds.Count)
        {
            throw new NotFoundException("租户不存在", ErrorCodes.PlatformResourceNotFound);
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

    private async Task<SystemTenantEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return (await db.Queryable<SystemTenantEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("租户不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task EnsureUniqueCodeAsync(string tenantCode, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = tenantCode.Trim();
        var exists = await db.Queryable<SystemTenantEntity>()
            .Where(item => item.TenantCode == normalizedCode && item.Id != (currentId ?? string.Empty) && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("租户编码已存在");
        }
    }

    private static void EnsureRequest(TenantUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantCode) || string.IsNullOrWhiteSpace(request.TenantName))
        {
            throw new ValidationException("租户编码和名称不能为空");
        }
    }

    private static TenantListItemResponse Map(SystemTenantEntity entity)
    {
        return new TenantListItemResponse(
            entity.Id,
            entity.TenantCode,
            entity.TenantName,
            entity.ShortName,
            entity.Status,
            entity.ExpiredAt,
            entity.ContactName,
            entity.ContactPhone,
            entity.ConfigJson,
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

    private static List<string> NormalizeIds(IReadOnlyList<string> ids)
    {
        return ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ISugarQueryable<SystemTenantEntity> ApplyDefaultSort(ISugarQueryable<SystemTenantEntity> query) =>
        query.OrderBy(item => item.CreatedTime, OrderByType.Desc);
}
