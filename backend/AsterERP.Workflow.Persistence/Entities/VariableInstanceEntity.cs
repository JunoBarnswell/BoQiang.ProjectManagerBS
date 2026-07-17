using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_VARIABLE")]
public class VariableInstanceEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "EXECUTION_ID_", IsNullable = true)]
    public string? ExecutionId { get; set; }

    [SugarColumn(ColumnName = "TASK_ID_", IsNullable = true)]
    public string? TaskId { get; set; }

    [SugarColumn(ColumnName = "BYTEARRAY_ID_", IsNullable = true)]
    public string? ByteArrayId { get; set; }

    [SugarColumn(ColumnName = "DOUBLE_", IsNullable = true)]
    public double? DoubleValue { get; set; }

    [SugarColumn(ColumnName = "LONG_", IsNullable = true)]
    public long? LongValue { get; set; }

    [SugarColumn(ColumnName = "TEXT_", IsNullable = true)]
    public string? TextValue { get; set; }

    [SugarColumn(ColumnName = "TEXT2_", IsNullable = true)]
    public string? TextValue2 { get; set; }

    [SugarColumn(ColumnName = "ACTIVITY_ID_", IsNullable = true)]
    public string? ActivityId { get; set; }

    [SugarColumn(ColumnName = "IS_ACTIVE_")]
    public bool IsActive { get; set; } = true;

    [SugarColumn(ColumnName = "IS_CONCURRENCY_SCOPE_")]
    public bool IsConcurrencyScope { get; set; }

    [SugarColumn(ColumnName = "CREATE_TIME_", IsNullable = true)]
    public DateTime? CreateTime { get; set; }

    [SugarColumn(ColumnName = "LAST_UPDATED_TIME_", IsNullable = true)]
    public DateTime? LastUpdatedTime { get; set; }
}
