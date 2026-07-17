using System.Text.Json;
using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.Agent;
using AsterERP.Api.Application.Ai.Tools.Workflow;
using AsterERP.Api.Modules.Ai;
using AsterERP.Shared;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AiWorkflowDraftParserTests
{
    [Fact]
    public void Parse_PurchaseRequirement_BuildsComplexApprovalDraft()
    {
        var parser = new WorkflowDraftParser();

        var draft = parser.Parse(
            "创建复杂采购订单审批流：10000以下部门负责人审批，10000到50000需要预算校验、法务会签和财务审批，50000以上需要财务总监，100000以上还要总经理审批。",
            "purchase.order");

        Assert.Equal("复杂采购订单审批", draft.WorkflowName);
        Assert.True(draft.Nodes.Count >= 8);
        Assert.Contains(draft.Nodes, item => item.Id == "legal_review");
        Assert.Contains(draft.Nodes, item => item.Id == "general_manager");
        Assert.Contains(draft.Edges, item => item.Condition == "amount >= 100000");
    }

    [Fact]
    public void Simulate_HighPurchaseAmount_ReachesGeneralManager()
    {
        var draft = new WorkflowDraftParser().Parse(
            "采购审批：10000以上预算校验，50000以上财务总监，100000以上总经理。",
            "purchase.order");
        var steps = new WorkflowSimulationEngine(new WorkflowConditionEvaluator()).Simulate(draft, new Dictionary<string, object?>
        {
            ["amount"] = 120000,
            ["contractRequired"] = true
        });

        Assert.Contains(steps, item => item.NodeId == "legal_review");
        Assert.Contains(steps, item => item.NodeId == "general_manager");
        Assert.Equal("complete", steps[^1].Action);
    }

    [Fact]
    public void SimulateTool_AppliesVariablesFromAgentInstruction()
    {
        var variables = new Dictionary<string, object?>
        {
            ["amount"] = 100000,
            ["contractRequired"] = false
        };

        WorkflowModelSimulateDraftTool.ApplyVariablesFromInstruction(
            variables,
            "模拟变量使用 amount=120000、contractRequired=true，并确认路径命中总经理。");

        Assert.Equal(120000m, variables["amount"]);
        Assert.Equal(true, variables["contractRequired"]);
    }

    [Fact]
    public void BusinessCanvasMapper_PurchaseDraft_BuildsDesignerCompatibleCanvas()
    {
        var draft = new WorkflowDraftParser().Parse(
            "请创建复杂采购订单审批流：采购员提交，采购专员复核，部门负责人审批；金额低于 10000 元直接归档；金额 10000 元及以上进入预算校验；需要合同时进入法务会签，不需要合同时直接进入财务审批；金额 50000 元及以上需要财务总监审批；金额 100000 元及以上需要总经理审批。",
            "purchase.order");
        var mapper = new WorkflowBusinessCanvasDraftMapper();

        var json = mapper.Map(draft);

        using var document = JsonDocument.Parse(json);
        var businessDesign = document.RootElement.GetProperty("businessDesign");
        var nodes = businessDesign.GetProperty("nodes");
        var edges = businessDesign.GetProperty("edges");
        Assert.True(nodes.GetArrayLength() >= 9);
        Assert.True(edges.GetArrayLength() >= 12);
        Assert.Contains(nodes.EnumerateArray(), item => item.GetProperty("label").GetString() == "法务会签");
        Assert.Contains(nodes.EnumerateArray(), item => item.GetProperty("label").GetString() == "总经理审批");
        Assert.Contains(edges.EnumerateArray(), item => item.GetProperty("conditionExpression").GetString() == "amount >= 100000");
    }

    [Fact]
    public void AutoImportPolicy_AllowsExistingLinkedDraftAndBlocksInvalidValidation()
    {
        var draft = new AiWorkflowDraftArtifactEntity
        {
            BpmnXml = "<definitions />",
            BusinessCanvasJson = "{\"businessDesign\":{\"nodes\":[{\"id\":\"start\",\"type\":\"start\"}],\"edges\":[]}}",
            ImportedWorkflowModelId = "model-existing"
        };
        var validReport = new AiWorkflowValidationReportEntity { IsValid = true, ErrorCount = 0 };
        var items = new List<AiTaskPlanItemEntity>
        {
            new()
            {
                OwnerType = AiTaskPlanConstants.OwnerType.Tool,
                ToolCode = "workflow.model.validateDraft",
                Status = AiTaskPlanConstants.ItemStatus.Succeeded
            }
        };

        Assert.Null(AiWorkflowDraftAutoImportService.GetBlockedReason(draft, validReport, items));

        var invalidReport = new AiWorkflowValidationReportEntity { IsValid = false, ErrorCount = 1 };
        var blockedReason = AiWorkflowDraftAutoImportService.GetBlockedReason(draft, invalidReport, items);
        Assert.Contains("校验存在 1 个错误", blockedReason);
    }
}
