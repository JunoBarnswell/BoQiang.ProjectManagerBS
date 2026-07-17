using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_entity_definitions")]
public sealed class ApplicationDataEntityDefinitionEntity : ApplicationDataCenterObjectEntity
{
    public string ModelId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceTable { get; set; }

    public string KeyField { get; set; } = "id";
}
