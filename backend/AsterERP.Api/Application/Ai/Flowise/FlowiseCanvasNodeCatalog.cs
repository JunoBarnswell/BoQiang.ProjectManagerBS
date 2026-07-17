using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

internal static class FlowiseCanvasNodeCatalog
{
    public static readonly FlowiseNodeCatalogItemDto[] Items =
    [
        Node("startAgentflow", "Start", "Workflow", "定义工作流的触发入口。", "player-play", [
            Param("startInputType", "Start Input Type", "options", defaultJson: "\"chatInput\"", options: [
                Option("Chat Input", "chatInput"),
                Option("Schedule Input", "scheduleInput"),
                Option("Form Input", "formInput"),
                Option("Webhook Trigger", "webhookTrigger")
            ]),
            Param("cronExpression", "Cron Expression", "string", defaultJson: "\"* * * * *\"", show: new Dictionary<string, object?> { ["startInputType"] = "scheduleInput" }),
            Param("webhookPath", "Webhook Path", "string", defaultJson: "\"/flowise/webhook\"", show: new Dictionary<string, object?> { ["startInputType"] = "webhookTrigger" }),
            Param("startState", "Flow State", "json", defaultJson: "[]", additional: true),
            Param("startPersistState", "Persist State", "boolean", defaultJson: "false", additional: true)
        ], [], [Anchor("output", "Output", "Workflow")]),
        Node("llm", "Chat Model", "AI", "调用当前 AsterERP AI 能力生成响应。", "module", [Param("model", "Model", "options"), Param("temperature", "Temperature", "number", defaultJson: "0.7")], [], [Anchor("model", "Model", "BaseChatModel")]),
        Node("llmAgentflow", "LLM", "Workflow", "按消息、记忆、结构化输出和流程状态配置调用大模型。", "sparkles", [
            Param("llmModel", "Model", "options", optional: true),
            Param("llmMessages", "Messages", "json", defaultJson: "[]", acceptVariable: true),
            Param("llmEnableMemory", "Enable Memory", "boolean", defaultJson: "true", additional: true),
            Param("llmMemoryType", "Memory Type", "options", defaultJson: "\"allMessages\"", options: [
                Option("All Messages", "allMessages"),
                Option("Window Size", "windowSize"),
                Option("Conversation Summary", "conversationSummary"),
                Option("Conversation Summary Buffer", "conversationSummaryBuffer")
            ], additional: true),
            Param("llmMemoryWindowSize", "Window Size", "number", defaultJson: "20", additional: true),
            Param("llmMemoryMaxTokenLimit", "Max Token Limit", "number", defaultJson: "2000", additional: true),
            Param("llmUserMessage", "Input Message", "string", additional: true, acceptVariable: true),
            Param("llmReturnResponseAs", "Return Response As", "options", defaultJson: "\"userMessage\"", options: [
                Option("User Message", "userMessage"),
                Option("Assistant Message", "assistantMessage")
            ], additional: true),
            Param("llmStructuredOutput", "JSON Structured Output", "json", defaultJson: "[]", additional: true),
            Param("llmUpdateState", "Update Flow State", "json", defaultJson: "[]", additional: true, acceptVariable: true)
        ], [Anchor("input", "Input", "Workflow")], [Anchor("output", "Output", "Workflow")]),
        Node("prompt", "Prompt Template", "AI", "维护提示词模板和变量。", "file-text", [Param("template", "Template", "code", acceptVariable: true), Param("variables", "Variables", "json", additional: true, acceptVariable: true)], [], [Anchor("prompt", "Prompt", "PromptTemplate")]),
        Node("agent", "Agent", "Agent", "组合模型、工具和记忆执行任务。", "robot", [Param("systemMessage", "System Message", "string", acceptVariable: true), Param("maxIterations", "Max Iterations", "number", additional: true)], [Anchor("model", "Model", "BaseChatModel"), Anchor("tools", "Tools", "Tool[]")], [Anchor("agent", "Agent", "AgentExecutor")]),
        Node("agentAgentflow", "Agent", "Workflow", "按消息、工具、知识库、记忆和流程状态配置执行多步推理。", "robot", [
            Param("agentModel", "Model", "options", optional: true),
            Param("agentMessages", "Messages", "json", defaultJson: "[]", acceptVariable: true),
            Param("agentTools", "Tools", "json", defaultJson: "[]", additional: true, acceptVariable: true),
            Param("agentKnowledgeDocumentStores", "Knowledge (Document Stores)", "json", defaultJson: "[]", additional: true),
            Param("agentKnowledgeVSEmbeddings", "Knowledge (Vector Embeddings)", "json", defaultJson: "[]", additional: true),
            Param("agentEnableMemory", "Enable Memory", "boolean", defaultJson: "true", additional: true),
            Param("agentMemoryType", "Memory Type", "options", defaultJson: "\"allMessages\"", options: [
                Option("All Messages", "allMessages"),
                Option("Window Size", "windowSize"),
                Option("Conversation Summary", "conversationSummary"),
                Option("Conversation Summary Buffer", "conversationSummaryBuffer")
            ], additional: true),
            Param("agentMemoryWindowSize", "Window Size", "number", defaultJson: "20", additional: true),
            Param("agentMemoryMaxTokenLimit", "Max Token Limit", "number", defaultJson: "2000", additional: true),
            Param("agentUserMessage", "Input Message", "string", additional: true, acceptVariable: true),
            Param("agentReturnResponseAs", "Return Response As", "options", defaultJson: "\"userMessage\"", options: [
                Option("User Message", "userMessage"),
                Option("Assistant Message", "assistantMessage")
            ], additional: true),
            Param("agentStructuredOutput", "JSON Structured Output", "json", defaultJson: "[]", additional: true),
            Param("agentUpdateState", "Update Flow State", "json", defaultJson: "[]", additional: true, acceptVariable: true)
        ], [Anchor("input", "Input", "Workflow")], [Anchor("output", "Output", "Workflow")]),
        Node("tool", "Tool", "Integration", "调用 Flowise Studio 工具资源。", "wrench", [Param("toolId", "Tool", "options")], [], [Anchor("tool", "Tool", "Tool")]),
        Node("runtime-data-model", "Runtime Data Model", "Integration", "查询 AsterERP 系统配置模型并把结果交给工作流。", "database", [
            Param("modelCode", "Model Code", "string", defaultJson: "\"runtime.menu\""),
            Param("pageCode", "Page Code", "string", additional: true),
            Param("keyword", "Keyword", "string", additional: true, acceptVariable: true),
            Param("pageIndex", "Page Index", "number", defaultJson: "1", additional: true),
            Param("pageSize", "Page Size", "number", defaultJson: "20", additional: true),
            Param("filters", "Filters", "json", defaultJson: "[]", additional: true, acceptVariable: true),
            Param("sorts", "Sorts", "json", defaultJson: "[]", additional: true, acceptVariable: true),
            Param("delayMs", "Delay Ms", "number", defaultJson: "0", additional: true)
        ], [Anchor("input", "Input", "json")], [Anchor("data", "Data", "json"), Anchor("tool", "Tool", "Tool")]),
        Node("credential", "Credential", "Security", "引用加密凭据，不在画布暴露明文。", "key", [Param("credentialId", "Credential", "credential")], [], [Anchor("credential", "Credential", "Credential")]),
        Node("retriever", "Retriever", "Knowledge", "从知识库或文档库检索上下文。", "database", [Param("topK", "Top K", "number", defaultJson: "4")], [Anchor("store", "Vector Store", "VectorStore")], [Anchor("retriever", "Retriever", "BaseRetriever")]),
        Node("document-store", "Document Store", "Knowledge", "引用文档库作为知识来源。", "database", [Param("storeId", "Document Store", "options")], [], [Anchor("store", "Vector Store", "VectorStore")]),
        Node("condition", "Condition", "Control", "按条件切换执行路径。", "check", [Param("expression", "Expression", "code", acceptVariable: true)], [Anchor("input", "Input", "any")], [Anchor("condition_true", "True", "condition"), Anchor("condition_false", "False", "condition")]),
        Node("httpAgentflow", "HTTP", "Integration", "发送受控 HTTP 请求并把响应交给后续工作流节点。", "plugs-connected", [
            Param("method", "Method", "options", defaultJson: "\"GET\"", options: [
                Option("GET", "GET"),
                Option("POST", "POST"),
                Option("PUT", "PUT"),
                Option("DELETE", "DELETE"),
                Option("PATCH", "PATCH")
            ]),
            Param("url", "URL", "string", acceptVariable: true),
            Param("headers", "Headers", "json", defaultJson: "[]", additional: true, acceptVariable: true),
            Param("queryParams", "Query Params", "json", defaultJson: "[]", additional: true, acceptVariable: true),
            Param("bodyType", "Body Type", "options", defaultJson: "\"json\"", options: [
                Option("JSON", "json"),
                Option("Raw", "raw"),
                Option("Form Data", "formData"),
                Option("x-www-form-urlencoded", "xWwwFormUrlencoded")
            ], additional: true),
            Param("body", "Body", "string", additional: true, acceptVariable: true),
            Param("responseType", "Response Type", "options", defaultJson: "\"json\"", options: [
                Option("JSON", "json"),
                Option("Text", "text"),
                Option("Raw (Base64)", "base64")
            ], additional: true)
        ], [Anchor("input", "Input", "json")], [Anchor("http", "HTTP", "json")]),
        Node("executeFlowAgentflow", "Execute Flow", "Workflow", "执行另一个工作流并把响应作为当前节点输出。", "route", [
            Param("executeFlowSelectedFlow", "Select Flow", "string"),
            Param("executeFlowInput", "Input", "string", acceptVariable: true),
            Param("executeFlowOverrideConfig", "Override Config", "json", defaultJson: "{}", additional: true, acceptVariable: true),
            Param("executeFlowReturnResponseAs", "Return Response As", "options", defaultJson: "\"userMessage\"", options: [
                Option("User Message", "userMessage"),
                Option("Assistant Message", "assistantMessage")
            ], additional: true),
            Param("executeFlowUpdateState", "Update Flow State", "json", defaultJson: "[]", additional: true, acceptVariable: true)
        ], [Anchor("input", "Input", "any")], [Anchor("output", "Output", "string")]),
        Node("customFunctionAgentflow", "Custom Function", "Workflow", "用受控函数表达式加工输入变量、流程状态和前序节点输出。", "code", [
            Param("customFunctionInputVariables", "Input Variables", "json", defaultJson: "[]", acceptVariable: true),
            Param("customFunctionJavascriptFunction", "Javascript Function", "code", acceptVariable: true),
            Param("customFunctionUpdateState", "Update Flow State", "json", defaultJson: "[]", additional: true, acceptVariable: true)
        ], [Anchor("input", "Input", "any")], [Anchor("output", "Output", "string")]),
        Node("transform", "Transform", "Data", "转换输入、输出或中间变量。", "brackets-curly", [Param("script", "Script", "code", acceptVariable: true)], [Anchor("input", "Input", "json")], [Anchor("output", "Output", "json")]),
        Node("iteration", "Iteration", "Workflow", "工作流迭代容器。", "refresh", [
            Param("items", "Items", "json", defaultJson: "[]", acceptVariable: true),
            Param("maxLoops", "Max Loops", "number", defaultJson: "3")
        ], [Anchor("items", "Items", "array")], [Anchor("done", "Done", "array")]),
        Node("loopAgentflow", "Loop", "Workflow", "回跳到前序节点并按最大次数重复执行。", "repeat", [
            Param("loopBackToNode", "Loop Back To", "string"),
            Param("maxLoopCount", "Max Loop Count", "number", defaultJson: "5"),
            Param("fallbackMessage", "Fallback Message", "string", additional: true, acceptVariable: true),
            Param("loopUpdateState", "Update Flow State", "json", defaultJson: "[]", additional: true, acceptVariable: true)
        ], [Anchor("input", "Input", "any")], [Anchor("output", "Output", "string")]),
        Node("directReplyAgentflow", "Direct Reply", "Workflow", "直接把消息模板回复给用户，支持引用前序节点输出。", "message-circle", [
            Param("directReplyMessage", "Message", "string", defaultJson: "\"\"", acceptVariable: true)
        ], [Anchor("input", "Input", "any")], []),
        Node("human-input", "Human Input", "Workflow", "等待人工输入后继续执行。", "user", [Param("message", "Message", "string", acceptVariable: true)], [Anchor("input", "Input", "any")], [Anchor("human_input", "Human Input", "human")]),
        Node("output", "Output Parser", "Result", "定义最终响应结构。", "target", [Param("schema", "Schema", "json", additional: true, acceptVariable: true)], [Anchor("input", "Input", "any")], [])
    ];

