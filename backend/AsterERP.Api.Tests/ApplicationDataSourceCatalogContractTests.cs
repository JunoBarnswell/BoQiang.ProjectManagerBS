using System.Reflection;
using AsterERP.Api.Controllers;
using AsterERP.Contracts.ApplicationDataCenter;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceCatalogContractTests
{
    [Fact]
    public void Snapshot_contract_exposes_version_lineage_and_changes()
    {
        var snapshot = new ApplicationDataSourceCatalogSnapshotResponse("id", "source", "Sqlite", "hash", DateTime.UtcNow, []);

        Assert.Equal(0, snapshot.VersionNo);
        Assert.Empty(snapshot.Changes);
        Assert.Null(snapshot.PreviousSnapshotId);
        Assert.Null(snapshot.PreviousSnapshotHash);
    }

    [Fact]
    public void Catalog_controller_exposes_single_node_refresh_with_view_permission()
    {
        var method = typeof(ApplicationDataCenterDataSourcesController).GetMethod(nameof(ApplicationDataCenterDataSourcesController.RefreshCatalogNodeAsync));

        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes<HttpPostAttribute>(), item => item.Template == "{id}/catalog/refresh-node");
        Assert.Contains(method.GetCustomAttributes(), attribute => attribute.GetType().Name == "PermissionAttribute");
    }

    [Fact]
    public void Refresh_request_requires_a_table_identity()
    {
        var request = new ApplicationDataSourceCatalogRefreshRequest("public", "orders");

        Assert.Equal("public", request.SchemaName);
        Assert.Equal("orders", request.TableName);
    }
}
