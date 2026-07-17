using AsterERP.Api.Modules.System.Permissions;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentCenterSeedService(
    ISqlSugarClient db)
{
    private static readonly IReadOnlyList<string> PermissionCodes =
    [
        AsterERP.Shared.PermissionCodes.AppDevelopmentCenterDesignerView,
        AsterERP.Shared.PermissionCodes.AppDevelopmentCenterDesignerEdit,
        AsterERP.Shared.PermissionCodes.AppDevelopmentCenterDesignerDelete,
        AsterERP.Shared.PermissionCodes.AppDevelopmentCenterDesignerPreview,
        AsterERP.Shared.PermissionCodes.AppDevelopmentCenterDesignerPublish,
        AsterERP.Shared.PermissionCodes.AppDevelopmentCenterDesignerPermissionEdit
    ];

    public Task SeedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SeedCoreAsync(cancellationToken);
    }

    private async Task SeedCoreAsync(CancellationToken cancellationToken)
    {
        foreach (var code in PermissionCodes)
        {
            var permission = await db.Queryable<SystemPermissionCodeEntity>()
                .Where(item => item.PermissionCode == code)
                .FirstAsync(cancellationToken);
            if (permission is null)
            {
                await db.Insertable(new SystemPermissionCodeEntity
                {
                    ModuleName = "ApplicationDevelopmentCenter",
                    PermissionCode = code,
                    PermissionName = ResolvePermissionName(code),
                    IsEnabled = true
                }).ExecuteCommandAsync(cancellationToken);
                continue;
            }

            permission.ModuleName = "ApplicationDevelopmentCenter";
            permission.PermissionName = ResolvePermissionName(code);
            permission.IsEnabled = true;
            permission.IsDeleted = false;
            permission.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(permission).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static string ResolvePermissionName(string code)
    {
        var segments = code.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 4
            ? $"{segments[2]} {segments[3]}"
            : "ApplicationDevelopmentCenter permission";
    }
}
