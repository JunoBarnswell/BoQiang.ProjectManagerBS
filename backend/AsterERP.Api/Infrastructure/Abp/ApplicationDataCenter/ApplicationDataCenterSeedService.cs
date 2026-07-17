using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;

public sealed class ApplicationDataCenterSeedService(ISqlSugarClient db)
{
    public Task SeedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var code in PermissionCodes.AppDataCenterPermissionCodes)
        {
            UpsertPermission(code, ResolvePermissionName(code));
        }

        return Task.CompletedTask;
    }

    private void UpsertPermission(string code, string name)
    {
        var permission = db.Queryable<SystemPermissionCodeEntity>()
            .First(item => item.PermissionCode == code);
        if (permission is null)
        {
            db.Insertable(new SystemPermissionCodeEntity
            {
                ModuleName = "ApplicationDataCenter",
                PermissionCode = code,
                PermissionName = name,
                IsEnabled = true
            }).ExecuteCommand();
            return;
        }

        permission.ModuleName = "ApplicationDataCenter";
        permission.PermissionName = name;
        permission.IsEnabled = true;
        permission.IsDeleted = false;
        permission.UpdatedTime = DateTime.UtcNow;
        db.Updateable(permission).ExecuteCommand();
    }

    private static string ResolvePermissionName(string code)
    {
        var segments = code.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 4
            ? $"{segments[2]} {segments[3]}"
            : "ApplicationDataCenter permission";
    }
}
