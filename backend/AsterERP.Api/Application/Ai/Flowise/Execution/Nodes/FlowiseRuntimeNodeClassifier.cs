namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseRuntimeNodeClassifier
{
    internal FlowiseRuntimeNodeKind Classify(FlowiseRuntimeNode node)
    {
        if (IsStartNode(node)) return FlowiseRuntimeNodeKind.Start;
        if (IsConditionNode(node)) return FlowiseRuntimeNodeKind.Condition;
        if (IsRuntimeDataModelNode(node)) return FlowiseRuntimeNodeKind.RuntimeDataModel;
        if (IsHttpNode(node)) return FlowiseRuntimeNodeKind.Http;
        if (IsExecuteFlowNode(node)) return FlowiseRuntimeNodeKind.ExecuteFlow;
        if (IsCustomFunctionNode(node)) return FlowiseRuntimeNodeKind.CustomFunction;
        if (IsLlmAgentNode(node)) return FlowiseRuntimeNodeKind.Llm;
        if (IsAgentAgentNode(node)) return FlowiseRuntimeNodeKind.Agent;
        if (IsDirectReplyNode(node)) return FlowiseRuntimeNodeKind.DirectReply;
        if (IsHumanInputNode(node)) return FlowiseRuntimeNodeKind.HumanInput;
        if (IsIterationNode(node)) return FlowiseRuntimeNodeKind.Iteration;
        if (IsLoopNode(node)) return FlowiseRuntimeNodeKind.Loop;
        return FlowiseRuntimeNodeKind.Unsupported;
    }

    internal bool IsSupported(FlowiseRuntimeNode node) => Classify(node) != FlowiseRuntimeNodeKind.Unsupported;

    internal bool IsToolNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("tool", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Contains("function", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Contains("mcp", StringComparison.OrdinalIgnoreCase);

    internal bool IsStartNode(FlowiseRuntimeNode node) =>
        node.NodeType.Equals("startAgentflow", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Equals("start", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Equals("Start", StringComparison.OrdinalIgnoreCase);

    internal bool IsConditionNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("condition", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("Condition", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("条件", StringComparison.OrdinalIgnoreCase);

    internal bool IsRuntimeDataModelNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("runtime-data-model", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Contains("runtimeDataModel", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("Runtime Data Model", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("系统配置模型", StringComparison.OrdinalIgnoreCase);

    internal bool IsHumanInputNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("human-input", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Contains("humanInput", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("Human Input", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("人工输入", StringComparison.OrdinalIgnoreCase);

    internal bool IsIterationNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("iteration", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("Iteration", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("迭代", StringComparison.OrdinalIgnoreCase);

    internal bool IsLoopNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("loopAgentflow", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Equals("loop", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Equals("Loop", StringComparison.OrdinalIgnoreCase);

    internal bool IsDirectReplyNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("directReplyAgentflow", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Contains("direct-reply", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("Direct Reply", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("直接回复", StringComparison.OrdinalIgnoreCase);

    internal bool IsHttpNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("httpAgentflow", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Equals("http", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Equals("HTTP", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("HTTP Request", StringComparison.OrdinalIgnoreCase);

    internal bool IsExecuteFlowNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("executeFlowAgentflow", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Contains("execute-flow", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("Execute Flow", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("执行工作流", StringComparison.OrdinalIgnoreCase);

    internal bool IsCustomFunctionNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("customFunctionAgentflow", StringComparison.OrdinalIgnoreCase) ||
        node.NodeType.Contains("custom-function", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("Custom Function", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Contains("自定义函数", StringComparison.OrdinalIgnoreCase);

    internal bool IsLlmAgentNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("llmAgentflow", StringComparison.OrdinalIgnoreCase) ||
        node.DisplayName.Equals("LLM", StringComparison.OrdinalIgnoreCase);

    internal bool IsAgentAgentNode(FlowiseRuntimeNode node) =>
        node.NodeType.Contains("agentAgentflow", StringComparison.OrdinalIgnoreCase);
}
