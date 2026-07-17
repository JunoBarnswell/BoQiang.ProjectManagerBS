using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using ClosedXML.Excel;
using SqlSugar;
using SugarDbType = SqlSugar.DbType;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed partial class ApplicationDataPreviewReader
{
    private const int MaxPreviewRows = 100;
    private readonly ApplicationDataSourceProviderRegistry providerRegistry;

    public ApplicationDataPreviewReader(ApplicationDataSourceProviderRegistry providerRegistry)
    {
        this.providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
    }

    public async Task<ApplicationDataCenterPreviewResponse> PreviewDatabaseAsync(
        ISqlSugarClient db,
        string? sql,
        string? tableName,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(maxRows <= 0 ? 20 : maxRows, 1, MaxPreviewRows);
        var provider = ResolveProvider(db);
        var previewSql = ResolvePreviewSql(sql, tableName, limit, provider);
        cancellationToken.ThrowIfCancellationRequested();
        var dataTable = await ExecuteDataTableAsync(db, previewSql, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return FromDataTable(dataTable, "数据库预览成功");
    }

    public ApplicationDataCenterPreviewResponse PreviewExcel(
        string filePath,
        string? sheetName,
        int headerRow,
        int dataStartRow,
        int maxRows)
    {
        if (!File.Exists(filePath))
        {
            throw new ValidationException("Excel 文件不存在", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        using var workbook = new XLWorkbook(filePath);
        var worksheet = string.IsNullOrWhiteSpace(sheetName)
            ? workbook.Worksheets.First()
            : workbook.Worksheets.Worksheet(sheetName);
        var header = Math.Max(headerRow <= 0 ? 1 : headerRow, 1);
        var start = Math.Max(dataStartRow <= 0 ? header + 1 : dataStartRow, header + 1);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastColumn == 0 || lastRow < start)
        {
            return new ApplicationDataCenterPreviewResponse([], [], "Excel 没有可预览数据");
        }

        var fields = new List<ApplicationDataCenterPreviewFieldResponse>();
        for (var column = 1; column <= lastColumn; column += 1)
        {
            var rawName = worksheet.Cell(header, column).GetValue<string>().Trim();
            var name = string.IsNullOrWhiteSpace(rawName) ? $"Column{column}" : rawName;
            fields.Add(new ApplicationDataCenterPreviewFieldResponse(
                NormalizeFieldCode(name, column),
                name,
                "Text",
                true,
                column == 1,
                column));
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var take = Math.Min(lastRow, start + Math.Clamp(maxRows <= 0 ? 20 : maxRows, 1, MaxPreviewRows) - 1);
        for (var row = start; row <= take; row += 1)
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
            {
                item[field.FieldCode] = worksheet.Cell(row, field.Order).GetFormattedString();
            }

            rows.Add(item);
        }

        return new ApplicationDataCenterPreviewResponse(rows, fields, "Excel 预览成功");
    }

    public ApplicationDataCenterPreviewResponse PreviewCsv(
        string filePath,
        string delimiter,
        bool firstRowHeader,
        int dataStartRow,
        Encoding encoding,
        int maxRows)
    {
        if (!File.Exists(filePath))
        {
            throw new ValidationException("CSV 文件不存在", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var separator = string.IsNullOrEmpty(delimiter) ? "," : delimiter;
        if (separator.Length != 1)
        {
            throw new ValidationException("CSV 分隔符必须是单个字符", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        using var reader = new StreamReader(filePath, encoding, detectEncodingFromByteOrderMarks: true);
        using var parser = new TextFieldParser(reader)
        {
            Delimiters = [separator],
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        var lines = new List<string[]>();
        while (!parser.EndOfData && lines.Count < Math.Clamp(maxRows <= 0 ? 20 : maxRows, 1, MaxPreviewRows) + 5)
        {
            lines.Add(parser.ReadFields() ?? []);
        }

        if (lines.Count == 0)
        {
            return new ApplicationDataCenterPreviewResponse([], [], "CSV 没有可预览数据");
        }

        var headerValues = firstRowHeader ? lines[0] : Enumerable.Range(1, lines[0].Length).Select(index => $"Column{index}").ToArray();
        var fields = headerValues
            .Select((name, index) => new ApplicationDataCenterPreviewFieldResponse(
                NormalizeFieldCode(string.IsNullOrWhiteSpace(name) ? $"Column{index + 1}" : name, index + 1),
                string.IsNullOrWhiteSpace(name) ? $"Column{index + 1}" : name,
                "Text",
                true,
                index == 0,
                index + 1))
            .ToArray();
        var startIndex = firstRowHeader ? 1 : Math.Max(dataStartRow - 1, 0);
        var rows = lines
            .Skip(startIndex)
            .Take(Math.Clamp(maxRows <= 0 ? 20 : maxRows, 1, MaxPreviewRows))
            .Select(values =>
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < fields.Length; index += 1)
                {
                    row[fields[index].FieldCode] = index < values.Length ? values[index] : null;
                }

                return (IReadOnlyDictionary<string, object?>)row;
            })
            .ToArray();

        return new ApplicationDataCenterPreviewResponse(rows, fields, "CSV 预览成功");
    }

    public static ApplicationDataCenterPreviewResponse FromDataTable(DataTable dataTable, string message)
    {
        var fields = dataTable.Columns
            .Cast<DataColumn>()
            .Select((column, index) => new ApplicationDataCenterPreviewFieldResponse(
                column.ColumnName,
                column.ColumnName,
                column.DataType.Name,
                column.AllowDBNull,
                index == 0,
                index + 1))
            .ToArray();
        var rows = dataTable.Rows
            .Cast<DataRow>()
            .Select(row =>
            {
                var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn column in dataTable.Columns)
                {
                    var value = row[column];
                    item[column.ColumnName] = value == DBNull.Value ? null : value;
                }

                return (IReadOnlyDictionary<string, object?>)item;
            })
            .ToArray();
        return new ApplicationDataCenterPreviewResponse(rows, fields, message);
    }

    private static async Task<DataTable> ExecuteDataTableAsync(
        ISqlSugarClient db,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("当前数据源不支持异步结果读取");
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        for (var index = 0; index < reader.FieldCount; index++)
            table.Columns.Add(reader.GetName(index), reader.GetFieldType(index));
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = table.NewRow();
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = await reader.IsDBNullAsync(index, cancellationToken) ? DBNull.Value : reader.GetValue(index);
            table.Rows.Add(row);
        }

        return table;
    }

    private IApplicationDataSourceProvider ResolveProvider(ISqlSugarClient db)
    {
        var providerType = db.CurrentConnectionConfig.DbType switch
        {
            SugarDbType.Sqlite => ApplicationDataSourceType.Sqlite,
            SugarDbType.MySql => ApplicationDataSourceType.MySql,
            SugarDbType.PostgreSQL => ApplicationDataSourceType.PostgreSql,
            SugarDbType.SqlServer => ApplicationDataSourceType.SqlServer,
            _ => throw new ValidationException("当前数据库 provider 不受支持", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };

        return providerRegistry.Resolve(providerType);
    }

    internal static string ResolvePreviewSql(
        string? sql,
        string? tableName,
        int limit,
        IApplicationDataSourceProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(sql))
        {
            var normalizedSql = ApplicationDataSourceSqlPolicy.RequireSelectSql(sql, "预览 SQL");
            return provider.BuildPreviewSql(normalizedSql, limit);
        }

        var normalizedTable = tableName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTable) || !TableNameRegex().IsMatch(normalizedTable))
        {
            throw new ValidationException("表名不能为空且只能包含字母、数字、下划线、点", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var tableParts = normalizedTable.Split('.', StringSplitOptions.None);
        if (tableParts.Length > 2 || tableParts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ValidationException("表名只允许使用 table 或 schema.table 格式", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (tableParts.Length == 2 && !provider.Capability.SupportsSchemas)
        {
            throw new ValidationException("当前数据库 provider 不支持 schema 表名", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var quotedTable = tableParts.Length == 2
            ? provider.QuoteQualified(tableParts[0], tableParts[1])
            : provider.QuoteQualified(null, tableParts[0]);
        return provider.BuildPreviewSql($"SELECT * FROM {quotedTable}", limit);
    }

    private static string NormalizeFieldCode(string value, int index)
    {
        var normalized = Regex.Replace(value.Trim(), "[^A-Za-z0-9_]", "_");
        if (string.IsNullOrWhiteSpace(normalized) || char.IsDigit(normalized[0]))
        {
            normalized = $"field_{index}";
        }

        return normalized;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.]*$")]
    private static partial Regex TableNameRegex();
}
