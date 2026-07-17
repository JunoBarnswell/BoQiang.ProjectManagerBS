using System.Text.RegularExpressions;
using Xunit;

namespace AsterERP.Api.Tests;

/// <summary>
/// Repository-level deletion guard for the single latest low-code semantic.
/// This test intentionally scans source text instead of compiled output so a
/// deleted entry point cannot be reintroduced by a stale generated artifact.
/// </summary>
public sealed class LatestOnlySourceScanGuardTests
{
    private static readonly Regex ForbiddenPattern = new(
        "DesignerRuntimeRenderer|runtimeDocumentCodec|simulatedWidth|(?<![A-Za-z0-9_])ConfigJson\\.Contains|(?<![A-Za-z0-9_])PublicConfigJson\\.Contains|allowLegacyTestPath\\s*=\\s*true|: this\\([^\\r\\n]*,\\s*true\\s*\\)|(?i:\\b(v3|v4)\\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LegacySemanticPattern = new(
        "(?i:\\blegacy\\b)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NumericDocumentVersionPattern = new(
        "(?i)(schemaVersion|publishedSchemaVersionNo|targetSchemaVersionNo)\\s*(===|!==|==|!=|>=|<=|>|<|\\?|:)\\s*[0-9]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void LatestOnlyProductionSources_have_no_forbidden_semantic_references()
    {
        var repositoryRoot = FindRepositoryRoot();
        var findings = Scan(repositoryRoot);

        Assert.True(
            findings.Count == 0,
            "FAIL latest-only deletion guard. Remove the reported production references before closing the quality gate:\n" +
            string.Join(Environment.NewLine, findings));
    }

    [Fact]
    public void ScanPolicy_excludes_migration_rejection_fields_and_test_fixtures()
    {
        var repositoryRoot = CreateFixtureRepository();
        try
        {
            var findings = Scan(repositoryRoot);

            Assert.DoesNotContain(findings, item => item.Contains("migration/ApplicationDesignerDocumentMigrationService.cs", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(findings, item => item.Contains("ApplicationDevelopmentSchemaValidator.cs", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(findings, item => item.Contains("ApplicationDevelopmentSchemaValidatorTests.cs", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(findings, item => item.Contains("LatestRuntimeEntry.ts", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(repositoryRoot);
        }
    }

    [Fact]
    public void ScanPolicy_rejects_numeric_document_version_routing_even_without_v3_or_v4_text()
    {
        var repositoryRoot = CreateFixtureRepository();
        try
        {
            WriteFile(
                repositoryRoot,
                "frontend/AsterERP.Web/src/runtime-kernel/LatestRuntimeEntry.ts",
                "const route = document.schemaVersion === 2 ? '/latest' : '/latest';\nconst legacyParser = 'legacy';\nconst configMatch = ConfigJson.Contains('value');\nconst unsafePath = 'allowLegacyTestPath = true';");

            var findings = Scan(repositoryRoot);

            Assert.Contains(findings, item =>
                item.Contains("LatestRuntimeEntry.ts", StringComparison.OrdinalIgnoreCase) &&
                item.Contains("numeric document version", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(findings, item =>
                item.Contains("LatestRuntimeEntry.ts", StringComparison.OrdinalIgnoreCase) &&
                item.Contains("legacy", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(findings, item =>
                item.Contains("LatestRuntimeEntry.ts", StringComparison.OrdinalIgnoreCase) &&
                item.Contains("ConfigJson.Contains", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(findings, item =>
                item.Contains("LatestRuntimeEntry.ts", StringComparison.OrdinalIgnoreCase) &&
                item.Contains("allowLegacyTestPath", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(repositoryRoot);
        }
    }

    [Fact]
    public void ScanPolicy_rejects_legacy_designer_runtime_renderer_import_from_runtime_page()
    {
        var repositoryRoot = CreateFixtureRepository();
        try
        {
            WriteFile(
                repositoryRoot,
                "frontend/AsterERP.Web/src/pages/runtime/RuntimePage.tsx",
                "import { DesignerRuntimeRenderer } from '../../shared/runtime/designer-document/DesignerRuntimeRenderer';\n" +
                "export function RuntimePage() { return <DesignerRuntimeRenderer />; }");

            var findings = Scan(repositoryRoot);

            Assert.Contains(findings, item =>
                item.Contains("pages/runtime/RuntimePage.tsx", StringComparison.OrdinalIgnoreCase) &&
                item.Contains("DesignerRuntimeRenderer", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(repositoryRoot);
        }
    }

    [Fact]
    public void ScanPolicy_does_not_scan_documented_migration_fixture_outside_protected_source_roots()
    {
        var repositoryRoot = CreateFixtureRepository();
        try
        {
            WriteFile(
                repositoryRoot,
                "docs/low-code-refactor/fixtures/legacy-designer-runtime-input.tsx",
                "export const migrationInput = 'DesignerRuntimeRenderer';");

            var findings = Scan(repositoryRoot);

            Assert.DoesNotContain(findings, item =>
                item.Contains("legacy-designer-runtime-input.tsx", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(repositoryRoot);
        }
    }

    [Fact]
    public void Formal_contract_is_latest_only_and_old_contract_filename_is_absent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var oldContract = Path.Combine(repositoryRoot, "docs", "contracts", "designer-document-v3.schema.json");
        var latestContract = Path.Combine(repositoryRoot, "docs", "contracts", "designer-document.latest.schema.json");

        Assert.False(File.Exists(oldContract), "The old versioned schema must not remain a formal contract.");
        Assert.True(File.Exists(latestContract), "The latest-only formal schema is missing.");

        var content = File.ReadAllText(latestContract);
        Assert.DoesNotContain("designer-document-v3", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("schemaVersion", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"const\": 3", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScanPolicy_rejects_versioned_formal_contract_in_contract_directory()
    {
        var repositoryRoot = CreateFixtureRepository();
        try
        {
            WriteFile(
                repositoryRoot,
                "docs/contracts/designer-document-v3.schema.json",
                "{\"title\":\"Designer Document V3\",\"schemaVersion\":3}");

            var findings = Scan(repositoryRoot);

            Assert.Contains(findings, item =>
                item.Contains("docs/contracts/designer-document-v3.schema.json", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(repositoryRoot);
        }
    }

    private static IReadOnlyList<string> Scan(string repositoryRoot)
    {
        var findings = new List<string>();
        foreach (var file in EnumerateProtectedFiles(repositoryRoot))
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (IsLegalMigrationOrRejectionInput(relativePath))
                {
                    continue;
                }

                if ((ForbiddenPattern.IsMatch(line) ||
                     IsLowCodePath(relativePath) && LegacySemanticPattern.IsMatch(line)) &&
                    !IsUnrelatedVersionLiteral(relativePath, line))
                {
                    findings.Add($"{relativePath}:{index + 1}: forbidden latest-only reference: {line.Trim()}");
                }

                if (NumericDocumentVersionPattern.IsMatch(line) && IsLowCodePath(relativePath))
                {
                    findings.Add($"{relativePath}:{index + 1}: numeric document version routing: {line.Trim()}");
                }
            }
        }

        return findings;
    }

    private static IEnumerable<string> EnumerateProtectedFiles(string repositoryRoot)
    {
        var roots = new[]
        {
            Path.Combine(repositoryRoot, "backend", "AsterERP.Api"),
            Path.Combine(repositoryRoot, "backend", "AsterERP.Contracts"),
            Path.Combine(repositoryRoot, "frontend", "AsterERP.Web", "src"),
            Path.Combine(repositoryRoot, "docs", "contracts")
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                throw new InvalidOperationException($"BLOCKED latest-only scan: protected source root does not exist: {root}");
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (IsExcludedPath(file) || !IsSourceFile(file) && !IsFormalContractFile(file))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/wwwroot/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/public/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/data/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/assets/", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".test.tsx", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSourceFile(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase);

    private static bool IsFormalContractFile(string path) =>
        path.Replace('\\', '/').Contains("/docs/contracts/", StringComparison.OrdinalIgnoreCase) &&
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static bool IsLegalMigrationOrRejectionInput(string relativePath) =>
        relativePath.EndsWith("ApplicationDesignerDocumentMigrationService.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith("ApplicationLegacyPageSchemaMigrationService.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith("ApplicationDevelopmentSchemaValidator.cs", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("development-center/low-code-studio/migration/", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith("designerDocumentCodec.ts", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith("designerDocumentValidator.ts", StringComparison.OrdinalIgnoreCase);

    private static bool IsLowCodePath(string relativePath) =>
        relativePath.Contains("ApplicationDevelopmentCenter", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("development-center/low-code-studio", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("development-center/full-designer", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("runtime-kernel", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("shared/runtime/designer-document", StringComparison.OrdinalIgnoreCase) ||
        relativePath.EndsWith("pages/runtime/RuntimePage.tsx", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains("docs/contracts/", StringComparison.OrdinalIgnoreCase);

    private static bool IsUnrelatedVersionLiteral(string relativePath, string line) =>
        !IsLowCodePath(relativePath) &&
        (line.Contains("deepseek-v4", StringComparison.OrdinalIgnoreCase) ||
         line.Contains("whisper-large-v3", StringComparison.OrdinalIgnoreCase));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("BLOCKED latest-only scan: AsterERP.sln was not found from the test directory.");
    }

    private static string CreateFixtureRepository()
    {
        var root = Path.Combine(Path.GetTempPath(), "AsterERP.LatestOnlyGuard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "backend", "AsterERP.Api", "Application", "ApplicationDevelopmentCenter", "Migrations"));
        Directory.CreateDirectory(Path.Combine(root, "backend", "AsterERP.Api", "Application", "ApplicationDevelopmentCenter"));
        Directory.CreateDirectory(Path.Combine(root, "backend", "AsterERP.Contracts"));
        Directory.CreateDirectory(Path.Combine(root, "backend", "AsterERP.Api.Tests"));
        Directory.CreateDirectory(Path.Combine(root, "frontend", "AsterERP.Web", "src", "runtime-kernel"));
        Directory.CreateDirectory(Path.Combine(root, "docs", "contracts"));
        WriteFile(root, "AsterERP.sln", "fixture");
        WriteFile(root, "backend/AsterERP.Api/Application/ApplicationDevelopmentCenter/Migrations/ApplicationDesignerDocumentMigrationService.cs", "var schemaVersion = 3;");
        WriteFile(root, "backend/AsterERP.Api/Application/ApplicationDevelopmentCenter/ApplicationDevelopmentSchemaValidator.cs", "Reject(schemaVersion);");
        WriteFile(root, "backend/AsterERP.Api.Tests/ApplicationDevelopmentSchemaValidatorTests.cs", "var schemaVersion = 3;");
        WriteFile(root, "frontend/AsterERP.Web/src/runtime-kernel/LatestRuntimeEntry.ts", "const runtime = 'v3';");
        return root;
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void TryDelete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
