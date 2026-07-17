using AsterERP.Contracts.System.Excel;

namespace AsterERP.Api.Application.System.Excel;

public interface IParameterExcelService
{
    Task<byte[]> ExportParametersAsync(CancellationToken cancellationToken = default);

    Task<ParameterExcelImportResponse> ImportParametersAsync(Stream stream, CancellationToken cancellationToken = default);
}
