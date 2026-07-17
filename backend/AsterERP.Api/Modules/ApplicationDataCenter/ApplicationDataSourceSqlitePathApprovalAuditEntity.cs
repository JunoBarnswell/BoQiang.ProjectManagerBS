using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_source_sqlite_path_approval_audits")]
public sealed class ApplicationDataSourceSqlitePathApprovalAuditEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ApprovalId { get; set; } = string.Empty;

    public string DataSourceId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string ActorUserId { get; set; } = string.Empty;

    [SugarColumn(Length = 4096)]
    public string Path { get; set; } = string.Empty;

    [SugarColumn(Length = 2000)]
    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
