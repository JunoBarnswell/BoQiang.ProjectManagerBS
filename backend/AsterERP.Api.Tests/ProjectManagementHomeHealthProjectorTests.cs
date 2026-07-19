using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Modules.ProjectManagement;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementHomeHealthProjectorTests
{
    private readonly ProjectManagementHomeHealthProjector projector = new();

    [Fact]
    public void Projector_applies_terminal_and_planning_states_before_risk_rules()
    {
        var now = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(ProjectManagementHomeHealthProjector.Completed, projector.Project(new ProjectManagementProjectEntity { Status = "Completed" }, 10, 10, now));
        Assert.Equal(ProjectManagementHomeHealthProjector.NoUpdateExpected, projector.Project(new ProjectManagementProjectEntity { Status = "Planning" }, 10, 10, now));
    }

    [Fact]
    public void Projector_marks_overdue_and_blocked_projects()
    {
        var now = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(ProjectManagementHomeHealthProjector.OffTrack, projector.Project(new ProjectManagementProjectEntity { Status = "Active", DueDate = now.AddDays(-1), ProgressPercent = 90 }, 2, 0, now));
        Assert.Equal(ProjectManagementHomeHealthProjector.AtRisk, projector.Project(new ProjectManagementProjectEntity { Status = "Active", DueDate = now.AddDays(30), ProgressPercent = 90 }, 2, 1, now));
    }

    [Fact]
    public void Projector_marks_missing_update_and_on_track_projects()
    {
        var now = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(ProjectManagementHomeHealthProjector.UpdateMissing, projector.Project(new ProjectManagementProjectEntity { Status = "Active", UpdatedTime = now.AddDays(-15) }, 0, 0, now));
        Assert.Equal(ProjectManagementHomeHealthProjector.OnTrack, projector.Project(new ProjectManagementProjectEntity { Status = "Active", UpdatedTime = now.AddDays(-1) }, 0, 0, now));
    }
}
