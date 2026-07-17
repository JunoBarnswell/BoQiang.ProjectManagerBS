using System.Runtime.CompilerServices;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiKernelChatRuntime(
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    ILogger<AiKernelChatRuntime> logger,
    IEnumerable<IFunctionInvocationFilter> functionInvocationFilters,
    IEnumerable<IAutoFunctionInvocationFilter> autoFunctionInvocationFilters,
    IEnumerable<IPromptRenderFilter> promptRenderFilters)
{
    private const string ChatServiceId = "astererp-chat";

    public async IAsyncEnumerable<AiKernelChatChunk> StreamAsync(
        AiKernelChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var kernel = CreateKernel(request.Endpoint);
        var agent = CreateAgent(kernel, request);
        var messages = request.Messages.Count == 0
            ? [new ChatMessageContent(AuthorRole.User, string.Empty)]
            : request.Messages.ToList();
        var options = new AgentInvokeOptions
        {
            KernelArguments = new KernelArguments(CreatePromptSettings(request))
        };
        var hasContent = false;

        await foreach (var item in agent.InvokeStreamingAsync(messages, null, options, cancellationToken))
        {
            var message = item.Message;
            if (!string.IsNullOrEmpty(message.Content))
            {
                hasContent = true;
                yield return new AiKernelChatChunk { ContentDelta = message.Content };
            }
        }

        if (!hasContent)
        {
            logger.LogWarning("SK agent returned an empty stream for model {ModelCode}", request.Endpoint.ModelCode);
        }

        yield return new AiKernelChatChunk { FinishReason = "stop" };
    }

    public async Task<string> CompleteAsync(AiKernelChatRequest request, CancellationToken cancellationToken)
    {
        var kernel = CreateKernel(request.Endpoint);
        var agent = CreateAgent(kernel, request);
        var options = new AgentInvokeOptions
        {
            KernelArguments = new KernelArguments(CreatePromptSettings(request))
        };
        var content = new System.Text.StringBuilder();
        var messages = request.Messages.Count == 0
            ? [new ChatMessageContent(AuthorRole.User, string.Empty)]
            : request.Messages.ToList();

        try
        {
            await foreach (var item in agent.InvokeAsync(messages, null, options, cancellationToken))
            {
                content.Append(item.Message.Content);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException(ErrorCodes.AiModelServiceUnavailable, $"模型服务不可用：{ex.Message}");
        }

        return content.ToString();
    }

    private Kernel CreateKernel(AiModelEndpoint endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint.BaseUrl))
        {
            throw new ValidationException("模型供应商未配置 BaseUrl", AsterERP.Shared.ErrorCodes.AiProviderMissing);
        }

        if (!Uri.TryCreate(endpoint.BaseUrl, UriKind.Absolute, out var endpointUri))
        {
            throw new ValidationException("模型供应商 BaseUrl 不是有效 URL", AsterERP.Shared.ErrorCodes.AiProviderMissing);
        }

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(loggerFactory);
        builder.AddOpenAIChatCompletion(
            modelId: endpoint.ModelCode,
            endpoint: endpointUri,
            apiKey: endpoint.ApiKey,
            orgId: null,
            serviceId: ChatServiceId,
            httpClient: httpClientFactory.CreateClient());
        var kernel = builder.Build();
        RegisterFilters(kernel);
        return kernel;
    }

    private void RegisterFilters(Kernel kernel)
    {
        foreach (var filter in functionInvocationFilters)
        {
            kernel.FunctionInvocationFilters.Add(filter);
        }

        foreach (var filter in autoFunctionInvocationFilters)
        {
            kernel.AutoFunctionInvocationFilters.Add(filter);
        }

        foreach (var filter in promptRenderFilters)
        {
            kernel.PromptRenderFilters.Add(filter);
        }
    }

    private static ChatCompletionAgent CreateAgent(Kernel kernel, AiKernelChatRequest request) =>
        new()
        {
            Name = string.IsNullOrWhiteSpace(request.AgentName) ? "AsterERP_AI_Agent" : NormalizeAgentName(request.AgentName),
            Instructions = ExtractSystemInstructions(request.Messages),
            Kernel = kernel,
            Arguments = new KernelArguments(CreatePromptSettings(request))
        };

    private static OpenAIPromptExecutionSettings CreatePromptSettings(AiKernelChatRequest request)
    {
        var endpoint = request.Endpoint;
        var settings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = endpoint.MaxOutputTokens,
            Temperature = endpoint.DefaultTemperature is null ? null : Convert.ToDouble(endpoint.DefaultTemperature.Value),
            TopP = endpoint.DefaultTopP is null ? null : Convert.ToDouble(endpoint.DefaultTopP.Value),
            User = request.UserId,
            ReasoningEffort = endpoint.ReasoningEffort,
            FunctionChoiceBehavior = request.EnabledFunctionNames.Count > 0
                ? FunctionChoiceBehavior.Auto()
                : FunctionChoiceBehavior.None()
        };

        if (request.JsonResponse)
        {
            settings.ResponseFormat = "json_object";
        }

        return settings;
    }

    private static string? ExtractSystemInstructions(IReadOnlyList<ChatMessageContent> messages)
    {
        var systemMessages = messages
            .Where(item => item.Role == AuthorRole.System && !string.IsNullOrWhiteSpace(item.Content))
            .Select(item => item.Content!.Trim())
            .ToArray();
        return systemMessages.Length == 0 ? null : string.Join("\n\n", systemMessages);
    }

    private static string NormalizeAgentName(string name)
    {
        var chars = name.Trim().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var normalized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "AsterERP_AI_Agent" : normalized;
    }
}
