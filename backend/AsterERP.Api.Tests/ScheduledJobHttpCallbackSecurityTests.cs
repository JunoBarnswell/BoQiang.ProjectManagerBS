using System.Net;
using AsterERP.Api.Domain.System.ScheduledJobs;
using AsterERP.Contracts.System.ScheduledJobs;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ScheduledJobHttpCallbackSecurityTests
{
    [Theory]
    [InlineData("http://localhost:8080/admin")]
    [InlineData("http://service.localhost/admin")]
    [InlineData("http://127.0.0.1:8080/admin")]
    [InlineData("http://10.0.0.10/admin")]
    [InlineData("http://172.16.0.10/admin")]
    [InlineData("http://192.168.1.10/admin")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]:8080/admin")]
    [InlineData("http://[fc00::1]/admin")]
    [InlineData("http://[fe80::1]/admin")]
    public void Callback_policy_rejects_local_and_private_destinations(string url)
    {
        var policy = new HttpCallbackDomainPolicy();
        var options = new SchedulerOptions { AllowedHosts = [new Uri(url).DnsSafeHost] };
        var callback = new HttpCallbackConfigDto(url, "GET", null, null);

        var exception = Assert.Throws<ValidationException>(() => policy.EnsureAllowed(callback, options));

        Assert.Equal(ErrorCodes.ScheduledJobHttpCallbackHostDenied, exception.Code);
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    public void Callback_address_policy_classifies_public_destinations(string value, bool expected)
    {
        Assert.Equal(expected, HttpCallbackDomainPolicy.IsPublicAddress(IPAddress.Parse(value)));
    }

    [Fact]
    public void Callback_policy_is_deny_by_default()
    {
        var policy = new HttpCallbackDomainPolicy();
        var callback = new HttpCallbackConfigDto("https://hooks.example.com/job", "POST", "{}", null);

        var exception = Assert.Throws<ValidationException>(() => policy.EnsureAllowed(callback, new SchedulerOptions()));

        Assert.Equal(ErrorCodes.ScheduledJobHttpCallbackHostDenied, exception.Code);
    }

    [Fact]
    public void Callback_policy_allows_explicit_public_host()
    {
        var policy = new HttpCallbackDomainPolicy();
        var options = new SchedulerOptions { AllowedHosts = ["hooks.example.com"] };
        var callback = new HttpCallbackConfigDto("https://hooks.example.com/job", "POST", "{}", null);

        policy.EnsureAllowed(callback, options);
    }
}
