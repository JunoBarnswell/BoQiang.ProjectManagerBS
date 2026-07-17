namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed record JobsInfrastructureSettings(
    bool AbpBackgroundJobsEnabled,
    bool MessagingJobsEnabled,
    int TestTimeoutSeconds);
