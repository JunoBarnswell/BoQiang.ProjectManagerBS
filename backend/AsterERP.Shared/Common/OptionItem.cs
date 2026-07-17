namespace AsterERP.Shared;

public sealed record OptionItem(
    string Label,
    string Value,
    string? Color = null,
    bool Disabled = false);
