namespace AsterERP.Api.Application.Platform.ApplicationPublishing;

public static class ApplicationPublishConstants
{
    public const string StatusPending = "Pending";
    public const string StatusRunning = "Running";
    public const string StatusSucceeded = "Succeeded";
    public const string StatusFailed = "Failed";
    public const string StatusBlocked = "Blocked";

    public const string StageQueued = "Queued";
    public const string StageScanning = "Scanning";
    public const string StageSource = "Source";
    public const string StageBackend = "Backend";
    public const string StageFrontend = "Frontend";
    public const string StageManifest = "Manifest";
    public const string StageLeakScan = "LeakScan";
    public const string StagePackage = "Package";
    public const string StageCompleted = "Completed";
}
