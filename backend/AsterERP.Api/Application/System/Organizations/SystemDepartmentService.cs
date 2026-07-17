using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Organizations;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Domain.System.Organizations;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.System.Organizations;

public sealed class SystemDepartmentService(
    ICurrentUser currentUser,
    IWorkspaceDatabaseAccessor databaseAccessor,
    IAuthSessionService authSessionService,
    IUnitOfWork unitOfWork) : ISystemDepartmentService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemDepartmentEntity>, OrderByType, ISugarQueryable<SystemDepartmentEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemDepartmentEntity>, OrderByType, ISugarQueryable<SystemDepartmentEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["deptCode"] = (query, order) => query.OrderBy(item => item.DeptCode, order),
            ["deptName"] = (query, order) => query.OrderBy(item => item.DeptName, order),
            ["managerName"] = (query, order) => query.OrderBy(item => item.ManagerName, order),
            ["phoneNumber"] = (query, order) => query.OrderBy(item => item.PhoneNumber, order),
            ["sortOrder"] = (query, order) => query.OrderBy(item => item.SortOrder, order),
            ["status"] = (query, order) => query.OrderBy(item => item.Status, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemDepartmentEntity>, GridFilter, ISugarQueryable<SystemDepartmentEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemDepartmentEntity>, GridFilter, ISugarQueryable<SystemDepartmentEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["deptCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.DeptCode),
            ["deptName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.DeptName),
            ["managerName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ManagerName),
            ["parentId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ParentId),
            ["phoneNumber"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.PhoneNumber),
            ["sortOrder"] = (query, filter) => GridFilterApplier.ApplyInt32(query, filter, item => item.SortOrder),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Status),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    public async Task<GridPageResult<DepartmentListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        var keyword = gridQuery.Keyword?.Trim();
        var status = NormalizeOptional(gridQuery.Status);
        var parentId = NormalizeOptional(gridQuery.ParentId);
        var query = databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>().Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(item =>
                item.DeptCode.Contains(keyword) ||
                item.DeptName.Contains(keyword) ||
                (item.ManagerName != null && item.ManagerName.Contains(keyword)) ||
                (item.PhoneNumber != null && item.PhoneNumber.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(parentId))
        {
            if (gridQuery.IncludeDescendants)
            {
                var allDepartments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
                    .Where(item => !item.IsDeleted)
                    .ToListAsync(cancellationToken);
                var descendantIds = ResolveDepartmentIdsWithDescendants(allDepartments, [parentId]);
                query = descendantIds.Count == 0
                    ? query.Where(item => item.Id == "__none__")
                    : query.Where(item => descendantIds.Contains(item.Id));
            }
            else
            {
                query = query.Where(item => item.ParentId == parentId);
            }
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var totalCount = new RefAsync<int>();
        var departments = await GridSortApplier
            .Apply(query, gridQuery.Sorts, Sorters, ApplyDefaultSort)
            .ToPageListAsync(gridQuery.PageIndex, gridQuery.PageSize, totalCount);

        return new GridPageResult<DepartmentListItemResponse>
        {
            Total = totalCount.Value,
            Items = await MapListAsync(departments, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<DepartmentTreeNodeResponse>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var departments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        var leaderNamesByDepartmentId = await ResolveLeaderNamesByDepartmentIdAsync(departments, cancellationToken);
        return BuildTree(departments, leaderNamesByDepartmentId);
    }

    public async Task<DepartmentListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        return await MapAsync(await GetRequiredAsync(id, cancellationToken), cancellationToken);
    }

    public async Task<DepartmentListItemResponse> CreateAsync(DepartmentUpsertRequest request, CancellationToken cancellationToken = default)
    {
        DepartmentDomainPolicy.EnsureUpsertRequest(request.DeptCode, request.DeptName);
        await EnsureUniqueCodeAsync(request.DeptCode, null, cancellationToken);
        await EnsureParentExistsAsync(request.ParentId, null, cancellationToken);

        var entity = new SystemDepartmentEntity
        {
            DeptCode = request.DeptCode.Trim(),
            DeptName = request.DeptName.Trim(),
            ParentId = NormalizeOptional(request.ParentId),
            ManagerName = NormalizeOptional(request.ManagerName),
            LeaderUserIdsJson = SerializeLeaderUserIds(NormalizeLeaderUserIds(request.LeaderUserIds)),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            SortOrder = request.SortOrder,
            Status = DepartmentDomainPolicy.NormalizeStatus(request.Status),
            Remark = NormalizeOptional(request.Remark)
        };
        await ApplyLeaderSnapshotAsync(entity, request, cancellationToken);

        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<DepartmentListItemResponse> UpdateAsync(string id, DepartmentUpsertRequest request, CancellationToken cancellationToken = default)
    {
        DepartmentDomainPolicy.EnsureUpsertRequest(request.DeptCode, request.DeptName);
        var entity = await GetRequiredAsync(id, cancellationToken);
        await EnsureUniqueCodeAsync(request.DeptCode, id, cancellationToken);
        await EnsureParentExistsAsync(request.ParentId, id, cancellationToken);

        entity.DeptCode = request.DeptCode.Trim();
        entity.DeptName = request.DeptName.Trim();
        entity.ParentId = NormalizeOptional(request.ParentId);
        entity.ManagerName = NormalizeOptional(request.ManagerName);
        entity.LeaderUserIdsJson = SerializeLeaderUserIds(NormalizeLeaderUserIds(request.LeaderUserIds));
        entity.PhoneNumber = NormalizeOptional(request.PhoneNumber);
        entity.SortOrder = request.SortOrder;
        entity.Status = DepartmentDomainPolicy.NormalizeStatus(request.Status);
        entity.Remark = NormalizeOptional(request.Remark);
        await ApplyLeaderSnapshotAsync(entity, request, cancellationToken);

        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await CascadeDeleteAsync([id], cancellationToken);
    }

    public async Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        var normalizedIds = NormalizeIds(ids);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        await CascadeDeleteAsync(normalizedIds, cancellationToken);
    }

    public async Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default)
    {
        var normalizedIds = NormalizeIds(ids);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var entities = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => normalizedIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (entities.Count != normalizedIds.Count)
        {
            throw new NotFoundException("部门不存在", ErrorCodes.DepartmentNotFound);
        }

        var normalizedStatus = DepartmentDomainPolicy.NormalizeStatus(status);
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

    private async Task<List<DepartmentListItemResponse>> MapListAsync(
        IReadOnlyList<SystemDepartmentEntity> departments,
        CancellationToken cancellationToken)
    {
        var allDepartments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>().Where(item => !item.IsDeleted).ToListAsync(cancellationToken);
        var namesById = allDepartments.ToDictionary(item => item.Id, item => item.DeptName);
        var deptIds = departments.Select(item => item.Id).ToList();
        var userCounts = deptIds.Count == 0
            ? new Dictionary<string, int>()
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
                .Where(item => deptIds.Contains(item.DeptId) && !item.IsDeleted && item.Status == "Enabled")
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.DeptId)
                .ToDictionary(group => group.Key, group => group.Select(item => item.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var positionCounts = deptIds.Count == 0
            ? new Dictionary<string, int>()
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
                .Where(item => deptIds.Contains(item.DeptId) && !item.IsDeleted)
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.DeptId)
                .ToDictionary(group => group.Key, group => group.Count());
        var leaderNamesByDepartmentId = await ResolveLeaderNamesByDepartmentIdAsync(departments, cancellationToken);

        return departments
            .Select(item => Map(
                item,
                item.ParentId is not null && namesById.TryGetValue(item.ParentId, out var parentName) ? parentName : null,
                userCounts.TryGetValue(item.Id, out var userCount) ? userCount : 0,
                positionCounts.TryGetValue(item.Id, out var positionCount) ? positionCount : 0,
                leaderNamesByDepartmentId.TryGetValue(item.Id, out var leaderNames) ? leaderNames : []))
            .ToList();
    }

    private async Task<DepartmentListItemResponse> MapAsync(SystemDepartmentEntity entity, CancellationToken cancellationToken)
    {
        var parentName = string.IsNullOrWhiteSpace(entity.ParentId)
            ? null
            : (await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
                .Where(item => item.Id == entity.ParentId && !item.IsDeleted)
                .Select(item => item.DeptName)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault();

        var userCount = (await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
            .Where(item => item.DeptId == entity.Id && !item.IsDeleted && item.Status == "Enabled")
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var positionCount = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>().Where(item => item.DeptId == entity.Id && !item.IsDeleted).CountAsync(cancellationToken);
        var leaderNames = await ResolveLeaderNamesAsync(DeserializeLeaderUserIds(entity.LeaderUserIdsJson), cancellationToken);
        return Map(entity, parentName, userCount, positionCount, leaderNames);
    }

    private static DepartmentListItemResponse Map(
        SystemDepartmentEntity entity,
        string? parentName,
        int userCount,
        int positionCount,
        IReadOnlyList<string> leaderNames)
    {
        var leaderUserIds = DeserializeLeaderUserIds(entity.LeaderUserIdsJson);
        return new DepartmentListItemResponse(
            entity.Id,
            entity.DeptCode,
            entity.DeptName,
            entity.ParentId,
            parentName,
            entity.ManagerName ?? string.Join("、", leaderNames),
            leaderUserIds,
            leaderNames,
            entity.PhoneNumber,
            entity.SortOrder,
            entity.Status,
            userCount,
            positionCount,
            entity.Remark);
    }

    private static IReadOnlyList<DepartmentTreeNodeResponse> BuildTree(
        IReadOnlyList<SystemDepartmentEntity> departments,
        IReadOnlyDictionary<string, IReadOnlyList<string>> leaderNamesByDepartmentId)
    {
        var builders = departments.ToDictionary(
            item => item.Id,
            item => new DepartmentNodeBuilder(new DepartmentTreeNodeResponse(
                item.Id,
                item.DeptCode,
                item.DeptName,
                item.ParentId,
                DeserializeLeaderUserIds(item.LeaderUserIdsJson),
                leaderNamesByDepartmentId.TryGetValue(item.Id, out var leaderNames) ? leaderNames : [],
                item.SortOrder,
                item.Status,
                [])));

        foreach (var department in departments)
        {
            if (!string.IsNullOrWhiteSpace(department.ParentId) &&
                builders.TryGetValue(department.ParentId, out var parent) &&
                builders.TryGetValue(department.Id, out var child))
            {
                parent.Children.Add(child);
            }
        }

        return departments
            .Where(item => string.IsNullOrWhiteSpace(item.ParentId) || !builders.ContainsKey(item.ParentId))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DeptName)
            .Select(item => builders[item.Id].ToResponse())
            .ToList();
    }

    private async Task<SystemDepartmentEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        return entity ?? throw new NotFoundException("部门不存在", ErrorCodes.DepartmentNotFound);
    }

    private async Task EnsureUniqueCodeAsync(string deptCode, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = deptCode.Trim();
        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => item.DeptCode == normalizedCode && item.Id != (currentId ?? string.Empty) && !item.IsDeleted)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new ValidationException("部门编码已存在", ErrorCodes.DuplicateDepartmentCode);
        }
    }

    private async Task EnsureParentExistsAsync(string? parentId, string? currentId, CancellationToken cancellationToken)
    {
        var normalizedParentId = NormalizeOptional(parentId);
        if (normalizedParentId is null)
        {
            return;
        }

        DepartmentDomainPolicy.EnsureNotSelfParent(normalizedParentId, currentId);

        var exists = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => item.Id == normalizedParentId && !item.IsDeleted)
            .AnyAsync(cancellationToken);

        if (!exists)
        {
            throw new ValidationException("上级部门不存在");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task ApplyLeaderSnapshotAsync(SystemDepartmentEntity entity, DepartmentUpsertRequest request, CancellationToken cancellationToken)
    {
        var leaderUserIds = NormalizeLeaderUserIds(request.LeaderUserIds);
        if (leaderUserIds.Count > 3)
        {
            throw new ValidationException("部门领导最多只能设置三位", ErrorCodes.ParameterInvalid);
        }

        if (leaderUserIds.Count == 0)
        {
            entity.LeaderUserIdsJson = null;
            return;
        }

        var leaders = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => leaderUserIds.Contains(item.Id) && !item.IsDeleted && item.Status == "Enabled")
            .ToListAsync(cancellationToken);
        if (leaders.Count != leaderUserIds.Count)
        {
            throw new ValidationException("部门领导必须选择有效且启用的用户", ErrorCodes.UserNotFound);
        }

        var leaderEmploymentUserIds = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
            .Where(item =>
                leaderUserIds.Contains(item.UserId) &&
                item.DeptId == entity.Id &&
                !item.IsDeleted &&
                item.Status == "Enabled")
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        var missingEmploymentUserIds = leaderUserIds
            .Where(id => !leaderEmploymentUserIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missingEmploymentUserIds.Count > 0)
        {
            var missingNames = leaders
                .Where(item => missingEmploymentUserIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase))
                .Select(item => item.DisplayName ?? item.UserName)
                .ToList();
            throw new ValidationException($"部门负责人必须在本部门有有效任职: {string.Join("、", missingNames)}", ErrorCodes.ParameterInvalid);
        }

        entity.LeaderUserIdsJson = SerializeLeaderUserIds(leaderUserIds);
        entity.ManagerName = string.Join("、", leaderUserIds
            .Select(id => leaders.First(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.DisplayName ?? item.UserName));
    }

    private async Task<Dictionary<string, IReadOnlyList<string>>> ResolveLeaderNamesByDepartmentIdAsync(
        IReadOnlyList<SystemDepartmentEntity> departments,
        CancellationToken cancellationToken)
    {
        var leaderIdsByDepartmentId = departments.ToDictionary(item => item.Id, item => DeserializeLeaderUserIds(item.LeaderUserIdsJson));
        var allLeaderIds = leaderIdsByDepartmentId.Values
            .SelectMany(item => item)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (allLeaderIds.Count == 0)
        {
            return [];
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => allLeaderIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var namesByUserId = users.ToDictionary(item => item.Id, item => item.DisplayName ?? item.UserName, StringComparer.OrdinalIgnoreCase);
        return leaderIdsByDepartmentId.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value
                .Select(id => namesByUserId.TryGetValue(id, out var name) ? name : id)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<string>> ResolveLeaderNamesAsync(IReadOnlyList<string> leaderUserIds, CancellationToken cancellationToken)
    {
        if (leaderUserIds.Count == 0)
        {
            return [];
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => leaderUserIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var namesByUserId = users.ToDictionary(item => item.Id, item => item.DisplayName ?? item.UserName, StringComparer.OrdinalIgnoreCase);
        return leaderUserIds
            .Select(id => namesByUserId.TryGetValue(id, out var name) ? name : id)
            .ToList();
    }

    private static List<string> NormalizeLeaderUserIds(IReadOnlyList<string>? leaderUserIds)
    {
        return (leaderUserIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? SerializeLeaderUserIds(IReadOnlyList<string> leaderUserIds)
    {
        return leaderUserIds.Count == 0 ? null : JsonSerializer.Serialize(leaderUserIds);
    }

    private static IReadOnlyList<string> DeserializeLeaderUserIds(string? leaderUserIdsJson)
    {
        if (string.IsNullOrWhiteSpace(leaderUserIdsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(leaderUserIdsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> NormalizeIds(IReadOnlyList<string> ids)
    {
        return ids.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task CascadeDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var normalizedIds = NormalizeIds(ids);
        var allDepartments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var requestedDepartments = allDepartments
            .Where(item => normalizedIds.Contains(item.Id))
            .ToList();

        if (requestedDepartments.Count != normalizedIds.Count)
        {
            throw new NotFoundException("部门不存在", ErrorCodes.DepartmentNotFound);
        }

        var departmentIds = ResolveDepartmentIdsWithDescendants(allDepartments, normalizedIds);
        var departmentsToDelete = allDepartments
            .Where(item => departmentIds.Contains(item.Id))
            .ToList();
        var positionsToDelete = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
            .Where(item => departmentIds.Contains(item.DeptId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var positionIds = positionsToDelete.Select(item => item.Id).ToList();
        var employmentsToDelete = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
            .Where(item => !item.IsDeleted && (departmentIds.Contains(item.DeptId) || positionIds.Contains(item.PositionId)))
            .ToListAsync(cancellationToken);
        var affectedUserIds = employmentsToDelete
            .Select(item => item.UserId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var legacyDepartmentUsers = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && departmentIds.Contains(item.DeptId!))
            .ToListAsync(cancellationToken);
        affectedUserIds.AddRange(legacyDepartmentUsers.Select(item => item.Id));
        affectedUserIds = affectedUserIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var affectedUsers = affectedUserIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
                .Where(item => affectedUserIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var outsideEmployments = affectedUserIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity>()
                .Where(item =>
                    affectedUserIds.Contains(item.UserId) &&
                    !item.IsDeleted &&
                    item.Status == "Enabled" &&
                    !departmentIds.Contains(item.DeptId) &&
                    !positionIds.Contains(item.PositionId))
                .OrderBy(item => item.IsPrimary, OrderByType.Desc)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(cancellationToken);
        var outsideByUser = outsideEmployments
            .GroupBy(item => item.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var usersToDelete = affectedUsers
            .Where(item => !outsideByUser.ContainsKey(item.Id))
            .ToList();
        var usersToKeep = affectedUsers
            .Where(item => outsideByUser.ContainsKey(item.Id))
            .ToList();
        var userIds = usersToDelete.Select(item => item.Id).ToList();

        if (userIds.Contains(currentUser.GetAsterErpUserId(), StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("不能删除当前登录用户", ErrorCodes.StateChangeNotAllowed);
        }

        var userRoleMappings = userIds.Count == 0
            ? []
            : await databaseAccessor.GetCurrentDb().Queryable<SystemUserRoleEntity>()
                .Where(item => userIds.Contains(item.UserId) && !item.IsDeleted)
                .ToListAsync(cancellationToken);

        var deletedTime = DateTime.UtcNow;
        foreach (var department in departmentsToDelete)
        {
            department.IsDeleted = true;
            department.DeletedTime = deletedTime;
            department.UpdatedTime = deletedTime;
        }

        foreach (var position in positionsToDelete)
        {
            position.IsDeleted = true;
            position.DeletedTime = deletedTime;
            position.UpdatedTime = deletedTime;
        }

        foreach (var user in usersToDelete)
        {
            user.IsDeleted = true;
            user.DeletedTime = deletedTime;
            user.UpdatedTime = deletedTime;
        }

        foreach (var user in usersToKeep)
        {
            var nextEmployment = outsideByUser[user.Id]
                .FirstOrDefault(item => item.IsPrimary)
                ?? outsideByUser[user.Id].First();
            user.DeptId = nextEmployment.DeptId;
            user.PositionId = nextEmployment.PositionId;
            user.UpdatedTime = deletedTime;
        }

        foreach (var employment in employmentsToDelete)
        {
            employment.IsDeleted = true;
            employment.DeletedTime = deletedTime;
            employment.UpdatedTime = deletedTime;
        }

        foreach (var mapping in userRoleMappings)
        {
            mapping.IsDeleted = true;
            mapping.DeletedTime = deletedTime;
            mapping.UpdatedTime = deletedTime;
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            if (userIds.Count > 0)
            {
                await authSessionService.RevokeSessionsByUserIdsAsync(userIds, cancellationToken);
            }

            if (departmentsToDelete.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(departmentsToDelete)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (positionsToDelete.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(positionsToDelete)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (usersToDelete.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(usersToDelete)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (usersToKeep.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(usersToKeep)
                    .UpdateColumns(item => new { item.DeptId, item.PositionId, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (userRoleMappings.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(userRoleMappings)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (employmentsToDelete.Count > 0)
            {
                await databaseAccessor.GetCurrentDb().Updateable(employmentsToDelete)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedTime, item.UpdatedTime })
                    .ExecuteCommandAsync(cancellationToken);
            }
        }, cancellationToken);
    }

    private static List<string> ResolveDepartmentIdsWithDescendants(
        IReadOnlyList<SystemDepartmentEntity> departments,
        IReadOnlyList<string> rootIds)
    {
        var normalizedRootIds = rootIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var resolved = new HashSet<string>(normalizedRootIds, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(normalizedRootIds);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            foreach (var childId in departments
                         .Where(item => string.Equals(item.ParentId, currentId, StringComparison.OrdinalIgnoreCase))
                         .Select(item => item.Id))
            {
                if (resolved.Add(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        return resolved.ToList();
    }

    private sealed class DepartmentNodeBuilder(DepartmentTreeNodeResponse value)
    {
        public DepartmentTreeNodeResponse Value { get; } = value;

        public List<DepartmentNodeBuilder> Children { get; } = [];

        public DepartmentTreeNodeResponse ToResponse()
        {
            return Value with
            {
                Children = Children
                    .OrderBy(item => item.Value.SortOrder)
                    .ThenBy(item => item.Value.DeptName)
                    .Select(item => item.ToResponse())
                    .ToList()
            };
        }
    }

    private static ISugarQueryable<SystemDepartmentEntity> ApplyDefaultSort(ISugarQueryable<SystemDepartmentEntity> query) =>
        query.OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc);
}
