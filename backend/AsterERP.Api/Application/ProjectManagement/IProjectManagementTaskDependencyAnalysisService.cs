using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskDependencyAnalysisService
{
    Task<ProjectManagementTaskDependencyAnalysisResponse> AnalyzeAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDependencyImpactPreviewResponse> PreviewImpactAsync(string projectId, ProjectManagementTaskDependencyImpactPreviewRequest request, CancellationToken cancellationToken = default);
}
