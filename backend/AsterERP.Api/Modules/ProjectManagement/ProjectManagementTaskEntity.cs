using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_tasks")]
public sealed class ProjectManagementTaskEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? MilestoneId { get; set; }
    [SugarColumn(IsNullable = true)] public string? ParentTaskId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? OccurrenceKey { get; set; }
    public string Title { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? Summary { get; set; }
    [SugarColumn(IsNullable = true)] public string? Description { get; set; }
    public string WorkItemType { get; set; } = "Task";
    [SugarColumn(IsNullable = true)] public string? ContentJson { get; set; }
    [SugarColumn(IsNullable = true)] public string? ContentText { get; set; }
    [SugarColumn(IsNullable = true)] public string? MentionUserIdsJson { get; set; }
    public string Status { get; set; } = "Todo";
    [SugarColumn(IsNullable = true)] public string? BlockedReason { get; set; }
    public string Priority { get; set; } = "Medium";
    public string RiskLevel { get; set; } = "None";
    [SugarColumn(IsNullable = true)] public string? RequirementType { get; set; }
    [SugarColumn(IsNullable = true)] public string? RequirementSource { get; set; }
    [SugarColumn(IsNullable = true)] public int? StoryPoints { get; set; }
    [SugarColumn(IsNullable = true)] public string? AssigneeUserId { get; set; }
    [SugarColumn(IsNullable = true)] public string? AssigneeEmploymentId { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? StartDate { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? DueDate { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? ActualStartAt { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? ActualEndAt { get; set; }
    public decimal ProgressPercent { get; set; }
    public decimal Weight { get; set; } = 1;
    public int SortOrder { get; set; }
    public int Depth { get; set; }
    public long VersionNo { get; set; } = 1;
}
