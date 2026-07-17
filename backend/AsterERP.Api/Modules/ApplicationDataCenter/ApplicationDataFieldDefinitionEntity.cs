using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_field_definitions")]
public sealed class ApplicationDataFieldDefinitionEntity : ApplicationDataCenterObjectEntity
{
    public string ModelId { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string FieldCode { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public string DataType { get; set; } = "Text";

    public string Binding { get; set; } = string.Empty;

    public bool IsPrimaryKey { get; set; }

    public bool IsNullable { get; set; } = true;

    public bool IsQueryable { get; set; } = true;

    public bool IsSortable { get; set; } = true;

    public bool IsWritable { get; set; } = true;

    public int SortOrder { get; set; }
}
