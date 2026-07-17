using AsterERP.Api.Infrastructure.Security;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed class ApplicationDataCenterSqlBuiltInVariableProvider(ICurrentUser currentUser)
{
    public IReadOnlyDictionary<string, object?> Build()
    {
        var now = DateTime.UtcNow;
        var userId = currentUser.GetAsterErpUserId();
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentUserId] = userId,
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentTenantId] = currentUser.GetAsterErpTenantId(),
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentAppCode] = currentUser.GetAsterErpAppCode(),
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentEmploymentId] = currentUser.GetAsterErpEmploymentId(),
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentDeptId] = currentUser.GetAsterErpDeptId(),
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentDeptIds] = currentUser.GetAsterErpDeptIds(),
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentPositionId] = currentUser.GetAsterErpPositionId(),
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentPositionIds] = currentUser.GetAsterErpPositionIds(),
            [ApplicationDataCenterSqlBuiltInVariableNames.CurrentDataScope] = currentUser.GetAsterErpDataScope(),
            [ApplicationDataCenterSqlBuiltInVariableNames.IsAuthenticated] = ToSqlBoolean(currentUser.IsAsterErpAuthenticated()),
            [ApplicationDataCenterSqlBuiltInVariableNames.IsPlatformAdmin] = ToSqlBoolean(currentUser.IsAsterErpPlatformAdmin()),
            [ApplicationDataCenterSqlBuiltInVariableNames.IsTenantAdmin] = ToSqlBoolean(currentUser.IsAsterErpTenantAdmin()),
            [ApplicationDataCenterSqlBuiltInVariableNames.AuditUserId] = userId,
            [ApplicationDataCenterSqlBuiltInVariableNames.AuditNow] = now,
            [ApplicationDataCenterSqlBuiltInVariableNames.AuditCreatedBy] = userId,
            [ApplicationDataCenterSqlBuiltInVariableNames.AuditUpdatedBy] = userId,
            [ApplicationDataCenterSqlBuiltInVariableNames.AuditCreatedTime] = now,
            [ApplicationDataCenterSqlBuiltInVariableNames.AuditUpdatedTime] = now
        };
    }

    private static int ToSqlBoolean(bool value) => value ? 1 : 0;
}
