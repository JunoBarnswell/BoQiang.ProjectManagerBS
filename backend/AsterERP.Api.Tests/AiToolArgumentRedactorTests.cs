using AsterERP.Api.Application.Ai.Tools;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AiKernelFunctionArgumentRedactorTests
{
    [Fact]
    public void RedactJson_Masks_Default_And_Definition_Sensitive_Arguments()
    {
        var definition = new AiKernelFunctionDefinition
        {
            ToolCode = "system.user.resetPassword",
            ToolName = "重置用户密码",
            ToolDomain = "system-admin",
            PermissionCode = "ai:tool:system-admin:operate",
            SensitiveArgumentNames = ["customSecret"]
        };
        var redactor = new AiKernelFunctionArgumentRedactor();

        var redacted = redactor.RedactJson(definition, """
        {
          "userId": "u1",
          "password": "P@ssw0rd",
          "headers": {
            "Authorization": "Bearer abc",
            "Trace": "visible"
          },
          "customSecret": "hidden"
        }
        """);

        Assert.Contains("***REDACTED***", redacted);
        Assert.Contains("\"userId\":\"u1\"", redacted);
        Assert.DoesNotContain("P@ssw0rd", redacted);
        Assert.DoesNotContain("Bearer abc", redacted);
        Assert.DoesNotContain("hidden", redacted);
    }

    [Fact]
    public void ToDto_Exposes_RequiredPermissions_And_SensitiveArguments()
    {
        var dto = new AiKernelFunctionDefinition
        {
            ToolCode = "system.role.grantMenus",
            ToolName = "角色授权菜单",
            ToolDomain = "system-admin",
            PermissionCode = "ai:tool:system-admin:grant",
            RequiredPermissionCodes = ["ai:tool:system-admin:grant", "system:role:grant"],
            SensitiveArgumentNames = ["authorization"]
        }.ToDto();

        Assert.Contains("ai:tool:system-admin:grant", dto.RequiredPermissionCodes);
        Assert.Contains("system:role:grant", dto.RequiredPermissionCodes);
        Assert.Contains("authorization", dto.SensitiveArgumentNames);
    }
}
