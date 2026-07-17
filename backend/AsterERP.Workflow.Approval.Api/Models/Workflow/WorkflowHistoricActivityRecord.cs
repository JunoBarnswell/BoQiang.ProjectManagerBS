namespace AsterERP.Workflow.Approval.Api.Models.Workflow;

[SqlSugar.SugarTable("act_hi_actinst")]
public class WorkflowHistoricActivityRecord
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true, ColumnName = "Id")]
    public string? Id { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "REV_")]
    public int? Rev { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "PROC_DEF_ID_")]
    public string? ProcDefId { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "PROC_INST_ID_")]
    public string? ProcInstId { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "EXECUTION_ID_")]
    public string? ExecutionId { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "ACT_ID_")]
    public string? ActId { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "TASK_ID_")]
    public string? TaskId { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "CALL_PROC_INST_ID_")]
    public string? CallProcInstId { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "ACT_NAME_")]
    public string? ActName { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "ACT_TYPE_")]
    public string? ActType { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "ASSIGNEE_")]
    public string? Assignee { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "START_TIME_")]
    public DateTime? StartTime { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "END_TIME_")]
    public DateTime? EndTime { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "DURATION_")]
    public string? Duration { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "TRANSACTION_ORDER_")]
    public int? TransactionOrder { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "DELETE_REASON_")]
    public string? DeleteReason { get; set; }

    [SqlSugar.SugarColumn(ColumnName = "TENANT_ID_")]
    public string? TenantId { get; set; }
}
