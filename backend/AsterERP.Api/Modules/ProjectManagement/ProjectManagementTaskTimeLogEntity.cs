using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_task_time_logs")]
public sealed class ProjectManagementTaskTimeLogEntity : EntityBase
{
    private int minutes;

    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    /// <summary>工时的持久化单位是整分钟，禁止出现负数。</summary>
    public int Minutes
    {
        get => minutes;
        set => minutes = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "工时分钟数不能为负数");
    }
    [SugarColumn(IsNullable = true)] public string? Note { get; set; }
    public long VersionNo { get; set; } = 1;
}
