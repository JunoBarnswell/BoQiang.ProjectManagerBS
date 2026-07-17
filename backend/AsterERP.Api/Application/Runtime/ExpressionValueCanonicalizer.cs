using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Contracts.Expressions;

namespace AsterERP.Api.Application.Runtime;

public static class ExpressionValueCanonicalizer
{
    public static string Serialize(ExpressionValueDto expression) => BuildNode(expression).ToJsonString();

    public static string ComputeHash(ExpressionValueDto expression) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(expression)))).ToLowerInvariant();

    public static IReadOnlyList<string> CollectDependencies(ExpressionValueDto expression)
    {
        var dependencies = new SortedSet<string>(StringComparer.Ordinal);
        Visit(expression, dependencies);
        return dependencies.ToArray();
    }

    private static JsonObject BuildNode(ExpressionValueDto expression)
    {
        var node = new JsonObject
        {
            ["version"] = expression.Version,
            ["kind"] = expression.Kind,
            ["dataType"] = expression.DataType
        };

        switch (expression.Kind)
        {
            case "literal":
                node["value"] = JsonSerializer.SerializeToNode(expression.Value);
                break;
            case "resourceRef":
                node["resourceId"] = expression.ResourceId;
                break;
            case "functionCall":
                node["functionId"] = expression.FunctionId;
                node["args"] = new JsonArray(expression.Args.Select(BuildNode).ToArray());
                break;
            case "conversion":
                node["input"] = expression.Input is null ? null : BuildNode(expression.Input);
                node["pipeline"] = new JsonArray(expression.Pipeline.Select(step => new JsonObject
                {
                    ["from"] = step.From,
                    ["name"] = step.Name,
                    ["to"] = step.To
                }).ToArray());
                break;
            case "condition":
                node["when"] = expression.When is null ? null : BuildNode(expression.When);
                node["then"] = expression.Then is null ? null : BuildNode(expression.Then);
                node["otherwise"] = expression.Otherwise is null ? null : BuildNode(expression.Otherwise);
                break;
            case "logic":
                node["operator"] = expression.Operator;
                node["args"] = new JsonArray(expression.Args.Select(BuildNode).ToArray());
                break;
            case "object":
                node["properties"] = new JsonObject(expression.Properties.OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary(item => item.Key, item => (JsonNode?)BuildNode(item.Value), StringComparer.Ordinal));
                break;
            case "array":
            case "template":
                node["items"] = new JsonArray(expression.Items.Select(BuildNode).ToArray());
                break;
            case "defaultValue":
                node["input"] = expression.Input is null ? null : BuildNode(expression.Input);
                node["fallback"] = JsonSerializer.SerializeToNode(expression.Fallback);
                break;
        }

        return node;
    }

    private static void Visit(ExpressionValueDto expression, ISet<string> dependencies)
    {
        if (expression.Kind == "resourceRef" && !string.IsNullOrWhiteSpace(expression.ResourceId)) dependencies.Add(expression.ResourceId.Trim());
        foreach (var argument in expression.Args) Visit(argument, dependencies);
        foreach (var item in expression.Items) Visit(item, dependencies);
        foreach (var property in expression.Properties.Values) Visit(property, dependencies);
        if (expression.Input is not null) Visit(expression.Input, dependencies);
        if (expression.When is not null) Visit(expression.When, dependencies);
        if (expression.Then is not null) Visit(expression.Then, dependencies);
        if (expression.Otherwise is not null) Visit(expression.Otherwise, dependencies);
    }
}
