using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_ACTINST")]
public class HistoricActivityInstanceEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "EXECUTION_ID_", IsNullable = true)]
    public string? ExecutionId { get; set; }

    [SugarColumn(ColumnName = "ACT_ID_", IsNullable = true)]
    public string? ActivityId { get; set; }

    [SugarColumn(ColumnName = "ACT_NAME_", IsNullable = true)]
    public string? ActivityName { get; set; }

    [SugarColumn(ColumnName = "ACT_TYPE_", IsNullable = true)]
    public string? ActivityType { get; set; }

    [SugarColumn(ColumnName = "START_TIME_", IsNullable = true)]
    public DateTime? StartTime { get; set; }

    [SugarColumn(ColumnName = "END_TIME_", IsNullable = true)]
    public DateTime? EndTime { get; set; }

    [SugarColumn(ColumnName = "DURATION_", IsNullable = true)]
    public long? DurationInMillis { get; set; }

    [SugarColumn(ColumnName = "TASK_ID_", IsNullable = true)]
    public string? TaskId { get; set; }

    [SugarColumn(ColumnName = "CALLED_PROC_INST_ID_", IsNullable = true)]
    public string? CalledProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "CALL_PROC_INST_ID_", IsNullable = true)]
    public string? FlowCallProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "ASSIGNEE_", IsNullable = true)]
    public string? Assignee { get; set; }

    [SugarColumn(ColumnName = "TRANSACTION_ORDER_", IsNullable = true)]
    public int? TransactionOrder { get; set; }

    [SugarColumn(ColumnName = "DELETE_REASON_", IsNullable = true)]
    public string? DeleteReason { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }
}
