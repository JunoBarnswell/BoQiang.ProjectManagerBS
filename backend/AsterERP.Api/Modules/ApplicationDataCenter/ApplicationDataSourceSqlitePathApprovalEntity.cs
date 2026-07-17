using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_source_sqlite_path_approvals")]
public sealed class ApplicationDataSourceSqlitePathApprovalEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string DataSourceId { get; set; } = string.Empty;

    [SugarColumn(Length = 4096)]
    public string Path { get; set; } = string.Empty;

    [SugarColumn(Length = 2000)]
    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public string? ApprovedBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ApprovedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RevokedBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? RevokedAt { get; set; }
}
