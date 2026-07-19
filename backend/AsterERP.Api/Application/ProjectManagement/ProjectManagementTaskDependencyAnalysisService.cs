using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 甘特图依赖分析的应用服务。先在 ORM 和对象授权边界内取同项目快照，再委托纯 CPM 算法；该服务不执行任何排程写入。
/// </summary>
public sealed class ProjectManagementTaskDependencyAnalysisService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy) : IProjectManagementTaskDependencyAnalysisService, ITransientDependency
{
    public async Task<ProjectManagementTaskDependencyAnalysisResponse> AnalyzeAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadSnapshotAsync(projectId, null, cancellationToken);
        return ProjectManagementTaskDependencyAnalysisCalculator.Calculate(snapshot.Tasks, snapshot.Links, snapshot.Milestones, cancellationToken);
    }

    public async Task<ProjectManagementTaskDependencyImpactPreviewResponse> PreviewImpactAsync(string projectId, ProjectManagementTaskDependencyImpactPreviewRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.TaskId)) throw new ValidationException("需要选择要预览的任务");
        if (!request.ProposedStartDate.HasValue || !request.ProposedDueDate.HasValue) throw new ValidationException("预览需要同时提供开始和完成日期");
        if (request.ProposedDueDate < request.ProposedStartDate) throw new ValidationException("完成日期不能早于开始日期");

        var baselineSnapshot = await LoadSnapshotAsync(projectId, null, cancellationToken);
        var target = baselineSnapshot.Tasks.FirstOrDefault(item => string.Equals(item.TaskId, request.TaskId.Trim(), StringComparison.Ordinal));
        if (target is null) throw new ValidationException("任务不存在、已删除或不属于当前项目", ErrorCodes.PlatformResourceNotFound);
        var baseline = ProjectManagementTaskDependencyAnalysisCalculator.Calculate(baselineSnapshot.Tasks, baselineSnapshot.Links, baselineSnapshot.Milestones, cancellationToken);
        var previewTasks = baselineSnapshot.Tasks.Select(item => string.Equals(item.TaskId, target.TaskId, StringComparison.Ordinal)
            ? item with { PlannedStart = request.ProposedStartDate, PlannedFinish = request.ProposedDueDate }
            : item).ToList();
        var preview = ProjectManagementTaskDependencyAnalysisCalculator.Calculate(previewTasks, baselineSnapshot.Links, baselineSnapshot.Milestones, cancellationToken);
        return new ProjectManagementTaskDependencyImpactPreviewResponse(baseline, preview, BuildSuggestions(baseline, preview));
    }

    private async Task<AnalysisSnapshot> LoadSnapshotAsync(string projectId, ProjectManagementTaskDependencyImpactPreviewRequest? _, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        if (string.IsNullOrWhiteSpace(projectId)) throw new ValidationException("项目不能为空");
        await accessPolicy.EnsureCanViewProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var projectExists = await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!projectExists) throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);

        // 三个有界查询均走已注册的 ORM 数据权限过滤；不会因前端传入 TaskId 扩大数据范围。
        var tasks = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .OrderBy(item => item.Id)
            .Take(20_001)
            .ToListAsync(cancellationToken);
        if (tasks.Count > 20_000) throw new ValidationException("项目任务数量超过依赖分析上限 20000");
        var links = await db.Queryable<ProjectManagementTaskDependencyEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .OrderBy(item => item.Id)
            .Take(40_001)
            .ToListAsync(cancellationToken);
        if (links.Count > 40_000) throw new ValidationException("项目依赖数量超过分析上限 40000");
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>()
            .Where(item => item.ProjectId == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .OrderBy(item => item.Id)
            .Take(5_001)
            .ToListAsync(cancellationToken);
        if (milestones.Count > 5_000) throw new ValidationException("项目里程碑数量超过分析上限 5000");

        return new(
            tasks.Select(item => new ProjectManagementTaskDependencyAnalysisTaskInput(item.Id, item.Title, item.MilestoneId, item.StartDate, item.DueDate)).ToList(),
            links.Select(item => new ProjectManagementTaskDependencyAnalysisLinkInput(item.Id, item.PredecessorTaskId, item.SuccessorTaskId, item.DependencyType, item.LagMinutes)).ToList(),
            milestones.Select(item => new ProjectManagementTaskDependencyAnalysisMilestoneInput(item.Id, item.MilestoneName, item.DueDate)).ToList());
    }

    private static IReadOnlyList<ProjectManagementTaskDependencyScheduleSuggestion> BuildSuggestions(
        ProjectManagementTaskDependencyAnalysisResponse baseline,
        ProjectManagementTaskDependencyAnalysisResponse preview)
    {
        var baselineById = baseline.Tasks.ToDictionary(item => item.TaskId, StringComparer.Ordinal);
        return preview.Tasks.Where(item => item.EarliestStart.HasValue && item.EarliestFinish.HasValue && baselineById.TryGetValue(item.TaskId, out var original) && original.EarliestStart.HasValue && original.EarliestFinish.HasValue)
            .Select(item => new { Preview = item, Baseline = baselineById[item.TaskId] })
            .Where(item => item.Preview.EarliestStart!.Value > item.Baseline.EarliestStart!.Value)
            .OrderBy(item => item.Preview.EarliestStart)
            .ThenBy(item => item.Preview.TaskId, StringComparer.Ordinal)
            .Select(item => new ProjectManagementTaskDependencyScheduleSuggestion(item.Preview.TaskId, item.Preview.Title,
                item.Baseline.EarliestStart!.Value, item.Preview.EarliestStart!.Value, item.Baseline.EarliestFinish!.Value,
                item.Preview.EarliestFinish!.Value, (int)Math.Ceiling((item.Preview.EarliestStart.Value - item.Baseline.EarliestStart.Value).TotalMinutes)))
            .ToList();
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);

    private sealed record AnalysisSnapshot(
        IReadOnlyList<ProjectManagementTaskDependencyAnalysisTaskInput> Tasks,
        IReadOnlyList<ProjectManagementTaskDependencyAnalysisLinkInput> Links,
        IReadOnlyList<ProjectManagementTaskDependencyAnalysisMilestoneInput> Milestones);
}
