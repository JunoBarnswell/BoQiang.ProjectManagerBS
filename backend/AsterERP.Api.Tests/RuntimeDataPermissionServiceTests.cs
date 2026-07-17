using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class RuntimeDataPermissionServiceTests
{
    [Fact]
    public async Task ReadPermission_AllowsPublishedPageModel()
    {
        var service = new RuntimeDataReadPermissionService(
            new FixedRuntimePageSchemaService(CreateDesignerPageSchema()),
            CreateCurrentUser("app:runtime:order:view"));

        await service.EnsureAsync("order", "order-page", null, CancellationToken.None);
    }

    [Fact]
    public async Task MutationPermission_AllowsPublishedPageModel()
    {
        var service = new RuntimeDataMutationPermissionService(
            new FixedRuntimePageSchemaService(CreateDesignerPageSchema()),
            CreateCurrentUser("app:runtime:order:edit"));

        await service.EnsureAsync("order", "order-page", null, "edit", CancellationToken.None);
    }

    [Fact]
    public async Task MutationPermission_AllowsDocumentRuntimeContextModelCodes()
    {
        var service = new RuntimeDataMutationPermissionService(
            new FixedRuntimePageSchemaService(CreateDesignerPageSchemaWithDocumentModelCodes()),
            CreateCurrentUser("app:runtime:order:edit"));

        await service.EnsureAsync("order_line", "order-page", null, "edit", CancellationToken.None);
    }

    [Fact]
    public async Task MutationPermission_RejectsModelNotDeclaredByPage()
    {
        var service = new RuntimeDataMutationPermissionService(
            new FixedRuntimePageSchemaService(CreateDesignerPageSchema()),
            CreateCurrentUser("app:runtime:order:edit"));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureAsync("invoice", "order-page", null, "edit", CancellationToken.None));

        Assert.Equal(ErrorCodes.RuntimeDataModelInvalid, error.Code);
    }

    [Fact]
    public async Task MutationPermission_StillRequiresActionPermission()
    {
        var service = new RuntimeDataMutationPermissionService(
            new FixedRuntimePageSchemaService(CreateDesignerPageSchema()),
            CreateCurrentUser("app:runtime:order:view"));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureAsync("order", "order-page", null, "edit", CancellationToken.None));

        Assert.Equal(ErrorCodes.PermissionDenied, error.Code);
    }

    private static RuntimePageSchemaResponse CreateDesignerPageSchema()
    {
        const string schemaJson = """
        {
          "renderer": "designerDocument",
          "runtimeContext": {
            "modelCode": "order"
          },
          "document": {
            "schemaVersion": 3,
            "runtimeContext": {
              "pageCode": "order-page"
            }
          }
        }
        """;

        return new RuntimePageSchemaResponse(
            "page-id",
            "tenant-a",
            "MES",
            "order-page",
            "订单页面",
            "designer",
            "order",
            "app:runtime:order:view",
            1,
            schemaJson);
    }

    private static RuntimePageSchemaResponse CreateDesignerPageSchemaWithDocumentModelCodes()
    {
        const string schemaJson = """
        {
          "renderer": "designerDocument",
          "document": {
            "schemaVersion": 3,
            "runtimeContext": {
              "pageCode": "order-page",
              "modelCode": "order",
              "modelCodes": ["order", "order_line", "order_unit"]
            }
          }
        }
        """;

        return new RuntimePageSchemaResponse(
            "page-id",
            "tenant-a",
            "MES",
            "order-page",
            "订单页面",
            "designer",
            null,
            "app:runtime:order:view",
            1,
            schemaJson);
    }

    private static ICurrentUser CreateCurrentUser(params string[] permissions)
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-a",
            "客户A",
            "MES",
            "客户A MES",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            permissions,
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new Volo.Abp.Users.CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }

    private sealed class FixedRuntimePageSchemaService(RuntimePageSchemaResponse page) : IRuntimePageSchemaService
    {
        public Task<RuntimePageSchemaResponse> GetPublishedPageAsync(
            string pageCode,
            string? previewPageId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(page);
        }
    }
}
