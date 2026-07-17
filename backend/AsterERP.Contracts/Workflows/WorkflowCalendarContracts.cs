namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowWorkCalendarResponse(
    string Id,
    string TenantId,
    string AppCode,
    DateTime CalendarDate,
    string DayType,
    bool IsWorkingDay,
    string CalendarName,
    string? Remark,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record WorkflowWorkCalendarUpsertRequest(
    string? Id,
    string? TenantId,
    string? AppCode,
    DateTime CalendarDate,
    string DayType,
    bool? IsWorkingDay,
    string CalendarName,
    string? Remark);
