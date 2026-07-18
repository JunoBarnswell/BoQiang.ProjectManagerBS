using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_activities")]
public sealed class ProjectManagementActivityEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? Summary { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;

    // EntityBase.Remark 保存结构化业务时间线载荷（来源、字段差异、批量摘要/明细）。
    // 该表是历史既有表，复用可空列以保证已部署 SQLite 库无迁移也能安全升级；平台操作审计不使用该载荷。
}
