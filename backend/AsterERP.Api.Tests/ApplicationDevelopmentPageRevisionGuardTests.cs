using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDevelopmentPageRevisionGuardTests
{
    [Fact]
    public void AllowsLegacyRequestWithoutTimestamp()
    {
        var guard = new ApplicationDevelopmentPageRevisionGuard();

        guard.EnsureCurrent(null, DateTime.UtcNow);
    }

    [Fact]
    public void AllowsMatchingTimestamp()
    {
        var guard = new ApplicationDevelopmentPageRevisionGuard();
        var timestamp = DateTime.UtcNow;

        guard.EnsureCurrent(timestamp, timestamp.AddMilliseconds(500));
    }

    [Fact]
    public void RejectsStaleTimestampWithStableConflictCode()
    {
        var guard = new ApplicationDevelopmentPageRevisionGuard();

        var exception = Assert.Throws<ValidationException>(() =>
            guard.EnsureCurrent(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow));

        Assert.Equal(ErrorCodes.ApplicationDevelopmentPageRevisionConflict, exception.Code);
    }
}
