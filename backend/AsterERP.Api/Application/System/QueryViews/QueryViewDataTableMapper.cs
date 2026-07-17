using System.Data;

namespace AsterERP.Api.Application.System.QueryViews;

public static class QueryViewDataTableMapper
{
    public static IReadOnlyList<Dictionary<string, object?>> ToRows(DataTable table)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (DataRow row in table.Rows)
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn column in table.Columns)
            {
                var value = row[column];
                item[column.ColumnName] = value == DBNull.Value ? null : value;
            }

            rows.Add(item);
        }

        return rows;
    }
}
