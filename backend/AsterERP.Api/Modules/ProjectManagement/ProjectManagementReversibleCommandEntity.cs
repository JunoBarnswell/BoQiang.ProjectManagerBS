using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

/// <summary>
/// 每个用户、工作区独立的可逆业务命令账本。
/// 保存的是可重新执行的正向/反向业务命令，不保存业务表快照。
/// </summary>
[SugarTable("pm_reversible_commands")]
public sealed class ProjectManagementReversibleCommandEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string OriginRequestId { get; set; } = string.Empty;
    public long SequenceNo { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateId { get; set; } = string.Empty;
    public string State { get; set; } = "Applied";
    public string ForwardCommandJson { get; set; } = "{}";
    public string InverseCommandJson { get; set; } = "{}";
    public string TraceId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? Summary { get; set; }
    public long VersionNo { get; set; } = 1;
    [SugarColumn(IsNullable = true)] public string? ActiveReplayDirection { get; set; }
    [SugarColumn(IsNullable = true)] public string? ActiveReplayRequestId { get; set; }
    [SugarColumn(IsNullable = true)] public string? ActiveReplayExecutionId { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? ActiveReplayLeaseExpiresAt { get; set; }
    [SugarColumn(IsNullable = true)] public string? LastUndoRequestId { get; set; }
    [SugarColumn(IsNullable = true)] public string? LastRedoRequestId { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? LastReplayedTime { get; set; }
}
