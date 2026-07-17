namespace AsterERP.Contracts.System.Excel;

public sealed record ParameterExcelImportRow(
    int RowNumber,
    string ParamName,
    string ParamKey,
    string ParamValue,
    string Category,
    bool IsEnabled,
    string? Remark);
