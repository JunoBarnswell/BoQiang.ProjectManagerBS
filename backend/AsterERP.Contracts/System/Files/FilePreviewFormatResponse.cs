namespace AsterERP.Contracts.System.Files;

public sealed record FilePreviewFormatResponse(
    string Extension,
    string Category,
    string ContentType,
    string ViewerType,
    string PreviewPipeline);
