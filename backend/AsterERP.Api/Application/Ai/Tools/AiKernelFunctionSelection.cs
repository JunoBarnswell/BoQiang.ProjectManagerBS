namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionSelection
{
    private readonly HashSet<string> toolCodes;
    private readonly HashSet<string> toolDomains;

    private AiKernelFunctionSelection(IEnumerable<string> enabledToolCodes, IEnumerable<string> enabledToolDomains)
    {
        toolCodes = enabledToolCodes
            .Select(NormalizeCode)
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        toolDomains = enabledToolDomains
            .Select(NormalizeDomain)
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> EnabledToolCodes => toolCodes;

    public IReadOnlyCollection<string> EnabledToolDomains => toolDomains;

    public bool HasSelection => toolCodes.Count > 0 || toolDomains.Count > 0;

    public static AiKernelFunctionSelection From(
        IEnumerable<string>? enabledToolCodes,
        IEnumerable<string>? enabledToolDomains) =>
        new(enabledToolCodes ?? [], enabledToolDomains ?? []);

    public bool Allows(AiKernelFunctionDefinition definition)
    {
        if (!HasSelection)
        {
            return false;
        }

        var toolCode = NormalizeCode(definition.ToolCode);
        if (toolCodes.Contains(toolCode))
        {
            return true;
        }

        var toolDomain = NormalizeDomain(definition.ToolDomain);
        if (toolDomain.Length > 0 && toolDomains.Contains(toolDomain))
        {
            return true;
        }

        var prefixDomain = InferDomainFromCode(toolCode);
        return prefixDomain.Length > 0 && toolDomains.Contains(prefixDomain);
    }

    private static string NormalizeCode(string? value) => value?.Trim() ?? string.Empty;

    private static string NormalizeDomain(string? value)
    {
        var normalized = value?.Trim().TrimEnd('.') ?? string.Empty;
        return normalized;
    }

    private static string InferDomainFromCode(string toolCode)
    {
        var separatorIndex = toolCode.IndexOf('.', StringComparison.Ordinal);
        return separatorIndex <= 0 ? string.Empty : toolCode[..separatorIndex];
    }
}
