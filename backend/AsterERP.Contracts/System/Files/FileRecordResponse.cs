namespace AsterERP.Contracts.System.Files;

public sealed record FileRecordResponse(
    string Id,
    string FileName,
    string ContentType,
    long Size,
    string RelativePath,
    DateTime CreatedTime,
    string? Remark,
    string Extension,
    string DownloadUrl,
    string PreviewUrl,
    bool PreviewSupported,
    string? PreviewCategory,
    string? PreviewType,
    string? PreviewPipeline);
