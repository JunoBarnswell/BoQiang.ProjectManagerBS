using System.Net;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataOutboundHttpClientSecurityTests
{
    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("http://service.localhost/admin")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://10.0.0.1/admin")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://192.0.2.1/test")]
    [InlineData("http://[::1]/admin")]
    [InlineData("http://[fc00::1]/admin")]
    [InlineData("http://[fe80::1]/admin")]
    [InlineData("http://[2001:db8::1]/test")]
    public void Uri_policy_rejects_private_local_and_reserved_targets(string value)
    {
        Assert.Throws<ValidationException>(() =>
            ApplicationDataOutboundHttpClient.EnsureAllowedUri(new Uri(value)));
    }

    [Theory]
    [InlineData("https://example.com/api")]
    [InlineData("https://8.8.8.8/dns-query")]
    [InlineData("https://1.1.1.1/")]
    public void Uri_policy_allows_public_http_targets(string value)
    {
        ApplicationDataOutboundHttpClient.EnsureAllowedUri(new Uri(value));
    }

    [Fact]
    public void Uri_policy_rejects_embedded_user_information()
    {
        Assert.Throws<ValidationException>(() =>
            ApplicationDataOutboundHttpClient.EnsureAllowedUri(
                new Uri("https://user:password@example.com/api")));
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("2001:4860:4860::8888", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("203.0.113.1", false)]
    [InlineData("::1", false)]
    [InlineData("64:ff9b::7f00:1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    public void Address_policy_classifies_global_targets(string value, bool expected)
    {
        Assert.Equal(expected, ApplicationDataOutboundHttpClient.IsPublicAddress(IPAddress.Parse(value)));
    }

    [Fact]
    public void Resolved_address_policy_rejects_mixed_public_and_private_answers()
    {
        Assert.Throws<ValidationException>(() =>
            ApplicationDataOutboundHttpClient.EnsureResolvedAddressesArePublic(
                [IPAddress.Parse("8.8.8.8"), IPAddress.Parse("127.0.0.1")]));
    }

    [Fact]
    public async Task Message_handler_rejects_host_override_before_connecting()
    {
        using var invoker = new HttpMessageInvoker(ApplicationDataOutboundHttpClient.CreatePrimaryHandler());
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://8.8.8.8/");
        request.Headers.Host = "localhost";

        await Assert.ThrowsAsync<ValidationException>(() =>
            invoker.SendAsync(request, CancellationToken.None));
    }

    [Fact]
    public void Primary_handler_disables_redirects_proxies_and_shared_cookies()
    {
        using var handler = ApplicationDataOutboundHttpClient.CreateSocketsHandler();

        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseProxy);
        Assert.False(handler.UseCookies);
        Assert.NotNull(handler.ConnectCallback);
    }

    [Fact]
    public void Scoped_factory_routes_only_default_client_to_hardened_client()
    {
        var innerFactory = new RecordingHttpClientFactory();
        var scopedFactory = new ApplicationDataScopedHttpClientFactory(innerFactory);

        using var defaultClient = scopedFactory.CreateClient(string.Empty);
        using var explicitClient = scopedFactory.CreateClient("explicit-client");

        Assert.Collection(
            innerFactory.RequestedNames,
            name => Assert.Equal(ApplicationDataOutboundHttpClient.Name, name),
            name => Assert.Equal("explicit-client", name));
    }

    [Fact]
    public void Module_injects_hardened_factory_only_into_remaining_http_outbound_services()
    {
        var services = new ServiceCollection();

        new AsterErpApplicationDataCenterModule().ConfigureServices(
            new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ApplicationDataScopedHttpClientFactory));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IApplicationMicroflowRuntimeService) &&
            descriptor.ImplementationFactory is not null);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(ApplicationDataSourceService) &&
            descriptor.ImplementationFactory is not null);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ApplicationDataApiRuntimeService) &&
            descriptor.ImplementationFactory is not null);
    }

    private sealed class RecordingHttpClientFactory : IHttpClientFactory
    {
        public List<string> RequestedNames { get; } = [];

        public HttpClient CreateClient(string name)
        {
            RequestedNames.Add(name);
            return new HttpClient(new StubHandler());
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
