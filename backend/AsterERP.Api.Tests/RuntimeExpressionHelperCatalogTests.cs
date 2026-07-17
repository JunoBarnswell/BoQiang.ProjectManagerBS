using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.Runtime;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class RuntimeExpressionHelperCatalogTests
{
    [Fact]
    public void Apply_ExecutesStringFormattingChain()
    {
        var result = ApplyAll(
            "  User Name  ",
            Helper("trim"),
            Helper("snakeCase"),
            Helper("prefix", ("prefix", "field_")));

        Assert.Equal("field_user_name", result);
    }

    [Fact]
    public void Apply_ExecutesCollectionProjectionAndAggregation()
    {
        var rows = new[]
        {
            new Dictionary<string, object?> { ["status"] = "active", ["amount"] = 10 },
            new Dictionary<string, object?> { ["status"] = "disabled", ["amount"] = 20 },
            new Dictionary<string, object?> { ["status"] = "active", ["amount"] = 30 }
        };

        var result = ApplyAll(
            rows,
            Helper("filterEquals", ("field", "status"), ("value", "active")),
            Helper("mapField", ("field", "amount")),
            Helper("sum"));

        Assert.Equal(40m, result);
    }

    [Fact]
    public void Apply_ExecutesDateAndDisplayHelpers()
    {
        var result = ApplyAll(
            "2026-06-30",
            Helper("addDays", ("days", 1)),
            Helper("formatDate", ("format", "yyyy-MM-dd")));

        Assert.Equal("2026-07-01", result);
    }

    [Fact]
    public void Apply_ExecutesEncodingMaskingAndMappingHelpers()
    {
        var encoded = ApplyAll("订单 A", Helper("base64Encode"));
        var decoded = ApplyAll(encoded, Helper("base64Decode"));
        var mapped = ApplyAll("1", Helper("mapValue", ("mapping", "0=禁用;1=启用"), ("defaultValue", "未知")));
        var masked = ApplyAll("13800138000", Helper("maskPhone"));

        Assert.Equal("订单 A", decoded);
        Assert.Equal("启用", mapped);
        Assert.Equal("138****8000", masked);
    }

    [Fact]
    public void Apply_ExecutesExtendedStringCleanupHelpers()
    {
        var text = ApplyAll(
            "订单: SO-001 / 状态: 已发布",
            Helper("textBetween", ("start", "订单: "), ("end", " /")),
            Helper("ensurePrefix", ("prefix", "ERP-")),
            Helper("removeSuffix", ("suffix", "-draft")));
        var chinese = ApplyAll("A客户B", Helper("onlyChinese"));

        Assert.Equal("ERP-SO-001", text);
        Assert.Equal("客户", chinese);
    }

    [Fact]
    public void Apply_ExecutesExtendedDateHelpers()
    {
        var added = ApplyAll("2026-06-30", Helper("addWeeks", ("weeks", 1)), Helper("formatDate", ("format", "yyyy-MM-dd")));
        var quarter = ApplyAll("2026-06-30", Helper("formatQuarter"));
        var minutes = ApplyAll("2026-06-30 10:30:00", Helper("diffMinutes", ("value", "2026-06-30 10:00:00")));

        Assert.Equal("2026-07-07", added);
        Assert.Equal("2026Q2", quarter);
        Assert.Equal(30, minutes);
    }

    [Fact]
    public void Apply_ExecutesExtendedCollectionAndObjectHelpers()
    {
        var rows = new[]
        {
            new Dictionary<string, object?> { ["id"] = "1", ["status"] = "active", ["amount"] = 10 },
            new Dictionary<string, object?> { ["id"] = "2", ["status"] = "disabled", ["amount"] = 20 },
            new Dictionary<string, object?> { ["id"] = "3", ["status"] = "active", ["amount"] = 30 }
        };

        var paged = ApplyAll(rows, Helper("rejectEquals", ("field", "status"), ("value", "disabled")), Helper("page", ("pageIndex", 1), ("pageSize", 1)));
        var selected = ApplyAll(new Dictionary<string, object?> { ["id"] = "1", ["name"] = "订单", ["token"] = "secret" }, Helper("omitFields", ("fields", "token")));

        var pageRow = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(paged));
        Assert.Equal("1", Assert.IsAssignableFrom<IDictionary<string, object?>>(pageRow)["id"]);
        Assert.False(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(selected).ContainsKey("token"));
    }

    [Fact]
    public void Apply_ExecutesExtendedValidationAndDisplayHelpers()
    {
        var guidOk = ApplyAll("b5f79358-4e63-47c2-89d3-9697a3f2386d", Helper("isGuid"));
        var urlOk = ApplyAll("https://example.com", Helper("isUrl"));
        var enabled = ApplyAll("1", Helper("enabledDisabled"));
        var currency = ApplyAll("¥1,234.50", Helper("parseCurrency"));

        Assert.True(Assert.IsType<bool>(guidOk));
        Assert.True(Assert.IsType<bool>(urlOk));
        Assert.Equal("启用", enabled);
        Assert.Equal(1234.50m, currency);
    }

    private static object? ApplyAll(object? value, params RuntimeExpressionHelperDto[] helpers)
    {
        var catalog = new RuntimeExpressionHelperCatalog();
        return helpers.Aggregate(value, (current, helper) => catalog.Apply(current, helper));
    }

    private static RuntimeExpressionHelperDto Helper(string name, params (string Key, object? Value)[] args) =>
        new()
        {
            Name = name,
            Args = args.ToDictionary(item => item.Key, item => item.Value)
        };
}
