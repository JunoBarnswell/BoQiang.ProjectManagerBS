using System.Net;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AuthenticationLoginRateLimitTests
{
    [Fact]
    public void Platform_and_application_login_actions_use_the_shared_rate_limit_policy()
    {
        AssertLoginActionPolicy(typeof(AuthController));
        AssertLoginActionPolicy(typeof(ApplicationAuthController));
    }

    [Fact]
    public void Login_rate_limit_settings_have_safe_defaults_and_bounded_overrides()
    {
        var defaults = AuthenticationRateLimitPolicy.ResolveSettings(
            new ConfigurationBuilder().Build());
        Assert.Equal(10, defaults.PermitLimit);
        Assert.Equal(60, defaults.WindowSeconds);

        var bounded = AuthenticationRateLimitPolicy.ResolveSettings(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:LoginRateLimitPermitCount"] = "1000",
                    ["Auth:LoginRateLimitWindowSeconds"] = "1"
                })
                .Build());
        Assert.Equal(100, bounded.PermitLimit);
        Assert.Equal(10, bounded.WindowSeconds);
    }

    [Fact]
    public void Login_rate_limit_partition_normalizes_ipv4_mapped_ipv6_addresses()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.20");

        Assert.Equal("203.0.113.20", AuthenticationRateLimitPolicy.ResolvePartitionKey(context));
    }

    [Fact]
    public void Rate_limiter_middleware_runs_after_routing_and_before_controller_mapping()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "backend", "AsterERP.Api", "Program.cs"));
        var routingIndex = source.IndexOf("app.UseRouting();", StringComparison.Ordinal);
        var limiterIndex = source.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal);
        var controllerIndex = source.IndexOf("app.MapControllers();", StringComparison.Ordinal);

        Assert.True(routingIndex >= 0);
        Assert.True(limiterIndex > routingIndex);
        Assert.True(controllerIndex > limiterIndex);
    }

    private static void AssertLoginActionPolicy(Type controllerType)
    {
        var method = controllerType.GetMethod("LoginAsync");
        Assert.NotNull(method);
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true));
        Assert.Equal(
            AuthenticationRateLimitPolicy.Name,
            Assert.IsType<EnableRateLimitingAttribute>(attribute).PolicyName);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("AsterERP repository root was not found.");
    }
}
