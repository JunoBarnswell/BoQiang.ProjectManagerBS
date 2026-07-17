using System.Text.RegularExpressions;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Infrastructure.QueryViews;

public static partial class QueryViewNames
{
    public static string StableViewName(string viewCode)
    {
        return $"vw_qry_{NormalizeViewCode(viewCode)}";
    }

    public static string VersionViewName(string viewCode, int versionNo)
    {
        return $"{StableViewName(viewCode)}_v{Math.Max(1, versionNo)}";
    }

    public static string NormalizeViewCode(string viewCode)
    {
        var normalized = viewCode.Trim();
        if (!ViewCodeRegex().IsMatch(normalized))
        {
            throw new ValidationException("视图编码只能包含字母、数字、下划线");
        }

        return normalized;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex ViewCodeRegex();
}
