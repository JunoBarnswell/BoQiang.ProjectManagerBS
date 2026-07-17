using System.Text.Json.Nodes;
using AsterERP.Contracts.System.Printing;

namespace AsterERP.Api.Application.System.Printing;

internal static class PrintVariableTreeBuilder
{
    public static IReadOnlyList<PrintVariableNodeResponse> Build(JsonObject testData)
    {
        return testData
            .Select(entry => BuildNode(entry.Key, entry.Key, entry.Value))
            .Where(node => node is not null)
            .Select(node => node!)
            .ToList();
    }

    private static PrintVariableNodeResponse? BuildNode(string id, string label, JsonNode? node)
    {
        return node switch
        {
            JsonObject obj => new PrintVariableNodeResponse(
                id,
                label,
                false,
                obj.Select(entry => BuildNode($"{id}.{entry.Key}", entry.Key, entry.Value))
                    .Where(child => child is not null)
                    .Select(child => child!)
                    .ToList()),
            JsonArray array => BuildArrayNode(id, label, array),
            _ => new PrintVariableNodeResponse(id, label, false, [])
        };
    }

    private static PrintVariableNodeResponse BuildArrayNode(string id, string label, JsonArray array)
    {
        var firstChildObject = array
            .OfType<JsonObject>()
            .FirstOrDefault();
        var children = firstChildObject is null
            ? []
            : firstChildObject
                .Select(entry => BuildNode($"{id}.{entry.Key}", entry.Key, entry.Value))
                .Where(child => child is not null)
                .Select(child => child!)
                .ToList();

        return new PrintVariableNodeResponse(id, label, true, children);
    }
}
