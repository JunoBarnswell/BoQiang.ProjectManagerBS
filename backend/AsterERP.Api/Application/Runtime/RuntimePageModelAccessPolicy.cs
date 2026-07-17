using System.Text.Json;
using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime;

public static class RuntimePageModelAccessPolicy
{
    public static bool IncludesModel(RuntimePageSchemaResponse page, string modelCode)
    {
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            return false;
        }

        if (Matches(page.ModelCode, modelCode))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(page.ArtifactJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(page.ArtifactJson);
            return IncludesModel(document.RootElement, modelCode);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IncludesModel(JsonElement root, string modelCode)
    {
        if (RuntimeContextIncludesModel(root, modelCode))
        {
            return true;
        }

        return root.TryGetProperty("document", out var document) &&
            document.ValueKind == JsonValueKind.Object &&
            RuntimeContextIncludesModel(document, modelCode);
    }

    private static bool RuntimeContextIncludesModel(JsonElement root, string modelCode)
    {
        if (!root.TryGetProperty("runtimeContext", out var runtimeContext) ||
            runtimeContext.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (runtimeContext.TryGetProperty("modelCode", out var contextModelCode) &&
            contextModelCode.ValueKind == JsonValueKind.String &&
            Matches(contextModelCode.GetString(), modelCode))
        {
            return true;
        }

        return runtimeContext.TryGetProperty("modelCodes", out var contextModelCodes) &&
            RuntimeContextModelCodesIncludesModel(contextModelCodes, modelCode);
    }

    private static bool RuntimeContextModelCodesIncludesModel(JsonElement modelCodes, string modelCode)
    {
        if (modelCodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in modelCodes.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && Matches(item.GetString(), modelCode))
                {
                    return true;
                }
            }

            return false;
        }

        if (modelCodes.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return modelCodes.GetString()?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => Matches(item, modelCode)) == true;
    }

    private static bool Matches(string? left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
