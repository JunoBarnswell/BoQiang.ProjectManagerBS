using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDevelopmentDesignerModeContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("structured")]
    [InlineData("STRUCTURED")]
    public void NormalizeDesignerMode_returns_the_single_latest_semantic(string? value)
    {
        Assert.Equal(ApplicationDevelopmentCenterService.LatestDesignerMode,
            ApplicationDevelopmentCenterService.NormalizeDesignerMode(value));
    }

    [Theory]
    [InlineData("FullDesigner")]
    [InlineData("BusinessObject")]
    [InlineData("v3")]
    [InlineData("v4")]
    [InlineData("runtimeRegistry.wms")]
    public void NormalizeDesignerMode_rejects_legacy_or_unknown_values(string value)
    {
        Assert.Throws<ValidationException>(() =>
            ApplicationDevelopmentCenterService.NormalizeDesignerMode(value));
    }
}
