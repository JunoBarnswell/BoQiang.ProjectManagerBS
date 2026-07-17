using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPageRevisionGuard
{
    private const double AllowedClockDriftMilliseconds = 1000;

    public void EnsureCurrent(DateTime? expectedUpdatedTime, DateTime? currentUpdatedTime)
    {
        if (!expectedUpdatedTime.HasValue)
        {
            return;
        }

        var current = currentUpdatedTime ?? DateTime.MinValue;
        if (Math.Abs((current - expectedUpdatedTime.Value).TotalMilliseconds) > AllowedClockDriftMilliseconds)
        {
            throw new ValidationException("页面草稿已被其他人修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }
    }
}
