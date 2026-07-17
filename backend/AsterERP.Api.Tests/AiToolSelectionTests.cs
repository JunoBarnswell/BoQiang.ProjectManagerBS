using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AiKernelFunctionSelectionTests
{
    [Fact]
    public void Selection_Allows_Multiple_Domains_And_Explicit_Tool_Codes()
    {
        var selection = AiKernelFunctionSelection.From(
            ["inventory.item.search"],
            ["workflow", "finance"]);

        Assert.True(selection.Allows(Definition("workflow.model.search", "workflow")));
        Assert.True(selection.Allows(Definition("finance.report.read", "finance")));
        Assert.True(selection.Allows(Definition("inventory.item.search", "inventory")));
        Assert.False(selection.Allows(Definition("crm.customer.search", "crm")));
    }

    [Fact]
    public void Catalog_Rejects_Unselected_Tool_As_Not_Registered_For_Current_Run()
    {
        var catalog = new AiKernelFunctionCatalog(
        [
            new FakeTool(Definition("workflow.model.search", "workflow")),
            new FakeTool(Definition("finance.report.read", "finance"))
        ]);
        var selection = AiKernelFunctionSelection.From(null, ["workflow"]);

        var registered = catalog.Require("workflow.model.search", selection);
        var exception = Assert.Throws<ValidationException>(() => catalog.Require("finance.report.read", selection));

        Assert.Equal("workflow.model.search", registered.Definition.ToolCode);
        Assert.Contains("未在本次运行中注册", exception.Message);
    }

    private static AiKernelFunctionDefinition Definition(string toolCode, string toolDomain) => new()
    {
        ToolCode = toolCode,
        ToolDomain = toolDomain,
        ToolName = toolCode,
        PermissionCode = "ai:tool:test"
    };

    private sealed class FakeTool(AiKernelFunctionDefinition definition) : IAiKernelFunction
    {
        public AiKernelFunctionDefinition Definition { get; } = definition;

        public Task<AiKernelFunctionResult> ExecuteAsync(
            AiKernelFunctionContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AiKernelFunctionResult
            {
                Content = Definition.ToolCode,
                ResultSummary = "ok"
            });
    }
}
