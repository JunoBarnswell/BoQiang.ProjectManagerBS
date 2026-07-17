namespace AsterERP.Api.Application.System.Files;

public sealed record FilePreviewStreamResult(
    string FileName,
    string ContentType,
    long Size,
    Stream Stream);
