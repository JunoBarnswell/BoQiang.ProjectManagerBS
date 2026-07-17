using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceConnectionCapabilityContractTests
{
    [Fact]
    public void Connection_capability_matches_shared_json_contract()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, "docs", "low-code-refactor", "application-data-source-connection-capabilities.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        foreach (var provider in document.RootElement.GetProperty("providers").EnumerateArray())
        {
            var name = provider.GetProperty("provider").GetString()!;
            var capability = ApplicationDataSourceConnectionCapability.ForProvider(name);

            Assert.Equal(provider.GetProperty("supportedSslModes").EnumerateArray().Select(item => item.GetString()).ToArray(), capability.SupportedSslModes.Select(item => item.ToString()).ToArray());
            Assert.Equal(provider.GetProperty("defaultSslMode").ValueKind == JsonValueKind.Null ? null : provider.GetProperty("defaultSslMode").GetString(), capability.DefaultSslMode?.ToString());
            Assert.Equal(provider.GetProperty("supportsConnectionTimeout").GetBoolean(), capability.SupportsConnectionTimeout);
            Assert.Equal(provider.GetProperty("supportsPoolSize").GetBoolean(), capability.SupportsPoolSize);
            Assert.Equal(provider.GetProperty("supportsCharset").GetBoolean(), capability.SupportsCharset);
            Assert.Equal(provider.GetProperty("defaultCharset").ValueKind == JsonValueKind.Null ? null : provider.GetProperty("defaultCharset").GetString(), capability.DefaultCharset);
        }
    }

    [Fact]
    public void Provider_capability_exposes_the_connection_contract_without_provider_file_changes()
    {
        var provider = new PostgreSqlApplicationDataSourceProvider();

        Assert.Equal("Preferred", provider.Capability.Connection.DefaultSslMode?.ToString());
        Assert.Contains(ApplicationDataSourceSslMode.Preferred, provider.Capability.Connection.SupportedSslModes);
        Assert.Equal("UTF8", provider.Capability.Connection.DefaultCharset);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
            directory = directory.Parent;
        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }
}
