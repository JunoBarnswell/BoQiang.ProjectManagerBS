using System.Text;

namespace AsterERP.Contracts.ApplicationDataCenter;

public static class ApplicationDataResourceId
{
    public static string Table(string dataSourceId, string? schemaName, string objectName) =>
        Build("table", dataSourceId, schemaName ?? string.Empty, objectName);

    public static string Column(string tableResourceId, string columnName) =>
        $"{tableResourceId}:column:{Encode(columnName)}";

    public static string MappingCacheColumn(string cacheId, string targetName) =>
        $"mapping-cache:{Encode(cacheId)}:column:{Encode(targetName)}";

    public static string MappingCacheParameter(string cacheId, string parameterName) =>
        $"mapping-cache:{Encode(cacheId)}:parameter:{Encode(parameterName)}";

    private static string Build(string kind, params string[] segments) =>
        $"data:{kind}:{string.Join(':', segments.Select(Encode))}";

    private static string Encode(string value) =>
        Convert.ToHexString(Encoding.UTF8.GetBytes(value.Trim())).ToLowerInvariant();
}
