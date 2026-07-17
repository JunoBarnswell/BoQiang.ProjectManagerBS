using AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AiDataCenterArgumentReaderTests
{
    [Fact]
    public void ReadNullableInt_Reads_Json_Number_And_Rejects_Invalid_Text()
    {
        var arguments = new Dictionary<string, object?>
        {
            ["expectedAffectedRows"] = 1
        };

        Assert.Equal(1, AiDataCenterArgumentReader.ReadNullableInt(arguments, "expectedAffectedRows"));

        arguments["expectedAffectedRows"] = "not-a-number";
        var exception = Assert.Throws<ValidationException>(() =>
            AiDataCenterArgumentReader.ReadNullableInt(arguments, "expectedAffectedRows"));

        Assert.Contains("expectedAffectedRows", exception.Message);
    }

    [Fact]
    public void ReadBool_Uses_Explicit_Confirmation_And_Defaults_When_Absent()
    {
        var arguments = new Dictionary<string, object?>
        {
            ["confirmed"] = "true"
        };

        Assert.True(AiDataCenterArgumentReader.ReadBool(arguments, "confirmed", false));
        Assert.False(AiDataCenterArgumentReader.ReadBool(new Dictionary<string, object?>(), "confirmed", false));
    }
}
