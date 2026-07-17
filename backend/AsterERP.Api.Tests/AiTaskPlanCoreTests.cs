using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.Agent;
using AsterERP.Api.Modules.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AiTaskPlanCoreTests
{
    [Fact]
    public void Parse_MapsV2PlanJsonToUpsertRequest()
    {
        var parser = new AiPlanParser();
        var request = parser.Parse("""
        {
          "title": "上线 AI 工作台",
          "goal": "完成 Ask Plan Agent 全链路",
          "executionStrategy": "Serial",
          "risks": ["权限遗漏"],
          "assumptions": ["已有模型配置"],
          "items": [
            {
              "id": "task-1",
              "title": "冻结契约",
              "description": "确认 DTO 与事件名",
              "priority": "P0",
              "ownerType": "Agent",
              "taskType": "Design",
              "acceptanceCriteria": ["状态枚举固定"]
            },
            {
              "id": "task-2",
              "title": "人工批准",
              "description": "用户确认计划",
              "priority": "P0",
              "ownerType": "User",
              "taskType": "Manual",
              "dependsOn": ["task-1"],
              "acceptanceCriteria": ["批准后结构冻结"]
            }
          ]
        }
        """);

        Assert.Equal("上线 AI 工作台", request.Title);
        Assert.Equal(AiTaskPlanConstants.PlanStatus.PlanReady, request.Status);
        Assert.Equal("Serial", request.ExecutionStrategy);
        Assert.Equal(2, request.Items.Count);
        Assert.Equal(AiTaskPlanConstants.OwnerType.User, request.Items[1].OwnerType);
        Assert.Contains("task-1", request.Items[1].DependsOnJson);
    }

    [Fact]
    public void Parse_NormalizesToolTasksFromModelFriendlyJson()
    {
        var parser = new AiPlanParser();
        var request = parser.Parse("""
        {
          "title": "复杂采购审批流",
          "goal": "生成 Workflow 草稿并校验模拟",
          "executionStrategy": "Serial",
          "tasks": [
            {
              "id": "task-1",
              "name": "生成流程草稿",
              "description": "根据采购审批需求生成草稿",
              "tool": "workflow.model.createDraftFromText",
              "parameters": {
                "businessType": "purchase.order"
              }
            }
          ]
        }
        """);

        Assert.Single(request.Items);
        var item = request.Items[0];
        Assert.Equal("生成流程草稿", item.Title);
        Assert.Equal(AiTaskPlanConstants.OwnerType.Tool, item.OwnerType);
        Assert.Equal(AiTaskPlanConstants.TaskType.Tool, item.TaskType);
        Assert.Equal("workflow.model.createDraftFromText", item.ToolCode);
        Assert.Contains("businessType", item.ExecutionHint);
        Assert.Contains("workflow.model.createDraftFromText", item.AcceptanceCriteriaJson);
    }

    [Fact]
    public void BuildPlanPrompt_Allows_SystemAdministration_Tool_Tasks()
    {
        var prompt = new AiPlanParser().BuildPlanPrompt();

        Assert.Contains("system-admin", prompt);
        Assert.Contains("system.user.grantRoles", prompt);
        Assert.Contains("system.role.grantMenus", prompt);
        Assert.Contains("system.scheduledJob.trigger", prompt);
        Assert.Contains("日志类不得生成新增、编辑、删除工具任务", prompt);
    }

    [Fact]
    public void ValidateUpsert_RejectsDependencyCycle()
    {
        var validator = new AiTaskPlanValidator();
        var request = new AsterERP.Contracts.Ai.AiTaskPlanUpsertRequest
        {
            Title = "循环依赖计划",
            Goal = "发现循环依赖",
            Items =
            [
                new() { Id = "a", Title = "A", Description = "A", DependsOnJson = "[\"b\"]", OwnerType = "Agent", TaskType = "Design", Priority = "P1", Status = "Pending" },
                new() { Id = "b", Title = "B", Description = "B", DependsOnJson = "[\"a\"]", OwnerType = "Agent", TaskType = "Design", Priority = "P1", Status = "Pending" }
            ]
        };

        var ex = Assert.Throws<ValidationException>(() => validator.ValidateUpsert(request, requireItems: true));
        Assert.Equal(ErrorCodes.AiPlanValidationFailed, ex.Code);
    }

    [Fact]
    public void Guard_AllowsOnlyApprovedOrPartialPlansToExecute()
    {
        var guard = new AiTaskPlanGuard();

        guard.EnsureCanExecute(AiTaskPlanConstants.PlanStatus.Approved);
        guard.EnsureCanExecute(AiTaskPlanConstants.PlanStatus.PartialCompleted);

        var blocked = Assert.Throws<ValidationException>(() => guard.EnsureCanExecute(AiTaskPlanConstants.PlanStatus.Blocked));
        Assert.Equal(ErrorCodes.AiPlanNotApproved, blocked.Code);
    }
}
