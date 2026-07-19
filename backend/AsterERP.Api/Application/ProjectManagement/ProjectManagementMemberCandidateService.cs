using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementMemberCandidateService
{
    Task<GridPageResult<ProjectManagementMemberCandidateResponse>> QueryAsync(
        ProjectManagementMemberCandidateQuery query,
        CancellationToken cancellationToken = default);
    Task<bool> IsSelectableAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> IsSelectableAsync(string userId, string? employmentId, CancellationToken cancellationToken = default);
}

public sealed class ProjectManagementMemberCandidateService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementMemberCandidateService
{
    public async Task<bool> IsSelectableAsync(string userId, CancellationToken cancellationToken = default)
        => await IsSelectableAsync(userId, null, cancellationToken);

    public async Task<bool> IsSelectableAsync(string userId, string? employmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetCurrentDb();
        var userExists = await db.Queryable<SystemUserEntity>()
            .Where(user => user.Id == userId && !user.IsDeleted && user.Status == "Enabled")
            .AnyAsync(cancellationToken);
        if (!userExists) return false;
        if (string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            return await databaseAccessor.MainDb.Queryable<SystemUserTenantMembershipEntity>()
                .Where(item => item.UserId == userId && item.TenantId == tenantId && !item.IsDeleted && item.Status == "Enabled" &&
                    (string.IsNullOrWhiteSpace(employmentId) || item.Id == employmentId))
                .AnyAsync(cancellationToken);
        }
        return await db.Queryable<SystemUserEmploymentEntity>()
            .Where(item => item.UserId == userId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && item.Status == "Enabled" &&
                (string.IsNullOrWhiteSpace(employmentId) || item.Id == employmentId))
            .AnyAsync(cancellationToken);
    }

    public async Task<GridPageResult<ProjectManagementMemberCandidateResponse>> QueryAsync(
        ProjectManagementMemberCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var db = databaseAccessor.GetCurrentDb();
        var normalizedKeyword = Normalize(query.Keyword);
        var normalizedDeptId = Normalize(query.DeptId);
        var normalizedPositionId = Normalize(query.PositionId);

        var userQuery = db.Queryable<SystemUserEntity>()
            .Where(user => !user.IsDeleted && user.Status == "Enabled");

        if (string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            userQuery = userQuery.Where(user =>
                SqlFunc.Subqueryable<SystemUserTenantMembershipEntity>()
                    .Where(membership =>
                        !membership.IsDeleted &&
                        membership.TenantId == tenantId &&
                        membership.Status == "Enabled" &&
                        membership.DeptId != null &&
                        membership.PositionId != null &&
                        membership.UserId == user.Id &&
                        (string.IsNullOrWhiteSpace(normalizedDeptId) || membership.DeptId == normalizedDeptId) &&
                        (string.IsNullOrWhiteSpace(normalizedPositionId) || membership.PositionId == normalizedPositionId))
                    .Any());
        }
        else
        {
            userQuery = userQuery.Where(user =>
                SqlFunc.Subqueryable<SystemUserEmploymentEntity>()
                    .Where(employment =>
                        !employment.IsDeleted &&
                        employment.Status == "Enabled" &&
                        employment.TenantId == tenantId &&
                        employment.AppCode == appCode &&
                        employment.UserId == user.Id &&
                        (string.IsNullOrWhiteSpace(normalizedDeptId) || employment.DeptId == normalizedDeptId) &&
                        (string.IsNullOrWhiteSpace(normalizedPositionId) || employment.PositionId == normalizedPositionId))
                    .Any());
        }

        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            userQuery = userQuery.Where(user =>
                user.UserName.Contains(normalizedKeyword) ||
                user.DisplayName.Contains(normalizedKeyword) ||
                (user.Email != null && user.Email.Contains(normalizedKeyword)) ||
                (user.PhoneNumber != null && user.PhoneNumber.Contains(normalizedKeyword)));
        }

        var totalCount = new RefAsync<int>();
        var users = await userQuery
            .OrderBy(user => user.DisplayName, OrderByType.Asc)
            .OrderBy(user => user.UserName, OrderByType.Asc)
            .ToPageListAsync(
                Math.Max(query.PageIndex, 1),
                Math.Clamp(query.PageSize, 1, 100),
                totalCount,
                cancellationToken);

