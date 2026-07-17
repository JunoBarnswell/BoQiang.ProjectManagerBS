using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_DETAIL")]
public class HistoricDetailEntity
{
    [SugarColumn(IsPrimaryKey = true, ColumnName = "ID_")]
    public string Id { get; set; } = null!;

    [SugarColumn(ColumnName = "REV_")]
    public int Revision { get; set; }

    [SugarColumn(ColumnName = "TYPE_", IsNullable = true)]
    public string? Type { get; set; }

    [SugarColumn(ColumnName = "PROC_INST_ID_", IsNullable = true)]
    public string? ProcessInstanceId { get; set; }

    [SugarColumn(ColumnName = "VAR_ID_", IsNullable = true)]
    public string? VariableId { get; set; }

    [SugarColumn(ColumnName = "NAME_", IsNullable = true)]
    public string? Name { get; set; }

    [SugarColumn(ColumnName = "VAR_TYPE_", IsNullable = true)]
    public string? VariableType { get; set; }

    [SugarColumn(ColumnName = "VAR_INST_ID_", IsNullable = true)]
    public string? VariableInstanceId { get; set; }

    [SugarColumn(ColumnName = "TIME_", IsNullable = true)]
    public DateTime? Time { get; set; }

    [SugarColumn(ColumnName = "BYTEARRAY_ID_", IsNullable = true)]
    public string? ByteArrayId { get; set; }

    [SugarColumn(ColumnName = "DOUBLE_", IsNullable = true)]
    public double? DoubleValue { get; set; }

    [SugarColumn(ColumnName = "LONG_", IsNullable = true)]
    public long? LongValue { get; set; }

    [SugarColumn(ColumnName = "TEXT_", IsNullable = true)]
    public string? TextValue { get; set; }

    [SugarColumn(ColumnName = "TEXT2_", IsNullable = true)]
    public string? TextValue2 { get; set; }
}
