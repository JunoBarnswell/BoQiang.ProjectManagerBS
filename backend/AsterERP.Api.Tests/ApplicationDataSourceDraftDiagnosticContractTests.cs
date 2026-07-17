using System.Reflection;
using AsterERP.Api.Controllers;
using AsterERP.Contracts.ApplicationDataCenter;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceDraftDiagnosticContractTests
{
    [Fact]
    public void DraftDiagnosticContractIsExposedWithoutPersistenceIdentity()
    {
        var method = typeof(ApplicationDataCenterDataSourcesController).GetMethod(nameof(ApplicationDataCenterDataSourcesController.DiagnoseDraftAsync));
        Assert.NotNull(method);
        Assert.Contains("draft/diagnose", method!.GetCustomAttributes().OfType<HttpPostAttribute>().Single().Template ?? string.Empty);
        Assert.True(typeof(ApplicationDataSourceDraftDiagnosticRequest).IsAssignableTo(method.GetParameters()[0].ParameterType));
        Assert.Equal(typeof(CancellationToken), method.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(Task<IActionResult>), method.ReturnType);
        Assert.DoesNotContain("Id", typeof(ApplicationDataSourceDraftDiagnosticRequest).GetProperties().Select(item => item.Name));
    }
}
