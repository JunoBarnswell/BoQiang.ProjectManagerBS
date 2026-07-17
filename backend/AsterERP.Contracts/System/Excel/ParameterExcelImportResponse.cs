namespace AsterERP.Contracts.System.Excel;

public sealed record ParameterExcelImportResponse(
    int TotalRows,
    int InsertedRows,
    int UpdatedRows);
