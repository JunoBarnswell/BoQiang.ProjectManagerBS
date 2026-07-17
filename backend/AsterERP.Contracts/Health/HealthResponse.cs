namespace AsterERP.Contracts.Health;

public sealed record HealthResponse(string Project, string Service, string Status, DateTimeOffset Time);
