using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.QueryViews;

[SugarTable("system_query_view_column_resources")]
public sealed class SystemQueryViewColumnResourceEntity : EntityBase
{
    public string TableResourceId { get; set; } = string.Empty;

    public string ColumnCode { get; set; } = string.Empty;

    public string ColumnName { get; set; } = string.Empty;

    public string ColumnComment { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public bool IsPrimaryKey { get; set; }

    public bool IsNullable { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}
