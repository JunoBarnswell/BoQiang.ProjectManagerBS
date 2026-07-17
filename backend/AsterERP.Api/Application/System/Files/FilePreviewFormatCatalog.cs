using AsterERP.Contracts.System.Files;

namespace AsterERP.Api.Application.System.Files;

public static class FilePreviewFormatCatalog
{
    private static readonly IReadOnlyList<FilePreviewFormat> Formats =
    [
        ..Build("office", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "word", "Word OpenXML", "docx", "docm", "dotx", "dotm"),
        ..Build("office", "application/msword", "word", "Word Binary", "doc", "dot"),
        ..Build("office", "application/vnd.openxmlformats-officedocument.presentationml.presentation", "presentation", "PowerPoint", "pptx", "pptm", "potx", "potm", "ppsx", "ppsm"),
        ..Build("office", "application/vnd.oasis.opendocument.presentation", "presentation", "PowerPoint", "odp"),
        ..Build("office", "application/rtf", "word", "Open Document", "rtf"),
        ..Build("office", "application/vnd.oasis.opendocument.text", "word", "Open Document", "odt"),
        ..Build("office", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "spreadsheet", "Spreadsheet", "xlsx", "xltx", "xlsm", "xlsb", "xls", "xlt", "xltm", "csv", "ods", "fods", "numbers"),
        ..Build("document", "application/pdf", "pdf", "PDF", "pdf"),
        ..Build("document", "application/ofd", "ofd", "OFD", "ofd"),
        ..Build("document", "text/plain", "typst", "Typst", "typ", "typst"),
        ..Build("archive", "application/zip", "archive", "Archive", "zip", "zipx", "7z", "rar", "tar", "gz", "gzip", "tgz", "bz2", "bzip2", "tbz", "tbz2", "xz", "txz", "lzma", "zst", "tzst", "cab", "ar", "cpio", "iso", "xar", "lha", "lzh", "jar", "war", "ear", "apk", "cbz", "cbr"),
        ..Build("email", "message/rfc822", "email", "Email", "eml", "msg", "mbox"),
        ..Build("eda", "application/octet-stream", "eda", "EDA", "olb", "dra", "gds", "oas", "oasis"),
        ..Build("cad", "application/octet-stream", "cad", "CAD", "dxf", "dwg", "dwf", "dwfx", "xps"),
        ..Build("model", "model/gltf-binary", "3d", "3D Model", "glb", "gltf", "obj", "stl", "ply", "fbx", "dae", "3ds", "3mf", "amf", "usd", "usda", "usdc", "usdz", "kmz", "step", "stp", "iges", "igs", "ifc", "3dm", "brep", "pcd", "wrl", "vrml", "xyz", "vtk", "vtp"),
        ..Build("geo", "application/geo+json", "geo", "Geospatial", "geojson", "kml", "gpx", "shp"),
        ..Build("drawing", "text/plain", "drawing", "Drawing", "excalidraw", "drawio", "dio", "mermaid", "mmd", "plantuml", "puml"),
        ..Build("mindmap", "application/octet-stream", "mindmap", "Mind Map", "xmind"),
        ..Build("ebook", "application/epub+zip", "epub", "EPUB", "epub"),
        ..Build("ebook", "application/octet-stream", "umd", "UMD", "umd"),
        ..Build("image", "image/*", "image", "Image", "gif", "jpg", "jpeg", "bmp", "tiff", "tif", "png", "svg", "webp", "avif", "ico", "heic", "heif", "jxl"),
        ..Build("markdown", "text/markdown", "markdown", "Markdown", "md", "markdown"),
        ..Build("code", "text/plain", "text", "Code and Text", "txt", "json", "js", "mjs", "cjs", "css", "java", "py", "html", "htm", "jsx", "ts", "tsx", "xml", "log", "vue", "yaml", "yml", "ini", "sh", "bash", "sql", "go", "rs", "php", "c", "cpp", "cc", "h", "hpp", "cs", "diff", "patch", "bundle", "bdl", "jsonc", "json5", "ipynb", "toml", "proto", "hcl", "tex", "gv", "http", "react", "rb", "swift", "kt", "kts", "scala", "dart", "lua", "pl", "ps1", "bat", "cmd", "dockerfile", "gitignore", "editorconfig"),
        ..Build("media", "video/mp4", "video", "Video", "mp4", "webm", "m3u8"),
        ..Build("media", "audio/mpeg", "audio", "Audio", "mp3", "mpeg", "wav", "ogg", "oga", "opus", "m4a", "aac", "flac", "weba", "midi", "mid"),
        ..Build("asset", "application/octet-stream", "data", "Data Asset", "ttf", "otf", "woff", "woff2", "psd", "ai", "eps", "sqlite", "wasm", "parquet", "avro", "webarchive")
    ];

    private static readonly IReadOnlyDictionary<string, FilePreviewFormat> ByExtension = Formats
        .GroupBy(item => item.Extension, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<FilePreviewFormat> All => Formats;

    public static bool IsSupported(string? extension) => Resolve(extension) is not null;

    public static FilePreviewFormat? Resolve(string? extension)
    {
        var normalized = NormalizeExtension(extension);
        return normalized is not null && ByExtension.TryGetValue(normalized, out var format)
            ? format
            : null;
    }

    public static IReadOnlyList<FilePreviewFormatResponse> ToResponses() =>
        Formats
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Extension)
            .Select(item => new FilePreviewFormatResponse(
                item.Extension,
                item.Category,
                item.ContentType,
                item.ViewerType,
                item.PreviewPipeline))
            .ToList();

    public static string NormalizeExtensionFromFileName(string? fileName) =>
        NormalizeExtension(Path.GetExtension(fileName ?? string.Empty)) ?? string.Empty;

    private static string? NormalizeExtension(string? extension)
    {
        var value = extension?.Trim().TrimStart('.').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<FilePreviewFormat> Build(
        string category,
        string contentType,
        string viewerType,
        string previewPipeline,
        params string[] extensions) =>
        extensions
            .Select(extension => new FilePreviewFormat(
                extension.TrimStart('.').ToLowerInvariant(),
                category,
                contentType,
                viewerType,
                previewPipeline))
            .ToList();
}
