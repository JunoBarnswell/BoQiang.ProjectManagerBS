namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementTaskRecurrenceGenerationJobArgs(
    string RecurrenceId,
    string TenantId,
    string AppCode,
    string SeriesOwnerUserId);
