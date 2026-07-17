using System.Text.RegularExpressions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class LowCodeSecurityGuardTests
{
    [Fact]
    public void Authoring_controller_actions_are_permission_bound()
    {
        var source = ReadProductionSource("backend/AsterERP.Api/Controllers/ApplicationDevelopmentCenterController.cs");
        var lines = source.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].Contains("[Http", StringComparison.Ordinal)) continue;
            var window = string.Join('\n', lines.Skip(index).Take(4));
            Assert.Matches(@"\[Permission\(PermissionCodes\.AppDevelopmentCenter(?:Designer(?:View|Edit|Delete|Preview|Publish|PermissionEdit)|MonitoringWrite)\)\]", window);
        }
    }

    [Fact]
    public void Runtime_and_authoring_sources_do_not_reintroduce_dynamic_code_execution()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "frontend", "AsterERP.Web", "src", "runtime-kernel", "ComponentRuntimeHost.tsx"),
            Path.Combine(root, "frontend", "AsterERP.Web", "src", "pages", "runtime", "RuntimePage.tsx"),
            Path.Combine(root, "backend", "AsterERP.Api", "Controllers", "ApplicationDevelopmentCenterController.cs")
        };
        var forbidden = new Regex(@"\beval\s*\(|new\s+Function\s*\(|innerHTML\s*=|document\.write\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (var file in files)
        {
            Assert.DoesNotMatch(forbidden, File.ReadAllText(file));
        }
    }

    private static string ReadProductionSource(string relativePath) => File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsterERP.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("BLOCKED quality guard: AsterERP.sln was not found.");
    }
}
