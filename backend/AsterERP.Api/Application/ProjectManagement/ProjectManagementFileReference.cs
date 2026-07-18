namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementFileReference(
    string FileId,
    string? BlobId = null);
