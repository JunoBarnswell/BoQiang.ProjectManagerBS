namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed record ApplicationDataSourceCatalogSql(
    string TablesSql,
    string ColumnsSql,
    string ConstraintsSql,
    string IndexesSql,
    string TriggersSql,
    string CommentsSql);
