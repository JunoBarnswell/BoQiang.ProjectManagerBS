using AsterERP.Api.Application.ApplicationDataCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationIntegrationTaskExecutionTests
{
    [Theory]
    [InlineData("Sqlite", "SQLite Error 19: UNIQUE constraint failed: orders.id", true)]
    [InlineData("MySql", "Error 1062: Duplicate entry '1' for key 'PRIMARY'", true)]
    [InlineData("SqlServer", "Violation of PRIMARY KEY constraint. The duplicate key value is (1). 2627", true)]
    [InlineData("PostgreSql", "23505: duplicate key value violates unique constraint", true)]
    [InlineData("Sqlite", "permission denied for table orders", false)]
    [InlineData("Sqlite", "foreign key constraint failed", false)]
    [InlineData("SqlServer", "Conversion failed when converting varchar value", false)]
    public void ClassifiesOnlyUniqueConstraintViolations(string provider, string message, bool expected)
    {
        Assert.Equal(expected, ApplicationIntegrationDuplicateViolationClassifier.IsUniqueConstraintViolation(provider, new InvalidOperationException(message)));
    }

    [Fact]
    public void ClassifiesUniqueViolationThroughInnerException()
    {
        var exception = new InvalidOperationException("provider wrapper", new InvalidOperationException("duplicate key value violates unique constraint"));

        Assert.True(ApplicationIntegrationDuplicateViolationClassifier.IsUniqueConstraintViolation("PostgreSql", exception));
    }
}
