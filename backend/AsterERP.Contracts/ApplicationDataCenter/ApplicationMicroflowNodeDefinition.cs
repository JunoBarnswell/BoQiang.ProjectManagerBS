namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowNodeDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    public Dictionary<string, object?> Config { get; set; } = [];
}
