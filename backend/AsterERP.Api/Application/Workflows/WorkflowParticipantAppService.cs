using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Workflows;
using SqlSugar;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowParticipantAppService(IWorkspaceDatabaseAccessor databaseAccessor) : IWorkflowParticipantAppService
{
    public async Task<IReadOnlyList<WorkflowParticipantResponse>> QueryAsync(string? keyword, string? type, CancellationToken cancellationToken = default)
    {
        var normalizedType = type?.Trim();
        var result = new List<WorkflowParticipantResponse>();

        if (MatchesType(normalizedType, "user"))
        {
            var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
                .Where(item => !item.IsDeleted && item.Status == "Enabled")
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), item => item.UserName.Contains(keyword!) || item.DisplayName.Contains(keyword!))
                .OrderBy(item => item.UserName)
                .Take(100)
                .ToListAsync(cancellationToken);
            var employmentSummaries = await ResolveEmploymentSummariesAsync(users.Select(item => item.Id).ToList(), cancellationToken);
            result.AddRange(users.Select(item => new WorkflowParticipantResponse(
                item.Id,
                item.UserName,
                item.DisplayName,
                "user",
                item.DeptId,
                item.Id,
                item.Email,
                employmentSummaries.TryGetValue(item.Id, out var summary) ? summary : null)));
        }

        if (MatchesType(normalizedType, "role"))
        {
            var roles = await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), item => item.RoleCode.Contains(keyword!) || item.RoleName.Contains(keyword!))
                .OrderBy(item => item.RoleCode)
                .Take(100)
                .ToListAsync(cancellationToken);
            result.AddRange(roles.Select(item => new WorkflowParticipantResponse(
                item.Id,
                item.RoleCode,
                item.RoleName,
                "role",
                null,
                WorkflowIdentityKeys.RoleGroup(item.Id),
                item.AppCode)));
        }

        if (MatchesType(normalizedType, "department"))
        {
            var departments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
                .Where(item => !item.IsDeleted && item.Status == "Enabled")
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), item => item.DeptCode.Contains(keyword!) || item.DeptName.Contains(keyword!))
                .OrderBy(item => item.SortOrder)
                .Take(100)
                .ToListAsync(cancellationToken);
            result.AddRange(departments.Select(item => new WorkflowParticipantResponse(
                item.Id,
                item.DeptCode,
                item.DeptName,
                "department",
                item.ParentId,
                WorkflowIdentityKeys.DepartmentGroup(item.Id),
                item.ManagerName)));
        }

        if (MatchesType(normalizedType, "position"))
        {
            var positions = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
                .Where(item => !item.IsDeleted && item.Status == "Enabled")
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), item => item.PositionCode.Contains(keyword!) || item.PositionName.Contains(keyword!))
                .OrderBy(item => item.SortOrder)
                .Take(100)
                .ToListAsync(cancellationToken);
            result.AddRange(positions.Select(item => new WorkflowParticipantResponse(
                item.Id,
                item.PositionCode,
                item.PositionName,
                "position",
                item.DeptId,
                WorkflowIdentityKeys.PositionGroup(item.Id),
                item.PositionLevel)));
        }

        if (MatchesDynamicType(normalizedType))
        {
            result.AddRange(GetDynamicParticipants(keyword, normalizedType));
        }

        return result;
    }

    private static bool MatchesType(string? requestedType, string actualType)
    {
        return string.IsNullOrWhiteSpace(requestedType) || string.Equals(requestedType, actualType, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDynamicType(string? requestedType)
    {
        return string.IsNullOrWhiteSpace(requestedType) ||
               string.Equals(requestedType, "dynamic", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(requestedType, "starter", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(requestedType, "starterManager", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(requestedType, "deptManager", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(requestedType, "previousApprover", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(requestedType, "formField", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<WorkflowParticipantResponse> GetDynamicParticipants(string? keyword, string? requestedType)
    {
        var items = new[]
        {
            new WorkflowParticipantResponse("starter", "starter", "发起人", "starter", null, "${starterUserId}", "流程发起用户"),
            new WorkflowParticipantResponse("starter-manager", "starterManager", "发起人上级", "starterManager", null, "${starterManagerUserId}", "按发起人组织关系解析"),
            new WorkflowParticipantResponse("department-manager", "deptManager", "部门负责人", "deptManager", null, "${starterDeptManagerUserId}", "按发起人部门解析"),
            new WorkflowParticipantResponse("previous-approver", "previousApprover", "上一审批人", "previousApprover", null, "${previousApproverUserId}", "上一审批任务处理人"),
            new WorkflowParticipantResponse("form-field", "formField", "表单字段指派", "formField", null, "${formField}", "由业务表单字段值解析"),
            new WorkflowParticipantResponse("expression", "dynamic", "自定义表达式", "dynamic", null, "${expression}", "由 BPMN 表达式解析")
        };

        return items.Where(item =>
            MatchesType(requestedType, item.Type) &&
            (string.IsNullOrWhiteSpace(keyword) ||
             item.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
             item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<Dictionary<string, string>> ResolveEmploymentSummariesAsync(
        IReadOnlyList<string> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEmploymentEntity, SystemDepartmentEntity, SystemPositionEntity>(
                (employment, department, position) => employment.DeptId == department.Id && employment.PositionId == position.Id)
            .Where((employment, department, position) =>
                userIds.Contains(employment.UserId) &&
                !employment.IsDeleted &&
                employment.Status == "Enabled" &&
                !department.IsDeleted &&
                !position.IsDeleted)
            .OrderBy((employment, department, position) => employment.IsPrimary, OrderByType.Desc)
            .OrderBy((employment, department, position) => employment.SortOrder)
            .Select((employment, department, position) => new
            {
                employment.UserId,
                department.DeptName,
                position.PositionName,
                employment.IsPrimary
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => item.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => string.Join("、", group.Select(item => $"{(item.IsPrimary ? "主:" : string.Empty)}{item.DeptName}/{item.PositionName}")),
                StringComparer.OrdinalIgnoreCase);
    }
}
