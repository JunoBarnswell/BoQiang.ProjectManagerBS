using AsterERP.Api.Infrastructure.Publishing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationPublishPathGuardTests
{
    [Theory]
    [InlineData("wms", "WMS")]
    [InlineData("MES_01", "MES_01")]
    [InlineData("system-app", "SYSTEM-APP")]
    public void NormalizeAppCode_accepts_safe_codes(string input, string expected)
    {
        var guard = CreateGuard();

        Assert.Equal(expected, guard.NormalizeAppCode(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../WMS")]
    [InlineData("WMS/DEV")]
    [InlineData("APP CODE")]
    [InlineData("012345678901234567890123456789012")]
    public void NormalizeAppCode_rejects_unsafe_codes(string input)
    {
        var guard = CreateGuard();

        Assert.Throws<InvalidOperationException>(() => guard.NormalizeAppCode(input));
    }

    [Fact]
    public void ResolveTaskRoot_keeps_task_directory_under_publish_root()
    {
        var guard = CreateGuard();
        var outputRoot = guard.ResolveOutputRoot();

        var taskRoot = guard.ResolveTaskRoot("wms", "task-1");

        Assert.StartsWith(outputRoot, taskRoot, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("WMS", "task-1"), taskRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureInsideRoot_rejects_path_traversal()
    {
        var guard = CreateGuard();
        var outputRoot = guard.ResolveOutputRoot();
        var outside = Path.Combine(outputRoot, "..", "outside");

        Assert.Throws<InvalidOperationException>(() => guard.EnsureInsideRoot(outside, outputRoot));
    }

    private static ApplicationPublishPathGuard CreateGuard()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "AsterERP.Tests", "repo", "backend", "AsterERP.Api");
        var environment = new TestWebHostEnvironment(contentRoot);
        return new ApplicationPublishPathGuard(
            environment,
            Options.Create(new ApplicationPublishOptions { OutputRoot = @"..\..\output\publish-apps" }));
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AsterERP.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(contentRootPath, "wwwroot");
    }
}
