namespace AsterERP.Contracts.System.Files;

public sealed record FileUploadResponse(
    string Id,
    string FileName,
    string DownloadUrl,
    long Size,
    string Extension,
    string PreviewUrl,
    bool PreviewSupported,
    string? PreviewCategory,
    string? PreviewType,
    string? PreviewPipeline);
