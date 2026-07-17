using System.Reflection;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Xunit;

namespace AsterERP.Api.Tests.Ai.KnowledgeGraph;

public sealed class ApplicationDevelopmentCenterPermissionTests
{
    [Theory]
    [InlineData(nameof(ApplicationDevelopmentCenterController.GetOverviewAsync), PermissionCodes.AppDevelopmentCenterDesignerView)]
    [InlineData(nameof(ApplicationDevelopmentCenterController.SaveAppConfigAsync), PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    [InlineData(nameof(ApplicationDevelopmentCenterController.DeletePageAsync), PermissionCodes.AppDevelopmentCenterDesignerDelete)]
    [InlineData(nameof(ApplicationDevelopmentCenterController.PreviewBusinessObjectAsync), PermissionCodes.AppDevelopmentCenterDesignerPreview)]
    [InlineData(nameof(ApplicationDevelopmentCenterController.CompilePreviewArtifactAsync), PermissionCodes.AppDevelopmentCenterDesignerPreview)]
    [InlineData(nameof(ApplicationDevelopmentCenterController.PublishBusinessObjectAsync), PermissionCodes.AppDevelopmentCenterDesignerPublish)]
    [InlineData(nameof(ApplicationDevelopmentCenterController.GetPermissionOptionsAsync), PermissionCodes.AppDevelopmentCenterDesignerPermissionEdit)]
    public async Task Controller_permission_filter_allows_matching_permission_and_denies_missing_permission(string actionName, string permissionCode)
    {
        var method = typeof(ApplicationDevelopmentCenterController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
        var permission = Assert.IsType<PermissionAttribute>(method?.GetCustomAttribute<PermissionAttribute>());

        var allowedContext = KnowledgeGraphTestSupport.CreateAuthorizationContext(
            KnowledgeGraphTestSupport.CreateCurrentUser(permissionCode),
            $"/api/application-development-center/{actionName}");
        await permission.OnAuthorizationAsync(allowedContext);
        Assert.Null(allowedContext.Result);

        var deniedContext = KnowledgeGraphTestSupport.CreateAuthorizationContext(
            KnowledgeGraphTestSupport.CreateCurrentUser("app:development-center:designer:unrelated"),
            $"/api/application-development-center/{actionName}");
        await permission.OnAuthorizationAsync(deniedContext);
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(deniedContext.Result);
        Assert.Equal(403, result.StatusCode);
    }
}
