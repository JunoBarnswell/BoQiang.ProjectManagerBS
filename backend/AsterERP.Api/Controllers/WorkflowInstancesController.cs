using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/instances")]
public sealed class WorkflowInstancesController(IWorkflowInstanceAppService workflowInstanceService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.WorkflowInstanceQuery)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowInstanceService.GetPageAsync(gridQuery, cancellationToken));
    }

    [HttpGet("mine")]
    [Permission(PermissionCodes.WorkflowInstanceQuery)]
    public async Task<IActionResult> GetMineAsync([FromQuery] GridQuery gridQuery, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowInstanceService.GetMineAsync(gridQuery, cancellationToken));
    }

    [HttpPost("start")]
    [Permission(PermissionCodes.WorkflowInstanceStart)]
    public async Task<IActionResult> StartAsync([FromBody] WorkflowStartInstanceRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowInstanceService.StartAsync(request, cancellationToken));
    }

    [HttpGet("{processInstanceId}")]
    [Permission(PermissionCodes.WorkflowInstanceQuery)]
    public async Task<IActionResult> GetDetailAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowInstanceService.GetDetailAsync(processInstanceId, cancellationToken));
    }

    [HttpPost("{processInstanceId}/withdraw")]
    [Permission(PermissionCodes.WorkflowInstanceWithdraw)]
    public async Task<IActionResult> WithdrawAsync(string processInstanceId, [FromBody] WorkflowTaskActionRequest? request, CancellationToken cancellationToken)
    {
        await workflowInstanceService.WithdrawAsync(processInstanceId, request?.Comment, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{processInstanceId}/terminate")]
    [Permission(PermissionCodes.WorkflowInstanceTerminate)]
    public async Task<IActionResult> TerminateAsync(string processInstanceId, [FromBody] WorkflowTaskActionRequest? request, CancellationToken cancellationToken)
    {
        await workflowInstanceService.TerminateAsync(processInstanceId, request?.Comment, cancellationToken);
        return ApiOk(true);
    }

    [HttpPut("{processInstanceId}/variables")]
    [Permission(PermissionCodes.WorkflowInstanceVariable)]
    public async Task<IActionResult> SetVariablesAsync(string processInstanceId, [FromBody] WorkflowInstanceVariableRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowInstanceService.SetVariablesAsync(processInstanceId, request, cancellationToken));
    }

    [HttpGet("{processInstanceId}/diagram")]
    [Permission(PermissionCodes.WorkflowInstanceQuery)]
    public async Task<IActionResult> GetHighlightedDiagramAsync(string processInstanceId, CancellationToken cancellationToken)
    {
        return ApiOk(await workflowInstanceService.GetHighlightedDiagramAsync(processInstanceId, cancellationToken));
    }

    [HttpPost("executions/{executionId}/signal")]
    [Permission(PermissionCodes.WorkflowInstanceVariable)]
    public async Task<IActionResult> SignalAsync(string executionId, [FromBody] WorkflowInstanceVariableRequest? request, CancellationToken cancellationToken)
    {
        await workflowInstanceService.SignalAsync(executionId, request, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("executions/{executionId}/messages/{messageName}")]
    [Permission(PermissionCodes.WorkflowInstanceVariable)]
    public async Task<IActionResult> MessageAsync(string executionId, string messageName, [FromBody] WorkflowInstanceVariableRequest? request, CancellationToken cancellationToken)
    {
        await workflowInstanceService.MessageAsync(executionId, messageName, request, cancellationToken);
        return ApiOk(true);
    }
}
