using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Shared;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementAbpModuleTests
{
    [Fact]
    public void Project_management_module_is_registered_by_the_host()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpProjectManagementModule)));

        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AsterERP.sln")))
        {
            root = root.Parent;
        }

        var hostModuleSource = File.ReadAllText(Path.Combine(
            root!.FullName,
            "backend",
            "AsterERP.Api",
            "Infrastructure",
            "Abp",
            "AsterErpAbpHostModule.cs"));

        Assert.Contains("typeof(AsterErpProjectManagementModule)", hostModuleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_management_permission_codes_are_unique_and_use_the_module_prefix()
    {
        var permissions = PermissionCodes.ProjectManagementPermissionCodes;

        Assert.NotEmpty(permissions);
        Assert.Equal(permissions.Count, permissions.Distinct(StringComparer.Ordinal).Count());
        Assert.All(permissions, permission => Assert.StartsWith("project-management:", permission, StringComparison.Ordinal));
    }

    [Fact]
    public void Project_management_permission_catalog_and_fixed_menu_use_the_same_view_permission()
    {
        var viewPermission = PermissionCodes.ProjectManagementProjectView;

        Assert.Contains(
            ApplicationShellPermissionCatalog.Definitions,
            item => item.PermissionCode == viewPermission && item.ModuleName == "ProjectManagement");
        Assert.Contains(
            ApplicationShellMenuCatalog.CoreItems,
            item => item.MenuCode == "project-management" && item.PermissionCode == viewPermission);
    }

    [Fact]
    public void Project_management_is_registered_in_publish_and_architecture_contracts()
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AsterERP.sln")))
        {
            root = root.Parent;
        }

        var repositoryRoot = root!.FullName;
        var moduleMap = File.ReadAllText(Path.Combine(repositoryRoot, "backend", "module-file-map.json"));
        var architecture = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "architecture-and-tech-framework.md"));
        var contracts = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "contracts.md"));

        Assert.Contains("\"moduleKey\": \"project-management\"", moduleMap, StringComparison.Ordinal);
        Assert.Contains("backend/AsterERP.Api/Application/ProjectManagement/**", moduleMap, StringComparison.Ordinal);
        Assert.Contains("ProjectManagement 模块契约", contracts, StringComparison.Ordinal);
        Assert.Contains("项目管理", architecture, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_management_im_target_contract_is_resolved_from_the_conversation_only()
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AsterERP.sln")))
        {
            root = root.Parent;
        }

        var repositoryRoot = root!.FullName;
        var serviceContract = File.ReadAllText(Path.Combine(repositoryRoot, "backend", "AsterERP.Api", "Application", "ProjectManagement", "IProjectManagementImConversationService.cs"));
        var controller = File.ReadAllText(Path.Combine(repositoryRoot, "backend", "AsterERP.Api", "Controllers", "ProjectManagementImConversationTargetsController.cs"));
        var contracts = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "contracts.md"));

        Assert.Contains("ResolveTargetAsync(string conversationId", serviceContract, StringComparison.Ordinal);
        Assert.Contains("[Route(\"api/project-management/im-conversations\")]", controller, StringComparison.Ordinal);
        Assert.Contains("[HttpGet(\"{conversationId}/target\")]", controller, StringComparison.Ordinal);
        Assert.Contains("/api/project-management/im-conversations/{conversationId}/target", contracts, StringComparison.Ordinal);
    }
}
