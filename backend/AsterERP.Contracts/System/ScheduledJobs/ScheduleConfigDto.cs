namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record ScheduleConfigDto(
    string Kind,
    int? IntervalValue,
    string? TimeOfDay,
    IReadOnlyList<int>? WeekDays,
    IReadOnlyList<int>? MonthDays,
    string? TimeZone);
