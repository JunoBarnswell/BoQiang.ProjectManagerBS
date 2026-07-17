using System.Text;
using System.Text.RegularExpressions;

namespace AsterERP.Api.Infrastructure.Publishing;

public sealed record ApplicationPublishLeakScanRequest(
    string RepositoryRoot,
    string ReleaseRoot,
    ApplicationPublishModuleFileMap ModuleFileMap,
    ApplicationPublishDependencySnapshot Snapshot);

public sealed class ApplicationPublishLeakScanner
{
    private const int MaxFindings = 200;

    private static readonly HashSet<string> ScannedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css",
        ".dll",
        ".exe",
        ".html",
        ".js",
        ".mjs"
    };

    private static readonly Regex ApiRouteRegex = new(
        "/api/[a-zA-Z0-9_./{}:-]+",
        RegexOptions.Compiled);

    private static readonly Regex DotNetTypeRegex = new(
        "\\b(?:class|interface|record)\\s+([A-Z][A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex FrontendSymbolRegex = new(
        "\\b(?:class|const|function)\\s+([A-Z][A-Za-z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly HashSet<string> GenericMarkerNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Field",
        "Index",
        "PermissionCodes",
        "Program"
    };

    public async Task<ApplicationPublishLeakScanReport> ScanAsync(
        ApplicationPublishLeakScanRequest request,
        CancellationToken cancellationToken)
    {
        var forbiddenMarkers = (await BuildForbiddenMarkersAsync(request, cancellationToken))
            .OrderBy(item => item.ModuleKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var findings = new List<ApplicationPublishLeakScanFinding>();
        var scannedFileCount = 0;
        var releaseRoot = Path.GetFullPath(request.ReleaseRoot);
        if (Directory.Exists(releaseRoot))
        {
            foreach (var file in Directory.EnumerateFiles(releaseRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ShouldScan(file))
                {
                    continue;
                }

                scannedFileCount++;
                var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                ScanFile(releaseRoot, file, bytes, forbiddenMarkers, findings);
                if (findings.Count >= MaxFindings)
                {
                    break;
                }
            }
        }

        return new ApplicationPublishLeakScanReport(
            DateTime.UtcNow,
            scannedFileCount,
            forbiddenMarkers.Count,
            findings);
    }

    private static async Task<IReadOnlyList<ForbiddenMarker>> BuildForbiddenMarkersAsync(
        ApplicationPublishLeakScanRequest request,
        CancellationToken cancellationToken)
    {
        if (string.Equals(request.Snapshot.PublishMode, "Full", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var resolved = request.Snapshot.ResolvedModules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var includedSourceFiles = BuildIncludedSourceFileSet(request.RepositoryRoot, request.ModuleFileMap, resolved);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var markers = new List<ForbiddenMarker>();
        foreach (var module in request.ModuleFileMap.Modules.Where(module => !resolved.Contains(module.ModuleKey)))
        {
            foreach (var marker in await BuildModuleMarkersAsync(request.RepositoryRoot, module, includedSourceFiles, cancellationToken))
            {
                if (IsSafeMarker(marker.Kind, marker.Value) && seen.Add($"{marker.ModuleKey}|{marker.Kind}|{marker.Value}"))
                {
                    markers.Add(marker);
                }
            }
        }

        return markers;
    }

    private static async Task<IReadOnlyList<ForbiddenMarker>> BuildModuleMarkersAsync(
        string repositoryRoot,
        ApplicationPublishModuleFileMapEntry module,
        ISet<string> includedSourceFiles,
        CancellationToken cancellationToken)
    {
        var markers = new List<ForbiddenMarker>();
        foreach (var prefix in module.PermissionPrefixes)
        {
            markers.Add(new ForbiddenMarker(module.ModuleKey, "permission", prefix));
        }

        foreach (var providerKey in module.ProviderKeys)
        {
            markers.Add(new ForbiddenMarker(module.ModuleKey, "provider", providerKey));
        }

        foreach (var sourceFile in EnumerateModuleSourceFiles(repositoryRoot, module))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullSourceFile = Path.GetFullPath(sourceFile);
            if (includedSourceFiles.Contains(fullSourceFile))
            {
                continue;
            }

            var fileName = Path.GetFileNameWithoutExtension(sourceFile);
            if (IsSafeMarker("fileName", fileName))
            {
                markers.Add(new ForbiddenMarker(module.ModuleKey, "fileName", fileName));
            }

            var extension = Path.GetExtension(sourceFile);
            if (!IsTextSourceExtension(extension))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(sourceFile, cancellationToken);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Match match in ApiRouteRegex.Matches(content))
                {
                    markers.Add(new ForbiddenMarker(module.ModuleKey, "apiRoute", match.Value));
                }

                foreach (Match match in DotNetTypeRegex.Matches(content))
                {
                    markers.Add(new ForbiddenMarker(module.ModuleKey, "typeName", match.Groups[1].Value));
                }
            }

            if (IsFrontendSourceExtension(extension))
            {
                foreach (Match match in ApiRouteRegex.Matches(content))
                {
                    markers.Add(new ForbiddenMarker(module.ModuleKey, "apiRoute", match.Value));
                }

                foreach (Match match in FrontendSymbolRegex.Matches(content))
                {
                    markers.Add(new ForbiddenMarker(module.ModuleKey, "frontendSymbol", match.Groups[1].Value));
                }
            }
        }

        return markers;
    }

    private static ISet<string> BuildIncludedSourceFileSet(
        string repositoryRoot,
        ApplicationPublishModuleFileMap moduleFileMap,
        ISet<string> resolvedModules)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in moduleFileMap.Skeleton)
        {
            foreach (var file in EnumeratePatternFiles(repositoryRoot, pattern))
            {
                files.Add(Path.GetFullPath(file));
            }
        }

        foreach (var module in moduleFileMap.Modules.Where(module => resolvedModules.Contains(module.ModuleKey)))
        {
            foreach (var file in EnumerateModuleSourceFiles(repositoryRoot, module))
            {
                files.Add(Path.GetFullPath(file));
            }
        }

        return files;
    }

    private static IEnumerable<string> EnumerateModuleSourceFiles(
        string repositoryRoot,
        ApplicationPublishModuleFileMapEntry module)
    {
        foreach (var pattern in module.FileGlobs)
        {
            foreach (var file in EnumeratePatternFiles(repositoryRoot, pattern))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumeratePatternFiles(
        string repositoryRoot,
        string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            yield break;
        }

        var normalized = pattern.Replace('\\', '/').Trim('/');
        if (normalized.EndsWith("/**", StringComparison.Ordinal))
        {
            var directory = Path.Combine(repositoryRoot, normalized[..^3].Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory))
            {
                yield break;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                yield return file;
            }

            yield break;
        }

        if (normalized.Contains('*', StringComparison.Ordinal))
        {
            foreach (var file in ExpandWildcardFiles(repositoryRoot, normalized))
            {
                yield return file;
            }

            yield break;
        }

        var exactPath = Path.Combine(repositoryRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(exactPath))
        {
            yield return exactPath;
        }
    }

    private static IEnumerable<string> ExpandWildcardFiles(string repositoryRoot, string normalizedPattern)
    {
        var slashIndex = normalizedPattern.LastIndexOf('/');
        var directory = slashIndex >= 0 ? normalizedPattern[..slashIndex] : string.Empty;
        var searchPattern = slashIndex >= 0 ? normalizedPattern[(slashIndex + 1)..] : normalizedPattern;
        var absoluteDirectory = Path.Combine(repositoryRoot, directory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absoluteDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(absoluteDirectory, searchPattern, SearchOption.TopDirectoryOnly);
    }

    private static void ScanFile(
        string releaseRoot,
        string file,
        byte[] bytes,
        IReadOnlyList<ForbiddenMarker> forbiddenMarkers,
        List<ApplicationPublishLeakScanFinding> findings)
    {
        foreach (var marker in forbiddenMarkers)
        {
            if (findings.Count >= MaxFindings)
            {
                return;
            }

            var utf8Index = bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(marker.Value));
            if (utf8Index >= 0)
            {
                findings.Add(CreateFinding(releaseRoot, file, marker, "utf8", utf8Index));
                continue;
            }

            var unicodeIndex = bytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(marker.Value));
            if (unicodeIndex >= 0)
            {
                findings.Add(CreateFinding(releaseRoot, file, marker, "utf16le", unicodeIndex));
            }
        }
    }

    private static ApplicationPublishLeakScanFinding CreateFinding(
        string releaseRoot,
        string file,
        ForbiddenMarker marker,
        string encoding,
        long offset)
    {
        return new ApplicationPublishLeakScanFinding(
            Path.GetRelativePath(releaseRoot, file).Replace('\\', '/'),
            marker.ModuleKey,
            marker.Kind,
            marker.Value,
            encoding,
            offset);
    }

    private static bool ShouldScan(string file)
    {
        var extension = Path.GetExtension(file);
        if (!ScannedExtensions.Contains(extension))
        {
            return false;
        }

        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(file).StartsWith("AsterERP.", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool IsTextSourceExtension(string extension)
    {
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFrontendSourceExtension(string extension)
    {
        return extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jsx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeMarker(string kind, string value)
    {
        if (value.Length < 5 || GenericMarkerNames.Contains(value))
        {
            return false;
        }

        if (kind.Equals("apiRoute", StringComparison.OrdinalIgnoreCase))
        {
            return value.Split('/', StringSplitOptions.RemoveEmptyEntries).Length >= 3;
        }

        return true;
    }

    private sealed record ForbiddenMarker(string ModuleKey, string Kind, string Value);
}
