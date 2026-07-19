using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

/// <summary>对外项目管理写入的幂等与审计账本。</summary>
[SugarTable("pm_external_api_requests")]
public sealed class ProjectManagementExternalApiRequestEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string CallerUserId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string TraceId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? ProjectId { get; set; }
    [SugarColumn(IsNullable = true)] public string? AggregateType { get; set; }
    [SugarColumn(IsNullable = true)] public string? AggregateId { get; set; }
    [SugarColumn(IsNullable = true)] public string? ResultJson { get; set; }
    [SugarColumn(IsNullable = true)] public int? ErrorCode { get; set; }
    [SugarColumn(IsNullable = true)] public string? ErrorMessage { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? CompletedTime { get; set; }
}
