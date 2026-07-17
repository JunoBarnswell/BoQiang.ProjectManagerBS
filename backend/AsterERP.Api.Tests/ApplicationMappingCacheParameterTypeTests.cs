using AsterERP.Contracts.ApplicationDataCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationMappingCacheParameterTypeTests
{
    [Theory]
    [InlineData("INTEGER", ApplicationMappingCacheParameterType.Number)]
    [InlineData("decimal(18,2)", ApplicationMappingCacheParameterType.Number)]
    [InlineData("boolean", ApplicationMappingCacheParameterType.Boolean)]
    [InlineData("timestamp with time zone", ApplicationMappingCacheParameterType.Date)]
    [InlineData("jsonb", ApplicationMappingCacheParameterType.Json)]
    public void FromColumn_ReturnsCanonicalType(string columnType, string expected) =>
        Assert.Equal(expected, ApplicationMappingCacheParameterType.FromColumn(columnType));

    [Fact]
    public void Normalize_RejectsUnknownType()
    {
        var exception = Assert.Throws<ArgumentException>(() => ApplicationMappingCacheParameterType.Normalize("money-like"));
        Assert.Contains("Unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsCompatible_RejectsParameterBoundToDifferentColumnType() =>
        Assert.False(ApplicationMappingCacheParameterType.IsCompatible("boolean", ApplicationMappingCacheParameterType.Number));
}

