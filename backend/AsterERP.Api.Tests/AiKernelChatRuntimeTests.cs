using System.Reflection;
using AsterERP.Api.Infrastructure.Ai;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AiKernelChatRuntimeTests
{
    [Fact]
    public void Prompt_settings_use_json_response_for_plan_mode()
    {
        var settings = CreatePromptSettings(new AiKernelChatRequest
        {
            Endpoint = BuildEndpoint(),
            JsonResponse = true,
            UserId = "u-1",
            Messages = [new ChatMessageContent(AuthorRole.User, "Generate plan JSON.")]
        });

        Assert.Equal("json_object", settings.ResponseFormat);
        Assert.Equal("u-1", settings.User);
        Assert.Equal(512, settings.MaxTokens);
    }

    [Fact]
    public void Prompt_settings_enable_auto_function_choice_only_when_functions_are_allowed()
    {
        var withoutFunctions = CreatePromptSettings(new AiKernelChatRequest { Endpoint = BuildEndpoint() });
        var withFunctions = CreatePromptSettings(new AiKernelChatRequest
        {
            Endpoint = BuildEndpoint(),
            EnabledFunctionNames = ["workflow.search"]
        });

        Assert.NotNull(withoutFunctions.FunctionChoiceBehavior);
        Assert.NotNull(withFunctions.FunctionChoiceBehavior);
        Assert.NotEqual(withoutFunctions.FunctionChoiceBehavior.GetType(), withFunctions.FunctionChoiceBehavior.GetType());
    }

    private static OpenAIPromptExecutionSettings CreatePromptSettings(AiKernelChatRequest request)
    {
        var method = typeof(AiKernelChatRuntime).GetMethod("CreatePromptSettings", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<OpenAIPromptExecutionSettings>(method.Invoke(null, [request]));
    }

    private static AiModelEndpoint BuildEndpoint() =>
        new()
        {
            ProviderId = "provider-1",
            ProviderCode = "openai-compatible",
            ProtocolType = "OpenAiCompatible",
            BaseUrl = "https://example.invalid/v1",
            ApiKey = "test-key",
            ModelConfigId = "model-1",
            ModelCode = "test-model",
            MaxOutputTokens = 512,
            DefaultTemperature = 0.2m,
            DefaultTopP = 0.9m
        };
}
