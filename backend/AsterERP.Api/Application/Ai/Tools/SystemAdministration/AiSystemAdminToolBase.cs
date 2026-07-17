namespace AsterERP.Api.Application.Ai.Tools.SystemAdministration;

public abstract class AiSystemAdminToolBase(AiKernelFunctionDefinition definition) : IAiKernelFunction
{
    public AiKernelFunctionDefinition Definition { get; } = definition;

    public abstract Task<AiKernelFunctionResult> ExecuteAsync(
        AiKernelFunctionContext context,
        CancellationToken cancellationToken);
}
