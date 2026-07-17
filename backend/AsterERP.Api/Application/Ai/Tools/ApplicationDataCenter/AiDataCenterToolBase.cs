using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Tools.ApplicationDataCenter;

public abstract class AiDataCenterToolBase : IAiKernelFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected AiDataCenterToolBase(AiKernelFunctionDefinition definition)
    {
        Definition = definition;
    }

    public AiKernelFunctionDefinition Definition { get; }

    public abstract Task<AiKernelFunctionResult> ExecuteAsync(AiKernelFunctionContext context, CancellationToken cancellationToken);

    protected static AiKernelFunctionResult Result(string summary, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new AiKernelFunctionResult
        {
            ResultSummary = summary,
            Content = json,
            EvidenceJson = json,
            OutputType = "Json"
        };
    }
}
