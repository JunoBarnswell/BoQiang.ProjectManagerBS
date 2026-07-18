using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementWorkspaceValidationExecutor(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IProjectManagementOperationWriter operationWriter)
{
    public async Task ExecuteAsync(ProjectManagementOperationJobArgs args, CancellationToken cancellationToken = default)
    {
        try
        {
            await operationWriter.StartAsync(args.OperationId, "maintenance.workspace-validation", "{}", args.TraceId, cancellationToken);
            var db = databaseAccessor.GetCurrentDb();
            if (!await operationWriter.ReportProgressAsync(args.OperationId, "ValidatingProjects", 10, cancellationToken)) return;
            var projectCount = await db.Queryable<ProjectManagementProjectEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);

            if (!await operationWriter.ReportProgressAsync(args.OperationId, "ValidatingTasks", 40, cancellationToken)) return;
            var taskCount = await db.Queryable<ProjectManagementTaskEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);

            if (!await operationWriter.ReportProgressAsync(args.OperationId, "ValidatingActivities", 70, cancellationToken)) return;
            var activityCount = await db.Queryable<ProjectManagementActivityEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);

            if (!await operationWriter.ReportProgressAsync(args.OperationId, "Finalizing", 90, cancellationToken)) return;
            var impact = JsonSerializer.Serialize(new { projectCount, taskCount, activityCount, validatedAt = DateTime.UtcNow });
            await operationWriter.CompleteWithImpactAsync(args.OperationId, impact, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await operationWriter.RequestCancellationAsync(args.OperationId, CancellationToken.None);
            await operationWriter.CancelAsync(args.OperationId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            await operationWriter.FailAsync(args.OperationId, exception.Message, CancellationToken.None);
        }
    }

}
