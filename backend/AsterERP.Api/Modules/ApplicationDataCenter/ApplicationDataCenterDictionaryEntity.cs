using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_dictionary_codes")]
public sealed class ApplicationDataCenterDictionaryEntity : ApplicationDataCenterObjectEntity
{
    [SugarColumn(IsNullable = true)]
    public string? RuntimeObjectId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RuntimeObjectCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DataSourceId { get; set; }
}
