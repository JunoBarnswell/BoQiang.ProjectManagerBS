using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_COMMENT")]
public class CommentEntity
{
    public const string TypeEvent = "event";
    public const string TypeComment = "comment";

    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(ColumnName = "TIME_", IsNullable = true)]
    public DateTime? Time { get; set; }

    [SugarColumn(ColumnName = "USER_ID_", IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(ColumnName = "TASK_ID_", IsNullable = true)]
    public string? TaskId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "ACTION_", IsNullable = true)]
    public string? Action { get; set; }

    [SugarColumn(ColumnName = "MESSAGE_", IsNullable = true)]
    public string? Message { get; set; }

    [SugarColumn(ColumnName = "FULL_MSG_", IsNullable = true)]
    public string? FullMessage { get; set; }
}
