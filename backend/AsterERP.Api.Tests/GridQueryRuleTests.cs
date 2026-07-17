using AsterERP.Api.Application.Common;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class GridQueryRuleTests
{
    [Fact]
    public void Grid_filter_rejects_field_outside_allow_list()
    {
        var filters = new[]
        {
            new GridFilter { Field = "roleNames", Operator = "contains", Value = "admin" }
        };

        var exception = Assert.Throws<ValidationException>(() =>
            GridFilterApplier.Apply("query", filters, new Dictionary<string, Func<string, GridFilter, string>>(StringComparer.OrdinalIgnoreCase)));

        Assert.Equal("筛选字段不允许: roleNames", exception.Message);
    }

    [Fact]
    public void Grid_sort_rejects_field_outside_allow_list()
    {
        var sorts = new[]
        {
            new GridSort { Field = "friendlySchedule", Order = "asc" }
        };

        var exception = Assert.Throws<ValidationException>(() =>
            GridSortApplier.Apply(
                "query",
                sorts,
                new Dictionary<string, Func<string, OrderByType, string>>(StringComparer.OrdinalIgnoreCase),
                query => query));

        Assert.Equal("排序字段不允许: friendlySchedule", exception.Message);
    }

    [Fact]
    public void Grid_sort_applies_duplicate_field_once()
    {
        var appliedFields = new List<string>();
        var sorts = new[]
        {
            new GridSort { Field = "displayName", Order = "asc" },
            new GridSort { Field = "DISPLAYNAME", Order = "desc" }
        };

        GridSortApplier.Apply(
            "query",
            sorts,
            new Dictionary<string, Func<string, OrderByType, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["displayName"] = (query, order) =>
                {
                    appliedFields.Add(order == OrderByType.Asc ? "displayName:asc" : "displayName:desc");
                    return query;
                }
            },
            query => query);

        Assert.Equal(["displayName:asc"], appliedFields);
    }
}
