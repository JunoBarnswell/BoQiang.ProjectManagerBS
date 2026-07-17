namespace AsterERP.Api.Application.Ai.Flowise;

public sealed record FlowiseScheduleExecutionJobArgs(
    string ScheduleRecordId,
    string TenantId,
    string AppCode,
    string OwnerUserId);
