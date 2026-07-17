using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_query_datasets")]
public sealed class ApplicationQueryDatasetEntity : ApplicationDataCenterObjectEntity
{
    [SugarColumn(IsNullable = true)]
    public string? SourceObjectId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RuntimeViewId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RuntimeViewCode { get; set; }

    public bool IsPhysicalView { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ViewSchemaName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ViewSql { get; set; }
}
