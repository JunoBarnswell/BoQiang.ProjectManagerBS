using System.Text.Json;
using System.Text.RegularExpressions;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

/// <summary>Runs the same non-mutating dependency checks used by preview and publish.</summary>
public sealed partial class ApplicationPageRuntimeEnvironmentCheckService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationPageMicroflowBindingValidator pageMicroflowBindingValidator,
    ApplicationDataCenterSqlScriptValidator sqlScriptValidator,
    ApplicationDataSourceService dataSourceService)
{
    public async Task<ApplicationDevelopmentEnvironmentCheckResponse> CheckAsync(
        string documentJson,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ApplicationDevelopmentEnvironmentDiagnostic>();
        if (string.IsNullOrWhiteSpace(documentJson))
        {
            diagnostics.Add(Error("document-empty", "page", "页面设计稿不能为空", "documentJson"));
            return new(false, diagnostics);
        }

        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        JsonDocument document;
        try { document = JsonDocument.Parse(documentJson); }
        catch (JsonException ex)
        {
            diagnostics.Add(Error("document-json-invalid", "page", $"页面设计稿 JSON 无效：{ex.Message}", "documentJson"));
            return new(false, diagnostics);
        }

        using (document)
        {
            ValidateFormResources(document.RootElement, diagnostics);
            try
            {
                await pageMicroflowBindingValidator.ValidateAsync(db, workspace, documentJson, cancellationToken);
            }
            catch (ValidationException ex)
            {
                diagnostics.Add(Error("page-microflow-contract-invalid", "microflow", ex.Message, "pageMicroflows", fixHint: "修复页面微流别名、入参、输出映射或发布状态。"));
            }

            var flowCodes = ReadFlowCodes(document.RootElement);
            if (flowCodes.Count > 0)
            {
                var flows = await db.Queryable<ApplicationMicroflowEntity>()
                    .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode &&
                                   item.ModuleKey == ApplicationDataCenterModuleKey.Microflow && !item.IsDeleted &&
                                   flowCodes.Contains(item.ObjectCode))
                    .ToListAsync(cancellationToken);
                foreach (var flow in flows)
                    await ValidateSqlDependenciesAsync(flow, diagnostics, cancellationToken);
            }
        }

        return new(!diagnostics.Any(item => item.Severity == "error"), diagnostics);
    }

    public async Task EnsurePassedAsync(string documentJson, CancellationToken cancellationToken = default)
    {
        var result = await CheckAsync(documentJson, cancellationToken);
        if (!result.Passed)
            throw new ValidationException(string.Join("；", result.Diagnostics.Where(item => item.Severity == "error").Select(item => item.Message)), AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private async Task ValidateSqlDependenciesAsync(
        ApplicationMicroflowEntity flow,
        List<ApplicationDevelopmentEnvironmentDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        ApplicationMicroflowDefinition definition;
        try { definition = ApplicationMicroflowDefinitionReader.Read(flow.ConfigJson); }
        catch (ValidationException ex)
        {
            diagnostics.Add(Error("microflow-definition-invalid", "microflow", ex.Message, "configJson", flow.ObjectCode));
            return;
        }

        foreach (var node in definition.Nodes.Where(item => string.Equals(item.Type, "return", StringComparison.OrdinalIgnoreCase)))
        {
            var output = ReadOutputSchema(node);
            if (output?.SqlScript is null) continue;
            var script = output.SqlScript;
            var errors = new List<string>();
            sqlScriptValidator.ValidateSqlScript(script, errors, $"微流 {flow.ObjectCode} 输出 {output.VariableCode}");
            foreach (var error in errors)
                diagnostics.Add(Error("sql-script-invalid", "database", error, $"nodes.{node.Id}.outputSchema.sqlScript", flow.ObjectCode));

            var dataSourceId = script.DataSourceId?.Trim();
            if (string.IsNullOrWhiteSpace(dataSourceId))
            {
                diagnostics.Add(Error("sql-data-source-missing", "database", $"微流 {flow.ObjectCode} 的 SQL Script 未配置数据源", $"nodes.{node.Id}.outputSchema.sqlScript.dataSourceId", flow.ObjectCode));
                continue;
            }

            IReadOnlyList<ApplicationDataSourceTableResponse> tables;
            try { tables = await dataSourceService.GetTablesAsync(dataSourceId, cancellationToken); }
            catch (Exception ex) when (ex is ValidationException or InvalidOperationException or IOException)
            {
                diagnostics.Add(Error("data-source-unavailable", "database", $"数据源 {dataSourceId} 无法读取表结构：{ex.Message}", $"nodes.{node.Id}.outputSchema.sqlScript.dataSourceId", flow.ObjectCode, dataSourceId));
                continue;
            }

            var tableNames = tables.Select(item => item.TableName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var table in ReferencedTables(script.Script))
            {
                if (!tableNames.Contains(table))
                    diagnostics.Add(Error("sql-table-missing", "database", $"微流 {flow.ObjectCode} 引用的数据表不存在：{table}", $"nodes.{node.Id}.outputSchema.sqlScript.script", flow.ObjectCode, dataSourceId, table, "在 SQL Script 中修正表名，或在对应数据源创建并发布该表。"));
            }
        }
    }

    private static void ValidateFormResources(JsonElement root, List<ApplicationDevelopmentEnvironmentDiagnostic> diagnostics)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Object)
        {
            foreach (var element in elements.EnumerateObject())
            {
                if (element.Value.TryGetProperty("bindings", out var bindings) && bindings.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("field", out var field) && field.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(field.GetString()))
                    fields.Add(field.GetString()!.Trim());
            }
        }
        foreach (var reference in ReadResourceReferences(root))
        {
            if (!reference.StartsWith("form:", StringComparison.OrdinalIgnoreCase)) continue;
            var field = reference["form:".Length..].Split('.', 2)[0];
            if (!string.IsNullOrWhiteSpace(field) && !fields.Contains(field))
                diagnostics.Add(Error("form-resource-missing", "binding", $"表单资源 {reference} 没有对应的页面控件字段", "pageMicroflows", fixHint: "为该字段添加绑定了 bindings.data.field 的表单控件，或修正微流入参表达式。"));
        }
    }

    private static HashSet<string> ReadFlowCodes(JsonElement root) => root.TryGetProperty("pageMicroflows", out var bindings) && bindings.ValueKind == JsonValueKind.Array
        ? bindings.EnumerateArray().Where(item => item.TryGetProperty("flowCode", out var code) && code.ValueKind == JsonValueKind.String).Select(item => item.GetProperty("flowCode").GetString()?.Trim()).Where(code => !string.IsNullOrWhiteSpace(code)).Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> ReadResourceReferences(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("resourceId") && property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString())) yield return property.Value.GetString()!.Trim();
                foreach (var value in ReadResourceReferences(property.Value)) yield return value;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) foreach (var value in ReadResourceReferences(item)) yield return value;
    }

    private static IEnumerable<string> ReferencedTables(string script) => TableReferenceRegex().Matches(script ?? string.Empty)
        .Select(match => match.Groups[1].Value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault())
        .Where(name => !string.IsNullOrWhiteSpace(name)).Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase);

    private static ApplicationMicroflowOutputSchemaDefinition? ReadOutputSchema(ApplicationMicroflowNodeDefinition node)
    {
        if (!node.Config.TryGetValue("outputSchema", out var raw) || raw is null) return null;
        return raw is JsonElement element
            ? element.Deserialize<ApplicationMicroflowOutputSchemaDefinition>(ApplicationDataCenterJson.Options)
            : JsonSerializer.Deserialize<ApplicationMicroflowOutputSchemaDefinition>(JsonSerializer.Serialize(raw, ApplicationDataCenterJson.Options), ApplicationDataCenterJson.Options);
    }

    private static ApplicationDevelopmentEnvironmentDiagnostic Error(string code, string category, string message, string? path = null, string? flowCode = null, string? dataSourceId = null, string? tableName = null, string? fixHint = null) =>
        new(code, category, "error", message, path, flowCode, dataSourceId, tableName, fixHint);

    [GeneratedRegex(@"\b(?:from|join|into|update|delete\s+from)\s+([A-Za-z_][A-Za-z0-9_\.]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TableReferenceRegex();
}
