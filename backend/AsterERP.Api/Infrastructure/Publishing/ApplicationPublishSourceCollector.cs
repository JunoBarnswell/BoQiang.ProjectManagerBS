using System.Security.Cryptography;

namespace AsterERP.Api.Infrastructure.Publishing;

public sealed record ApplicationPublishSourceRequest(
    string RepositoryRoot,
    string SourceRoot,
    string ManifestRoot,
    string AppCode,
    string PublishMode,
    IReadOnlyList<string> ResolvedModules,
    ApplicationPublishModuleFileMap ModuleFileMap);

public sealed class ApplicationPublishSourceCollector
{
    private static readonly string LegacyRuntimeRegistryPrefix = string.Concat("frontend/AsterERP.Web/src/apps/runtime", "Registry.");
    private static readonly string[] ExcludedDirectoryNames =
    [
        ".git",
        ".codex",
        ".claude",
        ".codegraph",
        ".npm-cache",
        ".sandbox-appdata",
        "artifacts",
        "bin",
        "data",
        "dist",
        "node_modules",
        "obj",
        "output"
    ];

    private static readonly string[] ExcludedFileSuffixes =
    [
        ".db",
        ".db-shm",
        ".db-wal",
        ".log",
        ".suo",
        ".user"
    ];

    public async Task<ApplicationPublishSourceResult> CreateAsync(
        ApplicationPublishSourceRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        Directory.CreateDirectory(request.SourceRoot);
        Directory.CreateDirectory(request.ManifestRoot);

        var included = new List<ApplicationPublishDependencyFile>();
        var copied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(request.PublishMode, "Full", StringComparison.OrdinalIgnoreCase))
        {
            await CopyOptionalPathAsync(request, "backend/module-file-map.json", "skeleton", "module-file-map", included, copied, cancellationToken);
            await CopyFullProjectAsync(request, included, copied, cancellationToken);
        }
        else
        {
            await CopyTrimmedProjectAsync(request, included, copied, cancellationToken);
        }

