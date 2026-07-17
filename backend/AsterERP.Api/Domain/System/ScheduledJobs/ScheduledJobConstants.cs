namespace AsterERP.Api.Domain.System.ScheduledJobs;

public static class ScheduledJobConstants
{
    public const string JobTypePreset = "Preset";
    public const string JobTypeHttpCallback = "HttpCallback";

    public const string StatusEnabled = "Enabled";
    public const string StatusPaused = "Paused";

    public const string TriggerAutomatic = "Automatic";
    public const string TriggerManual = "Manual";

    public const string ResultQueued = "Queued";
    public const string ResultSuccess = "Success";
    public const string ResultFailed = "Failed";

    public const string ScheduleEveryMinutes = "EveryMinutes";
    public const string ScheduleEveryHours = "EveryHours";
    public const string ScheduleDaily = "Daily";
    public const string ScheduleWeekly = "Weekly";
    public const string ScheduleMonthly = "Monthly";
}
