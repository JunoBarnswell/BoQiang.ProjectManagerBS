using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskQueryProtocolTests
{
    [Fact]
    public void Normalizes_shared_view_query_to_canonical_values_and_bounds()
    {
        var query = ProjectManagementTaskQueryProtocol.Normalize(new ProjectManagementTaskQuery(
            " project-a ", PageIndex: 0, PageSize: 999, Keyword: "  release  ", ViewKey: "BOARD",
            GroupBy: "ASSIGNEE", SortBy: "UPDATED", SortDirection: "DESC", Status: " Todo "));

        Assert.Equal("project-a", query.ProjectId);
        Assert.Equal("board", query.ViewKey);
        Assert.Equal("assignee", query.GroupBy);
        Assert.Equal("updated", query.SortBy);
        Assert.Equal("desc", query.SortDirection);
        Assert.Equal("release", query.Keyword);
        Assert.Equal("Todo", query.Status);
        Assert.Equal(1, query.PageIndex);
        Assert.Equal(200, query.PageSize);
    }

    [Theory]
    [InlineData("unknown", "tree", "asc", null)]
    [InlineData("tree", "unknown", "asc", null)]
    [InlineData("tree", "tree", "sideways", null)]
    [InlineData("tree", "tree", "asc", "unknown")]
    public void Rejects_values_outside_the_shared_whitelist(string viewKey, string sortBy, string sortDirection, string? groupBy)
    {
        Assert.Throws<ValidationException>(() => ProjectManagementTaskQueryProtocol.Normalize(new ProjectManagementTaskQuery(
            "project-a", ViewKey: viewKey, SortBy: sortBy, SortDirection: sortDirection, GroupBy: groupBy)));
    }

    [Fact]
    public void Rejects_invalid_project_status_and_date_range()
    {
        Assert.Throws<ValidationException>(() => ProjectManagementTaskQueryProtocol.Normalize(new ProjectManagementTaskQuery("project-a", Status: "NotAStatus")));
        Assert.Throws<ValidationException>(() => ProjectManagementTaskQueryProtocol.Normalize(new ProjectManagementTaskQuery(
            "project-a", DueFrom: new DateTime(2026, 7, 20), DueTo: new DateTime(2026, 7, 19))));
    }
}
