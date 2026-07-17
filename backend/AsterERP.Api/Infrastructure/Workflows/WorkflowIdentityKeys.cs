namespace AsterERP.Api.Infrastructure.Workflows;

public static class WorkflowIdentityKeys
{
    public static string RoleGroup(string roleId) => $"role:{roleId}";

    public static string DepartmentGroup(string departmentId) => $"dept:{departmentId}";

    public static string PositionGroup(string positionId) => $"position:{positionId}";
}
