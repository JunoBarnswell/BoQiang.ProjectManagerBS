using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.ApplicationDesigner;
using AsterERP.Contracts.Expressions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime;

public static class ExpressionValueContractValidator
{
    private const int MaximumDepth = 64;
    private const int MaximumNodes = 256;

    public static void Validate(ExpressionValueDto expression)
    {
        var state = new ValidationState();
        Visit(expression, "root", 0, state);
        var dependencies = ExpressionValueCanonicalizer.CollectDependencies(expression);
        if (expression.Dependencies.Count > 0 && !expression.Dependencies.SequenceEqual(dependencies, StringComparer.Ordinal)) throw Invalid("Expression dependencies are not canonical.");
        if (!string.IsNullOrWhiteSpace(expression.CanonicalHash) && !string.Equals(expression.CanonicalHash, ExpressionValueCanonicalizer.ComputeHash(expression), StringComparison.Ordinal)) throw Invalid("Expression canonical hash does not match the AST.");
    }

    private static void Visit(ExpressionValueDto node, string path, int depth, ValidationState state)
    {
        if (++state.Count > MaximumNodes) throw Invalid($"Expression node count exceeds {MaximumNodes}: {path}.");
        if (depth > MaximumDepth) throw Invalid($"Expression nesting exceeds {MaximumDepth}: {path}.");
        if (!state.Active.Add(node)) throw Invalid($"Expression graph contains a cycle: {path}.");
        if (!string.Equals(node.Version, "latest", StringComparison.Ordinal)) throw Invalid($"Expression version is not latest: {path}.");
        if (string.IsNullOrWhiteSpace(node.DataType)) throw Invalid($"Expression dataType is required: {path}.");

        switch (node.Kind)
        {
            case "literal": break;
            case "resourceRef": Require(node.ResourceId, "resourceId", path); break;
            case "functionCall":
                Require(node.FunctionId, "functionId", path);
                if (!new RuntimeExpressionHelperCatalog().Supports(node.FunctionId!)) throw Invalid($"Expression function is not registered: {node.FunctionId} ({path}).");
                VisitMany(node.Args, $"{path}.args", depth, state);
                break;
            case "conversion":
                if (node.Input is null) throw Invalid($"Expression conversion input is required: {path}.");
                if (node.Pipeline.Count == 0 || node.Pipeline.Any(step => string.IsNullOrWhiteSpace(step.From) || string.IsNullOrWhiteSpace(step.Name) || string.IsNullOrWhiteSpace(step.To) || !RuntimeCapabilityContract.ConverterNames.Contains(step.Name))) throw Invalid($"Expression conversion pipeline is not registered: {path}.");
                Visit(node.Input, $"{path}.input", depth + 1, state);
                break;
            case "condition":
                VisitRequired(node.When, $"{path}.when", depth, state);
                VisitRequired(node.Then, $"{path}.then", depth, state);
                VisitRequired(node.Otherwise, $"{path}.otherwise", depth, state);
                break;
            case "logic":
                if (node.Operator is not ("and" or "or" or "not") || (node.Operator == "not" ? node.Args.Count != 1 : node.Args.Count < 2)) throw Invalid($"Expression logic arity is invalid: {path}.");
                VisitMany(node.Args, $"{path}.args", depth, state);
                break;
            case "object":
                foreach (var item in node.Properties) Visit(item.Value, $"{path}.properties.{item.Key}", depth + 1, state);
                break;
            case "array":
            case "template":
                VisitMany(node.Items, $"{path}.items", depth, state);
                break;
            case "defaultValue":
                VisitRequired(node.Input, $"{path}.input", depth, state);
                break;
            default: throw Invalid($"Expression node kind is not supported: {node.Kind} ({path}).");
        }

        state.Active.Remove(node);
    }

    private static void VisitMany(IEnumerable<ExpressionValueDto> nodes, string path, int depth, ValidationState state)
    {
        var index = 0;
        foreach (var node in nodes) Visit(node, $"{path}[{index++}]", depth + 1, state);
    }

    private static void VisitRequired(ExpressionValueDto? node, string path, int depth, ValidationState state) => Visit(node ?? throw Invalid($"Expression node is required: {path}."), path, depth + 1, state);

    private static void Require(string? value, string name, string path)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Invalid($"Expression {name} is required: {path}.");
    }

    private static ValidationException Invalid(string message) => new(message, ErrorCodes.ParameterInvalid);

    private sealed class ValidationState
    {
        public int Count { get; set; }

        public HashSet<ExpressionValueDto> Active { get; } = [];
    }
}
