namespace AsterERP.Contracts.System.ScheduledJobs;

public sealed record ScheduledJobTypeOptionResponse(
    string Code,
    string Name,
    string Description,
    bool SupportsParameters);
