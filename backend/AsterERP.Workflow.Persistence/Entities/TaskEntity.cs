using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_TASK")]
public class TaskEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "EXECUTION_ID_", IsNullable = true)]
    public string? ExecutionId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "PARENT_TASK_ID_", IsNullable = true)]
    public string? ParentTaskId { get; set; }

    [SugarColumn(ColumnName = "DESCRIPTION_", IsNullable = true)]
    public string? Description { get; set; }

    [SugarColumn(ColumnName = "TASK_DEF_KEY_", IsNullable = true)]
    public string? TaskDefinitionKey { get; set; }

    [SugarColumn(ColumnName = "OWNER_", IsNullable = true)]
    public string? Owner { get; set; }

    [SugarColumn(ColumnName = "ASSIGNEE_", IsNullable = true)]
    public string? Assignee { get; set; }

    [SugarColumn(ColumnName = "DELEGATION_", IsNullable = true)]
    public string? DelegationState { get; set; }

    [SugarColumn(ColumnName = "PRIORITY_")]
    public int Priority { get; set; }

    [SugarColumn(ColumnName = "CREATE_TIME_", IsNullable = true)]
    public DateTime? CreateTime { get; set; }

    [SugarColumn(ColumnName = "DUE_DATE_", IsNullable = true)]
    public DateTime? DueDate { get; set; }

    [SugarColumn(ColumnName = "CATEGORY_", IsNullable = true)]
    public string? Category { get; set; }

    [SugarColumn(ColumnName = "SUSPENSION_STATE_")]
    public int SuspensionState { get; set; } = 1;

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(ColumnName = "FORM_KEY_", IsNullable = true)]
    public string? FormKey { get; set; }

    [SugarColumn(ColumnName = "CLAIM_TIME_", IsNullable = true)]
    public DateTime? ClaimTime { get; set; }
}
