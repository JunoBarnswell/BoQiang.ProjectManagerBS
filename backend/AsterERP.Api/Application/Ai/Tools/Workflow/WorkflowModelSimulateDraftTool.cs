using System.Globalization;
using System.Text.RegularExpressions;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowModelSimulateDraftTool(
    AiWorkflowArtifactService artifactService,
    WorkflowSimulationEngine simulationEngine) : AiWorkflowToolBase(
    AiWorkflowToolDefinition.Create(
        AiWorkflowToolCodes.ModelSimulateDraft,
        "模拟流程草稿",
        "使用样例业务数据模拟审批路径，不创建真实实例/待办/通知",
        "L1",
        PermissionCodes.AiToolWorkflowSimulate,
        PermissionCodes.WorkflowModelQuery,
        ["Plan", "Agent"]))
{
    public override async Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken)
    {
        var artifact = await artifactService.RequireDraftFromArgumentsAsync(context, cancellationToken);
        var draft = artifactService.ParseDraft(artifact);
        var variables = AiWorkflowArgumentReader.ReadObject(context.Arguments, "variables", draft.Variables);
        ApplyVariablesFromInstruction(variables, context.UserInstruction);
        if (!variables.ContainsKey("amount"))
        {
            variables["amount"] = 15000;
        }

        var steps = simulationEngine.Simulate(draft, variables);
        var report = await artifactService.SaveSimulationReportAsync(context, artifact, variables, steps, cancellationToken);
        var dto = AiWorkflowArtifactService.MapSimulation(report);
        return Result(
            $"流程草稿模拟完成，共 {steps.Count} 个步骤",
            dto,
            Evidence(("draftArtifactId", artifact.Id), ("simulationReportId", report.Id)),
            [
                Event("workflow_simulation_started", "开始模拟 Workflow 草稿", new { draftArtifactId = artifact.Id }),
                Event("workflow_simulation_completed", "Workflow 草稿模拟完成", new { draftArtifactId = artifact.Id, simulationReportId = report.Id, stepCount = steps.Count })
            ]);
    }

    public static void ApplyVariablesFromInstruction(IDictionary<string, object?> variables, string? instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            return;
        }

        var amountMatch = Regex.Match(instruction, @"\bamount\s*=\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            variables["amount"] = amount % 1 == 0 ? decimal.ToInt32(amount) : amount;
        }

        var contractMatch = Regex.Match(instruction, @"\bcontractRequired\s*=\s*(true|false)", RegexOptions.IgnoreCase);
        if (contractMatch.Success && bool.TryParse(contractMatch.Groups[1].Value, out var contractRequired))
        {
            variables["contractRequired"] = contractRequired;
        }
    }
}
