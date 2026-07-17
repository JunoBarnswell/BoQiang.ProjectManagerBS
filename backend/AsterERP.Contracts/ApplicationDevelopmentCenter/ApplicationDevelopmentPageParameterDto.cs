namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPageParameterDto
{
    public string Code { get; set; } = string.Empty;

    public object? DefaultValue { get; set; }

    public string Direction { get; set; } = "input";

    public string Name { get; set; } = string.Empty;

    public bool Required { get; set; }

    public string ValueType { get; set; } = "string";
}
