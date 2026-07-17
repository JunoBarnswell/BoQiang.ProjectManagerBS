using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_IDENTITYLINK")]
public class HistoricIdentityLinkEntity : AbstractEntity
{
    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(ColumnName = "USER_ID_", IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(ColumnName = "GROUP_ID_", IsNullable = true)]
    public string? GroupId { get; set; }

    [SugarColumn(ColumnName = "TASK_ID_", IsNullable = true)]
    public string? TaskId { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "SCOPE_ID_", IsNullable = true)]
    public string? ScopeId { get; set; }

    [SugarColumn(ColumnName = "SCOPE_TYPE_", IsNullable = true)]
    public string? ScopeType { get; set; }

    public bool IsUser => !string.IsNullOrEmpty(UserId);
    public bool IsGroup => !string.IsNullOrEmpty(GroupId);

    public override object GetPersistentState()
    {
        return new { Type, UserId, GroupId, TaskId, ProcessInstanceId };
    }
}