        return new ApplicationPublishSourceResult(request.SourceRoot, request.ManifestRoot, included);
    }

    public static IReadOnlyList<string> GetExcludedPatterns() =>
    [
        ".git",
        ".codex",
        ".claude",
        ".codegraph",
        "node_modules",
        "bin",
        "obj",
        "dist",
        "logs",
        "artifacts",
        "output",
        "backend/module-file-map.json (trimmed)",
        "frontend/AsterERP.Web/src/pages/dashboard/DashboardPage.tsx (trimmed)",
        "*.db",
        "*.db-shm",
        "*.db-wal",
        "*.log",
        "*.user",
        "*.suo"
    ];

    private static async Task CopyFullProjectAsync(
        ApplicationPublishSourceRequest request,
        List<ApplicationPublishDependencyFile> included,
        HashSet<string> copied,
        CancellationToken cancellationToken)
    {
        await CopyRequiredPathAsync(request, "AsterERP.sln", "*", "solution", included, copied, cancellationToken);
        await CopyRequiredPathAsync(request, "global.json", "*", "dotnet-sdk", included, copied, cancellationToken);
        await CopyOptionalPathAsync(request, "NuGet.Config", "*", "nuget-config", included, copied, cancellationToken);
        await CopyDirectoryAsync(request, "backend/AsterERP.Api", "*", "full-backend-web-host", included, copied, cancellationToken);
        await CopyDirectoryAsync(request, "backend/AsterERP.Shared", "*", "full-shared-contract-runtime", included, copied, cancellationToken);
        await CopyDirectoryAsync(request, "backend/AsterERP.Contracts", "*", "full-api-contracts", included, copied, cancellationToken);
        await CopyDirectoryAsync(request, "backend/AsterERP.Domain", "*", "full-domain-entity-base", included, copied, cancellationToken);
        await CopyDirectoryAsync(request, "frontend/AsterERP.Web", "*", "full-frontend-app", included, copied, cancellationToken);
    }

    private static async Task CopyTrimmedProjectAsync(
        ApplicationPublishSourceRequest request,
        List<ApplicationPublishDependencyFile> included,
        HashSet<string> copied,
        CancellationToken cancellationToken)
    {
        foreach (var pattern in request.ModuleFileMap.Skeleton)
        {
            await CopyPatternAsync(request, pattern, "skeleton", "skeleton", included, copied, cancellationToken);
        }

        var selectedModules = request.ModuleFileMap.Modules
            .Where(module => request.ResolvedModules.Contains(module.ModuleKey, StringComparer.OrdinalIgnoreCase))
            .OrderBy(module => module.ModuleKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var module in selectedModules)
        {
            foreach (var pattern in module.FileGlobs)
            {
                await CopyPatternAsync(request, pattern, module.ModuleKey, $"module:{module.ModuleKey}", included, copied, cancellationToken);
            }
        }
    }

    private static Task CopyRequiredPathAsync(
        ApplicationPublishSourceRequest request,
        string relativePath,
        string moduleKey,
        string reason,
        List<ApplicationPublishDependencyFile> included,
        HashSet<string> copied,
        CancellationToken cancellationToken)
    {
        EnsureSafeRelativeSourcePath(relativePath);
        var source = Path.Combine(request.RepositoryRoot, relativePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Required publish source file '{relativePath}' was not found.", source);
        }

        return CopyFileAsync(request, source, relativePath, moduleKey, reason, included, copied, cancellationToken);
    }

    private static async Task CopyOptionalPathAsync(
        ApplicationPublishSourceRequest request,
        string relativePath,
        string moduleKey,
        string reason,
        List<ApplicationPublishDependencyFile> included,
        HashSet<string> copied,
        CancellationToken cancellationToken)
    {
        EnsureSafeRelativeSourcePath(relativePath);
        var source = Path.Combine(request.RepositoryRoot, relativePath);
        if (File.Exists(source))
        {
            await CopyFileAsync(request, source, relativePath, moduleKey, reason, included, copied, cancellationToken);
        }
    }

    private static async Task CopyDirectoryAsync(
        ApplicationPublishSourceRequest request,
        string relativePath,
        string moduleKey,
        string reason,
        List<ApplicationPublishDependencyFile> included,
        HashSet<string> copied,
        CancellationToken cancellationToken)
    {
        EnsureSafeRelativeSourcePath(relativePath);
        var sourceRoot = Path.Combine(request.RepositoryRoot, relativePath);
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Required publish source directory '{relativePath}' was not found.");
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeFile = Path.GetRelativePath(request.RepositoryRoot, file).Replace('\\', '/');
            if (ShouldExclude(relativeFile, request))
            {
                continue;
            }

            await CopyFileAsync(request, file, relativeFile, moduleKey, reason, included, copied, cancellationToken);
        }
    }

    private static async Task CopyPatternAsync(
        ApplicationPublishSourceRequest request,
        string pattern,
        string moduleKey,
        string reason,
        List<ApplicationPublishDependencyFile> included,
        HashSet<string> copied,
        CancellationToken cancellationToken)
    {
        var normalizedPattern = pattern.Replace('\\', '/').Trim('/');
        EnsureSafeSourcePattern(normalizedPattern);
        if (normalizedPattern.EndsWith("/**", StringComparison.Ordinal))
        {
            var directory = normalizedPattern[..^3];
            await CopyDirectoryAsync(request, directory, moduleKey, reason, included, copied, cancellationToken);
            return;
        }

        if (normalizedPattern.Contains('*', StringComparison.Ordinal))
        {
            foreach (var file in ExpandWildcardFiles(request.RepositoryRoot, normalizedPattern))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativeFile = Path.GetRelativePath(request.RepositoryRoot, file).Replace('\\', '/');
                if (!ShouldExclude(relativeFile, request))
                {
                    await CopyFileAsync(request, file, relativeFile, moduleKey, reason, included, copied, cancellationToken);
                }
            }

            return;
        }

        var requiredPattern = ResolveRequiredPattern(normalizedPattern, out var isOptional);
        if (isOptional)
        {
            await CopyOptionalPathAsync(request, requiredPattern, moduleKey, reason, included, copied, cancellationToken);
            return;
        }

        await CopyRequiredPathAsync(request, requiredPattern, moduleKey, reason, included, copied, cancellationToken);
    }

    private static string ResolveRequiredPattern(string normalizedPattern, out bool isOptional)
    {
        isOptional = normalizedPattern.StartsWith("optional:", StringComparison.OrdinalIgnoreCase);
        return isOptional ? normalizedPattern["optional:".Length..].Trim('/') : normalizedPattern;
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

    private static bool ShouldExclude(string relativePath, ApplicationPublishSourceRequest request)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => ExcludedDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (ExcludedFileSuffixes.Any(suffix => relativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return string.Equals(request.PublishMode, "Trimmed", StringComparison.OrdinalIgnoreCase) &&
               relativePath.Replace('\\', '/') is
               "frontend/AsterERP.Web/src/app/router/workspaceRoutes.full.tsx" or
               "frontend/AsterERP.Web/src/app/router/workspaceRoutes.tsx" or
               "frontend/AsterERP.Web/src/app/navigation/routes.ts" or
               "frontend/AsterERP.Web/src/pages/dashboard/DashboardPage.tsx" or
               "frontend/AsterERP.Web/src/shared/i18n/messages.ts";
    }

    private static async Task CopyFileAsync(
        ApplicationPublishSourceRequest request,
        string source,
        string relativePath,
        string moduleKey,
        string reason,
        List<ApplicationPublishDependencyFile> included,
        HashSet<string> copied,
        CancellationToken cancellationToken)
    {
        relativePath = relativePath.Replace('\\', '/');
        EnsureSafeRelativeSourcePath(relativePath);
        var repositoryRoot = Path.GetFullPath(request.RepositoryRoot);
        var sourceRoot = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullSource = Path.GetFullPath(source);
        if (!IsInsideRoot(fullSource, sourceRoot))
        {
            throw new InvalidOperationException($"Publish source '{relativePath}' resolves outside the repository root.");
        }

        var fullTargetRoot = Path.GetFullPath(request.SourceRoot);
        var fullTarget = Path.GetFullPath(Path.Combine(fullTargetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsInsideRoot(fullTarget, fullTargetRoot))
        {
            throw new InvalidOperationException($"Publish target '{relativePath}' resolves outside the source root.");
        }

        if (!copied.Add(relativePath))
        {
            return;
        }

        var target = fullTarget;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true))
        await using (var targetStream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
        }

        await using var hashStream = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(hashStream, cancellationToken);
        var info = new FileInfo(target);
        included.Add(new ApplicationPublishDependencyFile(
            relativePath,
            moduleKey,
            reason,
            Convert.ToHexString(hash),
            info.Length));
    }

    private static void ValidateRequest(ApplicationPublishSourceRequest request)
    {
        if (!Directory.Exists(request.RepositoryRoot))
        {
            throw new InvalidOperationException("Publish repository root does not exist.");
        }

        if (string.IsNullOrWhiteSpace(request.AppCode))
        {
            throw new InvalidOperationException("Publish app code is required.");
        }

        if (!string.Equals(request.PublishMode, "Full", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.PublishMode, "Trimmed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported publish mode '{request.PublishMode}'.");
        }

        var patterns = request.ModuleFileMap.Skeleton
            .Concat(request.ModuleFileMap.Modules
                .Where(module => request.ResolvedModules.Contains(module.ModuleKey, StringComparer.OrdinalIgnoreCase))
                .SelectMany(module => module.FileGlobs));
        foreach (var pattern in patterns)
        {
            var normalized = pattern.Replace('\\', '/').Trim('/');
            EnsureSafeSourcePattern(normalized);
            if (normalized.StartsWith(LegacyRuntimeRegistryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Legacy runtime registry source '{pattern}' is not a valid publish input; publish RuntimeArtifact and manifest metadata instead.");
            }
        }
    }

    private static void EnsureSafeSourcePattern(string pattern)
    {
        var normalized = ResolveRequiredPattern(pattern, out _);
        if (string.IsNullOrWhiteSpace(normalized) ||
            Path.IsPathRooted(normalized) ||
            normalized.Contains(':', StringComparison.Ordinal) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException($"Invalid publish source path '{pattern}'.");
        }
    }

    private static void EnsureSafeRelativeSourcePath(string path)
    {
        EnsureSafeSourcePattern(path.Replace('\\', '/').Trim('/'));
    }

    private static bool IsInsideRoot(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
