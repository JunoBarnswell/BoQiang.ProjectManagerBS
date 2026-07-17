using System.Text.RegularExpressions;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public abstract class ApplicationDataSourceProviderBase(
    string type,
    string quoteStart,
    string quoteEnd,
    ApplicationDataSourceProviderCapability capability,
    ApplicationDataSourceCatalogSql catalog) : IApplicationDataSourceProvider
{
    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DangerousSqlToken = new("(?:;|--|/\\*|\\*/|\\b(?:DROP|ALTER|CREATE|SELECT|INSERT|UPDATE|DELETE|PRAGMA|ATTACH|DETACH)\\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string Type { get; } = type;
    public ApplicationDataSourceProviderCapability Capability { get; } = capability;
    public ApplicationDataSourceCatalogSql Catalog { get; } = catalog;

    public string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || !Identifier.IsMatch(identifier.Trim()))
        {
            throw new ValidationException("标识符仅允许字母、数字和下划线，且必须以字母或下划线开头", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return $"{quoteStart}{identifier.Trim()}{quoteEnd}";
    }

    public string QuoteQualified(string? schema, string identifier) =>
        string.IsNullOrWhiteSpace(schema) ? QuoteIdentifier(identifier) : $"{QuoteIdentifier(schema)}.{QuoteIdentifier(identifier)}";

    public virtual string BuildPageSql(string sourceSql, string orderBySql, int offset, int limit)
    {
        ValidatePage(offset, limit);

        return $"{NormalizeSourceSql(sourceSql)}{orderBySql} LIMIT {limit} OFFSET {offset}";
    }

    public string BuildCountSql(string quotedTableName, string whereSql) =>
        $"SELECT COUNT(1) FROM {RequireSqlFragment(quotedTableName)}{NormalizeOptionalFragment(whereSql)}";

    public virtual string BuildPreviewSql(string sourceSql, int maxRows)
    {
        var normalized = NormalizeSourceSql(sourceSql);
        if (maxRows < 1 || maxRows > Capability.MaxPreviewRows)
        {
            throw new ValidationException("预览行数超过数据源能力上限", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return $"SELECT * FROM ({normalized}) AS preview_source LIMIT {maxRows}";
    }

    public virtual string BuildTextSearchSql(string quotedColumnName, string parameterName) =>
        $"CAST({RequireSqlFragment(quotedColumnName)} AS TEXT) LIKE {BuildParameterName(parameterName.TrimStart('@'))}";

    public virtual string BuildCreateTableSql(
        string? schemaName,
        string tableName,
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> columns)
    {
        if (columns.Count == 0)
        {
            throw new ValidationException("至少需要一个字段", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var quotedTable = QuoteQualified(schemaName, tableName);
        var normalizedColumns = columns.Select(NormalizeColumn).ToArray();
        var columnSql = normalizedColumns.Select(BuildColumnDefinition).ToArray();
        var primaryKeys = normalizedColumns
            .Where(item => item.PrimaryKey)
            .Select(item => QuoteIdentifier(item.ColumnName))
            .ToArray();
        if (primaryKeys.Length > 0)
        {
            columnSql = [.. columnSql, $"PRIMARY KEY ({string.Join(", ", primaryKeys)})"];
        }

        return $"CREATE TABLE {quotedTable} ({string.Join(", ", columnSql)})";
    }

    public virtual IReadOnlyList<string> BuildAlterTableSql(
        string? schemaName,
        string tableName,
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> currentColumns,
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> desiredColumns)
    {
        var current = currentColumns.Select(NormalizeColumn).ToDictionary(item => item.ColumnName, StringComparer.OrdinalIgnoreCase);
        var desired = desiredColumns.Select(NormalizeColumn).ToDictionary(item => item.ColumnName, StringComparer.OrdinalIgnoreCase);
        var currentPrimaryKeys = current.Values.Where(item => item.PrimaryKey).Select(item => item.ColumnName).Order(StringComparer.OrdinalIgnoreCase);
        var desiredPrimaryKeys = desired.Values.Where(item => item.PrimaryKey).Select(item => item.ColumnName).Order(StringComparer.OrdinalIgnoreCase);
        if (!currentPrimaryKeys.SequenceEqual(desiredPrimaryKeys, StringComparer.OrdinalIgnoreCase))
            throw new ValidationException("修改主键必须通过专用约束计划，当前表设计器拒绝隐式重建主键。", ErrorCodes.ApplicationDataCenterInvalidConfig);

        var table = QuoteQualified(schemaName, tableName);
        var statements = new List<string>();
        foreach (var column in desired.Values.Where(item => !current.ContainsKey(item.ColumnName)))
            statements.Add($"ALTER TABLE {table} ADD COLUMN {BuildColumnDefinition(column)}");
        foreach (var column in current.Values.Where(item => !desired.ContainsKey(item.ColumnName)))
            statements.Add($"ALTER TABLE {table} DROP COLUMN {QuoteIdentifier(column.ColumnName)}");
        foreach (var column in desired.Values.Where(item => current.TryGetValue(item.ColumnName, out var existing) && !ColumnEquivalent(existing, item)))
            statements.Add(BuildAlterColumnSql(table, current[column.ColumnName], column));

        if (statements.Count == 0)
            throw new ValidationException("目标表结构没有变化。", ErrorCodes.ApplicationDataCenterInvalidConfig);

        return statements;
    }

    protected virtual string BuildAlterColumnSql(
        string qualifiedTableName,
        ApplicationDataSourceColumnDefinition current,
        ApplicationDataSourceColumnDefinition desired) =>
        throw new ValidationException(
            $"Provider {Type} 不支持安全的字段结构修改：{desired.ColumnName}。",
            ErrorCodes.ApplicationDataCenterInvalidConfig);

    private static bool ColumnEquivalent(
        ApplicationDataSourceColumnDefinition current,
        ApplicationDataSourceColumnDefinition desired) =>
        string.Equals(current.DataType, desired.DataType, StringComparison.OrdinalIgnoreCase) &&
        current.Nullable == desired.Nullable &&
        current.PrimaryKey == desired.PrimaryKey &&
        string.Equals(current.DefaultSql, desired.DefaultSql, StringComparison.Ordinal);

    public virtual string BuildCreateViewSql(string qualifiedViewName, string selectSql) =>
        $"CREATE VIEW {RequireSqlFragment(qualifiedViewName)} AS {NormalizeSourceSql(selectSql)}";

    public virtual string BuildCreateOrReplaceViewSql(string qualifiedViewName, string selectSql) =>
        $"CREATE OR REPLACE VIEW {RequireSqlFragment(qualifiedViewName)} AS {NormalizeSourceSql(selectSql)}";

    public virtual string BuildDropViewSql(string qualifiedViewName) =>
        $"DROP VIEW IF EXISTS {RequireSqlFragment(qualifiedViewName)}";

    public virtual string BuildValidateViewSql(string qualifiedViewName) =>
        $"SELECT * FROM {RequireSqlFragment(qualifiedViewName)} WHERE 1 = 0";

    protected void ValidatePage(int offset, int limit)
    {
        if (offset < 0 || limit < 1 || limit > Capability.MaxPageSize)
        {
            throw new ValidationException("分页参数超出安全范围", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    public string BuildParameterName(string name) =>
        $"@{ApplicationDataSourceSqlNamePolicy.RequireIdentifier(name, "参数名")}";

    public bool IsReadOnlyStatement(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || sql.Contains(';', StringComparison.Ordinal)) return false;
        var statement = sql.Trim();
        var keyword = ReadKeyword(statement, 0, out _);
        if (keyword is "SELECT" or "EXPLAIN" or "PRAGMA") return true;
        return keyword == "WITH" && ReadWithBodyKeyword(statement) == "SELECT";
    }

    private ApplicationDataSourceColumnDefinition NormalizeColumn(ApplicationDataSourceCreateTableColumnRequest column)
    {
        var normalizedName = column.ColumnName.Trim();
        _ = QuoteIdentifier(normalizedName);
        return new(
            normalizedName,
            NormalizeDataType(column.DataType),
            column.Nullable,
            column.PrimaryKey,
            ApplicationDataSourceDefaultExpression.Parse(column.DefaultValue, Type),
            column.Remark);
    }

    protected virtual string BuildColumnDefinition(ApplicationDataSourceColumnDefinition column)
    {
        var name = QuoteIdentifier(column.ColumnName);
        var sql = $"{name} {column.DataType}";
        if (!column.Nullable || column.PrimaryKey)
        {
            sql += " NOT NULL";
        }

        if (column.DefaultExpression is not null)
        {
            sql += $" DEFAULT {column.RenderDefault(Type)}";
        }

        return sql;
    }

    protected virtual string NormalizeDataType(string dataType)
    {
        var normalized = dataType.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 64 ||
            normalized.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '(' or ')' or ',' or ' ')) ||
            DangerousSqlToken.IsMatch(normalized))
        {
            throw new ValidationException("字段类型不合法", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return normalized;
    }

    protected static string NormalizeSourceSql(string sql)
    {
        var normalized = sql.Trim();
        if (normalized.Length == 0 || normalized.Contains(';', StringComparison.Ordinal))
        {
            throw new ValidationException("SQL 片段不能为空且不得包含多语句", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return normalized.TrimEnd();
    }

    protected static string RequireSqlFragment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains(';', StringComparison.Ordinal))
        {
            throw new ValidationException("SQL 片段不合法", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return value.Trim();
    }

    protected static string NormalizeOptionalFragment(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : RequireSqlFragment(value);

    private static string? ReadWithBodyKeyword(string sql)
    {
        var index = 4;
        SkipWhitespace(sql, ref index);
        var recursive = ReadKeyword(sql, index, out var afterRecursive);
        if (recursive == "RECURSIVE") index = afterRecursive;

        while (true)
        {
            SkipWhitespace(sql, ref index);
            if (ReadIdentifier(sql, ref index) is null) return null;
            SkipWhitespace(sql, ref index);
            if (Current(sql, index) == '(' && !SkipBalanced(sql, ref index)) return null;
            SkipWhitespace(sql, ref index);
            if (ReadKeyword(sql, index, out var afterAs) != "AS") return null;
            index = afterAs;
            SkipWhitespace(sql, ref index);
            if (Current(sql, index) != '(' || !SkipBalanced(sql, ref index)) return null;
            SkipWhitespace(sql, ref index);
            if (Current(sql, index) == ',')
            {
                index++;
                continue;
            }

            return ReadKeyword(sql, index, out _);
        }
    }

    private static string? ReadKeyword(string sql, int index, out int nextIndex)
    {
        SkipWhitespace(sql, ref index);
        var start = index;
        while (index < sql.Length && char.IsLetter(sql[index])) index++;
        nextIndex = index;
        return index == start ? null : sql[start..index].ToUpperInvariant();
    }

    private static string? ReadIdentifier(string sql, ref int index)
    {
        SkipWhitespace(sql, ref index);
        var start = index;
        while (index < sql.Length && (char.IsLetterOrDigit(sql[index]) || sql[index] == '_')) index++;
        return index == start ? null : sql[start..index];
    }

    private static bool SkipBalanced(string sql, ref int index)
    {
        if (Current(sql, index) != '(') return false;
        var depth = 0;
        while (index < sql.Length)
        {
            var current = sql[index++];
            if (current is '\'' or '"' or '`')
            {
                var quote = current;
                while (index < sql.Length)
                {
                    if (sql[index++] != quote) continue;
                    if (index < sql.Length && sql[index] == quote) { index++; continue; }
                    break;
                }
                continue;
            }

            if (current == '(') depth++;
            else if (current == ')' && --depth == 0) return true;
        }

        return false;
    }

    private static void SkipWhitespace(string sql, ref int index)
    {
        while (index < sql.Length && char.IsWhiteSpace(sql[index])) index++;
    }

    private static char Current(string sql, int index) => index < sql.Length ? sql[index] : '\0';
}

