using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_ID_INFO")]
public class ActIdInfoEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "USER_ID_", IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(ColumnName = "KEY_", IsNullable = true)]
    public string? Key { get; set; }

    [SugarColumn(ColumnName = "VALUE_", IsNullable = true)]
    public string? Value { get; set; }

    [SugarColumn(ColumnName = "PASSWORD_", IsNullable = true)]
    public string? PasswordValue { get; set; }

    [SugarColumn(ColumnName = "PARENT_ID_", IsNullable = true)]
    public string? ParentId { get; set; }
}
