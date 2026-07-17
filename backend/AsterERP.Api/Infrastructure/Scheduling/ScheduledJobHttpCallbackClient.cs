using System.Net.Http.Headers;
using System.Text;
using AsterERP.Contracts.System.ScheduledJobs;
using AsterERP.Api.Domain.System.ScheduledJobs;
using Microsoft.Extensions.Options;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ScheduledJobHttpCallbackClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SchedulerOptions> options,
    HttpCallbackDomainPolicy callbackPolicy) : IScheduledJobHttpCallbackClient
{
    public async Task<string> SendAsync(HttpCallbackConfigDto callback, CancellationToken cancellationToken = default)
    {
        var callbackUri = new Uri(callback.Url.Trim(), UriKind.Absolute);
        await callbackPolicy.EnsureResolvedAddressesAllowedAsync(callbackUri, cancellationToken);

        using var request = new HttpRequestMessage(new HttpMethod(callback.Method.Trim().ToUpperInvariant()), callbackUri);
        if (callback.Headers is not null)
        {
            foreach (var header in callback.Headers)
            {
                if (!string.IsNullOrWhiteSpace(header.Key) && !string.IsNullOrWhiteSpace(header.Value))
                {
                    request.Headers.TryAddWithoutValidation(header.Key.Trim(), header.Value.Trim());
                }
            }
        }

        if (request.Method == HttpMethod.Post)
        {
            request.Content = new StringContent(callback.BodyJson ?? "{}", Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
        }

        var client = httpClientFactory.CreateClient("scheduled-job-callback");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.Value.HttpCallbackTimeoutSeconds, 1, 60));

        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP 回调失败：{(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return string.IsNullOrWhiteSpace(content)
            ? $"HTTP {(int)response.StatusCode}"
            : content.Length > 500 ? content[..500] : content;
    }
}
