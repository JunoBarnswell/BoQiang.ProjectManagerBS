using AsterERP.Api.Domain.System.Parameters;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Parameters;
using ClosedXML.Excel;
using SqlSugar;
using AsterERP.Contracts.System.Excel;

namespace AsterERP.Api.Application.System.Excel;

public sealed class ParameterExcelService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IUnitOfWork unitOfWork) : IParameterExcelService
{
    public async Task<byte[]> ExportParametersAsync(CancellationToken cancellationToken = default)
    {
        var parameters = await databaseAccessor.GetCurrentDb().Queryable<SystemParameterEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.ParamKey, OrderByType.Asc)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Parameters");
        worksheet.Cell(1, 1).Value = "ParamName";
        worksheet.Cell(1, 2).Value = "ParamKey";
        worksheet.Cell(1, 3).Value = "ParamValue";
        worksheet.Cell(1, 4).Value = "Category";
        worksheet.Cell(1, 5).Value = "IsEnabled";
        worksheet.Cell(1, 6).Value = "Remark";

        for (var i = 0; i < parameters.Count; i++)
        {
            var row = i + 2;
            var item = parameters[i];
            worksheet.Cell(row, 1).Value = item.ParamName;
            worksheet.Cell(row, 2).Value = item.ParamKey;
            worksheet.Cell(row, 3).Value = ParameterSensitivityPolicy.MaskValue(item.ParamKey, item.ParamValue);
            worksheet.Cell(row, 4).Value = item.Category;
            worksheet.Cell(row, 5).Value = item.IsEnabled;
            worksheet.Cell(row, 6).Value = item.Remark ?? string.Empty;
        }

        using var memory = new MemoryStream();
        workbook.SaveAs(memory);
        return memory.ToArray();
    }

    public async Task<ParameterExcelImportResponse> ImportParametersAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet("Parameters");
        var usedRows = worksheet.RowsUsed().Skip(1).ToList();

        var rows = new List<ParameterExcelImportRow>();
        foreach (var row in usedRows)
        {
            rows.Add(new ParameterExcelImportRow(
                row.RowNumber(),
                row.Cell(1).GetString().Trim(),
                row.Cell(2).GetString().Trim(),
                row.Cell(3).GetString().Trim(),
                row.Cell(4).GetString().Trim(),
                row.Cell(5).GetBoolean(),
                row.Cell(6).GetString().Trim()));
        }

        var inserted = 0;
        var updated = 0;

        await unitOfWork.ExecuteAsync(async () =>
        {
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(row.ParamName) || string.IsNullOrWhiteSpace(row.ParamKey))
                {
                    continue;
                }

                var existing = (await databaseAccessor.GetCurrentDb().Queryable<SystemParameterEntity>()
                        .Where(item => item.ParamKey == row.ParamKey && !item.IsDeleted)
                        .Take(1)
                        .ToListAsync(cancellationToken))
                    .FirstOrDefault();

                if (existing is null)
                {
                    if (IsMaskedSensitiveImport(row.ParamKey, row.ParamValue))
                    {
                        continue;
                    }

                    var created = new SystemParameterEntity
                    {
                        ParamName = row.ParamName,
                        ParamKey = row.ParamKey,
                        ParamValue = row.ParamValue,
                        Category = string.IsNullOrWhiteSpace(row.Category) ? "general" : row.Category,
                        IsEnabled = row.IsEnabled,
                        Remark = string.IsNullOrWhiteSpace(row.Remark) ? null : row.Remark
                    };

                    await databaseAccessor.GetCurrentDb().Insertable(created).ExecuteCommandAsync(cancellationToken);
                    inserted++;
                    continue;
                }

                existing.ParamName = row.ParamName;
                if (!ParameterSensitivityPolicy.ShouldKeepExistingValue(row.ParamKey, row.ParamValue, existing.ParamValue))
                {
                    existing.ParamValue = row.ParamValue;
                }

                existing.Category = string.IsNullOrWhiteSpace(row.Category) ? "general" : row.Category;
                existing.IsEnabled = row.IsEnabled;
                existing.Remark = string.IsNullOrWhiteSpace(row.Remark) ? null : row.Remark;

                await databaseAccessor.GetCurrentDb().Updateable(existing).ExecuteCommandAsync(cancellationToken);
                updated++;
            }
        }, cancellationToken);

        return new ParameterExcelImportResponse(rows.Count, inserted, updated);
    }

    private static bool IsMaskedSensitiveImport(string paramKey, string paramValue)
    {
        return ParameterSensitivityPolicy.IsSensitiveKey(paramKey) &&
            paramValue == ParameterSensitivityPolicy.SensitiveValueMask;
    }
}

