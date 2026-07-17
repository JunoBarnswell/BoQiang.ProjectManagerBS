namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class JobsInfrastructureSettingsUpdate
{
    public bool? AbpBackgroundJobsEnabled { get; set; }

    public bool? MessagingJobsEnabled { get; set; }

    public int? TestTimeoutSeconds { get; set; }
}
