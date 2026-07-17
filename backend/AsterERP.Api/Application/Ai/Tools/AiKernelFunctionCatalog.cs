using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionCatalog(IEnumerable<IAiKernelFunction> tools)
{
    private readonly IReadOnlyList<IAiKernelFunction> orderedTools = tools
        .OrderBy(item => item.Definition.ToolCode, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<AiKernelFunctionDefinition> ListDefinitions(AiKernelFunctionSelection? selection = null) =>
        orderedTools
            .Where(item => selection is null || selection.Allows(item.Definition))
            .Select(item => item.Definition)
            .ToList();

    public IAiKernelFunction Require(string toolCode, AiKernelFunctionSelection? selection = null)
    {
        var normalized = NormalizeToolCode(toolCode);
        var tool = orderedTools.FirstOrDefault(item => string.Equals(item.Definition.ToolCode, normalized, StringComparison.OrdinalIgnoreCase));
        if (tool is null || (selection is not null && !selection.Allows(tool.Definition)))
        {
            throw new ValidationException($"AI 函数 {normalized} 未在本次运行中注册", ErrorCodes.AiKernelFunctionNotFound);
        }

        return tool;
    }

    private static string NormalizeToolCode(string toolCode)
    {
        var normalized = toolCode.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException("工具编码不能为空", ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }
}
