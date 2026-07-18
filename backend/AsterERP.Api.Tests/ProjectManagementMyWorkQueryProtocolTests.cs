using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementMyWorkQueryProtocolTests
{
    [Fact]
    public void Normalize_canonicalizes_category_sort_and_paging()
    {
        var result = ProjectManagementMyWorkQueryProtocol.Normalize(new ProjectManagementMyWorkQuery(
            PageIndex: 0,
            PageSize: 999,
            ProjectId: " project-a ",
            Category: "OVERDUE",
            SortBy: "UPDATED",
            SortDirection: "DESC"));

        Assert.Equal(1, result.PageIndex);
        Assert.Equal(200, result.PageSize);
        Assert.Equal("project-a", result.ProjectId);
        Assert.Equal("overdue", result.Category);
        Assert.Equal("updated", result.SortBy);
        Assert.Equal("desc", result.SortDirection);
    }

    [Theory]
    [InlineData("unknown", "dueDate", "asc")]
    [InlineData("today", "unknown", "asc")]
    [InlineData("today", "dueDate", "sideways")]
    public void Normalize_rejects_unsupported_query_values(string category, string sortBy, string direction)
    {
        Assert.Throws<ValidationException>(() => ProjectManagementMyWorkQueryProtocol.Normalize(new ProjectManagementMyWorkQuery(
            Category: category,
            SortBy: sortBy,
            SortDirection: direction)));
    }
}
