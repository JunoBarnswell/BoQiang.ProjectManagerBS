using System.Security.Cryptography;
using System.Text.Json;

namespace AsterERP.Api.Infrastructure.Publishing;

public sealed record ApplicationPublishModuleFileMapLoadResult(
    ApplicationPublishModuleFileMap Map,
    string Path,
    string Sha256);

public sealed class ApplicationPublishModuleFileMapLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<ApplicationPublishModuleFileMapLoadResult> LoadAsync(
        string repositoryRoot,
        CancellationToken cancellationToken)
    {
        var mapPath = Path.Combine(repositoryRoot, "backend", "module-file-map.json");
        if (!File.Exists(mapPath))
        {
            throw new FileNotFoundException("应用发布模块文件映射不存在，无法执行精确裁剪。", mapPath);
        }

        await using var stream = new FileStream(mapPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        stream.Position = 0;
        var map = await JsonSerializer.DeserializeAsync<ApplicationPublishModuleFileMap>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("应用发布模块文件映射为空。");

        Validate(map, repositoryRoot);
        return new ApplicationPublishModuleFileMapLoadResult(map, mapPath, Convert.ToHexString(hash));
    }

    private static void Validate(ApplicationPublishModuleFileMap map, string repositoryRoot)
    {
        if (map.Skeleton.Count == 0)
        {
            throw new InvalidOperationException("应用发布模块文件映射缺少 skeleton。");
        }

        var duplicateModule = map.Modules
            .Where(module => !module.IsEmpty)
            .GroupBy(module => module.ModuleKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateModule is not null)
        {
            throw new InvalidOperationException($"应用发布模块文件映射存在重复模块：{duplicateModule.Key}");
        }

        var moduleKeys = map.Modules.Select(module => module.ModuleKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var module in map.Modules)
        {
            if (module.IsEmpty)
            {
                throw new InvalidOperationException("应用发布模块文件映射包含空模块 key。");
            }

            foreach (var dependency in module.DependsOn)
            {
                if (!moduleKeys.Contains(dependency))
                {
                    throw new InvalidOperationException($"模块 '{module.ModuleKey}' 依赖未知模块 '{dependency}'。");
                }
            }
        }

        ValidatePatternsExist(repositoryRoot, "skeleton", map.Skeleton);
        foreach (var module in map.Modules)
        {
            ValidatePatternsExist(repositoryRoot, module.ModuleKey, module.FileGlobs);
        }
    }

    private static void ValidatePatternsExist(
        string repositoryRoot,
        string owner,
        IReadOnlyList<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            if (IsOptionalPattern(pattern))
            {
                continue;
            }

            var normalized = NormalizePattern(pattern);
            if (!PatternExists(repositoryRoot, normalized))
            {
                throw new FileNotFoundException($"应用发布模块文件映射 '{owner}' 声明的路径不存在：{pattern}", pattern);
            }
        }
    }

    private static bool PatternExists(string repositoryRoot, string normalizedPattern)
    {
        if (normalizedPattern.EndsWith("/**", StringComparison.Ordinal))
        {
            var directory = Path.Combine(repositoryRoot, normalizedPattern[..^3].Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(directory);
        }

        if (normalizedPattern.Contains('*', StringComparison.Ordinal))
        {
            var slashIndex = normalizedPattern.LastIndexOf('/');
            var directory = slashIndex >= 0 ? normalizedPattern[..slashIndex] : string.Empty;
            var searchPattern = slashIndex >= 0 ? normalizedPattern[(slashIndex + 1)..] : normalizedPattern;
            var absoluteDirectory = Path.Combine(repositoryRoot, directory.Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(absoluteDirectory) &&
                   Directory.EnumerateFiles(absoluteDirectory, searchPattern, SearchOption.TopDirectoryOnly).Any();
        }

        var path = Path.Combine(repositoryRoot, normalizedPattern.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path);
    }

    private static string NormalizePattern(string pattern) =>
        pattern.Replace('\\', '/').Trim('/');

    private static bool IsOptionalPattern(string pattern) =>
        NormalizePattern(pattern).StartsWith("optional:", StringComparison.OrdinalIgnoreCase);
}
