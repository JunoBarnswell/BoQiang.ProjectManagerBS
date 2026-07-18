using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskTemplateInstantiationService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementTaskTemplateCommandService taskCommand) : IProjectManagementTaskTemplateInstantiationService
{
    public async Task<ProjectManagementTaskTemplateInstantiationResponse> InstantiateAsync(ProjectManagementTaskTemplateResponse template, ProjectManagementTaskTemplateDefinition definition, ProjectManagementTaskTemplateInstantiateRequest request, CancellationToken cancellationToken = default)
    {
        var warnings = new List<ProjectManagementTaskTemplateInstantiationWarning>();
        var db = databaseAccessor.GetCurrentDb();
        var labels = await db.Queryable<ProjectManagementLabelEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted && (item.ProjectId == null || item.ProjectId == request.ProjectId)).ToListAsync(cancellationToken);
        var members = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.ProjectId == request.ProjectId && item.TenantId == Tenant() && item.AppCode == App() && item.IsActive && !item.IsDeleted).ToListAsync(cancellationToken);
        var project = (await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == request.ProjectId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).First();
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => item.ProjectId == request.ProjectId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).ToListAsync(cancellationToken);
        var commands = new List<ProjectManagementTaskTemplateNodeCreateCommand>();
        foreach (var node in definition.Nodes.OrderBy(item => item.ParentNodeKey is null ? 0 : 1).ThenBy(item => item.SortOrder))
        {
            var assignee = ResolveAssignee(node, request.RoleAssigneeMappings, members, project, warnings);
            var milestoneId = ResolveMilestone(node, request.MilestoneMappings, milestones, warnings);
            var labelIds = ResolveLabels(node, labels, warnings);
            var start = request.StartDate;
            DateTime? due = start.HasValue && node.DefaultDurationDays.HasValue ? start.Value.AddDays(node.DefaultDurationDays.Value) : null;
            commands.Add(new ProjectManagementTaskTemplateNodeCreateCommand(node.NodeKey,
                new ProjectManagementTaskUpsertRequest(AllocateCode(node.TaskCode), node.Title, node.Description, node.Status, node.Priority, milestoneId, node.ParentNodeKey, assignee?.UserId, assignee?.EmploymentId, start, due, 0, node.Weight, node.EstimateMinutes), labelIds));
        }
        var dependencies = definition.Dependencies.Select(item => new ProjectManagementTaskTemplateDependencyCreateCommand(item.PredecessorNodeKey, item.SuccessorNodeKey, item.DependencyType, item.LagMinutes)).ToList();
        var created = await taskCommand.CreateTemplateAsync(ProjectManagementTaskTemplateCapability.Instance, request.ProjectId, commands, dependencies, cancellationToken);
        return new ProjectManagementTaskTemplateInstantiationResponse(template.Id, template.VersionNo, definition.Nodes.Select(item => created.Tasks[item.NodeKey]).ToList(), warnings);
    }

    private ProjectManagementProjectMemberEntity? ResolveAssignee(ProjectManagementTaskTemplateNodeDefinition node, IReadOnlyDictionary<string, string>? mappings, IReadOnlyList<ProjectManagementProjectMemberEntity> members, ProjectManagementProjectEntity project, List<ProjectManagementTaskTemplateInstantiationWarning> warnings)
    {
        if (string.IsNullOrWhiteSpace(node.DefaultRoleCode)) return null;
        if (mappings is null || !mappings.TryGetValue(node.DefaultRoleCode, out var userId) || string.IsNullOrWhiteSpace(userId)) { warnings.Add(new("role-assignee-unmapped", node.NodeKey, $"默认角色 {node.DefaultRoleCode} 未映射，任务保持未指派")); return null; }
        var member = members.FirstOrDefault(item => string.Equals(item.UserId, userId.Trim(), StringComparison.Ordinal));
        if (member is null && !string.Equals(project.OwnerUserId, userId.Trim(), StringComparison.Ordinal)) { warnings.Add(new("role-assignee-invalid", node.NodeKey, $"角色 {node.DefaultRoleCode} 的目标成员无效，任务保持未指派")); return null; }
        return member ?? new ProjectManagementProjectMemberEntity { UserId = userId.Trim() };
    }

    private static string? ResolveMilestone(ProjectManagementTaskTemplateNodeDefinition node, IReadOnlyDictionary<string, string>? mappings, IReadOnlyList<ProjectManagementMilestoneEntity> milestones, List<ProjectManagementTaskTemplateInstantiationWarning> warnings)
    {
        if (node.MilestoneKey is null) return null;
        if (mappings is null || !mappings.TryGetValue(node.MilestoneKey, out var targetId) || !milestones.Any(item => item.Id == targetId)) { warnings.Add(new("milestone-unmapped", node.NodeKey, "模板里程碑未映射到目标项目，任务不关联里程碑")); return null; }
        return targetId;
    }

    private static IReadOnlyList<string> ResolveLabels(ProjectManagementTaskTemplateNodeDefinition node, IReadOnlyList<ProjectManagementLabelEntity> labels, List<ProjectManagementTaskTemplateInstantiationWarning> warnings)
    {
        var resolved = new List<string>();
        foreach (var label in node.Labels)
        {
            var target = labels.FirstOrDefault(item => string.Equals(item.LabelName, label.LabelName, StringComparison.Ordinal) && string.Equals(item.Color, label.Color, StringComparison.OrdinalIgnoreCase));
            if (target is null) warnings.Add(new("label-unmapped", node.NodeKey, $"标签 {label.LabelName} 在目标项目不可用，未关联")); else resolved.Add(target.Id);
        }
        return resolved;
    }

    private static string AllocateCode(string sourceCode) => $"{sourceCode}-{Guid.NewGuid():N}"[..Math.Min(sourceCode.Length + 33, 128)];
    private string Tenant() => currentUser.GetAsterErpTenantId()!.Trim();
    private string App() => currentUser.GetAsterErpAppCode()!.Trim().ToUpperInvariant();
}
