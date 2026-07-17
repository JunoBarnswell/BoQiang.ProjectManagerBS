using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace AsterERP.Api.Infrastructure.Publishing;

public sealed partial class ApplicationPublishPathGuard(
    IWebHostEnvironment environment,
    IOptions<ApplicationPublishOptions> options)
{
    public string NormalizeAppCode(string appCode)
    {
        var normalized = appCode.Trim().ToUpperInvariant();
        if (!AppCodeRegex().IsMatch(normalized))
        {
            throw new InvalidOperationException("Invalid app code.");
        }

        return normalized;
    }

    public string ResolveRepositoryRoot()
    {
        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        var parent = Directory.GetParent(contentRoot)?.Parent?.FullName;
        return Path.GetFullPath(parent ?? contentRoot);
    }

    public string ResolveOutputRoot()
    {
        var configured = options.Value.OutputRoot;
        var outputRoot = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);

        return Path.GetFullPath(outputRoot);
    }

    public string ResolveTaskRoot(string appCode, string taskId)
    {
        var root = Path.Combine(ResolveOutputRoot(), NormalizeAppCode(appCode), taskId);
        return EnsureInsideRoot(root, ResolveOutputRoot());
    }

    public string EnsureInsideRoot(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Output path is outside publish root.");
        }

        return fullPath;
    }

    [GeneratedRegex("^[A-Z0-9_-]{1,32}$")]
    private static partial Regex AppCodeRegex();
}
