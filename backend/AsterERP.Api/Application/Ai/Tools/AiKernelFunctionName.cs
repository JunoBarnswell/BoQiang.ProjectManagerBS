using System.Text;

namespace AsterERP.Api.Application.Ai.Tools;

public sealed record AiKernelFunctionName(string PluginName, string FunctionName);

public static class AiKernelFunctionNaming
{
    public static AiKernelFunctionName Resolve(AiKernelFunctionDefinition definition)
    {
        var domain = string.IsNullOrWhiteSpace(definition.ToolDomain)
            ? InferDomain(definition.ToolCode)
            : definition.ToolDomain;
        var function = string.IsNullOrWhiteSpace(domain)
            ? definition.ToolCode
            : definition.ToolCode.StartsWith($"{domain}.", StringComparison.OrdinalIgnoreCase)
                ? definition.ToolCode[(domain.Length + 1)..]
                : definition.ToolCode;

        return new AiKernelFunctionName(
            NormalizeIdentifier(domain, "astererp"),
            NormalizeIdentifier(function, "invoke"));
    }

    private static string InferDomain(string toolCode)
    {
        var index = toolCode.IndexOf('.', StringComparison.Ordinal);
        return index <= 0 ? "astererp" : toolCode[..index];
    }

    private static string NormalizeIdentifier(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        var normalized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
