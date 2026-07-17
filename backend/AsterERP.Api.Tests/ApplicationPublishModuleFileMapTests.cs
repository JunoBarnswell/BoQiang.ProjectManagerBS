using System.Text.Json;
using AsterERP.Api.Infrastructure.Publishing;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationPublishModuleFileMapTests
{
    [Fact]
    public async Task Module_file_map_loads_and_validates_dependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new ApplicationPublishModuleFileMapLoader();

        var result = await loader.LoadAsync(repositoryRoot, CancellationToken.None);

        Assert.NotEmpty(result.Sha256);
        Assert.NotEmpty(result.Map.Skeleton);
        Assert.NotEmpty(result.Map.Modules);
    }

    [Fact]
    public void Latest_frontend_module_map_does_not_reference_deleted_runtime_registries()
    {
        var repositoryRoot = FindRepositoryRoot();
        var map = LoadMap(repositoryRoot);
        var frontendPatterns = map.Skeleton
            .Concat(map.Modules.SelectMany(module => module.FileGlobs))
            .Where(pattern => pattern.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.DoesNotContain(frontendPatterns, pattern =>
            pattern.Contains("runtimeRegistry", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(frontendPatterns, pattern =>
            string.Equals(pattern, "frontend/AsterERP.Web/src/pages/runtime/**", StringComparison.Ordinal));
        Assert.Contains(frontendPatterns, pattern =>
            string.Equals(pattern, "frontend/AsterERP.Web/src/app/**", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_api_source_file_is_owned_by_exactly_one_publish_boundary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var map = LoadMap(repositoryRoot);
        var ownership = BuildOwnership(repositoryRoot, map, "backend/AsterERP.Api/");
        var apiFiles = GetProjectSourceFiles(repositoryRoot, "backend/AsterERP.Api");

        var missing = apiFiles
            .Where(path => !ownership.ContainsKey(path))
            .ToList();
        var duplicate = ownership
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key} => {string.Join(", ", pair.Value)}")
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(missing.Count == 0, $"未被 module-file-map 认领的 Api 源文件：{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
        Assert.True(duplicate.Count == 0, $"被多个发布边界重复认领的 Api 源文件：{Environment.NewLine}{string.Join(Environment.NewLine, duplicate)}");
    }

    [Fact]
    public void Every_shared_source_file_is_owned_by_exactly_one_publish_boundary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var map = LoadMap(repositoryRoot);
        var ownership = BuildOwnership(repositoryRoot, map, "backend/AsterERP.Shared/");
        var sharedFiles = GetProjectSourceFiles(repositoryRoot, "backend/AsterERP.Shared");

        var missing = sharedFiles
            .Where(path => !ownership.ContainsKey(path))
            .ToList();
        var duplicate = ownership
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key} => {string.Join(", ", pair.Value)}")
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(missing.Count == 0, $"未被 module-file-map 认领的 Shared 源文件：{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
        Assert.True(duplicate.Count == 0, $"被多个发布边界重复认领的 Shared 源文件：{Environment.NewLine}{string.Join(Environment.NewLine, duplicate)}");
    }

    [Fact]
    public void Production_security_configuration_validator_is_owned_by_skeleton_only()
    {
        var repositoryRoot = FindRepositoryRoot();
        var map = LoadMap(repositoryRoot);
        var validatorPath = "backend/AsterERP.Api/Infrastructure/Security/ProductionSecurityConfigurationValidator.cs";
        var ownership = BuildOwnership(repositoryRoot, map, "backend/AsterERP.Api/");

        Assert.True(ownership.TryGetValue(validatorPath, out var owners));
        Assert.Equal(["skeleton"], owners);
        Assert.DoesNotContain(
            ApplicationPublishSourceCollector.GetExcludedPatterns(),
            pattern => string.Equals(pattern, validatorPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Wms_runtime_closure_keeps_system_administration_out()
    {
        var repositoryRoot = FindRepositoryRoot();
        var map = LoadMap(repositoryRoot);
        var resolver = new ApplicationPublishModuleClosureResolver();

        var closure = resolver.Resolve(
            map,
            "WMS",
            ["runtime:page:query"],
            ["system.menus"]);

        Assert.Equal("Trimmed", closure.PublishMode);
        Assert.Contains("core.shell", closure.ModuleKeys);
        Assert.Contains("runtime.core", closure.ModuleKeys);
        Assert.DoesNotContain("system.administration", closure.ModuleKeys);
        Assert.Empty(closure.UnresolvedDependencies);
    }

    private static Dictionary<string, List<string>> BuildOwnership(
        string repositoryRoot,
        ApplicationPublishModuleFileMap map,
        string projectPrefix)
    {
        var ownership = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in map.Skeleton)
        {
            AddOwnedFiles(repositoryRoot, ownership, "skeleton", pattern, projectPrefix);
        }

        foreach (var module in map.Modules)
        {
            foreach (var pattern in module.FileGlobs)
            {
                AddOwnedFiles(repositoryRoot, ownership, module.ModuleKey, pattern, projectPrefix);
            }
        }

        return ownership;
    }

    private static void AddOwnedFiles(
        string repositoryRoot,
        Dictionary<string, List<string>> ownership,
        string owner,
        string pattern,
        string projectPrefix)
    {
        foreach (var file in ExpandPattern(repositoryRoot, pattern))
        {
            var relativePath = ToRelativePath(repositoryRoot, file);
            if (!relativePath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase) ||
                IsGeneratedBuildPath(relativePath) ||
                !relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!ownership.TryGetValue(relativePath, out var owners))
            {
                owners = [];
                ownership[relativePath] = owners;
            }

            owners.Add(owner);
        }
    }

    private static IEnumerable<string> ExpandPattern(string repositoryRoot, string pattern)
    {
        var normalized = pattern.Replace('\\', '/').Trim('/');
        if (normalized.EndsWith("/**", StringComparison.Ordinal))
        {
            var directory = Path.Combine(repositoryRoot, normalized[..^3].Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                : [];
        }

        if (normalized.Contains('*', StringComparison.Ordinal))
        {
            var slashIndex = normalized.LastIndexOf('/');
            var directory = slashIndex >= 0 ? normalized[..slashIndex] : string.Empty;
            var searchPattern = slashIndex >= 0 ? normalized[(slashIndex + 1)..] : normalized;
            var absoluteDirectory = Path.Combine(repositoryRoot, directory.Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(absoluteDirectory)
                ? Directory.EnumerateFiles(absoluteDirectory, searchPattern, SearchOption.TopDirectoryOnly)
                : [];
        }

        var path = Path.Combine(repositoryRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? [path] : [];
    }

    private static List<string> GetProjectSourceFiles(string repositoryRoot, string projectPath)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, projectPath.Replace('/', Path.DirectorySeparatorChar)), "*.cs", SearchOption.AllDirectories)
            .Select(path => ToRelativePath(repositoryRoot, path))
            .Where(path => !IsGeneratedBuildPath(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ApplicationPublishModuleFileMap LoadMap(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "backend", "module-file-map.json");
        var map = JsonSerializer.Deserialize<ApplicationPublishModuleFileMap>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
        return map ?? throw new InvalidOperationException("module-file-map.json 为空。");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("无法定位仓库根目录。");
    }

    private static string ToRelativePath(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');

    private static bool IsGeneratedBuildPath(string relativePath) =>
        relativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
}
