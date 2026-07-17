namespace AsterERP.Api.Application.System.Files;

public static class FileContentTypeResolver
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "application/pdf",
        ["doc"] = "application/msword",
        ["dot"] = "application/msword",
        ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["docm"] = "application/vnd.ms-word.document.macroEnabled.12",
        ["dotx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
        ["dotm"] = "application/vnd.ms-word.template.macroEnabled.12",
        ["xls"] = "application/vnd.ms-excel",
        ["xlt"] = "application/vnd.ms-excel",
        ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["xltx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
        ["xlsm"] = "application/vnd.ms-excel.sheet.macroEnabled.12",
        ["xlsb"] = "application/vnd.ms-excel.sheet.binary.macroEnabled.12",
        ["csv"] = "text/csv",
        ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["pptm"] = "application/vnd.ms-powerpoint.presentation.macroEnabled.12",
        ["ppsx"] = "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
        ["ppsm"] = "application/vnd.ms-powerpoint.slideshow.macroEnabled.12",
        ["potx"] = "application/vnd.openxmlformats-officedocument.presentationml.template",
        ["potm"] = "application/vnd.ms-powerpoint.template.macroEnabled.12",
        ["rtf"] = "application/rtf",
        ["odt"] = "application/vnd.oasis.opendocument.text",
        ["odp"] = "application/vnd.oasis.opendocument.presentation",
        ["ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        ["ofd"] = "application/ofd",
        ["zip"] = "application/zip",
        ["rar"] = "application/vnd.rar",
        ["7z"] = "application/x-7z-compressed",
        ["tar"] = "application/x-tar",
        ["gz"] = "application/gzip",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["bmp"] = "image/bmp",
        ["svg"] = "image/svg+xml",
        ["webp"] = "image/webp",
        ["avif"] = "image/avif",
        ["ico"] = "image/x-icon",
        ["tif"] = "image/tiff",
        ["tiff"] = "image/tiff",
        ["heic"] = "image/heic",
        ["heif"] = "image/heif",
        ["jxl"] = "image/jxl",
        ["mp4"] = "video/mp4",
        ["webm"] = "video/webm",
        ["mp3"] = "audio/mpeg",
        ["wav"] = "audio/wav",
        ["txt"] = "text/plain",
        ["log"] = "text/plain",
        ["md"] = "text/markdown",
        ["markdown"] = "text/markdown",
        ["json"] = "application/json",
        ["xml"] = "application/xml",
        ["html"] = "text/html",
        ["htm"] = "text/html",
        ["css"] = "text/css",
        ["js"] = "text/javascript",
        ["ts"] = "text/typescript"
    };

    public static string Resolve(string fileName, string? suppliedContentType)
    {
        var extension = FilePreviewFormatCatalog.NormalizeExtensionFromFileName(fileName);
        if (!string.IsNullOrWhiteSpace(extension) && ContentTypes.TryGetValue(extension, out var mapped))
        {
            return mapped;
        }

        var normalizedContentType = suppliedContentType?.Trim();
        return string.IsNullOrWhiteSpace(normalizedContentType) || normalizedContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            ? ResolveCatalogContentType(extension)
            : normalizedContentType;
    }

    private static string ResolveCatalogContentType(string extension)
    {
        var contentType = FilePreviewFormatCatalog.Resolve(extension)?.ContentType;
        return string.IsNullOrWhiteSpace(contentType) || contentType.Contains('*', StringComparison.Ordinal)
            ? "application/octet-stream"
            : contentType;
    }
}
