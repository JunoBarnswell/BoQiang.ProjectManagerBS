using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 任务参与人读取模型。任务列表或批量操作可一次传入多个任务，避免逐任务加载参与人。
/// </summary>
public sealed class ProjectManagementTaskParticipantProjection(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser)
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ProjectManagementTaskParticipantResponse>>> LoadByTaskIdsAsync(
        IReadOnlyCollection<string> taskIds,
        bool includeHistorical,
        CancellationToken cancellationToken = default)
    {
        var normalizedTaskIds = taskIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedTaskIds.Count == 0)
            return new Dictionary<string, IReadOnlyList<ProjectManagementTaskParticipantResponse>>(StringComparer.Ordinal);

        var db = databaseAccessor.GetCurrentDb();
        var participantQuery = db.Queryable<ProjectManagementTaskParticipantEntity>()
            .Where(item => normalizedTaskIds.Contains(item.TaskId));
        if (!includeHistorical)
            participantQuery = participantQuery.Where(item => !item.IsDeleted);

        var participants = await participantQuery
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var activeMemberKeys = await LoadActiveMemberKeysAsync(
            participants.Select(item => item.ProjectId).Distinct(StringComparer.Ordinal).ToList(),
            participants.Select(item => item.UserId).Distinct(StringComparer.Ordinal).ToList(),
            cancellationToken);

        return participants
            .GroupBy(item => item.TaskId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectManagementTaskParticipantResponse>)group
                    .Select(item => Map(item, activeMemberKeys.Contains(MemberKey(item.ProjectId, item.UserId))))
                    .ToList(),
                StringComparer.Ordinal);
    }

    public async Task<GridPageResult<ProjectManagementTaskParticipantCandidateResponse>> QueryCandidatesAsync(
        string projectId,
        ProjectManagementTaskParticipantCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var normalizedProjectId = Required(projectId, "项目不能为空");
        var keyword = NormalizeOptional(query.Keyword);
        var db = databaseAccessor.MainDb;
        var candidateMembers = db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(member => member.ProjectId == normalizedProjectId && member.TenantId == tenantId && member.AppCode == ProjectManagementPlatformScope.AppCode && member.IsActive && !member.IsDeleted)
            .Where(member => SqlFunc.Subqueryable<SystemUserEntity>()
                .Where(user => user.Id == member.UserId && user.Status == "Enabled" && !user.IsDeleted &&
                    SqlFunc.Subqueryable<SystemUserTenantMembershipEntity>()
                        .Where(membership => membership.UserId == user.Id && membership.TenantId == tenantId && membership.Status == "Enabled" && !membership.IsDeleted)
                        .Any())
                .Any());
        if (keyword is not null)
        {
            candidateMembers = candidateMembers.Where(member => SqlFunc.Subqueryable<SystemUserEntity>()
                .Where(user => user.Id == member.UserId &&
                    (user.UserName.Contains(keyword) || user.DisplayName.Contains(keyword) || (user.Email != null && user.Email.Contains(keyword))))
                .Any());
        }

        var total = new RefAsync<int>();
        var members = await candidateMembers
            .OrderBy(item => item.JoinedAt, OrderByType.Asc)
            .ToPageListAsync(Math.Max(1, query.PageIndex), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);
        var userIds = members.Select(item => item.UserId).Distinct(StringComparer.Ordinal).ToList();
        var users = userIds.Count == 0
            ? []
            : await db.Queryable<SystemUserEntity>()
                .Where(user => userIds.Contains(user.Id) && user.Status == "Enabled" && !user.IsDeleted)
                .ToListAsync(cancellationToken);
        var usersById = users.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return new GridPageResult<ProjectManagementTaskParticipantCandidateResponse>
        {
            Total = total.Value,
            Items = members
                .Where(member => usersById.ContainsKey(member.UserId))
                .Select(member =>
                {
                    var user = usersById[member.UserId];
                    return new ProjectManagementTaskParticipantCandidateResponse(
                        member.UserId,
                        member.EmploymentId,
                        member.RoleCode,
                        member.ScopeRootTaskId,
                        user.UserName,
                        user.DisplayName);
                })
                .ToList()
        };
    }

    /// <summary>
    /// 批量参与人变更使用的身份有效性集合，按用户集合一次读取，避免逐候选调用。
    /// </summary>
    public async Task<IReadOnlySet<string>> LoadSelectableUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedUserIds = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (normalizedUserIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        var tenantId = RequireTenantId();
        var db = databaseAccessor.MainDb;
        var users = await db.Queryable<SystemUserEntity>()
            .Where(user => normalizedUserIds.Contains(user.Id) && user.Status == "Enabled" && !user.IsDeleted &&
                SqlFunc.Subqueryable<SystemUserTenantMembershipEntity>()
                    .Where(membership => membership.UserId == user.Id && membership.TenantId == tenantId && membership.Status == "Enabled" && !membership.IsDeleted)
                    .Any())
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        return users.ToHashSet(StringComparer.Ordinal);
    }

    private async Task<HashSet<string>> LoadActiveMemberKeysAsync(
        IReadOnlyCollection<string> projectIds,
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0 || userIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        var members = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => projectIds.Contains(item.ProjectId) && userIds.Contains(item.UserId) && item.IsActive && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        return members.Select(item => MemberKey(item.ProjectId, item.UserId)).ToHashSet(StringComparer.Ordinal);
    }

    private static ProjectManagementTaskParticipantResponse Map(ProjectManagementTaskParticipantEntity entity, bool isProjectMemberActive) =>
        new(entity.Id, entity.TaskId, entity.UserId, entity.EmploymentId, entity.RoleCode, entity.VersionNo, !entity.IsDeleted, isProjectMemberActive);

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string MemberKey(string projectId, string userId) => $"{projectId}\u001f{userId}";
}
