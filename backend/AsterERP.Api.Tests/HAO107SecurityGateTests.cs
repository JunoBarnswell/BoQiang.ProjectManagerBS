using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class HAO107SecurityGateTests
{
    [Fact]
    public void Document_validator_rejects_duplicate_keys_at_any_json_depth()
    {
        var json = "{\"documentId\":\"doc-1\",\"revision\":1,\"pages\":[],\"elements\":{},\"metadata\":{\"name\":1,\"name\":2}}";

        var exception = Assert.Throws<ValidationException>(() => new ApplicationDevelopmentSchemaValidator().ValidateDraft(json));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, exception.Code);
    }

    [Fact]
    public void Document_validator_rejects_over_depth_documents_and_runtime_payloads()
    {
        var deep = new JsonObject
        {
            ["documentId"] = "deep-doc",
            ["revision"] = 1,
            ["pages"] = new JsonArray(new JsonObject { ["id"] = "page", ["rootElementId"] = "node-0" }),
            ["elements"] = CreateElementChain(ApplicationDevelopmentSchemaValidator.MaximumDepth + 1)
        };
        var validator = new ApplicationDevelopmentSchemaValidator();

        var depthException = Assert.Throws<ValidationException>(() => validator.ValidateDraft(deep.ToJsonString()));
        Assert.Equal(ErrorCodes.SchemaOrPayloadTooLarge, depthException.Code);

        var oversized = ValidDocument();
        oversized["description"] = new string('x', ApplicationDevelopmentSchemaValidator.RuntimeMaximumBytes);
        var sizeException = Assert.Throws<ValidationException>(() => validator.ValidateRuntimeArtifact(oversized.ToJsonString()));
        Assert.Equal(ErrorCodes.SchemaOrPayloadTooLarge, sizeException.Code);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Identifier_quoting_rejects_injection_in_object_and_schema_names(IApplicationDataSourceProvider provider)
    {
        Assert.Equal(provider.QuoteIdentifier("orders"), provider.QuoteQualified("sales", "orders").Split('.').Last());
        Assert.Throws<ValidationException>(() => provider.QuoteQualified("sales; DROP TABLE users", "orders"));
        Assert.Throws<ValidationException>(() => provider.QuoteQualified("sales", "orders] DROP TABLE users"));
    }

    [Fact]
    public void Raw_sql_policy_requires_read_only_single_statement_and_parameterization_keeps_values_out_of_sql()
    {
        Assert.Throws<ValidationException>(() => ApplicationDataSourceSqlPolicy.RequireSelectSql("SELECT id FROM orders; DELETE FROM orders"));
        Assert.Throws<ValidationException>(() => ApplicationDataSourceSqlPolicy.RequireSelectSql("SELECT id FROM orders /* DROP TABLE users */"));

        var parameterizer = new ApplicationDataCenterSqlScriptFunctionParameterizer(
            new ApplicationDataCenterSqlScriptExpressionParser(new ApplicationDataCenterSqlScriptExpressionTokenizer()),
            new ApplicationDataCenterSqlScriptExpressionEvaluator(
                new ApplicationDataCenterSqlScriptExpressionParser(new ApplicationDataCenterSqlScriptExpressionTokenizer()),
                new AsterERP.Api.Application.Runtime.ExpressionFunctions.RuntimeExpressionFunctionCatalog(),
                new RuntimeExpressionHelperCatalog(),
                null));
        var variables = new Dictionary<string, object?> { ["keyword"] = "' OR 1=1 --" };

        var result = parameterizer.Parameterize("SELECT id FROM orders WHERE name = @keyword", variables);

        Assert.Contains("@keyword", result.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("OR 1=1", result.Script, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("' OR 1=1 --", variables["keyword"]);
    }

    [Fact]
    public void Secret_summary_is_public_metadata_only_and_never_contains_ciphertext_or_plaintext()
    {
        var protector = new ApplicationDataSecretProtector(DataProtectionProvider.Create("HAO-107-tests"));
        const string secret = "{\"password\":\"do-not-return\",\"token\":\"sensitive-token\"}";
        var cipherText = protector.Protect(secret);
        var summary = protector.BuildPublicSecretSummary(cipherText, "ds-1:secret", DateTime.UtcNow);

        Assert.DoesNotContain(secret, summary, StringComparison.Ordinal);
        Assert.DoesNotContain(cipherText, summary, StringComparison.Ordinal);
        Assert.Contains("\"masked\":true", summary, StringComparison.Ordinal);
        Assert.Contains("\"hasSecret\":true", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Security_contract_keeps_server_permissions_and_audit_persistence_mandatory()
    {
        var root = FindRepositoryRoot();
        var controller = File.ReadAllText(Path.Combine(root, "backend", "AsterERP.Api", "Controllers", "ApplicationDataCenterDataSourcesController.cs"));
        var runtime = File.ReadAllText(Path.Combine(root, "backend", "AsterERP.Api", "Application", "ApplicationDataCenter", "ApplicationMicroflowRuntimeService.cs"));
        var engine = File.ReadAllText(Path.Combine(root, "backend", "AsterERP.Api", "Application", "ApplicationDataCenter", "ApplicationDataCenterSqlScriptEngine.cs"));
        var auditWriter = File.ReadAllText(Path.Combine(root, "backend", "AsterERP.Api", "Application", "ApplicationDataCenter", "ApplicationDataCenterSqlScriptAuditWriter.cs"));

        Assert.Contains("[Permission(PermissionCodes.AppDataCenterDataSource", controller, StringComparison.Ordinal);
        Assert.Contains("EnsureApiPermission", runtime, StringComparison.Ordinal);
        Assert.Contains("await auditWriter.WriteAsync(audit, CancellationToken.None)", engine, StringComparison.Ordinal);
        Assert.Contains("audit.TenantId = workspace.TenantId", auditWriter, StringComparison.Ordinal);
        Assert.Contains("audit.AppCode = workspace.AppCode", auditWriter, StringComparison.Ordinal);
    }

    [Fact]
    public void Sqlite_sandbox_and_application_scope_controls_are_present_at_the_real_boundaries()
    {
        var root = FindRepositoryRoot();
        var sandbox = File.ReadAllText(Path.Combine(root, "backend", "AsterERP.Api", "Application", "ApplicationDataCenter", "ApplicationDataSourceSqliteSandbox.cs"));
        var approval = File.ReadAllText(Path.Combine(root, "backend", "AsterERP.Api", "Application", "ApplicationDataCenter", "ApplicationDataSourceSqlitePathApprovalService.cs"));
        var resolver = File.ReadAllText(Path.Combine(root, "backend", "AsterERP.Api", "Application", "ApplicationDataCenter", "ApplicationDataCenterWorkspaceResolver.cs"));

        Assert.Contains("EnsureInsideRoot", sandbox, StringComparison.Ordinal);
        Assert.Contains("EnsureNoSymbolicLink", sandbox, StringComparison.Ordinal);
        Assert.Contains("RequireActiveAsync", sandbox, StringComparison.Ordinal);
        Assert.Contains("workspace.TenantId", approval, StringComparison.Ordinal);
        Assert.Contains("workspace.AppCode", approval, StringComparison.Ordinal);
        Assert.Contains("PermissionDenied", resolver, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> Providers()
    {
        yield return [new SqliteApplicationDataSourceProvider()];
        yield return [new MySqlApplicationDataSourceProvider()];
        yield return [new PostgreSqlApplicationDataSourceProvider()];
        yield return [new SqlServerApplicationDataSourceProvider()];
    }

    private static JsonObject CreateElementChain(int count)
    {
        var elements = new JsonObject();
        for (var index = 0; index < count; index++)
        {
            var id = $"node-{index}";
            elements[id] = new JsonObject
            {
                ["id"] = id,
                ["parentId"] = index == 0 ? null : $"node-{index - 1}",
                ["children"] = index == count - 1 ? new JsonArray() : new JsonArray($"node-{index + 1}"),
                ["type"] = "layout.page"
            };
        }

        return elements;
    }

    private static JsonObject ValidDocument() => new()
    {
        ["documentId"] = "document-1",
        ["revision"] = 1,
        ["pages"] = new JsonArray(new JsonObject { ["id"] = "page", ["rootElementId"] = "root" }),
        ["elements"] = new JsonObject
        {
            ["root"] = new JsonObject { ["id"] = "root", ["parentId"] = null, ["children"] = new JsonArray("child"), ["type"] = "layout.page" },
            ["child"] = new JsonObject { ["id"] = "child", ["parentId"] = "root", ["children"] = new JsonArray(), ["type"] = "layout.page" }
        }
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsterERP.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("AsterERP.sln was not found.");
    }
}
