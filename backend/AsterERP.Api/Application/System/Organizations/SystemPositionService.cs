using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Organizations;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Domain.System.Organizations;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.System.Organizations;

public sealed class SystemPositionService(IWorkspaceDatabaseAccessor databaseAccessor, IUnitOfWork unitOfWork) : ISystemPositionService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemPositionEntity>, OrderByType, ISugarQueryable<SystemPositionEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemPositionEntity>, OrderByType, ISugarQueryable<SystemPositionEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["positionCode"] = (query, order) => query.OrderBy(item => item.PositionCode, order),
            ["positionLevel"] = (query, order) => query.OrderBy(item => item.PositionLevel, order),
            ["positionName"] = (query, order) => query.OrderBy(item => item.PositionName, order),
            ["sortOrder"] = (query, order) => query.OrderBy(item => item.SortOrder, order),
            ["status"] = (query, order) => query.OrderBy(item => item.Status, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemPositionEntity>, GridFilter, ISugarQueryable<SystemPositionEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemPositionEntity>, GridFilter, ISugarQueryable<SystemPositionEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["deptId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.DeptId),
            ["positionCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.PositionCode),
            ["positionLevel"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.PositionLevel),
            ["positionName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.PositionName),
            ["sortOrder"] = (query, filter) => GridFilterApplier.ApplyInt32(query, filter, item => item.SortOrder),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Status),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    public async Task<GridPageResult<PositionListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        var keyword = gridQuery.Keyword?.Trim();
        var status = NormalizeOptional(gridQuery.Status);
        var deptId = NormalizeOptional(gridQuery.DeptId);
        var query = databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>().Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.PositionCode.Contains(keyword) ||
                item.PositionName.Contains(keyword) ||
                (item.PositionLevel != null && item.PositionLevel.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(deptId))
        {
            query = query.Where(item => item.DeptId == deptId);
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var totalCount = new RefAsync<int>();
        var positions = await GridSortApplier
            .Apply(query, gridQuery.Sorts, Sorters, ApplyDefaultSort)
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, totalCount);

        return new GridPageResult<PositionListItemResponse>
        {
            Total = totalCount.Value,
            Items = await MapListAsync(positions, cancellationToken)
        };
    }

    public async Task<PositionListItemResponse> CreateAsync(PositionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        PositionDomainPolicy.EnsureUpsertRequest(request.PositionCode, request.PositionName, request.DeptId);
        await EnsureDepartmentExistsAsync(request.DeptId, cancellationToken);
        await EnsureUniqueCodeAsync(request.PositionCode, null, cancellationToken);

        var entity = new SystemPositionEntity
        {
            PositionCode = request.PositionCode.Trim(),
            PositionName = request.PositionName.Trim(),
            DeptId = request.DeptId.Trim(),
            PositionLevel = NormalizeOptional(request.PositionLevel),
            SortOrder = request.SortOrder,
            Status = PositionDomainPolicy.NormalizeStatus(request.Status),
            Remark = NormalizeOptional(request.Remark)
        };

        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<PositionListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        return await MapAsync(await GetRequiredAsync(id, cancellationToken), cancellationToken);
    }

    public async Task<PositionListItemResponse> UpdateAsync(string id, PositionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        PositionDomainPolicy.EnsureUpsertRequest(request.PositionCode, request.PositionName, request.DeptId);
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureDepartmentExistsAsync(request.DeptId, cancellationToken);
        await EnsureUniqueCodeAsync(request.PositionCode, id, cancellationToken);

        entity.PositionCode = request.PositionCode.Trim();
        entity.PositionName = request.PositionName.Trim();
        entity.DeptId = request.DeptId.Trim();
        entity.PositionLevel = NormalizeOptional(request.PositionLevel);
        entity.SortOrder = request.SortOrder;
        entity.Status = PositionDomainPolicy.NormalizeStatus(request.Status);
        entity.Remark = NormalizeOptional(request.Remark);

        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await DeletePositionsAsync([id], cancellationToken);
    }

    public async Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        var normalizedIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return;
        }

        await DeletePositionsAsync(normalizedIds, cancellationToken);
    }

    public async Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default)
    {
        var normalizedIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return;
        }

        var entities = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (entities.Count != normalizedIds.Count)
        {
            throw new NotFoundException("岗位不存在", ErrorCodes.PositionNotFound);
        }

        var normalizedStatus = PositionDomainPolicy.NormalizeStatus(status);
        var updatedTime = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            entity.Status = normalizedStatus;
            entity.UpdatedTime = updatedTime;
        }

        await databaseAccessor.GetCurrentDb().Updateable(entities)
            .UpdateColumns(item => new { item.Status, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<List<PositionListItemResponse>> MapListAsync(
        IReadOnlyList<SystemPositionEntity> positions,
        CancellationToken cancellationToken)
    {
        var deptIds = positions.Select(item => item.DeptId).Distinct().ToList();
        var departments = deptIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
                .Where(item => deptIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var deptNames = departments.ToDictionary(item => item.Id, item => item.DeptName);
        var positionIds = positions.Select(item => item.Id).ToList();
        var userCounts = positionIds.Count == 0
            ? new Dictionary<string, int>()
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
                .Where(item => positionIds.Contains(item.PositionId) && !item.IsDeleted && item.Status == "Enabled")
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.PositionId)
                .ToDictionary(group => group.Key, group => group.Select(item => item.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        return positions
            .Select(item => Map(
                item,
                deptNames.TryGetValue(item.DeptId, out var deptName) ? deptName : "-",
                userCounts.TryGetValue(item.Id, out var userCount) ? userCount : 0))
            .ToList();
    }

    private async Task<PositionListItemResponse> MapAsync(SystemPositionEntity entity, CancellationToken cancellationToken)
    {
        var deptName = (await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => item.Id == entity.DeptId && !item.IsDeleted)
            .Select(item => item.DeptName)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault() ?? "-";
        var userCount = (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
            .Where(item => item.PositionId == entity.Id && !item.IsDeleted && item.Status == "Enabled")
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        return Map(entity, deptName, userCount);
    }

    private static PositionListItemResponse Map(SystemPositionEntity entity, string deptName, int userCount)
    {
        return new PositionListItemResponse(
            entity.Id,
            entity.PositionCode,
            entity.PositionName,
            entity.DeptId,
            deptName,
            entity.PositionLevel,
            entity.SortOrder,
            entity.Status,
            userCount,
            entity.Remark);
    }

    private async Task<SystemPositionEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity ?? throw new NotFoundException("岗位不存在", ErrorCodes.PositionNotFound);
    }

    private async Task EnsureDepartmentExistsAsync(string deptId, CancellationToken cancellationToken)
    {
        PositionDomainPolicy.EnsureDepartmentRequired(deptId);

        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => item.Id == deptId.Trim() && !item.IsDeleted && item.Status == "Enabled")
            .AnyAsync(cancellationToken);

        if (!exists)
        {
            throw new ValidationException("所属部门不存在或已停用");
        }
    }

    private async Task EnsureUniqueCodeAsync(string positionCode, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = positionCode.Trim();
        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
            .Where(item => item.PositionCode == normalizedCode && item.Id != (currentId ?? string.Empty) && !item.IsDeleted)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new ValidationException("岗位编码已存在", ErrorCodes.DuplicatePositionCode);
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task DeletePositionsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var normalizedIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return;
        }

        var entities = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (entities.Count != normalizedIds.Count)
        {
            throw new NotFoundException("岗位不存在", ErrorCodes.PositionNotFound);
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => normalizedIds.Contains(item.PositionId!) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var employments = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
            .Where(item => normalizedIds.Contains(item.PositionId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var deletedTime = DateTime.UtcNow;

        foreach (var user in users)
        {
            user.PositionId = null;
            user.UpdatedTime = deletedTime;
        }

        foreach (var entity in entities)
        {
            entity.IsDeleted = true;
            entity.DeletedTime = deletedTime;
            entity.UpdatedTime = deletedTime;
        }

        foreach (var employment in employments)
        {
            employment.IsDeleted = true;
            employment.DeletedTime = deletedTime;
            employment.UpdatedTime = deletedTime;
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            if (users.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(users)
                    .UpdateColumns(item => new { item.PositionId, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

            await databaseAccessor.GetCurrentDb().Updateable(entities)
                .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);

            if (employments.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(employments)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }
        }, cancellationToken);
    }

    private static ISugarQueryable<SystemPositionEntity> ApplyDefaultSort(ISugarQueryable<SystemPositionEntity> query) =>
        query.OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc);
}

