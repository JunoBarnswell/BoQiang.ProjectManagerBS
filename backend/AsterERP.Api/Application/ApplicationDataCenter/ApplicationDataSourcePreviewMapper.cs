using System.Data;
using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static class ApplicationDataSourcePreviewMapper
{
    public static ApplicationDataCenterPreviewResponse Map(DataTable dataTable, string message)
    {
        var fields = dataTable.Columns.Cast<DataColumn>()
            .Select((column, index) => new ApplicationDataCenterPreviewFieldResponse(column.ColumnName, column.ColumnName, column.DataType.Name, true, false, index + 1))
            .ToArray();
        var rows = dataTable.Rows.Cast<DataRow>()
            .Select(row => (IReadOnlyDictionary<string, object?>)dataTable.Columns.Cast<DataColumn>()
                .ToDictionary(column => column.ColumnName, column => row[column] is DBNull ? null : row[column], StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return new ApplicationDataCenterPreviewResponse(rows, fields, message);
    }
}
