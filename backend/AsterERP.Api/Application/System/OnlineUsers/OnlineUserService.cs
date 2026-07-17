using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Contracts.System.OnlineUsers;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Modules.System.Auth;
using AsterERP.Api.Modules.System.Users;
using SqlSugar;

namespace AsterERP.Api.Application.System.OnlineUsers;

public sealed class OnlineUserService(IWorkspaceDatabaseAccessor databaseAccessor) : IOnlineUserService
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(10);

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemAuthSessionEntity, SystemUserEntity>, GridFilter, ISugarQueryable<SystemAuthSessionEntity, SystemUserEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemAuthSessionEntity, SystemUserEntity>, GridFilter, ISugarQueryable<SystemAuthSessionEntity, SystemUserEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["clientIp"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (session, user) => session.ClientIp),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, (session, user) => session.CreatedTime),
            ["deptId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (session, user) => user.DeptId),
            ["displayName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (session, user) => user.DisplayName),
            ["expiresAt"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, (session, user) => session.ExpiresAt),
            ["lastSeenTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, (session, user) => session.LastSeenTime),
            ["sessionId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (session, user) => session.Id),
            ["userAgent"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (session, user) => session.UserAgent),
            ["userId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (session, user) => session.UserId),
            ["userName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, (session, user) => user.UserName)
        };

    public async Task<GridPageResult<OnlineUserResponse>> GetPageAsync(
        OnlineUserQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var now = DateTime.UtcNow;
        var activeAfter = now.Subtract(OnlineWindow);
        var keyword = query.Keyword?.Trim();

        var sessionQuery = databaseAccessor.GetCurrentDb().Queryable<SystemAuthSessionEntity, SystemUserEntity>(
                (session, user) => new JoinQueryInfos(
                    JoinType.Inner,
                    session.UserId == user.Id && !user.IsDeleted))
            .Where((session, user) =>
                !session.IsDeleted &&
                session.RevokedAt == null &&
                session.ExpiresAt > now &&
                session.LastSeenTime >= activeAfter);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            sessionQuery = sessionQuery.Where((session, user) =>
                session.UserId.Contains(keyword) ||
                user.UserName.Contains(keyword) ||
                user.DisplayName.Contains(keyword) ||
                (session.ClientIp != null && session.ClientIp.Contains(keyword)) ||
                (session.UserAgent != null && session.UserAgent.Contains(keyword)));
        }

        sessionQuery = GridFilterApplier.Apply(sessionQuery, query.Filters, Filterers);

        var total = await sessionQuery.CountAsync(cancellationToken);
        var items = await GridSortApplier
            .Apply(
                sessionQuery,
                query.Sorts,
                (nextQuery, field, order) => field switch
                {
                    "clientIp" => nextQuery.OrderBy((session, user) => session.ClientIp, order),
                    "createdTime" => nextQuery.OrderBy((session, user) => session.CreatedTime, order),
                    "deptId" => nextQuery.OrderBy((session, user) => user.DeptId, order),
                    "displayName" => nextQuery.OrderBy((session, user) => user.DisplayName, order),
                    "expiresAt" => nextQuery.OrderBy((session, user) => session.ExpiresAt, order),
                    "lastSeenTime" => nextQuery.OrderBy((session, user) => session.LastSeenTime, order),
                    "sessionId" => nextQuery.OrderBy((session, user) => session.Id, order),
                    "userAgent" => nextQuery.OrderBy((session, user) => session.UserAgent, order),
                    "userId" => nextQuery.OrderBy((session, user) => session.UserId, order),
                    "userName" => nextQuery.OrderBy((session, user) => user.UserName, order),
                    _ => null
                },
                nextQuery => nextQuery
                    .OrderBy((session, user) => session.LastSeenTime, OrderByType.Desc)
                    .OrderBy((session, user) => session.CreatedTime, OrderByType.Desc))
            .Select((session, user) => new OnlineUserRow
            {
                SessionId = session.Id,
                UserId = session.UserId,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                DeptId = user.DeptId,
                ClientIp = session.ClientIp,
                UserAgent = session.UserAgent,
                ExpiresAt = session.ExpiresAt,
                LastSeenTime = session.LastSeenTime,
                CreatedTime = session.CreatedTime
            })
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new GridPageResult<OnlineUserResponse>
        {
            Total = total,
            Items = items.Select(MapToResponse).ToList()
        };
    }

    public async Task ForceLogoutAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var normalizedSessionId = sessionId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return;
        }

        var session = (await databaseAccessor.GetCurrentDb().Queryable<SystemAuthSessionEntity>()
            .Where(item => item.Id == normalizedSessionId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();

        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        var revokedAt = DateTime.UtcNow;
        session.RevokedAt = revokedAt;
        session.UpdatedTime = revokedAt;
        await databaseAccessor.GetCurrentDb().Updateable(session)
            .UpdateColumns(item => new { item.RevokedAt, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    private static OnlineUserResponse MapToResponse(OnlineUserRow row) =>
        new(
            row.SessionId,
            row.UserId,
            row.UserName,
            row.DisplayName,
            row.DeptId,
            row.ClientIp,
            row.UserAgent,
            row.ExpiresAt,
            row.LastSeenTime,
            row.CreatedTime);

    private sealed class OnlineUserRow
    {
        public string SessionId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? DeptId { get; set; }

        public string? ClientIp { get; set; }

        public string? UserAgent { get; set; }

        public DateTime ExpiresAt { get; set; }

        public DateTime? LastSeenTime { get; set; }

        public DateTime CreatedTime { get; set; }
    }
}

