using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.ScheduledJobs;

namespace AsterERP.Api.Domain.System.ScheduledJobs;

public sealed class HttpCallbackDomainPolicy
{
    public void EnsureAllowed(HttpCallbackConfigDto? callback, SchedulerOptions options)
    {
        if (callback is null)
        {
            throw new ValidationException("请配置 HTTP 回调信息", ErrorCodes.ScheduledJobHttpCallbackInvalid);
        }

        if (!Uri.TryCreate(callback.Url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException("HTTP 回调地址无效", ErrorCodes.ScheduledJobHttpCallbackInvalid);
        }

        if (IsLocalOrPrivateLiteral(uri))
        {
            throw new ValidationException("HTTP 回调禁止访问本机或私有网络地址", ErrorCodes.ScheduledJobHttpCallbackHostDenied);
        }

        if (!options.AllowedHosts.Any(host => string.Equals(host?.Trim(), uri.DnsSafeHost, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("HTTP 回调地址不在允许域名内", ErrorCodes.ScheduledJobHttpCallbackHostDenied);
        }

        var method = callback.Method.Trim().ToUpperInvariant();
        if (method is not ("GET" or "POST"))
        {
            throw new ValidationException("HTTP 回调仅支持 GET 或 POST", ErrorCodes.ScheduledJobHttpCallbackInvalid);
        }

        if (!string.IsNullOrWhiteSpace(callback.BodyJson))
        {
            EnsureJson(callback.BodyJson);
        }

        if (callback.Headers is not null)
        {
            foreach (var key in callback.Headers.Keys)
            {
                var normalized = key.Trim().ToLowerInvariant();
                if (normalized.Contains("authorization", StringComparison.Ordinal) ||
                    normalized.Contains("token", StringComparison.Ordinal) ||
                    normalized.Contains("secret", StringComparison.Ordinal) ||
                    normalized.Contains("password", StringComparison.Ordinal))
                {
                    throw new ValidationException("HTTP Header 不允许保存敏感字段", ErrorCodes.ScheduledJobHttpCallbackInvalid);
                }
            }
        }
    }

    public async Task EnsureResolvedAddressesAllowedAsync(Uri uri, CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (SocketException)
        {
            throw new ValidationException("HTTP 回调域名无法解析", ErrorCodes.ScheduledJobHttpCallbackHostDenied);
        }

        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new ValidationException("HTTP 回调域名解析到了本机或私有网络地址", ErrorCodes.ScheduledJobHttpCallbackHostDenied);
        }
    }

    private static bool IsLocalOrPrivateLiteral(Uri uri)
    {
        if (uri.IsLoopback ||
            string.Equals(uri.DnsSafeHost, "localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.DnsSafeHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.DnsSafeHost, out var address) && !IsPublicAddress(address);
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] switch
            {
                0 or 10 or 127 => false,
                100 when bytes[1] is >= 64 and <= 127 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] is >= 16 and <= 31 => false,
                192 when bytes[1] == 168 => false,
                >= 224 => false,
                _ => true
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal &&
                   !address.IsIPv6SiteLocal &&
                   (bytes[0] & 0xFE) != 0xFC &&
                   bytes[0] != 0xFF;
        }

        return false;
    }

    private static void EnsureJson(string bodyJson)
    {
        try
        {
            using var _ = JsonDocument.Parse(bodyJson);
        }
        catch (JsonException)
        {
            throw new ValidationException("HTTP 请求体必须是合法 JSON", ErrorCodes.ScheduledJobHttpCallbackInvalid);
        }
    }
}
