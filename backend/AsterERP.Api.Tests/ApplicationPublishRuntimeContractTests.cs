using System.Reflection;
using System.Text.Json;
using AsterERP.Api.Application.Platform.ApplicationPublishing;
using AsterERP.Api.Infrastructure.Publishing;
using AsterERP.Api.Modules.Platform;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationPublishRuntimeContractTests
{
    [Theory]
    [InlineData("/MES", "/MES")]
    [InlineData("WMS", "/WMS")]
    [InlineData("/business_app", "/business_app")]
    public void Runtime_config_uses_a_single_safe_frontend_segment(string input, string expected)
    {
        var result = InvokeFrontendBasePath(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/MES/orders")]
    [InlineData("/api")]
    [InlineData("/hubs")]
    [InlineData("../MES")]
    [InlineData("/MES?tenant=other")]
    public void Runtime_config_rejects_frontend_path_escape_or_reserved_routes(string input)
    {
        Assert.Throws<InvalidOperationException>(() => InvokeFrontendBasePath(input));
    }

    [Fact]
    public void Runtime_config_output_is_nested_under_frontend_base_path()
    {
        var task = new SystemApplicationPublishTaskEntity
        {
            AppCode = "MES",
            TenantId = "tenant-a",
            FrontendBasePath = "/MES",
            FrontendApiBaseUrl = "/api",
            BackendHost = "127.0.0.1",
            BackendPort = 5000
        };
        var method = typeof(PlatformApplicationPublishRunner).GetMethod(
            "BuildRuntimeConfig",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var runtimeConfig = Assert.IsType<ApplicationPublishRuntimeConfig>(
            method!.Invoke(null, [task, Path.Combine("release-root")]));

        Assert.Equal("/MES", runtimeConfig.FrontendBasePath);
        Assert.Equal(
            Path.Combine("release-root", "wwwroot", "MES"),
            runtimeConfig.FrontendOutputPath);
    }

    [Fact]
    public void Runtime_file_contains_the_publish_workspace_identity()
    {
        var runtimeFile = new ApplicationPublishRuntimeFile(
            "MES",
            "tenant-a",
            "127.0.0.1",
            5000,
            "http://127.0.0.1:5000",
            "/MES",
            "/api",
            "release-root/wwwroot/MES");

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            runtimeFile,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("MES", document.RootElement.GetProperty("appCode").GetString());
        Assert.Equal("tenant-a", document.RootElement.GetProperty("tenantId").GetString());
        Assert.Equal("/MES", document.RootElement.GetProperty("frontendBasePath").GetString());
        Assert.Equal("release-root/wwwroot/MES", document.RootElement.GetProperty("frontendOutputPath").GetString());
    }

    [Fact]
    public void Publish_dependency_snapshot_reads_latest_runtime_artifacts_only()
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AsterERP.sln"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root!.FullName, "backend", "AsterERP.Api", "Application", "Platform", "ApplicationPublishing", "PlatformApplicationPublishRunner.cs"));

        Assert.Contains("ApplicationDesignerRuntimeArtifactEntity", source, StringComparison.Ordinal);
        Assert.Contains("ValidatePublishedArtifact", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemPageSchemaEntity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("system.page-schemas", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Publish_request_rejects_missing_tenant_scope(string? tenantId)
    {
        var method = typeof(PlatformApplicationPublishService).GetMethod(
            "RequireTenantId",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var exception = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, [tenantId]));
        Assert.IsType<ValidationException>(exception.InnerException);
    }

    private static string InvokeFrontendBasePath(string value)
    {
        var method = typeof(PlatformApplicationPublishRunner).GetMethod(
            "RequireFrontendBasePath",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        try
        {
            return (string)method!.Invoke(null, [value])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}
