namespace AsterERP.Api.Application.Ai.Tools.SystemAdministration;

public sealed class SystemAdminRegisteredTool(
    AiKernelFunctionDefinition definition,
    Func<AiKernelFunctionContext, CancellationToken, Task<AiKernelFunctionResult>> executor)
    : AiSystemAdminToolBase(definition)
{
    public override Task<AiKernelFunctionResult> ExecuteAsync(
        AiKernelFunctionContext context,
        CancellationToken cancellationToken) =>
        executor(context, cancellationToken);
}
