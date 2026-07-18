using Volo.Abp.BackgroundJobs;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementOperationJob(
    ProjectManagementOperationRunner runner) : IAsyncBackgroundJob<ProjectManagementOperationJobArgs>
{
    public Task ExecuteAsync(ProjectManagementOperationJobArgs args) => runner.ExecuteAsync(args);
}
