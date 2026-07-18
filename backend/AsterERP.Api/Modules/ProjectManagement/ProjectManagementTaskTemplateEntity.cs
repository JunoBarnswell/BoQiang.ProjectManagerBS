using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_templates")]
public sealed class ProjectManagementTaskTemplateEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? ProjectId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string DefinitionJson { get; set; } = "[]";
    [SugarColumn(IsNullable = true)] public string? RecurrenceExpression { get; set; }
    public long VersionNo { get; set; } = 1;
}

[SugarTable("pm_task_occurrences")]
public sealed class ProjectManagementTaskOccurrenceEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string OccurrenceKey { get; set; } = string.Empty;
    public DateTime OccurrenceDate { get; set; }
    public string RootTaskId { get; set; } = string.Empty;
    public long VersionNo { get; set; } = 1;
}
