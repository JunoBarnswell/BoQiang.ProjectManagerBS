using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_EVENT_LOG")]
public class EventLogEntryEntity : AbstractEntity
{
    [SugarColumn(IsIgnore = true)]
    public new string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "EXECUTION_ID_", IsNullable = true)]
    public string? ExecutionId { get; set; }

    [SugarColumn(ColumnName = "TASK_ID_", IsNullable = true)]
    public string? TaskId { get; set; }

    [SugarColumn(ColumnName = "TIME_STAMP_", IsNullable = true)]
    public DateTime? TimeStamp { get; set; }

    [SugarColumn(ColumnName = "USER_ID_", IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(ColumnName = "DATA_", IsNullable = true)]
    public byte[]? Data { get; set; }

    [SugarColumn(ColumnName = "LOCK_OWNER_", IsNullable = true)]
    public string? LockOwner { get; set; }

    [SugarColumn(ColumnName = "LOCK_TIME_", IsNullable = true)]
    public string? LockTime { get; set; }

    [SugarColumn(ColumnName = "IS_PROCESSED_")]
    public int Processed { get; set; }

    [SugarColumn(ColumnName = "LOG_NR_", ColumnDataType = "INTEGER", IsPrimaryKey = true, IsIdentity = true)]
    public long LogNumber { get; set; }

    public override object GetPersistentState()
    {
        return new { Type, ProcessDefinitionId, ProcessInstanceId, ExecutionId, TaskId, TimeStamp, UserId, Data };
    }
}