        var userIds = users.Select(user => user.Id).ToList();
        if (userIds.Count == 0)
        {
            return new GridPageResult<ProjectManagementMemberCandidateResponse>
            {
                Total = totalCount.Value,
                Items = []
            };
        }

        var employments = await LoadEmploymentsAsync(db, appCode, tenantId, userIds, cancellationToken);
        var departmentIds = employments.Select(item => item.DeptId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var positionIds = employments.Select(item => item.PositionId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var departments = departmentIds.Count == 0
            ? []
            : await db.Queryable<SystemDepartmentEntity>()
                .Where(department => departmentIds.Contains(department.Id) && !department.IsDeleted)
                .ToListAsync(cancellationToken);
        var positions = positionIds.Count == 0
            ? []
            : await db.Queryable<SystemPositionEntity>()
                .Where(position => positionIds.Contains(position.Id) && !position.IsDeleted)
                .ToListAsync(cancellationToken);
        var departmentNames = departments.ToDictionary(item => item.Id, item => item.DeptName, StringComparer.OrdinalIgnoreCase);
        var positionNames = positions.ToDictionary(item => item.Id, item => item.PositionName, StringComparer.OrdinalIgnoreCase);

        var items = users.Select(user =>
        {
            var employment = employments
                .Where(item => item.UserId == user.Id)
                .OrderByDescending(item => item.IsPrimary)
                .ThenBy(item => item.SortOrder)
                .First();
            departmentNames.TryGetValue(employment.DeptId, out var deptName);
            positionNames.TryGetValue(employment.PositionId, out var positionName);
            return new ProjectManagementMemberCandidateResponse(
                user.Id,
                user.UserName,
                user.DisplayName,
                employment.DeptId,
                deptName,
                employment.PositionId,
                positionName,
                employment.Id,
                employment.EmploymentName,
                user.Status,
                true);
        }).ToList();

        return new GridPageResult<ProjectManagementMemberCandidateResponse>
        {
            Total = totalCount.Value,
            Items = items
        };
    }

    private async Task<IReadOnlyList<MemberEmploymentRow>> LoadEmploymentsAsync(
        ISqlSugarClient db,
        string appCode,
        string tenantId,
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken)
    {
        if (string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            var memberships = await databaseAccessor.MainDb.Queryable<SystemUserTenantMembershipEntity>()
                .Where(membership =>
                    userIds.Contains(membership.UserId) &&
                    !membership.IsDeleted &&
                    membership.TenantId == tenantId &&
                    membership.Status == "Enabled")
                .ToListAsync(cancellationToken);

            return memberships
                .Where(item => !string.IsNullOrWhiteSpace(item.DeptId) && !string.IsNullOrWhiteSpace(item.PositionId))
                .Select(item => new MemberEmploymentRow(
                    item.Id,
                    item.UserId,
                    item.DeptId!,
                    item.PositionId!,
                    $"{item.DeptId}/{item.PositionId}",
                    item.IsDefault,
                    item.Status,
                    0))
                .ToList();
        }

        var query = db.Queryable<SystemUserEmploymentEntity>()
            .Where(employment =>
                userIds.Contains(employment.UserId) &&
                !employment.IsDeleted &&
                employment.Status == "Enabled");

        if (!string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(employment => employment.TenantId == tenantId && employment.AppCode == appCode);
        }

        var rows = await query
            .OrderByDescending(employment => employment.IsPrimary)
            .OrderBy(employment => employment.SortOrder, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => new MemberEmploymentRow(
                item.Id,
                item.UserId,
                item.DeptId,
                item.PositionId,
                item.EmploymentName,
                item.IsPrimary,
                item.Status,
                item.SortOrder))
            .ToList();
    }

    private string RequireTenantId() =>
        currentUser.GetAsterErpTenantId() ?? throw new InvalidOperationException("当前会话缺少租户上下文");

    private string RequireAppCode() =>
        currentUser.GetAsterErpAppCode() ?? throw new InvalidOperationException("当前会话缺少应用上下文");

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record MemberEmploymentRow(
        string Id,
        string UserId,
        string DeptId,
        string PositionId,
        string EmploymentName,
        bool IsPrimary,
        string Status,
        int SortOrder);
}
