namespace AsterERP.Api.Application.System.InfrastructureSettings;

public sealed record InfrastructureSettingDescriptor(
    string Key,
    string Name,
    string Category,
    string? DefaultValue = null,
    bool IsSecret = false);
