using AsterERP.Contracts.Health;

namespace AsterERP.Api.Application.Health;

public sealed class HealthService
{
    public HealthResponse GetStatus()
    {
        return new HealthResponse(
            Project: "AsterERP",
            Service: "AsterERP.Api",
            Status: "Running",
            Time: DateTimeOffset.UtcNow);
    }
}
