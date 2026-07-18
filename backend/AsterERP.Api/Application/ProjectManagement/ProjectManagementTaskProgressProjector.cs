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
        var db = databaseAccessor.GetCurrentDb();
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == projectId).ToListAsync(cancellationToken);
        var snapshot = ProjectManagementTaskProgressCalculator.Create(tasks);
        foreach (var task in tasks.Where(task => snapshot.ParentProgressByTaskId.TryGetValue(task.Id, out var progress) && task.ProgressPercent != progress))
        {
            task.ProgressPercent = snapshot.ParentProgressByTaskId[task.Id];
            await db.Updateable(task).UpdateColumns(item => new { item.ProgressPercent }).ExecuteCommandAsync(cancellationToken);
        }

        var project = (await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (project is not null && project.ProgressPercent != snapshot.ProjectProgressPercent)
        {
            project.ProgressPercent = snapshot.ProjectProgressPercent;
            await db.Updateable(project).UpdateColumns(item => new { item.ProgressPercent }).ExecuteCommandAsync(cancellationToken);
        }

        var milestones = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted).ToListAsync(cancellationToken);
        foreach (var milestone in milestones)
        {
            var progress = snapshot.GetMilestoneProgress(milestone.Id);
            if (milestone.ProgressPercent == progress) continue;
            milestone.ProgressPercent = progress;
            await db.Updateable(milestone).UpdateColumns(item => new { item.ProgressPercent }).ExecuteCommandAsync(cancellationToken);
        }
    }
}
