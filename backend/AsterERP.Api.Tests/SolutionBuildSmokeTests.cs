using AsterERP.Domain.Common;
using AsterERP.Shared;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class SolutionBuildSmokeTests
{
    [Fact]
    public void Shared_ApiResult_factory_returns_success_envelope()
    {
        var result = ApiResultFactory.Ok("ok", "trace-1");

        Assert.Equal(ErrorCodes.Success, result.Code);
        Assert.Equal("ok", result.Data);
        Assert.Equal("trace-1", result.TraceId);
    }

    [Fact]
    public void Domain_EntityBase_has_generated_id()
    {
        var entity = new TestEntity();

        Assert.False(string.IsNullOrWhiteSpace(entity.Id));
    }

    private sealed class TestEntity : EntityBase;
}
