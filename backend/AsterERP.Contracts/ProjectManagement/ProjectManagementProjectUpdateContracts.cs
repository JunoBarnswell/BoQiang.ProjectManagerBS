namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementProjectUpdateRequest(
    string Body,
    string? ClientMutationId = null);
