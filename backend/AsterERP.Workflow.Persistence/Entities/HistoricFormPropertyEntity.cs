using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_HI_DETAIL")]
public class HistoricFormPropertyEntity : HistoricDetailEntity
{
    public const string TypeFormProperty = "formProperty";

    public HistoricFormPropertyEntity()
    {
        Type = TypeFormProperty;
    }

    [SugarColumn(ColumnName = "PROPERTY_ID_", IsNullable = true)]
    public string? PropertyId { get; set; }

    [SugarColumn(ColumnName = "PROPERTY_VALUE_", IsNullable = true)]
    public string? PropertyValue { get; set; }
}
