using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_model_designs")]
public sealed class ApplicationDataModelDesignEntity : ApplicationDataCenterObjectEntity
{
    public string BuildMode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceDataSourceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RuntimeModelId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RuntimeModelCode { get; set; }
}
