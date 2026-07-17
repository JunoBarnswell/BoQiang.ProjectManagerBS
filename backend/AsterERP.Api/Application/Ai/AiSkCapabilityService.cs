using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai;

public sealed class AiSkCapabilityService
{
    public IReadOnlyList<AiSkCapabilityDto> ListCapabilities()
    {
        return
        [
            Implemented("kernel.di", "SemanticKernel", "AsterErpAiCenterModule.RegisterInfrastructureServices", "services.AddKernel already integrated."),
            Implemented("chat.agent.streaming", "SemanticKernel.Agent", "AiKernelChatRuntime.StreamAsync", "Ask/Plan streaming uses ChatCompletionAgent.InvokeStreamingAsync."),
            Implemented("chat.agent.complete", "SemanticKernel.Agent", "AiKernelChatRuntime.CompleteAsync", "Planning and context compression use ChatCompletionAgent.InvokeAsync."),
            Implemented("functions.invoke", "SemanticKernel.KernelFunction", "AiKernelFunctionService.InvokeKernelFunctionAsync", "Tool APIs are registered through KernelFunctionFactory and executed by Kernel.InvokeAsync."),
            Blocked("agent.group-chat", "SemanticKernel.AgentGroupChat", "AiStreamService.StreamCollaborativeAsync", "The old handwritten parallel collaboration chain has been removed; AgentGroupChat selection/termination strategy is still pending."),
            Blocked("agent.process", "SemanticKernel.Process", "AiAgentExecutionService.ExecuteAsync", "The stable SK Process .NET runtime is not yet available; execution remains blocked."),
            FrameworkUnavailable("rag.sqlite-vec", "SemanticKernel.VectorData", "AiKnowledgeService.SearchAsync", "A Microsoft/SK SQLite Vec provider has not been confirmed for the current package version, so a self-built vector store is unavailable."),
            FrameworkUnavailable("mcp.connector", "SemanticKernel.MCP", "AiSkCapabilityService.ListCapabilities", "The official SK .NET MCP connector API is not available in the current package set; no third-party shim is used."),
            FrameworkUnavailable("openapi.plugin", "Microsoft.SemanticKernel.Plugins.OpenApi", "AiSkCapabilityService.ListCapabilities", "The project does not yet reference the official OpenAPI plugin package; no custom bridge is used.")
        ];
    }

    private static AiSkCapabilityDto Implemented(string code, string frameworkType, string symbol, string reason) =>
        Create(code, "Implemented", frameworkType, symbol, reason);

    private static AiSkCapabilityDto FrameworkUnavailable(string code, string frameworkType, string symbol, string reason) =>
        Create(code, "FrameworkUnavailable", frameworkType, symbol, reason);

    private static AiSkCapabilityDto Blocked(string code, string frameworkType, string symbol, string reason) =>
        Create(code, "Blocked", frameworkType, symbol, reason);

    private static AiSkCapabilityDto Create(string code, string status, string frameworkType, string symbol, string reason) =>
        new()
        {
            CapabilityCode = code,
            Status = status,
            FrameworkType = frameworkType,
            ImplementationSymbol = symbol,
            Reason = reason
        };
}
