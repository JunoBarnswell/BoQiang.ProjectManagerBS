using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime.ExpressionFunctions;

internal static class RuntimeExpressionFunctionDefinitionFactory
{
    public static RuntimeExpressionFunctionDefinitionDto Create(
        string moduleKey,
        string moduleName,
        string namespaceName,
        string functionName,
        string canonicalName,
        string label,
        string description,
        string returnType,
        IReadOnlyList<RuntimeExpressionFunctionParameterDto>? parameters = null,
        bool requiresInput = true,
        bool sqlEnabled = true,
        bool deterministic = true,
        string disabledReason = "",
        IReadOnlyList<string>? examples = null) =>
        new()
        {
            CanonicalName = canonicalName,
            Description = description,
            Deterministic = deterministic,
            DisabledReason = disabledReason,
            Examples = examples?.ToList() ?? [],
            FunctionName = functionName,
            Label = label,
            ModuleKey = moduleKey,
            ModuleName = moduleName,
            Namespace = namespaceName,
            Parameters = parameters?.ToList() ?? [],
            QualifiedName = $"{namespaceName}.{functionName}",
            RequiresInput = requiresInput,
            ReturnType = returnType,
            SqlEnabled = sqlEnabled
        };

    public static RuntimeExpressionFunctionParameterDto Parameter(
        string name,
        string label,
        string dataType,
        string description = "",
        bool required = true,
        object? defaultValue = null) =>
        new()
        {
            DataType = dataType,
            DefaultValue = defaultValue,
            Description = description,
            Label = label,
            Name = name,
            Required = required
        };
}
