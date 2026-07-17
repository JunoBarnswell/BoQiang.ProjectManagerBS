using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_PROCINST")]
public class HistoricProcessInstanceEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "BUSINESS_KEY_", IsNullable = true)]
    public string? BusinessKey { get; set; }

    [SugarColumn(ColumnName = "START_TIME_", IsNullable = true)]
    public DateTime? StartTime { get; set; }

    [SugarColumn(ColumnName = "END_TIME_", IsNullable = true)]
    public DateTime? EndTime { get; set; }

    [SugarColumn(ColumnName = "DURATION_", IsNullable = true)]
    public long? DurationInMillis { get; set; }

    [SugarColumn(ColumnName = "START_USER_ID_", IsNullable = true)]
    public string? StartUserId { get; set; }

    [SugarColumn(ColumnName = "DELETE_REASON_", IsNullable = true)]
    public string? DeleteReason { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "SUPER_PROCESS_INSTANCE_ID_", IsNullable = true)]
    public string? SuperProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }
}
