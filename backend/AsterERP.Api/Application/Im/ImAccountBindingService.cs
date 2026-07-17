using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Im;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Im;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Im;

public sealed class ImAccountBindingService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IClock clock) : IImAccountBindingService
{
    public Task<ImAccountBindingResponse> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        return EnsureForUserAsync(tenantId, userId, cancellationToken);
    }

    public async Task<ImAccountBindingResponse> EnsureForUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var normalizedTenantId = NormalizeRequired(tenantId, "租户不能为空");
        var normalizedUserId = NormalizeRequired(userId, "用户不能为空");
        var normalizedAppCode = NormalizeOptional(currentUser.GetAsterErpAppCode())?.ToUpperInvariant();
        var db = databaseAccessor.MainDb;
        var user = string.Equals(normalizedAppCode, "SYSTEM", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(normalizedAppCode)
            ? await ResolveMainUserAsync(db, normalizedTenantId, normalizedUserId, cancellationToken)
            : await ResolveApplicationUserAsync(normalizedUserId, cancellationToken);

        if (user is null)
        {
            throw new ValidationException("目标用户不属于当前租户或已禁用", ErrorCodes.PermissionDenied);
        }

        var binding = await db.Queryable<ImAccountBindingEntity>()
            .FirstAsync(item => !item.IsDeleted && item.TenantId == normalizedTenantId && item.UserId == normalizedUserId, cancellationToken);
        if (binding is null)
        {
            binding = new ImAccountBindingEntity
            {
                TenantId = normalizedTenantId,
                UserId = normalizedUserId,
                ImAccountId = BuildImAccountId(normalizedTenantId, normalizedUserId),
                DisplayName = user.DisplayName,
                Status = "Enabled",
                BoundAt = clock.Now,
                LastSyncedAt = clock.Now,
                CreatedBy = currentUser.GetAsterErpUserId()
            };
            await db.Insertable(binding).ExecuteCommandAsync(cancellationToken);
        }
        else if (!string.Equals(binding.DisplayName, user.DisplayName, StringComparison.Ordinal) || binding.Status != "Enabled")
        {
            binding.DisplayName = user.DisplayName;
            binding.Status = "Enabled";
            binding.LastSyncedAt = clock.Now;
            binding.UpdatedBy = currentUser.GetAsterErpUserId();
            binding.UpdatedTime = clock.Now;
            await db.Updateable(binding).ExecuteCommandAsync(cancellationToken);
        }

        return Map(binding);
    }

    private static async Task<ImUserIdentity?> ResolveMainUserAsync(
        ISqlSugarClient db,
        string tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        var user = await db.Queryable<SystemUserTenantMembershipEntity, SystemUserEntity>(
                (membership, systemUser) => membership.UserId == systemUser.Id)
            .Where((membership, systemUser) =>
                !membership.IsDeleted &&
                !systemUser.IsDeleted &&
                membership.Status == "Enabled" &&
                systemUser.Status == "Enabled" &&
                membership.TenantId == tenantId &&
                membership.UserId == userId)
            .Select((membership, systemUser) => new { systemUser.Id, systemUser.UserName, systemUser.DisplayName })
            .FirstAsync(cancellationToken);

        return user is null
            ? null
            : new ImUserIdentity(user.Id, user.UserName, ResolveDisplayName(user.UserName, user.DisplayName));
    }

    private async Task<ImUserIdentity?> ResolveApplicationUserAsync(string userId, CancellationToken cancellationToken)
    {
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var user = await appDb.Queryable<SystemUserEntity>()
            .Where(item => item.Id == userId && !item.IsDeleted && item.Status == "Enabled")
            .Select(item => new { item.Id, item.UserName, item.DisplayName })
            .FirstAsync(cancellationToken);

        return user is null
            ? null
            : new ImUserIdentity(user.Id, user.UserName, ResolveDisplayName(user.UserName, user.DisplayName));
    }

    public static string BuildImAccountId(string tenantId, string userId) =>
        $"astererp.{tenantId.Trim().ToLowerInvariant()}.{userId.Trim().ToLowerInvariant()}";

    private string RequireTenantId() =>
        NormalizeRequired(currentUser.GetAsterErpTenantId(), "当前租户不能为空");

    private string RequireUserId() =>
        NormalizeRequired(currentUser.GetAsterErpUserId(), "当前用户不能为空");

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

    private static string ResolveDisplayName(string userName, string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim();

    private static ImAccountBindingResponse Map(ImAccountBindingEntity entity) =>
        new(entity.TenantId, entity.UserId, entity.ImAccountId, entity.DisplayName, entity.Status, entity.BoundAt);

    private sealed record ImUserIdentity(string Id, string UserName, string DisplayName);
}
