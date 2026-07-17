using AsterERP.Api.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProductionSecurityConfigurationValidatorTests
{
    [Fact]
    public void Production_rejects_wildcard_hosts_and_non_https_origin()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "*",
            ["Cors:FrontendOrigin"] = "http://localhost:5173",
            ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "astererp-keys"),
            ["SqlSugar:LogSql"] = "false"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionSecurityConfigurationValidator.Validate(configuration, CreateProductionEnvironment()));

        Assert.Contains("AllowedHosts", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_accepts_explicit_https_and_absolute_key_path()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "erp.example.com",
            ["Cors:FrontendOrigin"] = "https://erp.example.com",
            ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), "astererp-keys"),
            ["SqlSugar:LogSql"] = "false"
        });

        ProductionSecurityConfigurationValidator.Validate(configuration, CreateProductionEnvironment());
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment CreateProductionEnvironment() => new ProductionHostEnvironment();

    private sealed class ProductionHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "AsterERP.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
