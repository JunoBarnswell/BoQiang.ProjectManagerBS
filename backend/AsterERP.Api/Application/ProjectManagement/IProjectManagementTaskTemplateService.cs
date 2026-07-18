using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskTemplateService
{
    Task<IReadOnlyList<ProjectManagementTaskTemplateResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskTemplateResponse> CreateAsync(string projectId, ProjectManagementTaskTemplateUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskTemplateResponse> CreateFromTaskAsync(string projectId, ProjectManagementTaskTemplateCreateFromTaskRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskTemplateResponse> UpdateAsync(string projectId, string id, ProjectManagementTaskTemplateUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskTemplateInstantiationResponse> InstantiateAsync(string templateId, ProjectManagementTaskTemplateInstantiateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementTaskResponse>> ApplyAsync(string templateId, ProjectManagementTaskTemplateApplyRequest request, CancellationToken cancellationToken = default);
}
