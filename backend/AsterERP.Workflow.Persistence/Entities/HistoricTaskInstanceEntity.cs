using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_TASKINST")]
public class HistoricTaskInstanceEntity
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

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "PARENT_TASK_ID_", IsNullable = true)]
    public string? ParentTaskId { get; set; }

    [SugarColumn(ColumnName = "DESCRIPTION_", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "OWNER_", IsNullable = true)]
    public string? Owner { get; set; }

    [SugarColumn(ColumnName = "ASSIGNEE_", IsNullable = true)]
    public string? Assignee { get; set; }

    [SugarColumn(ColumnName = "START_TIME_", IsNullable = true)]
    public DateTime? StartTime { get; set; }

    [SugarColumn(ColumnName = "END_TIME_", IsNullable = true)]
    public DateTime? EndTime { get; set; }

    [SugarColumn(ColumnName = "DURATION_", IsNullable = true)]
    public long? DurationInMillis { get; set; }

    [SugarColumn(ColumnName = "DELETE_REASON_", IsNullable = true)]
    public string? DeleteReason { get; set; }

    [SugarColumn(ColumnName = "TASK_DEF_KEY_", IsNullable = true)]
    public string? TaskDefinitionKey { get; set; }

    [SugarColumn(ColumnName = "FORM_KEY_", IsNullable = true)]
    public string? FormKey { get; set; }

    [SugarColumn(ColumnName = "PRIORITY_")]
    public int Priority { get; set; }

    [SugarColumn(ColumnName = "DUE_DATE_", IsNullable = true)]
    public DateTime? DueDate { get; set; }

    [SugarColumn(ColumnName = "CATEGORY_", IsNullable = true)]
    public string? Category { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }
}
