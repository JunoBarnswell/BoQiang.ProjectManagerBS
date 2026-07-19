using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;
using Volo.Abp.DependencyInjection;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskProgressProjector(IWorkspaceDatabaseAccessor databaseAccessor)
    : IProjectManagementTaskProgressProjector, ITransientDependency
{
    public async Task RefreshAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted).ToListAsync(cancellationToken);
        var leaves = tasks.Where(task => !tasks.Any(child => child.ParentTaskId == task.Id)).ToList();
        var progress = Calculate(leaves);
        var project = (await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (project is not null && project.ProgressPercent != progress) { project.ProgressPercent = progress; await db.Updateable(project).UpdateColumns(item => new { item.ProgressPercent }).ExecuteCommandAsync(cancellationToken); }
        var milestoneIds = tasks.Where(task => !string.IsNullOrWhiteSpace(task.MilestoneId)).Select(task => task.MilestoneId!).Distinct(StringComparer.Ordinal).ToList();
        if (milestoneIds.Count == 0) return;
        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => milestoneIds.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        foreach (var milestone in milestones) { var value = Calculate(leaves.Where(task => task.MilestoneId == milestone.Id).ToList()); if (milestone.ProgressPercent != value) { milestone.ProgressPercent = value; await db.Updateable(milestone).UpdateColumns(item => new { item.ProgressPercent }).ExecuteCommandAsync(cancellationToken); } }
    }
    private static decimal Calculate(IReadOnlyList<ProjectManagementTaskEntity> tasks) { var weight = tasks.Sum(task => task.Weight); return weight <= 0 ? 0 : Math.Round(tasks.Sum(task => task.ProgressPercent * task.Weight) / weight, 2); }
}
