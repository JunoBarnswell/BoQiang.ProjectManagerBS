using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

internal sealed record ApplicationDataSourceSqlClause(string Sql, IReadOnlyList<SugarParameter> Parameters);
