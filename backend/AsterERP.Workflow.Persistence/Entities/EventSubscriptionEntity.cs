using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_EVENT_SUBSCR")]
public class EventSubscriptionEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "EVENT_TYPE_", IsNullable = true)]
    public string? EventType { get; set; }

    [SugarColumn(ColumnName = "EVENT_NAME_", IsNullable = true)]
    public string? EventName { get; set; }

    [SugarColumn(ColumnName = "EXECUTION_ID_", IsNullable = true)]
    public string? ExecutionId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "ACTIVITY_ID_", IsNullable = true)]
    public string? ActivityId { get; set; }

    [SugarColumn(ColumnName = "CONFIGURATION_", IsNullable = true)]
    public string? Configuration { get; set; }

    [SugarColumn(ColumnName = "CREATED_")]
    public DateTime Created { get; set; } = AbpTimeIdProvider.UtcNow;

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }

    [SugarColumn(ColumnName = "TENANT_ID_", IsNullable = true)]
    public string? TenantId { get; set; }
}

