namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public static class ApplicationDataCenterSqlBuiltInVariableNames
{
    public const string CurrentUserId = "currentUserId";
    public const string CurrentTenantId = "currentTenantId";
    public const string CurrentAppCode = "currentAppCode";
    public const string CurrentEmploymentId = "currentEmploymentId";
    public const string CurrentDeptId = "currentDeptId";
    public const string CurrentDeptIds = "currentDeptIds";
    public const string CurrentPositionId = "currentPositionId";
    public const string CurrentPositionIds = "currentPositionIds";
    public const string CurrentDataScope = "currentDataScope";
    public const string IsAuthenticated = "isAuthenticated";
    public const string IsPlatformAdmin = "isPlatformAdmin";
    public const string IsTenantAdmin = "isTenantAdmin";
    public const string AuditUserId = "auditUserId";
    public const string AuditNow = "auditNow";
    public const string AuditCreatedBy = "auditCreatedBy";
    public const string AuditUpdatedBy = "auditUpdatedBy";
    public const string AuditCreatedTime = "auditCreatedTime";
    public const string AuditUpdatedTime = "auditUpdatedTime";

    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        CurrentUserId,
        CurrentTenantId,
        CurrentAppCode,
        CurrentEmploymentId,
        CurrentDeptId,
        CurrentDeptIds,
        CurrentPositionId,
        CurrentPositionIds,
        CurrentDataScope,
        IsAuthenticated,
        IsPlatformAdmin,
        IsTenantAdmin,
        AuditUserId,
        AuditNow,
        AuditCreatedBy,
        AuditUpdatedBy,
        AuditCreatedTime,
        AuditUpdatedTime
    };

    public static IReadOnlySet<string> All => Names;

    public static bool IsReserved(string? name)
    {
        var normalized = ApplicationDataCenterSqlScriptValidator.NormalizeName(name);
        return !string.IsNullOrWhiteSpace(normalized) && Names.Contains(normalized);
    }
}
