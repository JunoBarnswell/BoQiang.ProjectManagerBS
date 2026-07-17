namespace AsterERP.Api.Domain.System.ScheduledJobs;

public sealed class SchedulerOptions
{
    public int WorkerCount { get; set; } = 2;

    public int HttpCallbackTimeoutSeconds { get; set; } = 10;

    public string[] AllowedHosts { get; set; } = [];
}
