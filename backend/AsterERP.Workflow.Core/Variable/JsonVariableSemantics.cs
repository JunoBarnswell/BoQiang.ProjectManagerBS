namespace AsterERP.Workflow.Core.Variable;

internal static class JsonVariableSemantics
{
    internal const int JsonTextMaxLength = 4000;

    internal static bool IsJsonText(object? value, out string json)
    {
        if (value is not string s)
        {
            json = string.Empty;
            return false;
        }

        var trimmed = s.TrimStart();
        if (!(trimmed.StartsWith("{") || trimmed.StartsWith("[")))
        {
            json = string.Empty;
            return false;
        }

        json = s;
        return true;
    }
}
