using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_EXECUTION")]
public class ExecutionEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "BUSINESS_KEY_", IsNullable = true)]
    public string? BusinessKey { get; set; }

    [SugarColumn(ColumnName = "PARENT_ID_", IsNullable = true)]
    public string? ParentId { get; set; }

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "SUPER_EXEC_", IsNullable = true)]
    public string? SuperExecutionId { get; set; }

    [SugarColumn(ColumnName = "ACT_ID_", IsNullable = true)]
    public string? ActivityId { get; set; }

    [SugarColumn(ColumnName = "IS_ACTIVE_")]
    public bool IsActive { get; set; } = true;

    [SugarColumn(ColumnName = "IS_CONCURRENT_")]
    public bool IsConcurrent { get; set; }

    [SugarColumn(ColumnName = "IS_SCOPE_")]
    public bool IsScope { get; set; }

    [SugarColumn(ColumnName = "IS_EVENT_SCOPE_")]
    public bool IsEventScope { get; set; }

    [SugarColumn(ColumnName = "SUSPENSION_STATE_")]
    public int SuspensionState { get; set; } = 1;

    [SugarColumn(ColumnName = "IS_ENDED_")]
    public bool IsEnded { get; set; }

    [SugarColumn(ColumnName = "CACHED_ENT_STATE_", IsNullable = true)]
    public int? CachedEntityState { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }
}
