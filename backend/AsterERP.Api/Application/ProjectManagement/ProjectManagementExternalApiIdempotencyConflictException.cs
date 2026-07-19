namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementExternalApiIdempotencyConflictException(string message) : Exception(message);
