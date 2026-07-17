using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.QueryViews;

[SugarTable("system_query_view_table_resources")]
public sealed class SystemQueryViewTableResourceEntity : EntityBase
{
    public string TableCode { get; set; } = string.Empty;

    public string TableName { get; set; } = string.Empty;

    public string TableComment { get; set; } = string.Empty;

    public string SchemaName { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
