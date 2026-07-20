namespace AsterERP.Contracts.ProjectManagement;

/// <summary>
/// A language-neutral description rendered by the receiving client or API culture.
/// </summary>
public sealed record ProjectManagementLocalizedText(
    string Key,
    IReadOnlyDictionary<string, string>? Arguments = null,
    string? Fallback = null);
