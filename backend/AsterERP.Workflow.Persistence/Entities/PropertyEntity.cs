using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_GE_PROPERTY")]
public class PropertyEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "NAME_")]
    public string Name { get; set; } = null!;

    [SugarColumn(ColumnName = "VALUE_", IsNullable = true)]
    public string? Value { get; set; }

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }
}
