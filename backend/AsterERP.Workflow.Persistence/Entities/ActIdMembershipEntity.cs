using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_ID_MEMBERSHIP")]
public class ActIdMembershipEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "USER_ID_")]
    public string UserId { get; set; } = null!;

    [SugarColumn(ColumnName = "GROUP_ID_")]
    public string GroupId { get; set; } = null!;
}
