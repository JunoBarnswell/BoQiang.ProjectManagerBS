using System.Net;
using System.Net.Sockets;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;

internal static class ApplicationDataOutboundHttpClient
{
    public const string Name = "application-data-outbound";

    private static readonly (IPAddress Network, int PrefixLength)[] NonPublicNetworks =
    [
        (IPAddress.Parse("0.0.0.0"), 8),
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("100.64.0.0"), 10),
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.0.0.0"), 24),
        (IPAddress.Parse("192.0.2.0"), 24),
        (IPAddress.Parse("192.88.99.0"), 24),
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("198.18.0.0"), 15),
        (IPAddress.Parse("198.51.100.0"), 24),
        (IPAddress.Parse("203.0.113.0"), 24),
        (IPAddress.Parse("224.0.0.0"), 4),
        (IPAddress.Parse("240.0.0.0"), 4),
        (IPAddress.Parse("::"), 128),
        (IPAddress.Parse("::1"), 128),
        (IPAddress.Parse("64:ff9b::"), 96),
        (IPAddress.Parse("64:ff9b:1::"), 48),
        (IPAddress.Parse("100::"), 64),
        (IPAddress.Parse("2001::"), 32),
        (IPAddress.Parse("2001:2::"), 48),
        (IPAddress.Parse("2001:10::"), 28),
        (IPAddress.Parse("2001:20::"), 28),
        (IPAddress.Parse("2001:db8::"), 32),
        (IPAddress.Parse("2002::"), 16),
        (IPAddress.Parse("3fff::"), 20),
        (IPAddress.Parse("5f00::"), 16),
        (IPAddress.Parse("fc00::"), 7),
        (IPAddress.Parse("fe80::"), 10),
        (IPAddress.Parse("fec0::"), 10),
        (IPAddress.Parse("ff00::"), 8)
    ];

    public static HttpMessageHandler CreatePrimaryHandler() =>
        new ApplicationDataOutboundHttpMessageHandler(CreateSocketsHandler());

    internal static SocketsHttpHandler CreateSocketsHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = ConnectPublicEndpointAsync
    };

    internal static void EnsureAllowedUri(Uri? uri)
    {
        if (uri is null ||
            !uri.IsAbsoluteUri ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "应用数据中心出站请求仅支持有效的 HTTP(S) 绝对地址",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            throw new ValidationException(
                "应用数据中心出站请求地址禁止携带用户信息",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var host = NormalizeHost(uri.DnsSafeHost);
        if (string.IsNullOrWhiteSpace(host) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "应用数据中心出站请求禁止访问本机地址",
                ErrorCodes.PermissionDenied);
        }

        if (IPAddress.TryParse(host, out var address) && !IsPublicAddress(address))
        {
            throw new ValidationException(
                "应用数据中心出站请求禁止访问私有或保留网络地址",
                ErrorCodes.PermissionDenied);
        }
    }

    internal static async Task<IPAddress[]> ResolvePublicAddressesAsync(
        string host,
        CancellationToken cancellationToken)
    {
        var normalizedHost = NormalizeHost(host);
        if (IPAddress.TryParse(normalizedHost, out var literalAddress))
        {
            var literalAddresses = new[] { literalAddress };
            EnsureResolvedAddressesArePublic(literalAddresses);
            return literalAddresses;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(normalizedHost, cancellationToken);
        }
        catch (SocketException)
        {
            throw new ValidationException(
                "应用数据中心出站请求目标域名无法解析",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        EnsureResolvedAddressesArePublic(addresses);
        return addresses;
    }

    internal static void EnsureResolvedAddressesArePublic(IReadOnlyCollection<IPAddress> addresses)
    {
        if (addresses.Count == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new ValidationException(
                "应用数据中心出站请求目标解析到了私有、回环或保留网络地址",
                ErrorCodes.PermissionDenied);
        }
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily is not AddressFamily.InterNetwork and not AddressFamily.InterNetworkV6 ||
            IPAddress.IsLoopback(address))
        {
            return false;
        }

        return !NonPublicNetworks.Any(range => IsInPrefix(address, range.Network, range.PrefixLength));
    }

    private static async ValueTask<Stream> ConnectPublicEndpointAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await ResolvePublicAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        Exception? lastError = null;

        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, context.DnsEndPoint.Port),
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (Exception exception)
            {
                lastError = exception;
                socket.Dispose();
            }
        }

        throw new HttpRequestException(
            "应用数据中心出站请求无法连接到已验证的公网地址",
            lastError);
    }

    private static bool IsInPrefix(IPAddress address, IPAddress network, int prefixLength)
    {
        if (address.AddressFamily != network.AddressFamily)
        {
            return false;
        }

        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var wholeBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var index = 0; index < wholeBytes; index++)
        {
            if (addressBytes[index] != networkBytes[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (addressBytes[wholeBytes] & mask) == (networkBytes[wholeBytes] & mask);
    }

    private static string NormalizeHost(string host) => host.Trim().TrimEnd('.');
}

internal sealed class ApplicationDataOutboundHttpMessageHandler(HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ApplicationDataOutboundHttpClient.EnsureAllowedUri(request.RequestUri);

        if (!string.IsNullOrWhiteSpace(request.Headers.Host) ||
            request.Headers.Contains("Proxy-Authorization") ||
            request.Headers.Contains("Proxy-Connection"))
        {
            throw new ValidationException(
                "应用数据中心出站请求禁止覆盖 Host 或代理认证头",
                ErrorCodes.PermissionDenied);
        }

        await ApplicationDataOutboundHttpClient.ResolvePublicAddressesAsync(
            request.RequestUri!.DnsSafeHost,
            cancellationToken);

        return await base.SendAsync(request, cancellationToken);
    }
}

internal sealed class ApplicationDataScopedHttpClientFactory(IHttpClientFactory innerFactory)
    : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        var resolvedName = string.IsNullOrEmpty(name)
            ? ApplicationDataOutboundHttpClient.Name
            : name;
        return innerFactory.CreateClient(resolvedName);
    }
}
