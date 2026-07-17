using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Files;
using AsterERP.Api.Infrastructure.Files;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Files;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.System.Files;

public sealed class FileAppService(
    IRepository<SystemFileRecordEntity> fileRepository,
    IFileStorageService fileStorageService,
    IFileContentHashService fileContentHashService,
    IUnitOfWork unitOfWork) : IFileAppService
{
    public async Task<GridPageResult<FileRecordResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default)
    {
        var keyword = gridQuery.Keyword?.Trim();
        var page = await fileRepository.GridPageAsync(
            gridQuery,
            string.IsNullOrWhiteSpace(keyword)
                ? null
                : item => item.FileName.Contains(keyword),
            cancellationToken: cancellationToken);

        return new GridPageResult<FileRecordResponse>
        {
            Total = page.Total,
            Summary = page.Summary,
            Items = page.Items.Select(Map).ToList()
        };
    }

    public async Task<FileRecordResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        return Map(await EnsureExistsAsync(id, cancellationToken));
    }

    public async Task<FileUploadResponse> UploadAsync(IFormFile file, string? remark, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new ValidationException("上传文件不能为空");
        }

        await using var readStream = file.OpenReadStream();
        var memory = new MemoryStream();
        await readStream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        var storedPath = await fileStorageService.SaveAsync(memory, file.FileName, cancellationToken);
        var sha256 = await fileContentHashService.ComputeSha256Async(memory, cancellationToken);
        var contentType = FileContentTypeResolver.Resolve(file.FileName, file.ContentType);

        var entity = new SystemFileRecordEntity
        {
            FileName = file.FileName,
            StoredPath = storedPath,
            ContentType = contentType,
            FileSize = file.Length,
            Sha256 = sha256,
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim()
        };

        await fileRepository.InsertAsync(entity, cancellationToken);

        return MapUpload(entity);
    }

    public Task<IReadOnlyList<FilePreviewFormatResponse>> GetPreviewFormatsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(FilePreviewFormatCatalog.ToResponses());
    }

    public async Task<(FileRecordResponse Metadata, Stream Stream)> DownloadAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await EnsureExistsAsync(id, cancellationToken);
        var stream = await fileStorageService.OpenReadAsync(record.StoredPath, cancellationToken);
        return (Map(record), stream);
    }

    public async Task<FilePreviewStreamResult> PreviewAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await EnsureExistsAsync(id, cancellationToken);
        var extension = FilePreviewFormatCatalog.NormalizeExtensionFromFileName(record.FileName);
        if (!FilePreviewFormatCatalog.IsSupported(extension))
        {
            throw new ValidationException("当前文件格式不支持预览");
        }

        var stream = await fileStorageService.OpenReadAsync(record.StoredPath, cancellationToken);
        var contentType = FileContentTypeResolver.Resolve(record.FileName, record.ContentType);
        return new FilePreviewStreamResult(record.FileName, contentType, record.FileSize, stream);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await EnsureExistsAsync(id, cancellationToken);

        await unitOfWork.ExecuteAsync(async () =>
        {
            await fileStorageService.DeleteAsync(record.StoredPath, cancellationToken);
            await fileRepository.DeleteAsync(record.Id, cancellationToken);
        }, cancellationToken);
    }

    private async Task<SystemFileRecordEntity> EnsureExistsAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await fileRepository.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        return entity ?? throw new NotFoundException("文件不存在", ErrorCodes.FileNotFound);
    }

    private static FileRecordResponse Map(SystemFileRecordEntity entity)
    {
        var extension = FilePreviewFormatCatalog.NormalizeExtensionFromFileName(entity.FileName);
        var format = FilePreviewFormatCatalog.Resolve(extension);

        return new FileRecordResponse(
            entity.Id,
            entity.FileName,
            FileContentTypeResolver.Resolve(entity.FileName, entity.ContentType),
            entity.FileSize,
            entity.StoredPath,
            entity.CreatedTime,
            entity.Remark,
            extension,
            BuildDownloadUrl(entity.Id),
            BuildPreviewUrl(entity.Id),
            format is not null,
            format?.Category,
            format?.ViewerType,
            format?.PreviewPipeline);
    }

    private static FileUploadResponse MapUpload(SystemFileRecordEntity entity)
    {
        var extension = FilePreviewFormatCatalog.NormalizeExtensionFromFileName(entity.FileName);
        var format = FilePreviewFormatCatalog.Resolve(extension);

        return new FileUploadResponse(
            entity.Id,
            entity.FileName,
            BuildDownloadUrl(entity.Id),
            entity.FileSize,
            extension,
            BuildPreviewUrl(entity.Id),
            format is not null,
            format?.Category,
            format?.ViewerType,
            format?.PreviewPipeline);
    }

    private static string BuildDownloadUrl(string id) => $"/api/system/files/{Uri.EscapeDataString(id)}/download";

    private static string BuildPreviewUrl(string id) => $"/api/system/files/{Uri.EscapeDataString(id)}/preview";
}