    private static FlowiseNodeCatalogItemDto Node(
        string nodeType,
        string displayName,
        string category,
        string description,
        string icon,
        IReadOnlyList<FlowiseNodeInputParamDto> inputParams,
        IReadOnlyList<FlowiseNodeAnchorDto> inputAnchors,
        IReadOnlyList<FlowiseNodeAnchorDto> outputAnchors) => new()
        {
            NodeType = nodeType,
            DisplayName = displayName,
            Category = category,
            Description = description,
            Icon = icon,
            Version = 1,
            InputParams = inputParams,
            InputAnchors = inputAnchors,
            OutputAnchors = outputAnchors,
            Tags = [category]
        };

    private static FlowiseNodeInputParamDto Param(
        string name,
        string label,
        string type,
        bool optional = false,
        bool additional = false,
        string defaultJson = "null",
        IReadOnlyList<FlowiseNodeOptionDto>? options = null,
        IReadOnlyDictionary<string, object?>? show = null,
        IReadOnlyDictionary<string, object?>? hide = null,
        bool acceptVariable = false) => new()
    {
        Name = name,
        Label = label,
        Type = type,
        AdditionalParams = additional,
        AcceptVariable = acceptVariable,
        DefaultJson = defaultJson,
        Optional = optional || additional,
        Options = options ?? [],
        Show = show,
        Hide = hide
    };

    private static FlowiseNodeOptionDto Option(string label, string value) => new()
    {
        Label = label,
        Value = value
    };

    private static FlowiseNodeAnchorDto Anchor(string name, string label, string type) => new()
    {
        Name = name,
        Label = label,
        Type = type
    };
}
