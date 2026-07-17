using AsterERP.Api.Infrastructure.Publishing;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationPublishFrontendTargetWriterTests
{
    [Fact]
    public async Task WriteAsync_GeneratesPagesRuntimeRoute()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"astererp-publish-target-{Guid.NewGuid():N}");
        try
        {
            var writer = new ApplicationPublishFrontendTargetWriter();
            var snapshot = CreateSnapshot("/pages/orders_page");

            await writer.WriteAsync(tempRoot, snapshot, CancellationToken.None);

            var workspaceRoutes = await File.ReadAllTextAsync(
                Path.Combine(tempRoot, "frontend", "AsterERP.Web", "src", "app", "router", "workspaceRoutes.target.tsx"));
            var navigationRoutes = await File.ReadAllTextAsync(
                Path.Combine(tempRoot, "frontend", "AsterERP.Web", "src", "app", "navigation", "routes.target.ts"));

            Assert.Contains("path: 'pages/:pageCode'", workspaceRoutes);
            Assert.Contains("path: '/pages/:pageCode'", navigationRoutes);
            Assert.DoesNotContain("runtime/:pageCode", workspaceRoutes);
            Assert.DoesNotContain("/runtime/:pageCode", navigationRoutes);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_TreatsLegacyRuntimeRouteAsPagesRoute()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"astererp-publish-target-{Guid.NewGuid():N}");
        try
        {
            var writer = new ApplicationPublishFrontendTargetWriter();
            var snapshot = CreateSnapshot("/runtime/orders_page");

            await writer.WriteAsync(tempRoot, snapshot, CancellationToken.None);

            var workspaceRoutes = await File.ReadAllTextAsync(
                Path.Combine(tempRoot, "frontend", "AsterERP.Web", "src", "app", "router", "workspaceRoutes.target.tsx"));
            var navigationRoutes = await File.ReadAllTextAsync(
                Path.Combine(tempRoot, "frontend", "AsterERP.Web", "src", "app", "navigation", "routes.target.ts"));

            Assert.Contains("path: 'pages/:pageCode'", workspaceRoutes);
            Assert.Contains("path: '/pages/:pageCode'", navigationRoutes);
            Assert.DoesNotContain("runtime/:pageCode", workspaceRoutes);
            Assert.DoesNotContain("/runtime/:pageCode", navigationRoutes);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WriteAsync_IncludesWorkflowBindingsRouteWhenWorkflowRoutesAreReferenced()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"astererp-publish-target-{Guid.NewGuid():N}");
        try
        {
            var writer = new ApplicationPublishFrontendTargetWriter();
            var snapshot = CreateSnapshot("/workflows/tasks");

            await writer.WriteAsync(tempRoot, snapshot, CancellationToken.None);

            var workspaceRoutes = await File.ReadAllTextAsync(
                Path.Combine(tempRoot, "frontend", "AsterERP.Web", "src", "app", "router", "workspaceRoutes.target.tsx"));
            var navigationRoutes = await File.ReadAllTextAsync(
                Path.Combine(tempRoot, "frontend", "AsterERP.Web", "src", "app", "navigation", "routes.target.ts"));

            Assert.Contains("const WorkflowBindingsPage", workspaceRoutes);
            Assert.Contains("path: 'workflows/bindings'", workspaceRoutes);
            Assert.Contains("path: '/workflows/bindings'", navigationRoutes);
            Assert.Contains("nav.workflowBindings", navigationRoutes);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static ApplicationPublishDependencySnapshot CreateSnapshot(string frontendRoute) =>
        new(
            AppCode: "MES",
            TenantId: "tenant-a",
            PublishMode: "Incremental",
            Menus: [new { menuCode = "orders" }],
            PageSchemas: [new { pageCode = "orders_page" }],
            DataModels: [],
            PermissionCodes: [],
            BackendRoutes: [],
            FrontendRoutes: [frontendRoute],
            Providers: [],
            ResolvedModules: [],
            ClosureEdges: [],
            UnresolvedDependencies: [],
            ModuleFileMapSha256: "test");
}
