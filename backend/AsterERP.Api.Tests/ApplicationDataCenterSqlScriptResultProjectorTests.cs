using AsterERP.Api.Application.ApplicationDataCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataCenterSqlScriptResultProjectorTests
{
    [Fact]
    public void BuildPreview_ProjectsJsonObjectAndArrayObjectFields()
    {
        var projector = new ApplicationDataCenterSqlScriptResultProjector();
        var rows = new[]
        {
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = "order-1",
                ["customer"] = "{\"id\":\"customer-1\",\"name\":\"客户一\"}",
                ["customerList"] = "[{\"id\":\"customer-1\",\"name\":\"客户一\"},{\"id\":\"customer-2\",\"name\":\"客户二\"}]"
            } as IReadOnlyDictionary<string, object?>
        };

        var preview = projector.BuildPreview(rows, 1, 20, "ok");

        var customer = Assert.IsType<Dictionary<string, object?>>(preview.Rows.Single()["customer"]);
        Assert.Equal("客户一", customer["name"]);
        var customerList = Assert.IsType<object[]>(preview.Rows.Single()["customerList"]);
        Assert.Equal(2, customerList.Length);
        Assert.Equal("object", preview.Fields.Single(field => field.FieldCode == "customer").ValueKind);
        Assert.Equal("arrayObject", preview.Fields.Single(field => field.FieldCode == "customerList").ValueKind);
        Assert.Contains(preview.Fields.Single(field => field.FieldCode == "customerList").Children!, field => field.FieldCode == "name");

        var customerListDataset = Assert.Single(preview.Datasets!, item => item.Key == "main.customerList");
        Assert.Equal(2, customerListDataset.TotalRows);
        Assert.Contains(customerListDataset.Fields, field => field.FieldCode == "name");
        Assert.All(customerListDataset.Rows, row => Assert.Equal(1, row["__parentRowIndex"]));
    }

    [Fact]
    public void BuildPreview_KeepsInvalidJsonAsScalar()
    {
        var projector = new ApplicationDataCenterSqlScriptResultProjector();
        var rows = new[]
        {
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = "order-1",
                ["customerList"] = "[{bad json}]"
            } as IReadOnlyDictionary<string, object?>
        };

        var preview = projector.BuildPreview(rows, 1, 20, "ok");

        Assert.Equal("[{bad json}]", preview.Rows.Single()["customerList"]);
        Assert.Equal("scalar", preview.Fields.Single(field => field.FieldCode == "customerList").ValueKind);
        Assert.Single(preview.Datasets!);
    }
}
