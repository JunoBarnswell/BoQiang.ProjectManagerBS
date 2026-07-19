using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskTemplateService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser, ProjectManagementAccessPolicy? accessPolicy = null) : IProjectManagementTaskTemplateService
{
    public async Task<IReadOnlyList<ProjectManagementTaskTemplateResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        return (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskTemplateEntity>().Where(item => !item.IsDeleted && (item.ProjectId == null || item.ProjectId == projectId)).OrderBy(item => item.TemplateName).ToListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskTemplateResponse> CreateAsync(string projectId, ProjectManagementTaskTemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(projectId, cancellationToken: cancellationToken);
        ValidateDefinition(request.DefinitionJson);
        var db = databaseAccessor.GetProjectManagementDb();
        var code = Required(request.TemplateCode, "模板编码不能为空");
        if (await db.Queryable<ProjectManagementTaskTemplateEntity>().AnyAsync(item => item.ProjectId == projectId && item.TemplateCode == code && !item.IsDeleted, cancellationToken)) throw new ValidationException("模板编码已存在");
        var entity = new ProjectManagementTaskTemplateEntity { TenantId = Tenant(), AppCode = App(), ProjectId = projectId, TemplateCode = code, TemplateName = Required(request.TemplateName, "模板名称不能为空"), DefinitionJson = request.DefinitionJson, RecurrenceExpression = Optional(request.RecurrenceExpression), CreatedBy = User(), CreatedTime = DateTime.UtcNow };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementTaskTemplateResponse> UpdateAsync(string projectId, string id, ProjectManagementTaskTemplateUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(projectId, cancellationToken: cancellationToken);
        var db = databaseAccessor.GetProjectManagementDb();
        var entity = (await db.Queryable<ProjectManagementTaskTemplateEntity>().Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务模板不存在", ErrorCodes.PlatformResourceNotFound);
        EnsureVersion(entity.VersionNo, request.VersionNo); ValidateDefinition(request.DefinitionJson);
        entity.TemplateCode = Required(request.TemplateCode, "模板编码不能为空"); entity.TemplateName = Required(request.TemplateName, "模板名称不能为空"); entity.DefinitionJson = request.DefinitionJson; entity.RecurrenceExpression = Optional(request.RecurrenceExpression); entity.VersionNo++; entity.UpdatedBy = User(); entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<ProjectManagementTaskResponse>> ApplyAsync(string templateId, ProjectManagementTaskTemplateApplyRequest request, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var template = (await db.Queryable<ProjectManagementTaskTemplateEntity>().Where(item => item.Id == templateId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务模板不存在", ErrorCodes.PlatformResourceNotFound);
        await EnsureProjectAsync(request.ProjectId, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageTaskAsync(request.ProjectId, cancellationToken: cancellationToken);
        var nodes = ParseDefinition(template.DefinitionJson);
        var occurrenceKey = Required(request.OccurrenceKey, "OccurrenceKey 不能为空");
        if (await db.Queryable<ProjectManagementTaskOccurrenceEntity>().AnyAsync(item => item.TemplateId == templateId && item.ProjectId == request.ProjectId && item.OccurrenceKey == occurrenceKey && !item.IsDeleted, cancellationToken))
            return await FindOccurrenceTasksAsync(templateId, request.ProjectId, occurrenceKey, cancellationToken);
        var created = new List<ProjectManagementTaskEntity>();
        var byCode = new Dictionary<string, ProjectManagementTaskEntity>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var parent = string.IsNullOrWhiteSpace(node.ParentCode) ? null : byCode.GetValueOrDefault(node.ParentCode!);
            if (!string.IsNullOrWhiteSpace(node.ParentCode) && parent is null) throw new ValidationException("模板父任务编码不存在");
            var task = new ProjectManagementTaskEntity { TenantId = Tenant(), AppCode = App(), ProjectId = request.ProjectId, ParentTaskId = parent?.Id, TaskCode = $"{node.TaskCode}-{occurrenceKey}", OccurrenceKey = occurrenceKey, Title = node.Title, Status = ProjectManagementDomainRules.TaskTodo, Priority = node.Priority, DueDate = request.OccurrenceDate.AddDays(node.DueDays), ProgressPercent = 0, Weight = node.Weight, Depth = parent is null ? 0 : parent.Depth + 1, CreatedBy = User(), CreatedTime = DateTime.UtcNow };
            ProjectManagementDomainRules.EnsureTaskDepth(task.Depth);
            created.Add(task); byCode.Add(node.TaskCode, task);
        }
        db.Ado.BeginTran();
        try
        {
            if (created.Count > 0) await db.Insertable(created).ExecuteCommandAsync(cancellationToken);
            var occurrence = new ProjectManagementTaskOccurrenceEntity { TenantId = Tenant(), AppCode = App(), TemplateId = templateId, ProjectId = request.ProjectId, OccurrenceKey = occurrenceKey, OccurrenceDate = request.OccurrenceDate, RootTaskId = created.FirstOrDefault()?.Id ?? string.Empty, CreatedBy = User(), CreatedTime = DateTime.UtcNow };
            await db.Insertable(occurrence).ExecuteCommandAsync(cancellationToken);
            db.Ado.CommitTran();
        }
        catch { db.Ado.RollbackTran(); throw; }
        return created.Select(item => Map(item)).ToList();
    }

    private async Task<IReadOnlyList<ProjectManagementTaskResponse>> FindOccurrenceTasksAsync(string templateId, string projectId, string occurrenceKey, CancellationToken cancellationToken)
    {
        _ = (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskOccurrenceEntity>().Where(item => item.TemplateId == templateId && item.ProjectId == projectId && item.OccurrenceKey == occurrenceKey && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).First();
        var tasks = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId && item.OccurrenceKey == occurrenceKey && !item.IsDeleted).ToListAsync(cancellationToken);
        return tasks.Select(Map).ToList();
    }

    private static List<TemplateNode> ParseDefinition(string json) { ValidateDefinition(json); return JsonSerializer.Deserialize<List<TemplateNode>>(json) ?? []; }
    private static void ValidateDefinition(string json)
    {
        List<TemplateNode> nodes;
        try { nodes = JsonSerializer.Deserialize<List<TemplateNode>>(json) ?? throw new ValidationException("模板定义不能为空"); } catch (JsonException) { throw new ValidationException("模板定义不是有效 JSON"); }
        if (nodes.Count == 0 || nodes.Count > 1000) throw new ValidationException("模板任务数量必须在 1 到 1000 之间");
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes) { if (string.IsNullOrWhiteSpace(node.TaskCode) || string.IsNullOrWhiteSpace(node.Title) || !codes.Add(node.TaskCode)) throw new ValidationException("模板任务编码不能为空且不能重复"); if (!string.IsNullOrWhiteSpace(node.ParentCode) && node.ParentCode == node.TaskCode) throw new ValidationException("模板不能自引用"); if (node.Weight <= 0 || node.DueDays < 0) throw new ValidationException("模板任务权重或日期偏移无效"); }
        if (nodes.Any(node => !string.IsNullOrWhiteSpace(node.ParentCode) && !codes.Contains(node.ParentCode!))) throw new ValidationException("模板包含不存在的父任务");
    }

    private sealed record TemplateNode(string TaskCode, string Title, string? ParentCode = null, string Priority = "Medium", int DueDays = 0, decimal Weight = 1);
    private async Task EnsureProjectAsync(string id, CancellationToken token) { if (!await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == id && !item.IsDeleted).AnyAsync(token)) throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound); Tenant(); App(); }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private static string App() => ProjectManagementPlatformScope.AppCode;
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long request) { if (request <= 0 || current != request) throw new ValidationException("模板已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static ProjectManagementTaskTemplateResponse Map(ProjectManagementTaskTemplateEntity entity) => new(entity.Id, entity.ProjectId, entity.TemplateCode, entity.TemplateName, entity.DefinitionJson, entity.RecurrenceExpression, entity.VersionNo);
    private static ProjectManagementTaskResponse Map(ProjectManagementTaskEntity entity) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Description, entity.Status, entity.Priority, entity.AssigneeUserId, entity.AssigneeEmploymentId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.Weight, entity.EstimateMinutes, entity.ActualMinutes, entity.SortOrder, entity.Depth, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime);
}
