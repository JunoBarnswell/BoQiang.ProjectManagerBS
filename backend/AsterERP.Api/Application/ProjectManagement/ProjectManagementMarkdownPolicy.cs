using System.Net;
using System.Text.RegularExpressions;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// Project-management Markdown is intentionally a small, link-only text format.
/// Raw HTML and images are not persisted; unsafe link destinations degrade to
/// their visible label so storage and rendering have the same safe semantics.
/// </summary>
internal static partial class ProjectManagementMarkdownPolicy
{
    public const int MaxLength = 20_000;

    public static string? NormalizeOptional(string? value, string tooLongMessage = "Markdown 内容不能超过 20000 个字符")
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var markdown = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        if (markdown.Length > MaxLength) throw new ValidationException(tooLongMessage);

        markdown = HtmlCommentPattern().Replace(markdown, string.Empty);
        markdown = HtmlTagPattern().Replace(markdown, string.Empty);
        markdown = MarkdownLinkPattern().Replace(markdown, static match =>
        {
            var label = match.Groups["label"].Value.Trim();
            if (label.Length == 0) return string.Empty;
            var destination = FirstDestinationToken(match.Groups["destination"].Value);
            return IsSafeLinkDestination(destination) && !match.Groups["image"].Success
                ? $"[{label}]({destination})"
                : label;
        });

        // Any angle brackets left after tag removal are not part of the supported subset.
        markdown = markdown.Replace("<", string.Empty, StringComparison.Ordinal).Replace(">", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(markdown) ? null : markdown;
    }

    public static string NormalizeRequired(string? value, string emptyMessage, string tooLongMessage)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException(emptyMessage);
        return NormalizeOptional(value, tooLongMessage) ?? throw new ValidationException(emptyMessage);
    }

    private static string FirstDestinationToken(string value)
    {
        var candidate = value.Trim();
        if (candidate.StartsWith('<') && candidate.IndexOf('>') is var end && end > 1)
            return candidate[1..end].Trim();
        var separator = candidate.IndexOfAny([' ', '\t', '\n']);
        return separator >= 0 ? candidate[..separator] : candidate;
    }

    private static bool IsSafeLinkDestination(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var decoded = WebUtility.HtmlDecode(value.Trim());
        try { decoded = Uri.UnescapeDataString(decoded); } catch (UriFormatException) { return false; }
        decoded = Regex.Replace(decoded, @"[\u0000-\u0020]", string.Empty, RegexOptions.CultureInvariant);
        return Uri.TryCreate(decoded, UriKind.Absolute, out var uri) &&
            (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"<!--[\s\S]*?-->", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCommentPattern();

    [GeneratedRegex(@"</?[A-Za-z][^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"(?<image>!)?\[(?<label>[^\]\r\n]{0,500})\]\((?<destination>(?:[^()\r\n]|\([^()\r\n]*\))*)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkPattern();
}
