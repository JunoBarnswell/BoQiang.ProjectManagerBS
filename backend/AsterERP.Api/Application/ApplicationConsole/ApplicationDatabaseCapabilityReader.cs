using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.System.Logs;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Contracts.ApplicationConsole;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationDatabaseCapabilityReader(ILogger<ApplicationDatabaseCapabilityReader> logger)
{
    public async Task<ApplicationConsoleCapabilityCountsResponse> ReadCountsAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        var businessRootMenuCount = await SafeCountAsync(
            () => appDb.Queryable<SystemMenuEntity>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    !item.IsDeleted &&
                    item.Visible &&
                    item.ParentCode == null &&
                    item.MenuType != "Button" &&
                    item.MenuType != "按钮")
                .CountAsync(cancellationToken),
            nameof(SystemMenuEntity));
        var businessMenuCount = await SafeCountAsync(
            () => appDb.Queryable<SystemMenuEntity>()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    !item.IsDeleted &&
                    item.Visible &&
                    item.MenuType != "Button" &&
                    item.MenuType != "按钮")
                .CountAsync(cancellationToken),
            nameof(SystemMenuEntity));
        var permissionCount = await SafeCountAsync(
            () => appDb.Queryable<SystemPermissionCodeEntity>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .CountAsync(cancellationToken),
            nameof(SystemPermissionCodeEntity));

        return new ApplicationConsoleCapabilityCountsResponse(
            businessRootMenuCount,
            businessMenuCount,
            permissionCount,
            await SafeCountAsync(
                () => appDb.Queryable<ApplicationDevelopmentPageEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
                    .CountAsync(cancellationToken),
                nameof(ApplicationDevelopmentPageEntity)),
            await SafeCountAsync(
                () => appDb.Queryable<ApplicationDevelopmentPageEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && item.Status == "Published")
                    .CountAsync(cancellationToken),
                nameof(ApplicationDevelopmentPageEntity)),
            await SafeCountAsync(
                () => appDb.Queryable<SystemDataModelEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
                    .CountAsync(cancellationToken),
                nameof(SystemDataModelEntity)),
            await SafeCountAsync(
                () => appDb.Queryable<WorkflowModelExtensionEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
                    .CountAsync(cancellationToken),
                nameof(WorkflowModelExtensionEntity)),
            await SafeCountAsync(
                () => appDb.Queryable<SystemApplicationPublishTaskEntity>()
                    .Where(item => item.AppCode == appCode && !item.IsDeleted && (item.TenantId == null || item.TenantId == tenantId))
                    .CountAsync(cancellationToken),
                nameof(SystemApplicationPublishTaskEntity)),
            await SafeCountAsync(
                () => appDb.Queryable<ApplicationDevelopmentPageEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && item.Status == "Draft")
                    .CountAsync(cancellationToken),
                nameof(ApplicationDevelopmentPageEntity)),
            await SafeCountAsync(
                () => appDb.Queryable<SystemMenuEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && item.ScopeType == "ApplicationDraftPreview")
                    .CountAsync(cancellationToken),
                nameof(SystemMenuEntity)),
            await SafeCountAsync(
                () => appDb.Queryable<ApplicationDevelopmentVersionEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && item.Status == "Draft")
                    .CountAsync(cancellationToken),
                nameof(ApplicationDevelopmentVersionEntity)),
            await SafeCountAsync(
                () => appDb.Queryable<ApplicationDevelopmentVersionEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && item.Status == "Published")
                    .CountAsync(cancellationToken),
                nameof(ApplicationDevelopmentVersionEntity)));
    }

    public async Task<IReadOnlyList<ApplicationConsoleRecentItemResponse>> ReadRecentPublishesAsync(
        ISqlSugarClient appDb,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default)
    {
        return await SafeListAsync(
            async () =>
            {
                var tasks = await appDb.Queryable<SystemApplicationPublishTaskEntity>()
                    .Where(item => item.AppCode == appCode && !item.IsDeleted && (item.TenantId == null || item.TenantId == tenantId))
                    .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                    .Take(5)
                    .ToListAsync(cancellationToken);

                return tasks.Select(item => new ApplicationConsoleRecentItemResponse(
                    item.Id,
                    string.IsNullOrWhiteSpace(item.Version) ? "应用发布任务" : $"版本 {item.Version}",
                    $"{item.Stage} / {item.ProgressPercent}%",
                    item.CreatedTime,
                    item.Status)).ToArray();
            },
            nameof(SystemApplicationPublishTaskEntity));
    }

    public async Task<IReadOnlyList<ApplicationConsoleRecentItemResponse>> ReadRecentAuditsAsync(
        ISqlSugarClient appDb,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        return await SafeListAsync(
            async () =>
            {
                var logs = await appDb.Queryable<SystemOperationLogEntity>()
                    .Where(item => !item.IsDeleted && item.UserId == userId)
                    .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                    .Take(5)
                    .ToListAsync(cancellationToken);

                return logs.Select(item => new ApplicationConsoleRecentItemResponse(
                    item.Id,
                    string.IsNullOrWhiteSpace(item.ActionName) ? item.RequestPath : item.ActionName,
                    item.IsSuccess ? "执行成功" : item.ErrorMessage ?? "执行失败",
                    item.CreatedTime,
                    item.IsSuccess ? "Success" : "Failed")).ToArray();
            },
            nameof(SystemOperationLogEntity));
    }

    private async Task<int> SafeCountAsync(Func<Task<int>> countAsync, string source)
    {
        try
        {
            return await countAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Application database count skipped for {Source}", source);
            return 0;
        }
    }

    private async Task<IReadOnlyList<ApplicationConsoleRecentItemResponse>> SafeListAsync(
        Func<Task<IReadOnlyList<ApplicationConsoleRecentItemResponse>>> listAsync,
        string source)
    {
        try
        {
            return await listAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Application database recent list skipped for {Source}", source);
            return [];
        }
    }
}
