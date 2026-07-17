namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentNextActionDto
{
    public string Code { get; set; } = string.Empty;

    public string? Route { get; set; }

    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
