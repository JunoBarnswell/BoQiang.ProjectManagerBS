using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Im;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Auth;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Im;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Im;

public sealed class ImUserDirectoryService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IImAccountBindingService accountBindingService,
    IImPresenceService presenceService) : IImUserDirectoryService
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(10);

    public async Task<ImDirectoryResponse> GetDirectoryAsync(string? keyword, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var currentUserId = RequireUserId();
        var normalizedKeyword = NormalizeOptional(keyword);
        var userRows = string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase)
            ? await LoadMainDirectoryUsersAsync(tenantId, currentUserId, cancellationToken)
            : await LoadApplicationDirectoryUsersAsync(currentUserId, cancellationToken);
        var filteredUsers = FilterUsers(userRows, normalizedKeyword);
        var departments = string.Equals(appCode, "SYSTEM", StringComparison.OrdinalIgnoreCase)
            ? await LoadMainDepartmentsAsync(filteredUsers, cancellationToken)
            : await LoadApplicationDepartmentsAsync(cancellationToken);
        departments = EnsureMissingDepartments(departments, filteredUsers);
        var conversations = await LoadConversationSummariesAsync(tenantId, currentUserId, cancellationToken);
        var online = await LoadOnlineUsersAsync(tenantId, appCode, cancellationToken);

        var usersByDepartment = new Dictionary<string, List<ImDirectoryUserResponse>>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in filteredUsers)
        {
            var binding = await accountBindingService.EnsureForUserAsync(tenantId, user.UserId, cancellationToken);
            conversations.TryGetValue(user.UserId, out var conversation);
            online.LastSeenByUserId.TryGetValue(user.UserId, out var lastSeen);
            var item = new ImDirectoryUserResponse(
                user.UserId,
                user.UserName,
                user.DisplayName,
                user.DeptId,
                user.DeptName,
                user.PositionId,
                user.PositionName,
                user.EmploymentId,
                user.EmploymentName,
                user.IsPrimaryEmployment,
                binding.ImAccountId,
                online.OnlineUserIds.Contains(user.UserId),
                lastSeen,
                conversation?.ConversationId,
                conversation?.LastMessagePreview,
                conversation?.LastMessageAt,
                conversation?.UnreadCount ?? 0);

            if (!usersByDepartment.TryGetValue(item.DeptId, out var items))
            {
                items = [];
                usersByDepartment[item.DeptId] = items;
            }

            items.Add(item);
        }

        var nodes = BuildDepartmentTree(departments, usersByDepartment, normalizedKeyword);
        return new ImDirectoryResponse(nodes);
    }

    public async Task<IReadOnlyList<ImUserSearchItemResponse>> SearchAsync(ImUserSearchQuery query, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(query.Take, 1, 50);
        var directory = await GetDirectoryAsync(query.Keyword, cancellationToken);
        return FlattenUsers(directory.Departments)
            .GroupBy(item => item.UserId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(item => new ImUserSearchItemResponse(
                item.UserId,
                item.UserName,
                item.DisplayName,
                item.DeptId,
                item.PositionId,
                item.ImAccountId))
            .ToList();
    }

    private async Task<IReadOnlyList<DirectoryUserRow>> LoadApplicationDirectoryUsersAsync(
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var users = await appDb.Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Enabled" && item.Id != currentUserId)
            .ToListAsync(cancellationToken);
        var employments = await appDb.Queryable<SystemUserEmploymentEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.Status == "Enabled")
            .ToListAsync(cancellationToken);
        var departments = await appDb.Queryable<SystemDepartmentEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var positions = await appDb.Queryable<SystemPositionEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);

        return BuildDirectoryUsers(users, employments, departments, positions);
    }

    private async Task<IReadOnlyList<DirectoryUserRow>> LoadMainDirectoryUsersAsync(
        string tenantId,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var db = databaseAccessor.MainDb;
        var rows = await db.Queryable<SystemUserTenantMembershipEntity, SystemUserEntity>(
                (membership, user) => membership.UserId == user.Id)
            .Where((membership, user) =>
                !membership.IsDeleted &&
                !user.IsDeleted &&
                membership.Status == "Enabled" &&
                user.Status == "Enabled" &&
                membership.TenantId == tenantId &&
                membership.UserId != currentUserId)
            .Select((membership, user) => new
            {
                user.Id,
                user.UserName,
                user.DisplayName,
                membership.DeptId,
                membership.PositionId
            })
            .ToListAsync(cancellationToken);

        var deptIds = rows.Select(item => item.DeptId).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var positionIds = rows.Select(item => item.PositionId).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var departments = deptIds.Count == 0
            ? []
            : await db.Queryable<SystemDepartmentEntity>().Where(item => deptIds.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        var positions = positionIds.Count == 0
            ? []
            : await db.Queryable<SystemPositionEntity>().Where(item => positionIds.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        var deptMap = departments.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var positionMap = positions.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        return rows
            .Where(item => !string.IsNullOrWhiteSpace(item.DeptId) && !string.IsNullOrWhiteSpace(item.PositionId))
            .Select(item =>
            {
                deptMap.TryGetValue(item.DeptId!, out var dept);
                positionMap.TryGetValue(item.PositionId!, out var position);
                var deptName = dept?.DeptName;
                var positionName = position?.PositionName;
                return new DirectoryUserRow(
                    item.Id,
                    item.UserName,
                    ResolveDisplayName(item.UserName, item.DisplayName),
                    item.DeptId!,
                    deptName,
                    item.PositionId!,
                    positionName,
                    $"{item.Id}:{item.DeptId}:{item.PositionId}",
                    string.IsNullOrWhiteSpace(deptName) || string.IsNullOrWhiteSpace(positionName) ? "默认任职" : $"{deptName}/{positionName}",
                    true,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<DirectoryDepartmentRow>> LoadApplicationDepartmentsAsync(CancellationToken cancellationToken)
    {
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        return (await appDb.Queryable<SystemDepartmentEntity>()
                .Where(item => !item.IsDeleted && item.Status == "Enabled")
                .OrderBy(item => item.SortOrder, OrderByType.Asc)
                .OrderBy(item => item.CreatedTime, OrderByType.Asc)
                .ToListAsync(cancellationToken))
            .Select(item => new DirectoryDepartmentRow(item.Id, item.DeptCode, item.DeptName, item.ParentId, item.SortOrder))
            .ToList();
    }

    private async Task<IReadOnlyList<DirectoryDepartmentRow>> LoadMainDepartmentsAsync(
        IReadOnlyList<DirectoryUserRow> users,
        CancellationToken cancellationToken)
    {
        var deptIds = users.Select(item => item.DeptId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (deptIds.Count == 0)
        {
            return [];
        }

        return (await databaseAccessor.MainDb.Queryable<SystemDepartmentEntity>()
                .Where(item => deptIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken))
            .Select(item => new DirectoryDepartmentRow(item.Id, item.DeptCode, item.DeptName, item.ParentId, item.SortOrder))
            .ToList();
    }

    private async Task<Dictionary<string, ConversationSummary>> LoadConversationSummariesAsync(
        string tenantId,
        string currentUserId,
        CancellationToken cancellationToken)
    {
        var db = databaseAccessor.MainDb;
        var participants = await db.Queryable<ImConversationParticipantEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.UserId == currentUserId)
            .ToListAsync(cancellationToken);
        if (participants.Count == 0)
        {
            return new Dictionary<string, ConversationSummary>(StringComparer.OrdinalIgnoreCase);
        }

        var conversationIds = participants.Select(item => item.ConversationId).ToList();
        var conversations = await db.Queryable<ImConversationEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && conversationIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var participantMap = participants.ToDictionary(item => item.ConversationId, StringComparer.OrdinalIgnoreCase);
        return conversations.ToDictionary(
            item => ResolvePeerUserId(item, currentUserId),
            item => new ConversationSummary(
                item.Id,
                item.LastMessagePreview,
                item.LastMessageAt,
                participantMap.TryGetValue(item.Id, out var participant) ? participant.UnreadCount : 0),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<OnlineSnapshot> LoadOnlineUsersAsync(string tenantId, string appCode, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeAfter = now.Subtract(OnlineWindow);
        var rows = await databaseAccessor.MainDb.Queryable<SystemAuthSessionEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.RevokedAt == null &&
                item.ExpiresAt > now &&
                item.CurrentTenantId == tenantId &&
                item.CurrentAppCode == appCode &&
                item.LastSeenTime >= activeAfter)
            .Select(item => new { item.UserId, item.LastSeenTime })
            .ToListAsync(cancellationToken);
        var lastSeen = rows
            .GroupBy(item => item.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Max(item => item.LastSeenTime),
                StringComparer.OrdinalIgnoreCase);
        var online = lastSeen.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var userId in presenceService.GetOnlineUserIds(tenantId, appCode))
        {
            online.Add(userId);
            lastSeen.TryAdd(userId, now);
        }

        return new OnlineSnapshot(online, lastSeen);
    }

    private static IReadOnlyList<DirectoryUserRow> BuildDirectoryUsers(
        IReadOnlyList<SystemUserEntity> users,
        IReadOnlyList<SystemUserEmploymentEntity> employments,
        IReadOnlyList<SystemDepartmentEntity> departments,
        IReadOnlyList<SystemPositionEntity> positions)
    {
        var userMap = users.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var deptMap = departments.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var positionMap = positions.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        return employments
            .Where(item =>
                userMap.ContainsKey(item.UserId))
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.SortOrder)
            .Select(item =>
            {
                var user = userMap[item.UserId];
                deptMap.TryGetValue(item.DeptId, out var dept);
                positionMap.TryGetValue(item.PositionId, out var position);
                return new DirectoryUserRow(
                    user.Id,
                    user.UserName,
                    ResolveDisplayName(user.UserName, user.DisplayName),
                    item.DeptId,
                    dept?.DeptName ?? "未归属部门",
                    item.PositionId,
                    position?.PositionName,
                    item.Id,
                    item.EmploymentName,
                    item.IsPrimary,
                    dept?.DeptCode ?? item.DeptId,
                    user.Email ?? string.Empty,
                    user.PhoneNumber ?? string.Empty);
            })
            .ToList();
    }

    private static IReadOnlyList<DirectoryDepartmentRow> EnsureMissingDepartments(
        IReadOnlyList<DirectoryDepartmentRow> departments,
        IReadOnlyList<DirectoryUserRow> users)
    {
        var departmentIds = departments.Select(item => item.DeptId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = users
            .Where(item => !departmentIds.Contains(item.DeptId))
            .GroupBy(item => item.DeptId, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DirectoryDepartmentRow(group.Key, group.First().DeptCode, group.First().DeptName ?? "未归属部门", null, int.MaxValue))
            .ToList();
        return missing.Count == 0 ? departments : departments.Concat(missing).ToList();
    }

    private static IReadOnlyList<DirectoryUserRow> FilterUsers(IReadOnlyList<DirectoryUserRow> users, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return users;
        }

        return users
            .Where(item =>
                Contains(item.UserName, keyword) ||
                Contains(item.DisplayName, keyword) ||
                Contains(item.DeptName, keyword) ||
                Contains(item.DeptCode, keyword) ||
                Contains(item.PositionName, keyword) ||
                Contains(item.Email, keyword) ||
                Contains(item.PhoneNumber, keyword))
            .ToList();
    }

    private static IReadOnlyList<ImDirectoryDepartmentNodeResponse> BuildDepartmentTree(
        IReadOnlyList<DirectoryDepartmentRow> departments,
        IReadOnlyDictionary<string, List<ImDirectoryUserResponse>> usersByDepartment,
        string? keyword)
    {
        var lookup = departments
            .GroupBy(item => item.DeptId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToDictionary(
                item => item.DeptId,
                item => new DepartmentNodeBuilder(item, usersByDepartment.TryGetValue(item.DeptId, out var users) ? users : []),
                StringComparer.OrdinalIgnoreCase);

        foreach (var department in departments)
        {
            if (!string.IsNullOrWhiteSpace(department.ParentId) &&
                lookup.TryGetValue(department.DeptId, out var node) &&
                lookup.TryGetValue(department.ParentId, out var parent))
            {
                parent.Children.Add(node);
            }
        }

        var roots = departments
            .Where(item => string.IsNullOrWhiteSpace(item.ParentId) || !lookup.ContainsKey(item.ParentId))
            .OrderBy(item => item.SortOrder)
            .Select(item => lookup[item.DeptId])
            .ToList();

        return roots
            .Select(item => item.ToResponse(!string.IsNullOrWhiteSpace(keyword)))
            .Where(item => item.Users.Count > 0 || item.Children.Count > 0 || string.IsNullOrWhiteSpace(keyword))
            .ToList();
    }

    private static IReadOnlyList<ImDirectoryUserResponse> FlattenUsers(IReadOnlyList<ImDirectoryDepartmentNodeResponse> departments)
    {
        var users = new List<ImDirectoryUserResponse>();
        foreach (var department in departments)
        {
            users.AddRange(department.Users);
            users.AddRange(FlattenUsers(department.Children));
        }

        return users;
    }

    private string RequireTenantId() =>
        NormalizeRequired(currentUser.GetAsterErpTenantId(), "当前租户不能为空");

    private string RequireAppCode() =>
        NormalizeRequired(currentUser.GetAsterErpAppCode(), "当前应用不能为空").ToUpperInvariant();

    private string RequireUserId() =>
        NormalizeRequired(currentUser.GetAsterErpUserId(), "当前用户不能为空");

    private static string ResolvePeerUserId(ImConversationEntity conversation, string currentUserId) =>
        string.Equals(conversation.ParticipantAUserId, currentUserId, StringComparison.OrdinalIgnoreCase)
            ? conversation.ParticipantBUserId
            : conversation.ParticipantAUserId;

    private static bool Contains(string? source, string keyword) =>
        !string.IsNullOrWhiteSpace(source) && source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static string ResolveDisplayName(string userName, string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim();

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.AuthenticationRequired);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record DirectoryDepartmentRow(string DeptId, string DeptCode, string DeptName, string? ParentId, int SortOrder);

    private sealed record DirectoryUserRow(
        string UserId,
        string UserName,
        string DisplayName,
        string DeptId,
        string? DeptName,
        string PositionId,
        string? PositionName,
        string EmploymentId,
        string EmploymentName,
        bool IsPrimaryEmployment,
        string DeptCode,
        string Email,
        string PhoneNumber);

    private sealed record ConversationSummary(string ConversationId, string? LastMessagePreview, DateTime? LastMessageAt, int UnreadCount);

    private sealed record OnlineSnapshot(IReadOnlySet<string> OnlineUserIds, IReadOnlyDictionary<string, DateTime?> LastSeenByUserId);

    private sealed class DepartmentNodeBuilder(DirectoryDepartmentRow department, IReadOnlyList<ImDirectoryUserResponse> users)
    {
        private readonly DirectoryDepartmentRow value = department;

        public List<DepartmentNodeBuilder> Children { get; } = [];

        public ImDirectoryDepartmentNodeResponse ToResponse(bool pruneEmpty)
        {
            var children = Children
                .OrderBy(item => item.value.SortOrder)
                .Select(item => item.ToResponse(pruneEmpty))
                .Where(item => !pruneEmpty || item.Users.Count > 0 || item.Children.Count > 0)
                .ToList();
            var sortedUsers = users
                .OrderByDescending(item => item.IsOnline)
                .ThenByDescending(item => item.IsPrimaryEmployment)
                .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ImDirectoryDepartmentNodeResponse(
                value.DeptId,
                value.DeptCode,
                value.DeptName,
                value.ParentId,
                value.SortOrder,
                sortedUsers,
                children);
        }
    }
}
