using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskRecurrenceService
{
    Task<IReadOnlyList<ProjectManagementTaskRecurrenceResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskRecurrenceResponse> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskRecurrenceResponse> CreateAsync(string projectId, ProjectManagementTaskRecurrenceCreateRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskRecurrenceResponse> UpdateAsync(string id, ProjectManagementTaskRecurrenceUpdateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementTaskRecurrenceOccurrenceResponse>> QueryOccurrencesAsync(string id, CancellationToken cancellationToken = default);
    Task EditOccurrenceAsync(string recurrenceId, string occurrenceId, ProjectManagementTaskRecurrenceOccurrenceEditRequest request, CancellationToken cancellationToken = default);
    Task DeleteOccurrenceAsync(string recurrenceId, string occurrenceId, ProjectManagementTaskRecurrenceOccurrenceDeleteRequest request, CancellationToken cancellationToken = default);
    Task GenerateAsync(ProjectManagementTaskRecurrenceGenerationJobArgs args, CancellationToken cancellationToken = default);
}
