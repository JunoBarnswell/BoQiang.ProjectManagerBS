using System.Text;
using System.Text.Json;
using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseExecutionResultBuilder
{
    internal IReadOnlyList<FlowiseSourceDocumentDto> BuildRuntimeModelSourceDocuments(IReadOnlyList<RuntimeDataModelNodeResult> results) =>
        results.Select(result => new FlowiseSourceDocumentDto
        {
            Content = BuildRuntimeModelSummary(result),
            MetadataJson = JsonSerializer.Serialize(new
            {
                result.NodeId,
                result.NodeLabel,
                result.ModelCode,
                result.Request,
                result.Iteration,
                fields = result.Response.Fields.Select(field => field.FieldCode).ToList()
            }),
            Score = 1,
            SourceId = result.NodeId
        }).ToList();

    internal string BuildRuntimeModelAnswer(IReadOnlyList<RuntimeDataModelNodeResult> results)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var result in results.OrderBy(item => item.ExecutionIndex))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            builder.Append(BuildRuntimeModelSummary(result));
        }

        return builder.ToString();
    }

    internal string BuildRuntimeModelSummary(RuntimeDataModelNodeResult result)
    {
        var visibleFields = result.Response.Fields
            .Where(field => field.Visible)
            .OrderBy(field => field.Order)
            .Take(8)
            .ToList();
        var builder = new StringBuilder();
        builder.Append("系统配置模型 ");
        builder.Append(result.ModelCode);
        builder.Append(" 查询完成：共 ");
        builder.Append(result.Response.Total);
        builder.Append(" 条，当前返回 ");
        builder.Append(result.Response.Rows.Count);
        builder.Append(" 条。");

        if (result.Response.Rows.Count == 0)
        {
            return builder.ToString();
        }

        builder.AppendLine();
        var rowIndex = 1;
        foreach (var row in result.Response.Rows.Take(5))
        {
            var cells = visibleFields
                .Select(field => $"{field.FieldName}={FormatCellValue(row.TryGetValue(field.FieldCode, out var value) ? value : null)}")
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
            builder.Append(rowIndex);
            builder.Append(". ");
            builder.Append(string.Join("；", cells));
            builder.AppendLine();
            rowIndex += 1;
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatCellValue(object? value) =>
        value switch
        {
            null => string.Empty,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element when element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value) ?? string.Empty
        };
}
