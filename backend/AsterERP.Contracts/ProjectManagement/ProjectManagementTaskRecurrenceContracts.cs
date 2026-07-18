namespace AsterERP.Contracts.ProjectManagement;

/// <summary>重复任务的频率。Custom 通过 <see cref="ProjectManagementTaskRecurrenceRuleRequest.CustomUnit"/> 指定实际单位。</summary>
public static class ProjectManagementTaskRecurrenceFrequencies
{
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";
    public const string Custom = "Custom";
}

public static class ProjectManagementTaskRecurrenceScopes
{
    public const string ThisOccurrence = "ThisOccurrence";
    public const string ThisAndFuture = "ThisAndFuture";
    public const string EntireSeries = "EntireSeries";
}

public sealed record ProjectManagementTaskRecurrenceRuleRequest(
    string Frequency,
    DateTime StartAtLocal,
    string TimeZoneId,
    int Interval = 1,
    IReadOnlyList<DayOfWeek>? DaysOfWeek = null,
    int? DayOfMonth = null,
    string? CustomUnit = null,
    DateTime? EndsAtLocal = null,
    int? GenerationWindowDays = null);

public sealed record ProjectManagementTaskRecurrenceCreateRequest(
    string SourceTaskId,
    ProjectManagementTaskRecurrenceRuleRequest Rule);

public sealed record ProjectManagementTaskRecurrenceUpdateRequest(
    ProjectManagementTaskRecurrenceRuleRequest Rule,
    long VersionNo);

public sealed record ProjectManagementTaskRecurrenceOccurrenceEditRequest(
    string Scope,
    ProjectManagementTaskUpsertRequest Task,
    long OccurrenceVersionNo,
    long RecurrenceVersionNo);

public sealed record ProjectManagementTaskRecurrenceOccurrenceDeleteRequest(
    string Scope,
    long OccurrenceVersionNo,
    long RecurrenceVersionNo);

public sealed record ProjectManagementTaskRecurrenceResponse(
    string Id,
    string ProjectId,
    string SourceTaskId,
    string Frequency,
    int Interval,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    int? DayOfMonth,
    string? CustomUnit,
    DateTime StartAtLocal,
    DateTime? EndsAtLocal,
    string TimeZoneId,
    int GenerationWindowDays,
    bool IsActive,
    long VersionNo);

public sealed record ProjectManagementTaskRecurrenceOccurrenceResponse(
    string Id,
    string RecurrenceId,
    string ProjectId,
    string TaskId,
    string RecurrenceKey,
    DateTime ScheduledAtLocal,
    DateTime ScheduledAtUtc,
    string State,
    long VersionNo);
