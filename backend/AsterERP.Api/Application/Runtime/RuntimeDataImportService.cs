using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using ClosedXML.Excel;

namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimeDataImportService(
    IRuntimeDataModelService runtimeDataModelService,
    IRuntimePageSchemaService pageSchemaService,
    ICurrentUser currentUser)
{
    private const int MaxImportRows = 1000;
    private const int MaxPreviewRows = 200;
    private const int MaxExportRows = 5000;
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<(byte[] Content, string FileName)> BuildImportTemplateAsync(
        string modelCode,
        string pageCode,
        CancellationToken cancellationToken = default)
    {
        await EnsurePageActionAsync(modelCode, pageCode, "import", cancellationToken);
        var fields = await GetWritableFieldsAsync(modelCode, cancellationToken);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Import");

        for (var index = 0; index < fields.Count; index++)
        {
            var cell = worksheet.Cell(1, index + 1);
            cell.Value = fields[index].FieldCode;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return (stream.ToArray(), $"{modelCode}-import-template-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    public async Task<RuntimeImportPreviewResponse> PreviewAsync(
        string modelCode,
        string pageCode,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        await EnsurePageActionAsync(modelCode, pageCode, "import", cancellationToken);
        var fields = await GetWritableFieldsAsync(modelCode, cancellationToken);
        var rows = ReadImportRows(stream, fields, MaxPreviewRows);
        return BuildPreviewResponse(modelCode, pageCode, fields, rows);
    }

    public async Task<RuntimeImportResponse> ImportAsync(
        string modelCode,
        string pageCode,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        await EnsurePageActionAsync(modelCode, pageCode, "import", cancellationToken);
        var fields = await GetWritableFieldsAsync(modelCode, cancellationToken);
        var rows = ReadImportRows(stream, fields, MaxImportRows);
        var createdRows = 0;
        var errors = new List<RuntimeImportPreviewRowResponse>();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.Errors.Count > 0)
            {
                errors.Add(row);
                continue;
            }

            try
            {
                await runtimeDataModelService.CreateAsync(modelCode, row.Values, cancellationToken);
                createdRows++;
            }
            catch (Exception ex) when (ex is ValidationException or NotFoundException)
            {
                errors.Add(row with { Errors = [ex.Message] });
            }
        }

        return new RuntimeImportResponse(
            modelCode,
            pageCode,
            rows.Count,
            createdRows,
            errors.Count,
            errors);
    }

    public async Task<RuntimeExportResponse> ExportAsync(
        string modelCode,
        RuntimeExportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PageCode))
        {
            throw new ValidationException("导出必须提供页面编码", ErrorCodes.PermissionDenied);
        }

        await EnsurePageActionAsync(modelCode, request.PageCode, "export", cancellationToken);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? MaxExportRows : request.PageSize, 1, MaxExportRows);
        var query = new RuntimeQueryRequest(
            1,
            pageSize,
            request.Keyword,
            request.Filters,
            request.Sorts,
            request.PageCode);
        var result = await runtimeDataModelService.QueryAsync(modelCode, query, cancellationToken);
        var columns = ResolveExportColumns(request.Columns, result.Fields, result.Rows);
        var content = BuildExportWorkbook(columns, result.Rows);
        var fileName = $"{modelCode}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return new RuntimeExportResponse(fileName, ExcelContentType, Convert.ToBase64String(content), result.Total);
    }

    private async Task EnsurePageActionAsync(
        string modelCode,
        string pageCode,
        string action,
        CancellationToken cancellationToken)
    {
        var page = await pageSchemaService.GetPublishedPageAsync(pageCode, cancellationToken: cancellationToken);
        if (!string.Equals(page.ModelCode, modelCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("运行时页面与数据模型不匹配", ErrorCodes.RuntimeDataModelInvalid);
        }

        var permissionCode = ResolveActionPermission(page.PermissionCode, page.PageCode, action);
        if (!currentUser.HasAsterErpPermission(permissionCode))
        {
            throw new ValidationException("无权限执行该运行时页面动作", ErrorCodes.PermissionDenied);
        }
    }

    private async Task<IReadOnlyList<RuntimeDataFieldResponse>> GetWritableFieldsAsync(
        string modelCode,
        CancellationToken cancellationToken)
    {
        var definition = await runtimeDataModelService.GetPublishedDefinitionAsync(modelCode, cancellationToken);
        var fields = definition.ToFieldResponses()
            .Where(item => item.Visible && item.Writable)
            .OrderBy(item => item.Order)
            .ToArray();

        if (fields.Length == 0)
        {
            throw new ValidationException("当前运行模型没有可导入字段", ErrorCodes.RuntimeFieldNotAllowed);
        }

        return fields;
    }

    private static IReadOnlyList<RuntimeImportPreviewRowResponse> ReadImportRows(
        Stream stream,
        IReadOnlyList<RuntimeDataFieldResponse> fields,
        int maxRows)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new ValidationException("导入文件没有工作表", ErrorCodes.ParameterInvalid);
        var headerRow = worksheet.FirstRowUsed()
            ?? throw new ValidationException("导入文件没有表头", ErrorCodes.ParameterInvalid);
        var fieldMap = BuildHeaderFieldMap(fields);
        var columnFields = ResolveColumnFields(headerRow, fieldMap);
        var rows = new List<RuntimeImportPreviewRowResponse>();

        foreach (var row in worksheet.RowsUsed().Where(item => item.RowNumber() > headerRow.RowNumber()))
        {
            if (rows.Count >= maxRows)
            {
                rows.Add(new RuntimeImportPreviewRowResponse(row.RowNumber(), new Dictionary<string, object?>(), [$"单次最多导入 {maxRows} 行"]));
                break;
            }

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            for (var index = 0; index < columnFields.Count; index++)
            {
                var field = columnFields[index];
                if (field is null)
                {
                    continue;
                }

                var cell = row.Cell(index + 1);
                try
                {
                    values[field.FieldCode] = ConvertCellValue(cell, field);
                }
                catch (ValidationException ex)
                {
                    errors.Add(ex.Message);
                }
            }

            if (values.Count == 0 && errors.Count == 0)
            {
                continue;
            }

            rows.Add(new RuntimeImportPreviewRowResponse(row.RowNumber(), values, errors));
        }

        return rows;
    }

    private static RuntimeImportPreviewResponse BuildPreviewResponse(
        string modelCode,
        string pageCode,
        IReadOnlyList<RuntimeDataFieldResponse> fields,
        IReadOnlyList<RuntimeImportPreviewRowResponse> rows)
    {
        var invalidRows = rows.Count(item => item.Errors.Count > 0);
        return new RuntimeImportPreviewResponse(
            modelCode,
            pageCode,
            rows.Count,
            rows.Count - invalidRows,
            invalidRows,
            fields,
            rows);
    }

    private static Dictionary<string, RuntimeDataFieldResponse> BuildHeaderFieldMap(
        IReadOnlyList<RuntimeDataFieldResponse> fields)
    {
        var map = new Dictionary<string, RuntimeDataFieldResponse>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            map[field.FieldCode] = field;
            map[field.FieldName] = field;
        }

        return map;
    }

    private static IReadOnlyList<RuntimeDataFieldResponse?> ResolveColumnFields(
        IXLRow headerRow,
        IReadOnlyDictionary<string, RuntimeDataFieldResponse> fieldMap)
    {
        return headerRow.CellsUsed()
            .Select(cell =>
            {
                var header = cell.GetString().Trim();
                return fieldMap.TryGetValue(header, out var field) ? field : null;
            })
            .ToArray();
    }

    private static object? ConvertCellValue(IXLCell cell, RuntimeDataFieldResponse field)
    {
        var text = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (IsInteger(field.DataType))
        {
            return long.TryParse(text, out var value)
                ? value
                : throw new ValidationException($"{field.FieldName} 必须是整数", ErrorCodes.ParameterInvalid);
        }

        if (IsDecimal(field.DataType))
        {
            return decimal.TryParse(text, out var value)
                ? value
                : throw new ValidationException($"{field.FieldName} 必须是数字", ErrorCodes.ParameterInvalid);
        }

        if (IsBoolean(field.DataType))
        {
            return bool.TryParse(text, out var value)
                ? value
                : throw new ValidationException($"{field.FieldName} 必须是布尔值", ErrorCodes.ParameterInvalid);
        }

        if (IsDateTime(field.DataType))
        {
            return DateTime.TryParse(text, out var value)
                ? value
                : throw new ValidationException($"{field.FieldName} 必须是日期时间", ErrorCodes.ParameterInvalid);
        }

        return text;
    }

    private static IReadOnlyList<string> ResolveExportColumns(
        IReadOnlyList<string>? requestedColumns,
        IReadOnlyList<RuntimeDataFieldResponse> fields,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var exportable = fields
            .Where(item => item.Visible && item.Exportable)
            .Select(item => item.FieldCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requestedColumns is { Count: > 0 })
        {
            return requestedColumns.Where(exportable.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var rowColumns = rows.FirstOrDefault()?.Keys ?? [];
        return rowColumns.Where(exportable.Contains).ToArray();
    }

    private static byte[] BuildExportWorkbook(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Export");
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = columns[columnIndex];
            worksheet.Cell(1, columnIndex + 1).Style.Font.Bold = true;
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                rows[rowIndex].TryGetValue(columns[columnIndex], out var value);
                worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = value?.ToString() ?? string.Empty;
            }
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string ResolveActionPermission(string? viewPermissionCode, string pageCode, string action)
    {
        if (!string.IsNullOrWhiteSpace(viewPermissionCode) &&
            viewPermissionCode.EndsWith(":view", StringComparison.OrdinalIgnoreCase))
        {
            return $"{viewPermissionCode[..^":view".Length]}:{action}";
        }

        return PermissionCodes.BuildAppRuntimePagePermission(pageCode, action);
    }

    private static bool IsInteger(string dataType) =>
        dataType.Contains("int", StringComparison.OrdinalIgnoreCase);

    private static bool IsDecimal(string dataType) =>
        dataType.Contains("decimal", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("numeric", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("money", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("real", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("double", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("float", StringComparison.OrdinalIgnoreCase);

    private static bool IsBoolean(string dataType) =>
        dataType.Contains("bool", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(dataType, "bit", StringComparison.OrdinalIgnoreCase);

    private static bool IsDateTime(string dataType) =>
        dataType.Contains("date", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("time", StringComparison.OrdinalIgnoreCase);
}
