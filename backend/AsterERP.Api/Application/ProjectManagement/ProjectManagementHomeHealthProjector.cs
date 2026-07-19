using AsterERP.Api.Modules.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementHomeHealthProjector
{
    public const string Completed = "Completed";
    public const string UpdateMissing = "UpdateMissing";
    public const string AtRisk = "AtRisk";
    public const string OffTrack = "OffTrack";
    public const string OnTrack = "OnTrack";
    public const string NoUpdateExpected = "NoUpdateExpected";

    public string Project(
        ProjectManagementProjectEntity project,
        int openIssueCount,
        int blockedIssueCount,
        DateTime nowUtc)
    {
        if (project.Status == ProjectManagementDomainRules.ProjectCompleted) return Completed;
        if (project.Status is ProjectManagementDomainRules.ProjectPlanning or ProjectManagementDomainRules.ProjectPaused)
            return NoUpdateExpected;
        if (project.DueDate is not null && project.DueDate.Value < nowUtc && openIssueCount > 0)
            return OffTrack;
        if (blockedIssueCount > 0 ||
            (project.DueDate is not null && project.DueDate.Value <= nowUtc.AddDays(14) && project.ProgressPercent < 60))
            return AtRisk;
        if (project.UpdatedTime is null || project.UpdatedTime.Value < nowUtc.AddDays(-14)) return UpdateMissing;
        return OnTrack;
    }
}
