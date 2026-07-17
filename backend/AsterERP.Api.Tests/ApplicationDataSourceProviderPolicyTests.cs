using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Controllers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceProviderPolicyTests
{
    [Theory]
    [InlineData(ApplicationDataSourceType.Sqlite)]
    [InlineData(ApplicationDataSourceType.MySql)]
    [InlineData(ApplicationDataSourceType.PostgreSql)]
    [InlineData(ApplicationDataSourceType.SqlServer)]
    [InlineData(ApplicationDataSourceType.ApplicationDatabase)]
    [InlineData(ApplicationDataSourceType.Excel)]
    [InlineData(ApplicationDataSourceType.Csv)]
    public void Supported_provider_is_allowed(string provider) =>
        ApplicationDataSourceProviderPolicy.EnsureSupportedForWrite(provider);

    [Theory]
    [InlineData("REST")]
    [InlineData("MinIO")]
    [InlineData("S3")]
    [InlineData("OSS")]
    [InlineData("Kafka")]
    [InlineData("RabbitMQ")]
    public void Retired_provider_is_fail_closed_and_has_migration_diagnostic(string provider)
    {
        var exception = Assert.Throws<ValidationException>(() => ApplicationDataSourceProviderPolicy.EnsureSupportedForWrite(provider));

        Assert.True(ApplicationDataSourceProviderPolicy.IsRetired(provider));
        Assert.Contains("迁移", exception.Message, StringComparison.Ordinal);
        Assert.Contains(provider, ApplicationDataSourceProviderPolicy.GetMigrationDiagnostic(provider), StringComparison.Ordinal);
    }

    [Fact]
    public void Data_source_catalog_exposes_only_supported_provider_types()
    {
        var types = new ApplicationDataCenterTemplateCatalog()
            .GetTypeOptions(ApplicationDataCenterModuleKey.DataSource)
            .Select(option => option.Type)
            .ToArray();

        Assert.All(types, ApplicationDataSourceProviderPolicy.EnsureSupportedForWrite);
        Assert.DoesNotContain(types, ApplicationDataSourceProviderPolicy.IsRetired);
    }

    [Fact]
    public void Migration_inventory_endpoint_is_view_protected()
    {
        var method = typeof(ApplicationDataCenterDataSourcesController)
            .GetMethod(nameof(ApplicationDataCenterDataSourcesController.GetMigrationInventoryAsync));

        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false).Cast<HttpGetAttribute>(), attribute => attribute.Template == "migration-required");
        Assert.Contains(method.GetCustomAttributes(inherit: false), attribute => attribute.GetType().Name == "PermissionAttribute");
    }
}
