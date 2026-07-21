using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 任务模板只保存可复用的计划快照。任务实体、标签关联和依赖边的实际创建由
/// <see cref="IProjectManagementTaskTemplateInstantiationService"/> 编排，避免绕过各聚合既有规则。
/// </summary>
public sealed class ProjectManagementTaskTemplateService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    ProjectManagementTaskHierarchy? taskHierarchy = null,
    IProjectManagementTaskTemplateInstantiationService? instantiationService = null) : IProjectManagementTaskTemplateService
{
    private const int DefinitionSchemaVersion = 1;
    private const int MaxTemplateNodeCount = 1000;

    public async Task<IReadOnlyList<ProjectManagementTaskTemplateResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await Policy().EnsureCanViewProjectAsync(projectId, cancellationToken);
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskTemplateEntity>()
                .Where(item => item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted && (item.ProjectId == null || item.ProjectId == projectId))
                .OrderBy(item => item.TemplateName)
                .ToListAsync(cancellationToken))
            .Select(Map)
            .ToList();
    }

    public async Task<ProjectManagementTaskTemplateResponse> CreateAsync(
        string projectId,
        ProjectManagementTaskTemplateUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(projectId, cancellationToken: cancellationToken);
        var definition = ProjectManagementTaskTemplateDefinitionSerializer.Parse(request.DefinitionJson);
        return await CreateSnapshotAsync(projectId, request.TemplateCode, request.TemplateName, definition, request.IsGlobal,
            request.RecurrenceExpression, cancellationToken);
    }

    public async Task<ProjectManagementTaskTemplateResponse> CreateFromTaskAsync(
        string projectId,
        ProjectManagementTaskTemplateCreateFromTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(projectId, cancellationToken: cancellationToken);
        var sourceTaskId = Required(request.SourceTaskId, "源任务不能为空");
        var db = databaseAccessor.GetCurrentDb();
        var root = (await db.Queryable<ProjectManagementTaskEntity>()
                .Where(item => item.Id == sourceTaskId && item.TenantId == Tenant() && item.AppCode == App() && item.ProjectId == projectId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("源任务不存在", ErrorCodes.PlatformResourceNotFound);

        var subtree = (await Hierarchy().LoadSubtreeAsync(db, projectId, root.Id, cancellationToken))
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Depth)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.CreatedTime)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToList();
        if (subtree.Count == 0) throw new ValidationException("任务模板至少需要一个有效任务");

        var taskIds = subtree.Select(item => item.Id).ToList();
        var memberRoles = await LoadMemberRolesAsync(projectId, cancellationToken);
        var labelsByTask = await LoadLabelsAsync(projectId, taskIds, cancellationToken);
        var milestones = await LoadMilestonesAsync(projectId, subtree, cancellationToken);
        var nodeKeys = subtree.Select((item, index) => new { item.Id, Key = $"node-{index + 1}" })
            .ToDictionary(item => item.Id, item => item.Key, StringComparer.Ordinal);
        var nodes = subtree.Select(task => new ProjectManagementTaskTemplateNodeDefinition(
            nodeKeys[task.Id],
            task.TaskCode,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            ResolveRoleCode(task, memberRoles),
            labelsByTask.GetValueOrDefault(task.Id, []),
            DefaultDurationDays(task.StartDate, task.DueDate),
            task.MilestoneId is null ? null : MilestoneKey(task.MilestoneId),
            task.ParentTaskId is not null && nodeKeys.TryGetValue(task.ParentTaskId, out var parentKey) ? parentKey : null,
            task.Weight,
            task.SortOrder)).ToList();

        var dependencies = await LoadTemplateDependenciesAsync(projectId, taskIds, nodeKeys, cancellationToken);
        var definition = new ProjectManagementTaskTemplateDefinition(DefinitionSchemaVersion, nodes, milestones, dependencies);
        return await CreateSnapshotAsync(projectId, request.TemplateCode, request.TemplateName, definition, request.IsGlobal,
            request.RecurrenceExpression, cancellationToken);
    }

    public async Task<ProjectManagementTaskTemplateResponse> UpdateAsync(
        string projectId,
        string id,
        ProjectManagementTaskTemplateUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(projectId, cancellationToken: cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var scopeProjectId = request.IsGlobal ? null : projectId;
        var entity = (await db.Queryable<ProjectManagementTaskTemplateEntity>()
                .Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.ProjectId == scopeProjectId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("任务模板不存在", ErrorCodes.PlatformResourceNotFound);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var definition = ProjectManagementTaskTemplateDefinitionSerializer.Parse(request.DefinitionJson);
        var code = Required(request.TemplateCode, "模板编码不能为空");
        if (await db.Queryable<ProjectManagementTaskTemplateEntity>()
            .AnyAsync(item => item.Id != entity.Id && item.TenantId == Tenant() && item.AppCode == App() && item.ProjectId == scopeProjectId && item.TemplateCode == code && !item.IsDeleted, cancellationToken))
            throw new ValidationException("模板编码已存在");
        entity.TemplateCode = code;
        entity.TemplateName = Required(request.TemplateName, "模板名称不能为空");
        entity.DefinitionJson = ProjectManagementTaskTemplateDefinitionSerializer.Serialize(definition);
        entity.RecurrenceExpression = Optional(request.RecurrenceExpression);
        entity.VersionNo++;
        entity.UpdatedBy = User();
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementTaskTemplateInstantiationResponse> InstantiateAsync(
        string templateId,
        ProjectManagementTaskTemplateInstantiateRequest request,
        CancellationToken cancellationToken = default)
    {
        var targetProjectId = Required(request.ProjectId, "目标项目不能为空");
        await EnsureProjectAsync(targetProjectId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(targetProjectId, cancellationToken: cancellationToken);
        var template = await GetTemplateAsync(templateId, cancellationToken);
        var definition = ProjectManagementTaskTemplateDefinitionSerializer.Parse(template.DefinitionJson);
        if (instantiationService is null)
            throw new ValidationException("任务模板实例化服务未配置");
        return await instantiationService.InstantiateAsync(Map(template), definition, request with { ProjectId = targetProjectId }, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectManagementTaskResponse>> ApplyAsync(
        string templateId,
        ProjectManagementTaskTemplateApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await InstantiateAsync(templateId,
            new ProjectManagementTaskTemplateInstantiateRequest(request.ProjectId, request.OccurrenceDate, ClientRequestId: request.OccurrenceKey),
            cancellationToken);
        return result.Tasks;
    }

    private async Task<ProjectManagementTaskTemplateResponse> CreateSnapshotAsync(
        string sourceProjectId,
        string templateCode,
        string templateName,
        ProjectManagementTaskTemplateDefinition definition,
        bool isGlobal,
        string? recurrenceExpression,
        CancellationToken cancellationToken)
    {
        ProjectManagementTaskTemplateDefinitionSerializer.Validate(definition);
        var db = databaseAccessor.GetCurrentDb();
        var code = Required(templateCode, "模板编码不能为空");
        var targetProjectId = isGlobal ? null : sourceProjectId;
        if (await db.Queryable<ProjectManagementTaskTemplateEntity>()
            .AnyAsync(item => item.TenantId == Tenant() && item.AppCode == App() && item.ProjectId == targetProjectId && item.TemplateCode == code && !item.IsDeleted, cancellationToken))
            throw new ValidationException("模板编码已存在");
        var entity = new ProjectManagementTaskTemplateEntity
        {
            TenantId = Tenant(),
            AppCode = App(),
            ProjectId = targetProjectId,
            TemplateCode = code,
            TemplateName = Required(templateName, "模板名称不能为空"),
            DefinitionJson = ProjectManagementTaskTemplateDefinitionSerializer.Serialize(definition),
            RecurrenceExpression = Optional(recurrenceExpression),
            VersionNo = 1,
            CreatedBy = User(),
            CreatedTime = DateTime.UtcNow
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    private async Task<ProjectManagementTaskTemplateEntity> GetTemplateAsync(string id, CancellationToken cancellationToken)
    {
        var templateId = Required(id, "任务模板不能为空");
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskTemplateEntity>()
                .Where(item => item.Id == templateId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("任务模板不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<Dictionary<string, string>> LoadMemberRolesAsync(string projectId, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == Tenant() && item.AppCode == App() && item.IsActive && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        return rows.GroupBy(item => item.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().RoleCode, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, IReadOnlyList<ProjectManagementTaskTemplateLabelDefinition>>> LoadLabelsAsync(
        string projectId,
        IReadOnlyCollection<string> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0) return [];
        var db = databaseAccessor.GetCurrentDb();
        var links = await db.Queryable<ProjectManagementTaskLabelEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == Tenant() && item.AppCode == App() && taskIds.Contains(item.TaskId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var labelIds = links.Select(item => item.LabelId).Distinct(StringComparer.Ordinal).ToList();
        var labels = labelIds.Count == 0 ? [] : await db.Queryable<ProjectManagementLabelEntity>()
            .Where(item => labelIds.Contains(item.Id) && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var labelById = labels.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return links.GroupBy(item => item.TaskId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectManagementTaskTemplateLabelDefinition>)group
                    .Where(link => labelById.ContainsKey(link.LabelId))
                    .Select(link => labelById[link.LabelId])
                    .OrderBy(label => label.LabelName, StringComparer.Ordinal)
                    .Select(label => new ProjectManagementTaskTemplateLabelDefinition($"label:{label.LabelName}:{label.Color}", label.LabelName, label.Color))
                    .ToList(),
                StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<ProjectManagementTaskTemplateMilestoneDefinition>> LoadMilestonesAsync(
        string projectId,
        IReadOnlyCollection<ProjectManagementTaskEntity> tasks,
        CancellationToken cancellationToken)
    {
        var milestoneIds = tasks.Where(item => !string.IsNullOrWhiteSpace(item.MilestoneId)).Select(item => item.MilestoneId!).Distinct(StringComparer.Ordinal).ToList();
        if (milestoneIds.Count == 0) return [];
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementMilestoneEntity>()
                .Where(item => item.ProjectId == projectId && item.TenantId == Tenant() && item.AppCode == App() && milestoneIds.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.MilestoneName, StringComparer.Ordinal)
            .Select(item => new ProjectManagementTaskTemplateMilestoneDefinition(MilestoneKey(item.Id), item.MilestoneName))
            .ToList();
    }

    private async Task<IReadOnlyList<ProjectManagementTaskTemplateDependencyDefinition>> LoadTemplateDependenciesAsync(
        string projectId,
        IReadOnlyCollection<string> taskIds,
        IReadOnlyDictionary<string, string> nodeKeys,
        CancellationToken cancellationToken)
    {
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskDependencyEntity>()
                .Where(item => item.ProjectId == projectId && item.TenantId == Tenant() && item.AppCode == App() && taskIds.Contains(item.PredecessorTaskId) && taskIds.Contains(item.SuccessorTaskId) && !item.IsDeleted)
                .ToListAsync(cancellationToken))
            .OrderBy(item => item.CreatedTime)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new ProjectManagementTaskTemplateDependencyDefinition(nodeKeys[item.PredecessorTaskId], nodeKeys[item.SuccessorTaskId], item.DependencyType, item.LagMinutes))
            .ToList();
    }

    private async Task EnsureProjectAsync(string id, CancellationToken cancellationToken)
    {
        var projectId = Required(id, "项目不能为空");
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
                .Where(item => item.Id == projectId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
                .AnyAsync(cancellationToken))
            throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private ProjectManagementTaskHierarchy Hierarchy() => taskHierarchy ?? new ProjectManagementTaskHierarchy();
    private static string? ResolveRoleCode(ProjectManagementTaskEntity task, IReadOnlyDictionary<string, string> memberRoles) =>
        task.AssigneeUserId is not null && memberRoles.TryGetValue(task.AssigneeUserId, out var role) ? role : null;
    private static int? DefaultDurationDays(DateTime? startDate, DateTime? dueDate) =>
        startDate.HasValue && dueDate.HasValue ? Math.Max(0, (dueDate.Value.Date - startDate.Value.Date).Days) : null;
    private static string MilestoneKey(string milestoneId) => "milestone:" + milestoneId;
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long request) { if (request <= 0 || current != request) throw new ValidationException("模板已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static ProjectManagementTaskTemplateResponse Map(ProjectManagementTaskTemplateEntity entity) => new(entity.Id, entity.ProjectId, entity.ProjectId is null ? ProjectManagementTaskTemplateScopes.Global : ProjectManagementTaskTemplateScopes.Project, entity.TemplateCode, entity.TemplateName, entity.DefinitionJson, entity.RecurrenceExpression, entity.VersionNo);
}

public static class ProjectManagementTaskTemplateDefinitionSerializer
{
    public static ProjectManagementTaskTemplateDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ValidationException("模板定义不能为空");
        try
        {
            var definition = JsonSerializer.Deserialize<ProjectManagementTaskTemplateDefinition>(json);
            if (definition is not null && definition.Nodes is not null)
            {
                Validate(definition);
                return definition;
            }
        }
        catch (JsonException)
        {
            // 兼容旧版扁平节点，下一次更新模板会写为结构化快照。
        }

        try
        {
            var legacyNodes = JsonSerializer.Deserialize<List<LegacyTemplateNode>>(json) ?? throw new ValidationException("模板定义不能为空");
            var definition = new ProjectManagementTaskTemplateDefinition(1,
                legacyNodes.Select((node, index) => new ProjectManagementTaskTemplateNodeDefinition(
                    $"node-{index + 1}", node.TaskCode, node.Title, null, "Todo", node.Priority, null, [],
                    node.DueDays, null, string.IsNullOrWhiteSpace(node.ParentCode) ? null : ResolveLegacyParentKey(legacyNodes, node.ParentCode), node.Weight, (index + 1) * 1024)).ToList(),
                [], []);
            Validate(definition);
            return definition;
        }
        catch (JsonException)
        {
            throw new ValidationException("模板定义不是有效 JSON");
        }
    }

    public static string Serialize(ProjectManagementTaskTemplateDefinition definition)
    {
        Validate(definition);
        return JsonSerializer.Serialize(definition);
    }

    public static void Validate(ProjectManagementTaskTemplateDefinition definition)
    {
        if (definition.SchemaVersion != 1) throw new ValidationException("任务模板定义版本不受支持");
        if (definition.Nodes is null || definition.Nodes.Count is < 1 or > 1000) throw new ValidationException("模板任务数量必须在 1 到 1000 之间");
        var nodeByKey = new Dictionary<string, ProjectManagementTaskTemplateNodeDefinition>(StringComparer.Ordinal);
        foreach (var node in definition.Nodes)
        {
            var nodeKey = Required(node.NodeKey, "模板节点标识不能为空");
            if (!nodeByKey.TryAdd(nodeKey, node)) throw new ValidationException("模板节点标识不能重复");
            _ = Required(node.TaskCode, "模板任务编码不能为空");
            _ = Required(node.Title, "模板任务标题不能为空");
            _ = ProjectManagementDomainRules.RequireTaskStatus(node.Status);
            if (!new[] { "Low", "Medium", "High", "Urgent" }.Contains(node.Priority, StringComparer.Ordinal))
                throw new ValidationException("模板任务优先级不受支持");
            if (node.Weight <= 0 || node.DefaultDurationDays < 0 || node.SortOrder < 0)
                throw new ValidationException("模板任务权重、默认工期或排序无效");
            if (node.Labels is null || node.Labels.Any(label => string.IsNullOrWhiteSpace(label.LabelKey) || string.IsNullOrWhiteSpace(label.LabelName) || string.IsNullOrWhiteSpace(label.Color)))
                throw new ValidationException("模板标签无效");
            if (node.Labels.Select(label => label.LabelKey).Distinct(StringComparer.Ordinal).Count() != node.Labels.Count)
                throw new ValidationException("模板节点标签不能重复");
        }
        foreach (var node in definition.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.ParentNodeKey)) continue;
            if (!nodeByKey.ContainsKey(node.ParentNodeKey) || string.Equals(node.NodeKey, node.ParentNodeKey, StringComparison.Ordinal))
                throw new ValidationException("模板包含不存在或无效的父任务");
        }
        EnsureAcyclic(nodeByKey);
        var milestoneKeys = (definition.Milestones ?? []).Select(item => Required(item.MilestoneKey, "模板里程碑标识不能为空")).ToHashSet(StringComparer.Ordinal);
        if (milestoneKeys.Count != (definition.Milestones ?? []).Count) throw new ValidationException("模板里程碑标识不能重复");
        if (definition.Nodes.Any(node => node.MilestoneKey is not null && !milestoneKeys.Contains(node.MilestoneKey))) throw new ValidationException("模板任务引用了不存在的里程碑");
        var dependencyPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependency in definition.Dependencies ?? [])
        {
            if (!nodeByKey.ContainsKey(dependency.PredecessorNodeKey) || !nodeByKey.ContainsKey(dependency.SuccessorNodeKey) ||
                string.Equals(dependency.PredecessorNodeKey, dependency.SuccessorNodeKey, StringComparison.Ordinal) || dependency.LagMinutes < 0)
                throw new ValidationException("模板任务依赖无效");
            _ = ProjectManagementDomainRules.RequireDependencyType(dependency.DependencyType);
            if (!dependencyPairs.Add(dependency.PredecessorNodeKey + "\u001f" + dependency.SuccessorNodeKey)) throw new ValidationException("模板任务依赖不能重复");
        }
    }

    private static void EnsureAcyclic(IReadOnlyDictionary<string, ProjectManagementTaskTemplateNodeDefinition> nodeByKey)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodeByKey.Values) Visit(node.NodeKey);
        return;
        void Visit(string nodeKey)
        {
            if (visited.Contains(nodeKey)) return;
            if (!visiting.Add(nodeKey)) throw new ValidationException("模板任务树不能形成循环");
            var parent = nodeByKey[nodeKey].ParentNodeKey;
            if (!string.IsNullOrWhiteSpace(parent)) Visit(parent);
            visiting.Remove(nodeKey);
            visited.Add(nodeKey);
        }
    }

    private static string? ResolveLegacyParentKey(IReadOnlyList<LegacyTemplateNode> nodes, string? parentCode)
    {
        if (string.IsNullOrWhiteSpace(parentCode)) return null;
        var index = nodes.Select((node, index) => new { node, index }).FirstOrDefault(item => string.Equals(item.node.TaskCode, parentCode, StringComparison.Ordinal))?.index;
        return index is null ? parentCode : $"node-{index.Value + 1}";
    }

    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private sealed record LegacyTemplateNode(string TaskCode, string Title, string? ParentCode = null, string Priority = "Medium", int DueDays = 0, decimal Weight = 1);
}
