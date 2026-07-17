using AsterERP.Shared;
using AsterERP.Contracts.System.Files;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.System.Files;

public interface IFileAppService
{
    Task<GridPageResult<FileRecordResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<FileRecordResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FilePreviewFormatResponse>> GetPreviewFormatsAsync(CancellationToken cancellationToken = default);

    Task<FileUploadResponse> UploadAsync(IFormFile file, string? remark, CancellationToken cancellationToken = default);

    Task<(FileRecordResponse Metadata, Stream Stream)> DownloadAsync(string id, CancellationToken cancellationToken = default);

    Task<FilePreviewStreamResult> PreviewAsync(string id, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
