using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.Im;

public sealed class ImSeedService(ISqlSugarClient db)
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var permission in PermissionDefinitions)
        {
            var existing = db.Queryable<SystemPermissionCodeEntity>()
                .First(item => item.PermissionCode == permission.Code);
            if (existing is null)
            {
                db.Insertable(new SystemPermissionCodeEntity
                {
                    ModuleName = "IM",
                    PermissionCode = permission.Code,
                    PermissionName = permission.Name,
                    IsEnabled = true
                }).ExecuteCommand();
                continue;
            }

            existing.ModuleName = "IM";
            existing.PermissionName = permission.Name;
            existing.IsEnabled = true;
            existing.IsDeleted = false;
            existing.DeletedBy = null;
            existing.DeletedTime = null;
            existing.UpdatedTime = DateTime.UtcNow;
            db.Updateable(existing).ExecuteCommand();
        }

        return Task.CompletedTask;
    }

    private static readonly IReadOnlyList<(string Code, string Name)> PermissionDefinitions =
    [
        (PermissionCodes.ImConversationView, "IM 会话查看"),
        (PermissionCodes.ImConversationCreate, "IM 会话创建"),
        (PermissionCodes.ImMessageSend, "IM 消息发送"),
        (PermissionCodes.ImMessageRead, "IM 消息读取"),
        (PermissionCodes.ImUserSearch, "IM 用户搜索")
    ];
}
