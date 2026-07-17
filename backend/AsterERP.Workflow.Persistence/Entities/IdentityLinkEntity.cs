using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_IDENTITYLINK")]
public class IdentityLinkEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "USER_ID_", IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(ColumnName = "GROUP_ID_", IsNullable = true)]
    public string? GroupId { get; set; }

    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(ColumnName = "TASK_ID_", IsNullable = true)]
    public string? TaskId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "PROC_DEF_ID_", IsNullable = true)]
    public string? ProcessDefinitionId { get; set; }
}
