namespace AsterERP.Api.Application.System.Files;

public sealed record FilePreviewFormat(
    string Extension,
    string Category,
    string ContentType,
    string ViewerType,
    string PreviewPipeline);
