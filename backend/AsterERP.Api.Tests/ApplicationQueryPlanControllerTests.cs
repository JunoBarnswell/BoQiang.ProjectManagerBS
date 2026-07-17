using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationQueryPlanControllerTests
{
    [Fact]
    public void RiskConfirmationEndpointRequiresEditPermissionAndHasDedicatedRoute()
    {
        var method = typeof(ApplicationDataCenterQueryDatasetsController)
            .GetMethod(nameof(ApplicationDataCenterQueryDatasetsController.IssueQueryPlanRiskConfirmationAsync));

        Assert.NotNull(method);
        var route = Assert.Single(Attribute.GetCustomAttributes(method!, typeof(HttpPostAttribute)).Cast<HttpPostAttribute>());
        var permission = Assert.Single(Attribute.GetCustomAttributes(method, typeof(PermissionAttribute)).Cast<PermissionAttribute>());

        Assert.Equal("query-plan/risk-confirmation", route.Template);
        Assert.Equal(PermissionCodes.AppDataCenterQueryDatasetEdit, permission.Code);
    }

    [Fact]
    public void ControlledWriteEndpointRequiresEditPermissionAndHasDedicatedRoute()
    {
        var method = typeof(ApplicationDataCenterQueryDatasetsController)
            .GetMethod(nameof(ApplicationDataCenterQueryDatasetsController.ExecuteControlledWriteQueryPlanAsync));

        Assert.NotNull(method);
        var route = Assert.Single(Attribute.GetCustomAttributes(method!, typeof(HttpPostAttribute)).Cast<HttpPostAttribute>());
        var permission = Assert.Single(Attribute.GetCustomAttributes(method, typeof(PermissionAttribute)).Cast<PermissionAttribute>());

        Assert.Equal("query-plan/write", route.Template);
        Assert.Equal(PermissionCodes.AppDataCenterQueryDatasetEdit, permission.Code);
    }
}
