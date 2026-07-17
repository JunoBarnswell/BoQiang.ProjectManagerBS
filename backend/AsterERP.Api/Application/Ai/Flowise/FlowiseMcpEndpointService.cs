using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using SqlSugar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseMcpEndpointService(
    ISqlSugarClient db,
    IFlowiseExecutionService executionService) : IFlowiseMcpEndpointService
{
    private static readonly JsonElement NullId = JsonDocument.Parse("null").RootElement.Clone();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FlowiseMcpJsonRpcResponse> HandleAsync(
        string chatflowId,
        string token,
        FlowiseMcpJsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        var loadResult = await LoadAndVerifyAsync(chatflowId, token, cancellationToken);
        if (loadResult.Status == McpEndpointLoadStatus.NotFound)
        {
            return Error(request.Id, -32001, "MCP server not found");
        }

        if (loadResult.Status == McpEndpointLoadStatus.Unauthorized)
        {
            return Error(request.Id, -32001, "Unauthorized");
        }

        var chatflow = loadResult.Chatflow!;
        var config = loadResult.Config!;
        return request.Method switch
        {
            "initialize" => Result(request.Id, new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { tools = new { } },
                serverInfo = new { name = $"flowise-{GetToolName(config, chatflow)}", version = "1.0.0" }
            }),
            "tools/list" => Result(request.Id, new { tools = new[] { BuildToolDescriptor(chatflow, config) } }),
            "tools/call" => await CallToolAsync(chatflow, config, request, cancellationToken),
            _ => Error(request.Id, -32601, $"Method not found: {request.Method}")
        };
    }

    private async Task<FlowiseMcpJsonRpcResponse> CallToolAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseMcpServerConfigDocument config,
        FlowiseMcpJsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        var parameters = request.Params.HasValue && request.Params.Value.ValueKind == JsonValueKind.Object
            ? request.Params.Value
            : default;
        var requestedToolName = ReadString(parameters, "name");
        var toolName = GetToolName(config, chatflow);
        if (!string.Equals(requestedToolName, toolName, StringComparison.Ordinal))
        {
            return Error(request.Id, -32602, "Unknown MCP tool.");
        }

        var arguments = ReadObject(parameters, "arguments");
        var question = ResolveQuestion(chatflow, arguments);
        var execution = await executionService.StartMcpAsync(
            chatflow,
            JsonSerializer.Serialize(new { arguments, question, source = "mcp" }, JsonOptions),
            question,
            $"{chatflow.Id}:mcp:{Guid.NewGuid():N}",
            cancellationToken);
        var text = execution.Status == "Failed"
            ? execution.ErrorMessage ?? "An error occurred while executing the tool. Please try again later."
            : ExtractAnswer(execution.OutputJson);

        return Result(request.Id, new
        {
            content = new[] { new { type = "text", text } },
            isError = execution.Status == "Failed"
        });
    }

    private async Task<McpEndpointLoadResult> LoadAndVerifyAsync(
        string chatflowId,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chatflowId) || string.IsNullOrWhiteSpace(token))
        {
            return McpEndpointLoadResult.NotFound();
        }

        var chatflow = await db.Queryable<FlowiseChatFlowEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == chatflowId.Trim(), cancellationToken);
        if (chatflow is null)
        {
            return McpEndpointLoadResult.NotFound();
        }

        var config = ReadConfig(chatflow.McpServerConfig);
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.Token))
        {
            return McpEndpointLoadResult.NotFound();
        }

        if (!TokenEquals(config.Token, token))
        {
            return McpEndpointLoadResult.Unauthorized();
        }

        return McpEndpointLoadResult.Success(chatflow, config);
    }

    private static object BuildToolDescriptor(FlowiseChatFlowEntity chatflow, FlowiseMcpServerConfigDocument config) => new
    {
        name = GetToolName(config, chatflow),
        description = GetToolDescription(config, chatflow),
        inputSchema = BuildInputSchema(chatflow)
    };

    private static object BuildInputSchema(FlowiseChatFlowEntity chatflow)
    {
        if (string.Equals(chatflow.Type, FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase) &&
            TryBuildFormInputSchema(chatflow.FlowData, out var formSchema))
        {
            return formSchema;
        }

        return new
        {
            type = "object",
            properties = new
            {
                question = new { type = "string", description = "The question or prompt to send to the chatflow" }
            },
            required = new[] { "question" }
        };
    }

    private static bool TryBuildFormInputSchema(string flowData, out object schema)
    {
        schema = new
        {
            type = "object",
            properties = new
            {
                form = new { type = "object", description = "Form inputs for the agent flow" }
            },
            required = new[] { "form" }
        };

        try
        {
            using var document = JsonDocument.Parse(flowData);
            if (!document.RootElement.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var startNode = nodes.EnumerateArray().FirstOrDefault(node =>
                node.TryGetProperty("data", out var data) &&
                ReadString(data, "name") == "startAgentflow");
            if (startNode.ValueKind == JsonValueKind.Undefined ||
                !startNode.TryGetProperty("data", out var startData) ||
                !startData.TryGetProperty("inputs", out var inputs) ||
                ReadString(inputs, "startInputType") != "formInput")
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveQuestion(FlowiseChatFlowEntity chatflow, JsonElement arguments)
    {
        if (string.Equals(chatflow.Type, FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase) &&
            arguments.ValueKind == JsonValueKind.Object &&
            arguments.TryGetProperty("form", out var form))
        {
            return JsonSerializer.Serialize(form, JsonOptions);
        }

        return ReadString(arguments, "question");
    }

    private static FlowiseMcpServerConfigDocument ReadConfig(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new FlowiseMcpServerConfigDocument();
        }

        try
        {
            return JsonSerializer.Deserialize<FlowiseMcpServerConfigDocument>(value, JsonOptions) ?? new FlowiseMcpServerConfigDocument();
        }
        catch
        {
            return new FlowiseMcpServerConfigDocument();
        }
    }

    private static string ExtractAnswer(string outputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(outputJson);
            return ReadString(document.RootElement, "answer") is { Length: > 0 } answer
                ? answer
                : outputJson;
        }
        catch
        {
            return outputJson;
        }
    }

    private static string GetToolName(FlowiseMcpServerConfigDocument config, FlowiseChatFlowEntity chatflow) =>
        !string.IsNullOrWhiteSpace(config.ToolName)
            ? config.ToolName
            : SanitizeToolName(chatflow.Name);

    private static string GetToolDescription(FlowiseMcpServerConfigDocument config, FlowiseChatFlowEntity chatflow) =>
        !string.IsNullOrWhiteSpace(config.Description) ? config.Description : $"Execute the \"{chatflow.Name}\" flow";

    private static string SanitizeToolName(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
        }

        var normalized = builder.ToString().Trim('_');
        return normalized.Length == 0 ? "chatflow_tool" : normalized[..Math.Min(normalized.Length, 64)];
    }

    private static JsonElement ReadObject(JsonElement value, string propertyName)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Object)
        {
            return property.Clone();
        }

        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static string ReadString(JsonElement value, string propertyName)
    {
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool TokenEquals(string storedToken, string providedToken)
    {
        var storedBytes = Encoding.UTF8.GetBytes(storedToken);
        var providedBytes = Encoding.UTF8.GetBytes(providedToken);
        return storedBytes.Length == providedBytes.Length && CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes);
    }

    private static FlowiseMcpJsonRpcResponse Result(JsonElement? id, object result) => new()
    {
        Id = id ?? NullId,
        Result = result
    };

    private static FlowiseMcpJsonRpcResponse Error(JsonElement? id, int code, string message) => new()
    {
        Error = new FlowiseMcpJsonRpcError { Code = code, Message = message },
        Id = id ?? NullId
    };

    private enum McpEndpointLoadStatus
    {
        Success,
        NotFound,
        Unauthorized
    }

    private sealed class McpEndpointLoadResult
    {
        private McpEndpointLoadResult(McpEndpointLoadStatus status, FlowiseChatFlowEntity? chatflow, FlowiseMcpServerConfigDocument? config)
        {
            Status = status;
            Chatflow = chatflow;
            Config = config;
        }

        public FlowiseChatFlowEntity? Chatflow { get; }

        public FlowiseMcpServerConfigDocument? Config { get; }

        public McpEndpointLoadStatus Status { get; }

        public static McpEndpointLoadResult NotFound() => new(McpEndpointLoadStatus.NotFound, null, null);

        public static McpEndpointLoadResult Success(FlowiseChatFlowEntity chatflow, FlowiseMcpServerConfigDocument config) => new(McpEndpointLoadStatus.Success, chatflow, config);

        public static McpEndpointLoadResult Unauthorized() => new(McpEndpointLoadStatus.Unauthorized, null, null);
    }
}
