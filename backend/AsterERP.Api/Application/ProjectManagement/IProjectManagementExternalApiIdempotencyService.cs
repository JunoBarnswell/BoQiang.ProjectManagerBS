namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementExternalApiIdempotencyService
{
    Task<ProjectManagementExternalApiIdempotencyResult<T>> ExecuteAsync<T>(
        ProjectManagementExternalApiIdempotencyRequest request,
        Func<CancellationToken, Task<T>> action,
        Func<T, ProjectManagementExternalApiResource> resourceSelector,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectManagementExternalApiIdempotencyRequest(
    string Operation,
    string IdempotencyKey,
    string RequestHash,
    string Source,
    string TraceId);

public sealed record ProjectManagementExternalApiIdempotencyResult<T>(T Result, bool Replayed);

public sealed record ProjectManagementExternalApiResource(string ProjectId, string AggregateType, string AggregateId);
