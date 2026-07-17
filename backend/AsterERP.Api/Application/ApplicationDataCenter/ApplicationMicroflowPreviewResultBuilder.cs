using System.Collections;
using System.Globalization;
using System.Text.Json;
using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowPreviewResultBuilder
{
    private const int DefaultMaxRows = 100;
    private const int MaxAllowedRows = 500;

    public ApplicationMicroflowPreviewResponse Build(
        string mode,
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowExecuteResponse execution,
        int? maxRows,
        string? preferredResultPath)
    {
        var rowLimit = Math.Clamp(maxRows ?? DefaultMaxRows, 1, MaxAllowedRows);
        var datasets = new List<ApplicationMicroflowPreviewDatasetResponse>();
        var datasetPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var previewOutputs = BuildPreviewOutputDefinitions(definition);
        var outputSchemas = BuildOutputSchemaMap(previewOutputs);
        var resultSchemaFields = ResolveResultSchemaFields(previewOutputs);

        AddDataset(datasets, datasetPaths, "result", "返回结果", "result", execution.Result, rowLimit, resultSchemaFields);
        if (!string.IsNullOrWhiteSpace(preferredResultPath))
        {
            var preferredPath = preferredResultPath.Trim();
            AddDataset(
                datasets,
                datasetPaths,
                "preferred",
                $"指定结果 {preferredPath}",
                preferredPath,
                RuntimeExpressionPathReader.Read(BuildPreviewRoot(execution), preferredPath),
                rowLimit,
                ResolveSchemaFieldsForPath(outputSchemas, preferredPath));
        }

        AddVariableDataset(datasets, datasetPaths, execution.Variables, "items", "variables.items", rowLimit, ResolveSchemaFieldsForPath(outputSchemas, "variables.items"));
        AddVariableDataset(datasets, datasetPaths, execution.Variables, "sqlRows", "variables.sqlRows", rowLimit, ResolveSchemaFieldsForPath(outputSchemas, "variables.sqlRows"));
        foreach (var output in previewOutputs)
        {
            var outputCode = output.VariableCode.Trim();
            if (string.IsNullOrWhiteSpace(outputCode))
            {
                continue;
            }

            var outputPath = $"variables.{outputCode}";
            var outputValue = execution.Variables.TryGetValue(outputCode, out var value)
                ? value
                : CreateOutputPlaceholderValue(output);
            AddDataset(
                datasets,
                datasetPaths,
                BuildDatasetKey(outputPath),
                string.IsNullOrWhiteSpace(output.VariableName) ? outputCode : output.VariableName,
                outputPath,
                outputValue,
                rowLimit,
                output.Fields);
        }

        foreach (var variable in execution.Variables.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(variable.Key, "items", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(variable.Key, "sqlRows", StringComparison.OrdinalIgnoreCase) ||
                outputSchemas.ContainsKey(variable.Key))
            {
                continue;
            }

            if (IsCollectionCandidate(variable.Value))
            {
                AddVariableDataset(
                    datasets,
                    datasetPaths,
                    execution.Variables,
                    variable.Key,
                    $"variables.{variable.Key}",
                    rowLimit,
                    null);
            }
        }

        var primaryDatasetKey = ResolvePrimaryDatasetKey(datasets, preferredResultPath);
        return new ApplicationMicroflowPreviewResponse(
            execution.FlowCode,
            mode,
            datasets.Count == 0 ? "预览执行完成，未识别到可表格展示的数据集" : "预览执行完成",
            primaryDatasetKey,
            datasets,
            BuildTrace(definition, execution.Trace),
            BuildVariables(execution.Variables, datasetPaths),
            execution);
    }

    private static void AddVariableDataset(
        List<ApplicationMicroflowPreviewDatasetResponse> datasets,
        Dictionary<string, string> datasetPaths,
        IReadOnlyDictionary<string, object?> variables,
        string variableName,
        string sourcePath,
        int rowLimit,
        IReadOnlyList<ApplicationMicroflowFieldDefinition>? schemaFields)
    {
        if (!variables.TryGetValue(variableName, out var value))
        {
            return;
        }

        AddDataset(
            datasets,
            datasetPaths,
            BuildDatasetKey(sourcePath),
            variableName,
            sourcePath,
            value,
            rowLimit,
            schemaFields);
    }

    private static void AddDataset(
        List<ApplicationMicroflowPreviewDatasetResponse> datasets,
        Dictionary<string, string> datasetPaths,
        string key,
        string title,
        string sourcePath,
        object? value,
        int rowLimit,
        IReadOnlyList<ApplicationMicroflowFieldDefinition>? schemaFields = null)
    {
        if (datasets.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var dataset = TryBuildDataset(key, title, sourcePath, value, rowLimit, schemaFields);
        if (dataset is null)
        {
            return;
        }

        datasets.Add(dataset);
        datasetPaths[sourcePath] = dataset.Key;
    }

    private static ApplicationMicroflowPreviewDatasetResponse? TryBuildDataset(
        string key,
        string title,
        string sourcePath,
        object? value,
        int rowLimit,
        IReadOnlyList<ApplicationMicroflowFieldDefinition>? schemaFields)
    {
        value = NormalizeJsonValue(value);
        var configuredFields = MapSchemaFields(schemaFields);
        if (value is null)
        {
            return configuredFields.Count == 0
                ? null
                : new ApplicationMicroflowPreviewDatasetResponse(
                    key,
                    title,
                    sourcePath,
                    [],
                    configuredFields,
                    0,
                    false);
        }

        if (value is ApplicationDataCenterPreviewResponse preview)
        {
            var rows = TakeRows(preview.Rows, rowLimit, out var truncated);
            return new ApplicationMicroflowPreviewDatasetResponse(
                key,
                title,
                sourcePath,
                rows,
                configuredFields.Count == 0 ? preview.Fields : configuredFields,
                preview.Rows.Count,
                truncated);
        }

        if (value is RuntimeQueryResponse query)
        {
            var rows = TakeRows(query.Rows, rowLimit, out var truncated);
            return new ApplicationMicroflowPreviewDatasetResponse(
                key,
                title,
                sourcePath,
                rows,
                configuredFields.Count == 0 ? query.Fields.Select(MapRuntimeField).ToArray() : configuredFields,
                query.Total,
                truncated || query.Total > rows.Count);
        }

        if (value is RuntimeDetailResponse detail)
        {
            var row = NormalizeRow(detail.Row);
            return new ApplicationMicroflowPreviewDatasetResponse(
                key,
                title,
                sourcePath,
                [row],
                configuredFields.Count == 0 ? detail.Fields.Select(MapRuntimeField).ToArray() : configuredFields,
                1,
                false);
        }

        if (IsDictionary(value))
        {
            var row = NormalizeRow(value);
            return new ApplicationMicroflowPreviewDatasetResponse(
                key,
                title,
                sourcePath,
                [row],
                configuredFields.Count == 0 ? InferFields([row]) : configuredFields,
                1,
                false);
        }

        if (!IsCollectionCandidate(value))
        {
            return null;
        }

        var normalizedRows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var item in (IEnumerable)value)
        {
            normalizedRows.Add(NormalizeRow(item));
        }

        var displayedRows = normalizedRows.Take(rowLimit).ToArray();
        return new ApplicationMicroflowPreviewDatasetResponse(
            key,
            title,
            sourcePath,
            displayedRows,
            configuredFields.Count == 0 ? InferFields(displayedRows) : configuredFields,
            normalizedRows.Count,
            normalizedRows.Count > displayedRows.Length);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> TakeRows(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        int rowLimit,
        out bool truncated)
    {
        truncated = rows.Count > rowLimit;
        return rows
            .Take(rowLimit)
            .Select(NormalizeRow)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, object?> NormalizeRow(object? value)
    {
        value = NormalizeJsonValue(value);
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.ToDictionary(
                item => item.Key,
                item => NormalizeCell(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(
                item => item.Key,
                item => NormalizeCell(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        if (value is IDictionary nonGenericDictionary)
        {
            return nonGenericDictionary
                .Cast<DictionaryEntry>()
                .ToDictionary(
                    item => item.Key?.ToString() ?? string.Empty,
                    item => NormalizeCell(item.Value),
                    StringComparer.OrdinalIgnoreCase);
        }

        if (IsScalar(value))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = NormalizeCell(value)
            };
        }

        return value?
            .GetType()
            .GetProperties()
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToDictionary(
                property => property.Name,
                property => NormalizeCell(property.GetValue(value)),
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static object? NormalizeCell(object? value)
    {
        return ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(value);
    }

    private static IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> InferFields(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var fieldCodes = rows
            .SelectMany(row => row.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return fieldCodes
            .Select((fieldCode, index) => new ApplicationDataCenterPreviewFieldResponse(
                fieldCode,
                fieldCode,
                InferDataType(rows.Select(row => row.TryGetValue(fieldCode, out var value) ? value : null)),
                true,
                string.Equals(fieldCode, "id", StringComparison.OrdinalIgnoreCase),
                index + 1))
            .ToArray();
    }

    private static ApplicationDataCenterPreviewFieldResponse MapRuntimeField(RuntimeDataFieldResponse field) =>
        new(
            field.FieldCode,
            field.FieldName,
            field.DataType,
            !field.Required,
            string.Equals(field.FieldCode, "id", StringComparison.OrdinalIgnoreCase),
            field.Order);

    private static IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> MapSchemaFields(
        IReadOnlyList<ApplicationMicroflowFieldDefinition>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return [];
        }

        var visibleFields = fields.Where(field => field.Visible).ToArray();
        var sourceFields = visibleFields.Length == 0 ? fields : visibleFields;
        return sourceFields
            .Where(field => !string.IsNullOrWhiteSpace(field.FieldCode))
            .Select((field, index) => new ApplicationDataCenterPreviewFieldResponse(
                field.FieldCode.Trim(),
                string.IsNullOrWhiteSpace(field.FieldName) ? field.FieldCode.Trim() : field.FieldName.Trim(),
                string.IsNullOrWhiteSpace(field.DataType) ? "Text" : field.DataType.Trim(),
                !field.Required,
                string.Equals(field.FieldCode, "id", StringComparison.OrdinalIgnoreCase),
                index + 1))
            .ToArray();
    }

    private static Dictionary<string, IReadOnlyList<ApplicationMicroflowFieldDefinition>> BuildOutputSchemaMap(
        IReadOnlyList<ApplicationMicroflowVariableDefinition> outputs) =>
        outputs
            .Where(output => !string.IsNullOrWhiteSpace(output.VariableCode) && output.Fields.Count > 0)
            .GroupBy(output => output.VariableCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ApplicationMicroflowFieldDefinition>)group.First().Fields,
                StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<ApplicationMicroflowVariableDefinition> BuildPreviewOutputDefinitions(
        ApplicationMicroflowDefinition definition)
    {
        var result = new List<ApplicationMicroflowVariableDefinition>();
        var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var returnOutput in definition.Nodes
            .Where(node => string.Equals(node.Type, "return", StringComparison.OrdinalIgnoreCase))
            .Select(TryReadNodeOutputSchema)
            .Where(item => item is not null))
        {
            if (returnOutput is null || string.IsNullOrWhiteSpace(returnOutput.VariableCode) || !usedCodes.Add(returnOutput.VariableCode.Trim()))
            {
                continue;
            }

            result.Add(returnOutput);
        }

        foreach (var nodeOutput in definition.Nodes
            .Where(node => !string.Equals(node.Type, "return", StringComparison.OrdinalIgnoreCase))
            .Select(TryReadNodeOutputSchema)
            .Where(item => item is not null))
        {
            if (nodeOutput is null || string.IsNullOrWhiteSpace(nodeOutput.VariableCode) || !usedCodes.Add(nodeOutput.VariableCode.Trim()))
            {
                continue;
            }

            result.Add(nodeOutput);
        }

        foreach (var output in definition.Outputs)
        {
            if (string.IsNullOrWhiteSpace(output.VariableCode) || !usedCodes.Add(output.VariableCode.Trim()))
            {
                continue;
            }

            result.Add(output);
        }

        return result;
    }

    private static ApplicationMicroflowVariableDefinition? TryReadNodeOutputSchema(ApplicationMicroflowNodeDefinition node)
    {
        if (NodeCannotPublishOutputSchema(node.Type) ||
            !node.Config.TryGetValue("outputSchema", out var rawSchema))
        {
            return null;
        }

        var schema = ToObjectDictionary(rawSchema);
        if (schema is null)
        {
            return null;
        }

        var variableCode = ReadString(schema, "variableCode");
        if (string.IsNullOrWhiteSpace(variableCode))
        {
            return null;
        }

        return new ApplicationMicroflowVariableDefinition
        {
            DefaultValue = CreateOutputDefaultValue(ReadString(schema, "valueType")),
            Fields = ReadSchemaFields(schema.TryGetValue("fields", out var fields) ? fields : null),
            ValueType = string.IsNullOrWhiteSpace(ReadString(schema, "valueType")) ? InferNodeOutputValueType(node.Type) : ReadString(schema, "valueType"),
            VariableCode = variableCode.Trim(),
            VariableName = string.IsNullOrWhiteSpace(ReadString(schema, "variableName")) ? variableCode.Trim() : ReadString(schema, "variableName").Trim()
        };
    }

    private static bool NodeCannotPublishOutputSchema(string nodeType) =>
        string.Equals(nodeType, "start", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(nodeType, "end", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(nodeType, "decision", StringComparison.OrdinalIgnoreCase);

    private static string InferNodeOutputValueType(string nodeType)
    {
        if (string.Equals(nodeType, "detail", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeType, "compositeDetail", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeType, "create", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeType, "change", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeType, "delete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeType, "compositeDelete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeType, "loop", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(nodeType, "setVariable", StringComparison.OrdinalIgnoreCase))
        {
            return "object";
        }

        return "array";
    }

    private static IReadOnlyList<ApplicationMicroflowFieldDefinition>? ResolveResultSchemaFields(
        IReadOnlyList<ApplicationMicroflowVariableDefinition> outputs)
    {
        var structuredOutputs = outputs
            .Where(output =>
                output.Fields.Count > 0 &&
                (string.Equals(output.ValueType, "array", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(output.ValueType, "object", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return structuredOutputs.Length == 1 ? structuredOutputs[0].Fields : null;
    }

    private static IReadOnlyList<ApplicationMicroflowFieldDefinition>? ResolveSchemaFieldsForPath(
        IReadOnlyDictionary<string, IReadOnlyList<ApplicationMicroflowFieldDefinition>> outputSchemas,
        string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        var normalized = sourcePath.Trim();
        const string variablePrefix = "variables.";
        if (!normalized.StartsWith(variablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var variableCode = normalized[variablePrefix.Length..];
        return outputSchemas.TryGetValue(variableCode, out var fields) ? fields : null;
    }

    private static object? CreateOutputPlaceholderValue(ApplicationMicroflowVariableDefinition output)
    {
        if (output.DefaultValue is not null)
        {
            return output.DefaultValue;
        }

        if (string.Equals(output.ValueType, "array", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        if (string.Equals(output.ValueType, "object", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    private static object? CreateOutputDefaultValue(string valueType)
    {
        if (string.Equals(valueType, "array", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        if (string.Equals(valueType, "object", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    private static List<ApplicationMicroflowFieldDefinition> ReadSchemaFields(object? value)
    {
        value = NormalizeJsonValue(value);
        if (value is not IEnumerable enumerable || value is string)
        {
            return [];
        }

        var result = new List<ApplicationMicroflowFieldDefinition>();
        foreach (var item in enumerable)
        {
            var field = ToObjectDictionary(item);
            if (field is null)
            {
                continue;
            }

            var fieldCode = ReadString(field, "fieldCode");
            if (string.IsNullOrWhiteSpace(fieldCode))
            {
                continue;
            }

            result.Add(new ApplicationMicroflowFieldDefinition
            {
                DataType = string.IsNullOrWhiteSpace(ReadString(field, "dataType")) ? "string" : ReadString(field, "dataType"),
                FieldCode = fieldCode.Trim(),
                FieldName = string.IsNullOrWhiteSpace(ReadString(field, "fieldName")) ? fieldCode.Trim() : ReadString(field, "fieldName").Trim(),
                ReadOnly = ReadBoolean(field, "readOnly"),
                Required = ReadBoolean(field, "required"),
                Visible = !field.TryGetValue("visible", out var visible) || ReadBooleanValue(visible),
                Writable = !field.TryGetValue("writable", out var writable) || ReadBooleanValue(writable)
            });
        }

        return result;
    }

    private static IReadOnlyList<ApplicationMicroflowPreviewTraceItemResponse> BuildTrace(
        ApplicationMicroflowDefinition definition,
        IReadOnlyList<string> trace)
    {
        var nodes = definition.Nodes.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        return trace.Select((entry, index) =>
        {
            var parts = entry.Split(':', 2, StringSplitOptions.TrimEntries);
            var nodeType = parts.Length > 0 ? parts[0] : string.Empty;
            var nodeId = parts.Length > 1 ? parts[1] : entry;
            var node = nodes.TryGetValue(nodeId, out var matched) ? matched : null;
            return new ApplicationMicroflowPreviewTraceItemResponse(
                index + 1,
                node?.Id ?? nodeId,
                string.IsNullOrWhiteSpace(node?.Name) ? nodeId : node.Name,
                string.IsNullOrWhiteSpace(node?.Type) ? nodeType : node.Type);
        }).ToArray();
    }

    private static IReadOnlyList<ApplicationMicroflowPreviewVariableResponse> BuildVariables(
        IReadOnlyDictionary<string, object?> variables,
        IReadOnlyDictionary<string, string> datasetPaths) =>
        variables
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var path = $"variables.{item.Key}";
                datasetPaths.TryGetValue(path, out var datasetKey);
                return new ApplicationMicroflowPreviewVariableResponse(
                    item.Key,
                    DescribeValueType(item.Value),
                    DescribeValue(item.Value),
                    datasetKey);
            })
            .ToArray();

    private static Dictionary<string, object?> BuildPreviewRoot(ApplicationMicroflowExecuteResponse execution) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["result"] = execution.Result,
            ["variables"] = execution.Variables
        };

    private static string? ResolvePrimaryDatasetKey(
        IReadOnlyList<ApplicationMicroflowPreviewDatasetResponse> datasets,
        string? preferredResultPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredResultPath))
        {
            var preferred = datasets.FirstOrDefault(item => string.Equals(item.SourcePath, preferredResultPath.Trim(), StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred.Key;
            }
        }

        return datasets.FirstOrDefault(item => string.Equals(item.Key, "result", StringComparison.OrdinalIgnoreCase))?.Key
            ?? datasets.FirstOrDefault(item => string.Equals(item.SourcePath, "variables.items", StringComparison.OrdinalIgnoreCase))?.Key
            ?? datasets.FirstOrDefault()?.Key;
    }

    private static string BuildDatasetKey(string sourcePath) => sourcePath;

    private static string InferDataType(IEnumerable<object?> values)
    {
        var firstValue = values.FirstOrDefault(value => value is not null);
        firstValue = ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(firstValue);
        return firstValue switch
        {
            null => "Text",
            bool => "Boolean",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "Number",
            DateTime or DateTimeOffset => "DateTime",
            _ when IsDictionary(firstValue) => "object",
            IEnumerable when firstValue is not string => "array",
            _ => "Text"
        };
    }

    private static string DescribeValueType(object? value)
    {
        value = NormalizeJsonValue(value);
        return value switch
        {
            null => "null",
            string => "string",
            bool => "boolean",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "number",
            DateTime or DateTimeOffset => "datetime",
            _ when IsDictionary(value) => "object",
            IEnumerable when value is not string => "array",
            _ => "object"
        };
    }

    private static string DescribeValue(object? value)
    {
        value = NormalizeJsonValue(value);
        if (value is null)
        {
            return "null";
        }

        if (value is string text)
        {
            return text.Length > 120 ? text[..120] + "..." : text;
        }

        if (IsScalar(value))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (IsDictionary(value))
        {
            return $"对象({CountDictionaryItems(value)})";
        }

        if (value is IEnumerable enumerable and not string)
        {
            return $"数组({CountEnumerableItems(enumerable)})";
        }

        return value.ToString() ?? string.Empty;
    }

    private static bool IsCollectionCandidate(object? value) =>
        value is IEnumerable and not string && !IsDictionary(value);

    private static bool IsDictionary(object? value) =>
        value is IReadOnlyDictionary<string, object?> ||
        value is IDictionary<string, object?> ||
        value is IDictionary;

    private static bool IsScalar(object? value) =>
        value is null ||
        value is string ||
        value is bool ||
        value is char ||
        value is DateTime ||
        value is DateTimeOffset ||
        value.GetType().IsPrimitive ||
        value is decimal;

    private static int CountDictionaryItems(object value) =>
        value switch
        {
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.Count,
            IDictionary<string, object?> dictionary => dictionary.Count,
            IDictionary nonGenericDictionary => nonGenericDictionary.Count,
            _ => 0
        };

    private static int CountEnumerableItems(IEnumerable enumerable)
    {
        if (enumerable is ICollection collection)
        {
            return collection.Count;
        }

        var count = 0;
        foreach (var _ in enumerable)
        {
            count += 1;
        }

        return count;
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    item => item.Name,
                    item => NormalizeJsonValue(item.Value),
                    StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    private static IReadOnlyDictionary<string, object?>? ToObjectDictionary(object? value)
    {
        value = NormalizeJsonValue(value);
        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }

        if (value is IDictionary nonGenericDictionary)
        {
            return nonGenericDictionary
                .Cast<DictionaryEntry>()
                .ToDictionary(
                    item => item.Key?.ToString() ?? string.Empty,
                    item => item.Value,
                    StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    private static string ReadString(IReadOnlyDictionary<string, object?> dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) ? Convert.ToString(NormalizeJsonValue(value), CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static bool ReadBoolean(IReadOnlyDictionary<string, object?> dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) && ReadBooleanValue(value);

    private static bool ReadBooleanValue(object? value)
    {
        value = NormalizeJsonValue(value);
        return value switch
        {
            bool boolean => boolean,
            string text => bool.TryParse(text, out var parsed) && parsed,
            byte or sbyte or short or ushort or int or uint or long or ulong => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0,
            _ => false
        };
    }
}
