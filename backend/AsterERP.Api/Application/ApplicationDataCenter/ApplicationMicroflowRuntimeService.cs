using System.Text.Json;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowRuntimeService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IRuntimeDataModelService runtimeDataModelService,
    RuntimeValueExpressionEvaluator expressionEvaluator,
    ApplicationDataCenterSqlScriptEngine sqlScriptEngine,
    ApplicationDataCenterSqlScriptValidator sqlScriptValidator,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataPreviewReader previewReader,
    ICurrentUser currentUser,
    IHttpClientFactory httpClientFactory,
    ILogger<ApplicationMicroflowRuntimeService> logger) : IApplicationMicroflowRuntimeService
{
    private const int MaxSteps = 500;

    public async Task<ApplicationMicroflowExecuteResponse> ExecuteAsync(
        string flowCode,
        ApplicationMicroflowExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        ApplicationMicroflowEntity? entity = null;
        try
        {
            logger.LogDebug(
                "Microflow execution starting. FlowCode={FlowCode} StartNodeId={StartNodeId} InputVariableCount={VariableCount}",
                flowCode,
                request.StartNodeId,
                request.Variables?.Count ?? 0);

            var workspace = workspaceResolver.Resolve();
            var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
            entity = (await db.Queryable<ApplicationMicroflowEntity>()
                .Where(item =>
                    item.ObjectCode == flowCode &&
                    item.ModuleKey == ApplicationDataCenterModuleKey.Microflow &&
                    item.Status == ApplicationDataCenterObjectStatus.Published &&
                    !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault()
                ?? throw new NotFoundException("微流不存在或未发布", ErrorCodes.ApplicationDataCenterObjectNotFound);
            var definition = ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson);
            return await ExecuteDefinitionCoreAsync(flowCode, definition, request, entity.Id, elapsed, cancellationToken);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Microflow execution rejected. FlowCode={FlowCode} FlowId={FlowId} ElapsedMs={ElapsedMs}",
                flowCode,
                entity?.Id,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Microflow execution failed. FlowCode={FlowCode} FlowId={FlowId} ElapsedMs={ElapsedMs}",
                flowCode,
                entity?.Id,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<ApplicationMicroflowExecuteResponse> ExecuteDefinitionAsync(
        string flowCode,
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            logger.LogDebug(
                "Microflow draft execution starting. FlowCode={FlowCode} StartNodeId={StartNodeId} InputVariableCount={VariableCount}",
                flowCode,
                request.StartNodeId,
                request.Variables?.Count ?? 0);

            return await ExecuteDefinitionCoreAsync(flowCode, definition, request, null, elapsed, cancellationToken);
        }
        catch (ValidationException exception)
        {
            logger.LogWarning(
                exception,
                "Microflow draft execution rejected. FlowCode={FlowCode} ElapsedMs={ElapsedMs}",
                flowCode,
                elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Microflow draft execution failed. FlowCode={FlowCode} ElapsedMs={ElapsedMs}",
                flowCode,
                elapsed.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task<ApplicationMicroflowExecuteResponse> ExecuteDefinitionCoreAsync(
        string flowCode,
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowExecuteRequest request,
        string? flowId,
        Stopwatch elapsed,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object?>(request.Variables ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var variable in definition.Variables.Concat(definition.Inputs).Concat(definition.Outputs))
        {
            if (!variables.ContainsKey(variable.VariableCode))
            {
                variables[variable.VariableCode] = variable.DefaultValue;
            }
        }

        foreach (var variable in ApplicationMicroflowGlobalVariableNodeReader.ReadVariables(definition))
        {
            if (!string.IsNullOrWhiteSpace(variable.VariableCode))
            {
                variables[variable.VariableCode] = variable.DefaultValue;
            }
        }

        variables["auditNow"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        variables["auditUserId"] = currentUser.GetAsterErpUserId();
        variables["currentEmploymentId"] = currentUser.GetAsterErpEmploymentId();
        variables["currentDeptId"] = currentUser.GetAsterErpDeptId();
        variables["currentDeptIds"] = currentUser.GetAsterErpDeptIds();
        variables["currentPositionId"] = currentUser.GetAsterErpPositionId();
        variables["currentPositionIds"] = currentUser.GetAsterErpPositionIds();

        var trace = new List<string>();
        var node = ResolveStartNode(definition, request.StartNodeId);
        var context = new MicroflowRuntimeExecutionContext(
            request.PageCode,
            request.PreviewPageId,
            request.ModelCode,
            request.Action);
        object? result = null;
        for (var step = 0; node is not null && step < MaxSteps; step += 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ApplicationMicroflowGlobalVariableNodeReader.IsGlobalVariableNode(node))
            {
                node = ResolveNextNode(definition, node, null);
                continue;
            }

            logger.LogDebug(
                "Microflow node executing. FlowCode={FlowCode} NodeId={NodeId} NodeType={NodeType} Step={Step} VariableCount={VariableCount}",
                flowCode,
                node.Id,
                node.Type,
                step,
                variables.Count);
            trace.Add($"{node.Type}:{node.Id}");
            var outcome = await ExecuteNodeAsync(definition, node, variables, context, cancellationToken);
            if (outcome.Stop)
            {
                result = outcome.Result;
                logger.LogInformation(
                    "Microflow execution completed. FlowCode={FlowCode} FlowId={FlowId} Steps={StepCount} VariableCount={VariableCount} ElapsedMs={ElapsedMs}",
                    flowCode,
                    flowId,
                    trace.Count,
                    variables.Count,
                    elapsed.ElapsedMilliseconds);
                return new ApplicationMicroflowExecuteResponse(flowCode, result, variables, trace);
            }

            result = outcome.Result ?? result;
            node = ResolveNextNode(definition, node, outcome.Branch);
        }

        if (trace.Count >= MaxSteps)
        {
            throw new ValidationException("微流执行超过最大步数，可能存在循环未退出", ErrorCodes.ApplicationDataCenterRuntimeFailed);
        }

        logger.LogInformation(
            "Microflow execution completed. FlowCode={FlowCode} FlowId={FlowId} Steps={StepCount} VariableCount={VariableCount} ElapsedMs={ElapsedMs}",
            flowCode,
            flowId,
            trace.Count,
            variables.Count,
            elapsed.ElapsedMilliseconds);
        return new ApplicationMicroflowExecuteResponse(flowCode, result, variables, trace);
    }

    private async Task<NodeOutcome> ExecuteNodeAsync(
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        MicroflowRuntimeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var type = node.Type.Trim();
        if (ApplicationMicroflowGlobalVariableNodeReader.IsGlobalVariableNode(node))
        {
            return NodeOutcome.Continue();
        }

        if (type.Equals("start", StringComparison.OrdinalIgnoreCase) || type.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            return type.Equals("end", StringComparison.OrdinalIgnoreCase)
                ? NodeOutcome.StopWith(null)
                : NodeOutcome.Continue();
        }

        if (type.Equals("return", StringComparison.OrdinalIgnoreCase))
        {
            return NodeOutcome.StopWith(await ExecuteReturnNodeAsync(definition, node, variables, cancellationToken));
        }

        if (type.Equals("decision", StringComparison.OrdinalIgnoreCase))
        {
            var branch = EvaluateDecisionNode(node, variables) ? "true" : "false";
            logger.LogDebug(
                "Microflow decision resolved. NodeId={NodeId} Branch={Branch}",
                node.Id,
                branch);
            return NodeOutcome.ForBranch(branch);
        }

        if (type.Equals("loop", StringComparison.OrdinalIgnoreCase))
        {
            await ExecuteLoopAsync(definition, node, variables, context, cancellationToken);
            return NodeOutcome.ForBranch("done");
        }

        if (type.Equals("setVariable", StringComparison.OrdinalIgnoreCase))
        {
            var variableCode = RequiredString(node, "variableCode");
            RuntimeExpressionPathWriter.Write(variables, variableCode, EvaluateNodeExpression(node, "valueExpression", variables));
            logger.LogDebug(
                "Microflow variable assigned. NodeId={NodeId} VariableCode={VariableCode}",
                node.Id,
                variableCode);
            return NodeOutcome.Continue();
        }

        if (type.Equals("retrieve", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("query", StringComparison.OrdinalIgnoreCase))
        {
            var response = await runtimeDataModelService.QueryAsync(
                RequiredString(node, "modelCode"),
                BuildQuery(node, variables, context),
                cancellationToken);
            WriteTarget(node, variables, response.Rows);
            logger.LogDebug(
                "Microflow query node completed. NodeId={NodeId} NodeType={NodeType} ModelCode={ModelCode} RowCount={RowCount}",
                node.Id,
                type,
                RequiredString(node, "modelCode"),
                response.Rows.Count);
            return NodeOutcome.WithResult(response.Rows);
        }

        if (type.Equals("detail", StringComparison.OrdinalIgnoreCase))
        {
            var modelCode = RequiredString(node, "modelCode");
            var id = RequiredText(EvaluateNodeExpression(node, "idExpression", variables), "Detail 节点缺少主键表达式结果");
            var response = await runtimeDataModelService.GetDetailAsync(modelCode, id, cancellationToken);
            WriteTarget(node, variables, response.Row);
            logger.LogDebug(
                "Microflow detail node completed. NodeId={NodeId} ModelCode={ModelCode} DataId={DataId}",
                node.Id,
                modelCode,
                id);
            return NodeOutcome.WithResult(response.Row);
        }

        if (type.Equals("compositeDetail", StringComparison.OrdinalIgnoreCase))
        {
            var request = BuildCompositeDetailRequest(node, variables, context);
            var response = await runtimeDataModelService.GetCompositeDetailAsync(
                request,
                cancellationToken);
            WriteTarget(node, variables, response);
            logger.LogDebug(
                "Microflow composite detail node completed. NodeId={NodeId} RootModelCode={RootModelCode} RootId={RootId} ChildGroupCount={ChildGroupCount}",
                node.Id,
                request.RootModelCode,
                request.RootId,
                response.Children.Count);
            return NodeOutcome.WithResult(response);
        }

        if (type.Equals("create", StringComparison.OrdinalIgnoreCase))
        {
            var response = await runtimeDataModelService.CreateAsync(
                RequiredString(node, "modelCode"),
                BuildValues(node, variables),
                cancellationToken);
            WriteTarget(node, variables, response.Row);
            logger.LogDebug(
                "Microflow create node completed. NodeId={NodeId} ModelCode={ModelCode} DataId={DataId}",
                node.Id,
                RequiredString(node, "modelCode"),
                response.Id);
            return NodeOutcome.WithResult(response.Row);
        }

        if (type.Equals("compositeCreate", StringComparison.OrdinalIgnoreCase))
        {
            var response = await runtimeDataModelService.CreateCompositeAsync(
                BuildCompositeCreateRequest(node, variables, context),
                cancellationToken);
            WriteTarget(node, variables, response);
            return NodeOutcome.WithResult(response);
        }

        if (type.Equals("compositeUpdate", StringComparison.OrdinalIgnoreCase))
        {
            var response = await runtimeDataModelService.UpdateCompositeAsync(
                BuildCompositeUpdateRequest(node, variables, context),
                cancellationToken);
            WriteTarget(node, variables, response);
            logger.LogDebug(
                "Microflow composite update node completed. NodeId={NodeId} RootId={RootId} ChildGroupCount={ChildGroupCount}",
                node.Id,
                response.Root.Id,
                response.Children.Count);
            return NodeOutcome.WithResult(response);
        }

        if (type.Equals("change", StringComparison.OrdinalIgnoreCase))
        {
            var modelCode = RequiredString(node, "modelCode");
            var id = RequiredText(EvaluateNodeExpression(node, "idExpression", variables), "Change 节点缺少主键表达式结果");
            await runtimeDataModelService.UpdateFieldsAsync(modelCode, id, BuildValues(node, variables), cancellationToken);
            var response = await runtimeDataModelService.GetDetailAsync(modelCode, id, cancellationToken);
            WriteTarget(node, variables, response.Row);
            logger.LogDebug(
                "Microflow change node completed. NodeId={NodeId} ModelCode={ModelCode} DataId={DataId}",
                node.Id,
                modelCode,
                id);
            return NodeOutcome.WithResult(response.Row);
        }

        if (type.Equals("delete", StringComparison.OrdinalIgnoreCase))
        {
            var response = await runtimeDataModelService.DeleteAsync(
                RequiredString(node, "modelCode"),
                RequiredText(EvaluateNodeExpression(node, "idExpression", variables), "Delete 节点缺少主键表达式结果"),
                cancellationToken);
            WriteTarget(node, variables, response);
            logger.LogDebug(
                "Microflow delete node completed. NodeId={NodeId} ModelCode={ModelCode} DataId={DataId}",
                node.Id,
                RequiredString(node, "modelCode"),
                response.Id);
            return NodeOutcome.WithResult(response);
        }

        if (type.Equals("compositeDelete", StringComparison.OrdinalIgnoreCase))
        {
            var response = await runtimeDataModelService.DeleteCompositeAsync(
                BuildCompositeDeleteRequest(node, variables, context),
                cancellationToken);
            WriteTarget(node, variables, response);
            return NodeOutcome.WithResult(response);
        }

        if (type.Equals("callApi", StringComparison.OrdinalIgnoreCase))
        {
            var response = await ExecuteCallApiAsync(node, variables, context, cancellationToken);
            WriteTarget(node, variables, response);
            logger.LogDebug(
                "Microflow Call API node completed. NodeId={NodeId} RoutePath={RoutePath}",
                node.Id,
                ReadString(node.Config, "routePath") ?? ReadString(node.Config, "path"));
            return NodeOutcome.WithResult(response);
        }

        if (type.Equals("runSql", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "runSql 节点已移除，请改用 Return 节点 SQL Script",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        throw new ValidationException(
            $"微流节点类型 {type} 尚未实现运行逻辑",
            ErrorCodes.ApplicationDataCenterRuntimeFailed);
    }

    private async Task<object?> ExecuteReturnNodeAsync(
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        var schema = ReadReturnOutputSchema(node);
        EnsureReturnOutputSchema(node, schema);
        var sourceMode = ResolveReturnSourceMode(schema);

        object? result;
        if (string.Equals(sourceMode, "sqlScript", StringComparison.OrdinalIgnoreCase))
        {
            var sqlRows = await ExecuteReturnSqlScriptAsync(definition, node, schema, variables, cancellationToken);
            result = schema.Fields.Count == 0
                ? sqlRows
                : string.Equals(schema.ValueType, "array", StringComparison.OrdinalIgnoreCase)
                    ? ProjectReturnSqlArray(node, schema, variables, sqlRows)
                    : ProjectReturnObject(node, schema, variables);
        }
        else if (string.Equals(schema.ValueType, "array", StringComparison.OrdinalIgnoreCase))
        {
            result = ProjectReturnArray(node, schema, variables);
        }
        else
        {
            result = ProjectReturnObject(node, schema, variables);
        }

        RuntimeExpressionPathWriter.Write(variables, schema.VariableCode, result);
        logger.LogDebug(
            "Microflow return node projected result. NodeId={NodeId} VariableCode={VariableCode} ValueType={ValueType} FieldCount={FieldCount}",
            node.Id,
            schema.VariableCode,
            schema.ValueType,
            schema.Fields.Count);
        return result;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteReturnSqlScriptAsync(
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowOutputSchemaDefinition schema,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        var execution = await sqlScriptEngine.ExecuteAsync(
            new ApplicationDataCenterSqlScriptExecutionRequest
            {
                ContextDataSourceId = ResolveContextDataSourceId(definition),
                ExpressionContext = BuildContext(variables),
                SqlScript = schema.SqlScript!,
                SourceId = node.Id,
                SourceKind = "MicroflowReturnSql",
                SourceName = node.Name
            },
            cancellationToken);
        var rows = execution.Rows;
        variables["sqlRows"] = rows;
        variables["sqlRow"] = rows.FirstOrDefault();
        logger.LogDebug(
            "Microflow Return SQL script completed. NodeId={NodeId} RowCount={RowCount}",
            node.Id,
            rows.Count);
        return rows;
    }

    private IReadOnlyDictionary<string, object?> ProjectReturnObject(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowOutputSchemaDefinition schema,
        Dictionary<string, object?> variables) =>
        schema.Fields.ToDictionary(
            field => field.FieldCode.Trim(),
            field => EvaluateReturnField(node, field, schema.Fields.IndexOf(field), variables),
            StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<IReadOnlyDictionary<string, object?>> ProjectReturnArray(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowOutputSchemaDefinition schema,
        Dictionary<string, object?> variables)
    {
        if (schema.ArrayExpression is not null)
        {
            var rows = ResolveReturnArraySource(node, schema.ArrayExpression, variables)
                .Cast<object?>()
                .Select((row, index) =>
                {
                    var rowVariables = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase)
                    {
                        ["item"] = row,
                        ["row"] = row,
                        ["currentRow"] = row,
                        ["index"] = index
                    };
                    return ProjectReturnObject(node, schema, rowVariables);
                })
                .ToArray();
            return rows;
        }

        var values = schema.Fields
            .Select((field, index) => new ReturnFieldValue(field, EvaluateReturnField(node, field, index, variables)))
            .ToArray();
        var arrayValues = values
            .Select(item => new { item.Field, Rows = TryReadReturnFieldRows(item.Value) })
            .Where(item => item.Rows is not null)
            .ToArray();
        if (arrayValues.Length == 0)
        {
            return [values.ToDictionary(
                item => item.Field.FieldCode.Trim(),
                item => item.Value,
                StringComparer.OrdinalIgnoreCase)];
        }

        var rowCount = arrayValues.Max(item => item.Rows!.Count);
        var projectedRows = new List<IReadOnlyDictionary<string, object?>>(rowCount);
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in values)
            {
                var fieldCode = item.Field.FieldCode.Trim();
                var fieldRows = TryReadReturnFieldRows(item.Value);
                row[fieldCode] = fieldRows is null
                    ? item.Value
                    : rowIndex < fieldRows.Count ? fieldRows[rowIndex] : null;
            }

            projectedRows.Add(row);
        }

        return projectedRows;
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object?>> ProjectReturnSqlArray(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowOutputSchemaDefinition schema,
        Dictionary<string, object?> variables,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> sqlRows)
    {
        return sqlRows
            .Select((row, index) =>
            {
                var rowVariables = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase)
                {
                    ["sqlRow"] = row,
                    ["currentRow"] = row,
                    ["item"] = row,
                    ["row"] = row,
                    ["index"] = index
                };
                return ProjectReturnObject(node, schema, rowVariables);
            })
            .ToArray();
    }

    private global::System.Collections.IEnumerable ResolveReturnArraySource(
        ApplicationMicroflowNodeDefinition node,
        RuntimeValueExpressionDto arrayExpression,
        Dictionary<string, object?> variables)
    {
        var descriptor = CreateNodeExpressionDescriptor(node, "outputSchema.arrayExpression", null, "source");
        object? value;
        try
        {
            value = EvaluateExpression(arrayExpression, variables, descriptor);
        }
        catch (ValidationException exception)
        {
            if (!IsDataTypeValidationException(exception))
            {
                throw;
            }

            value = EvaluateWithoutExpectedType(arrayExpression, variables, descriptor);
            throw new ValidationException(
                BuildMicroflowExpressionError(
                    node,
                    arrayExpression,
                    "outputSchema.arrayExpression",
                    "Return 节点数组来源必须返回数组或集合",
                    value,
                    null),
                exception.Code);
        }

        if (!TryReadReturnEnumerable(value, out var items))
        {
            throw new ValidationException(
                BuildMicroflowExpressionError(
                    node,
                    arrayExpression,
                    "outputSchema.arrayExpression",
                    "Return 节点数组来源必须返回数组或集合",
                    value,
                    null),
                ErrorCodes.ParameterInvalid);
        }

        return items!;
    }

    private object? EvaluateReturnField(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowFieldDefinition field,
        int fieldIndex,
        Dictionary<string, object?> variables)
    {
        if (field.Expression is null)
        {
            throw new ValidationException(
                $"Return 节点字段缺少来源表达式。nodeId={node.Id}，nodeName={node.Name}，fieldCode={field.FieldCode}，expressionName=outputSchema.fields[{fieldIndex}].expression",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return EvaluateExpression(
            field.Expression,
            variables,
            CreateNodeExpressionDescriptor(
                node,
                $"outputSchema.fields[{fieldIndex}].expression",
                null,
                field.FieldCode));
    }

    private static ApplicationMicroflowOutputSchemaDefinition ReadReturnOutputSchema(ApplicationMicroflowNodeDefinition node) =>
        ReadJson<ApplicationMicroflowOutputSchemaDefinition>(node.Config, "outputSchema") ?? new();

    private void EnsureReturnOutputSchema(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowOutputSchemaDefinition schema)
    {
        var variableCode = schema.VariableCode.Trim();
        if (string.IsNullOrWhiteSpace(variableCode))
        {
            throw new ValidationException(
                $"Return 节点缺少返回变量编码。nodeId={node.Id}，nodeName={node.Name}，expressionName=outputSchema.variableCode",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var sourceMode = ResolveReturnSourceMode(schema);
        if (schema.Fields.Count == 0 && !string.Equals(sourceMode, "sqlScript", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"Return 节点未配置返回字段。nodeId={node.Id}，nodeName={node.Name}，variableCode={variableCode}，expressionName=outputSchema.fields",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (!string.Equals(sourceMode, "fields", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sourceMode, "sqlScript", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"Return 节点返回来源模式无效。nodeId={node.Id}，nodeName={node.Name}，sourceMode={schema.SourceMode}",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (string.Equals(sourceMode, "fields", StringComparison.OrdinalIgnoreCase))
        {
            if (schema.ArrayExpression is not null && !HasValueExpression(schema.ArrayExpression))
            {
                throw new ValidationException(
                    $"Return 节点数组来源表达式无效。nodeId={node.Id}，nodeName={node.Name}，expressionName=outputSchema.arrayExpression",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (sqlScriptValidator.HasConfiguredSqlScript(schema.SqlScript))
            {
                throw new ValidationException(
                    $"Return 节点字段配置模式不能同时配置 SQL 脚本。nodeId={node.Id}，nodeName={node.Name}",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }
        }
        else
        {
            if (schema.ArrayExpression is not null)
            {
                throw new ValidationException(
                    $"Return 节点 SQL 脚本模式不能配置字段模式数组来源。nodeId={node.Id}，nodeName={node.Name}",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var errors = new List<string>();
            sqlScriptValidator.Validate(node, schema, errors);
            if (errors.Count > 0)
            {
                throw new ValidationException(string.Join("；", errors), ErrorCodes.ApplicationDataCenterInvalidConfig);
            }
        }

        var fieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < schema.Fields.Count; index++)
        {
            var field = schema.Fields[index];
            var fieldCode = field.FieldCode.Trim();
            if (string.IsNullOrWhiteSpace(fieldCode))
            {
                throw new ValidationException(
                    $"Return 节点返回字段编码不能为空。nodeId={node.Id}，nodeName={node.Name}，expressionName=outputSchema.fields[{index}].fieldCode",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (!fieldCodes.Add(fieldCode))
            {
                throw new ValidationException(
                    $"Return 节点返回字段编码重复。nodeId={node.Id}，nodeName={node.Name}，fieldCode={fieldCode}，expressionName=outputSchema.fields[{index}].fieldCode",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (!HasValueExpression(field.Expression))
            {
                throw new ValidationException(
                    $"Return 节点字段缺少来源表达式。nodeId={node.Id}，nodeName={node.Name}，fieldCode={fieldCode}，expressionName=outputSchema.fields[{index}].expression",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }
        }
    }

    private static string ResolveReturnSourceMode(ApplicationMicroflowOutputSchemaDefinition schema) =>
        string.IsNullOrWhiteSpace(schema.SourceMode) ? "fields" : schema.SourceMode.Trim();

    private static string? ResolveContextDataSourceId(ApplicationMicroflowDefinition definition) =>
        definition.DataMappings
            .FirstOrDefault(item => string.Equals(item.Target, "dataSourceId", StringComparison.OrdinalIgnoreCase))
            ?.Expression?.Value?.ToString();

    private async Task ExecuteLoopAsync(
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        MicroflowRuntimeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var items = ResolveLoopItems(node, variables);
        var itemVariable = ReadString(node.Config, "itemVariable") ?? "item";
        var bodyNodeId = ReadString(node.Config, "bodyNodeId");
        if (string.IsNullOrWhiteSpace(bodyNodeId))
        {
            logger.LogDebug(
                "Microflow loop skipped because body node is empty. NodeId={NodeId}",
                node.Id);
            return;
        }

        var index = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            variables[itemVariable] = item;
            var body = definition.Nodes.FirstOrDefault(candidate => candidate.Id == bodyNodeId)
                ?? throw new ValidationException("Loop 节点指定的循环体不存在", ErrorCodes.ApplicationDataCenterInvalidConfig);
            await ExecuteNodeAsync(definition, body, variables, context, cancellationToken);
            index += 1;
        }
        logger.LogDebug(
            "Microflow loop completed. NodeId={NodeId} ItemVariable={ItemVariable} IterationCount={IterationCount}",
            node.Id,
            itemVariable,
            index);
    }

    private global::System.Collections.IEnumerable ResolveLoopItems(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables)
    {
        var expression = ReadExpression(node, "collectionExpression");
        var descriptor = CreateNodeExpressionDescriptor(node, "collectionExpression", null, null);
        object? collection;
        try
        {
            collection = EvaluateExpression(expression, variables, descriptor);
        }
        catch (ValidationException exception)
        {
            if (!IsDataTypeValidationException(exception))
            {
                throw;
            }

            collection = EvaluateWithoutExpectedType(expression, variables, descriptor);
            throw new ValidationException(BuildLoopCollectionError(node, expression, collection), exception.Code);
        }

        if (collection is string || collection is not global::System.Collections.IEnumerable items)
        {
            throw new ValidationException(BuildLoopCollectionError(node, expression, collection), ErrorCodes.ParameterInvalid);
        }

        return items;
    }

    private static string BuildLoopCollectionError(
        ApplicationMicroflowNodeDefinition node,
        RuntimeValueExpressionDto? expression,
        object? actualValue)
    {
        var actualType = actualValue is null ? "null" : actualValue.GetType().Name;
        return $"Loop 节点集合表达式必须返回数组或集合。nodeId={node.Id}，nodeName={node.Name}，expression={FormatExpression(expression)}，actualType={actualType}";
    }

    private async Task<object?> ExecuteCallApiAsync(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        MicroflowRuntimeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var routePath = NormalizeApiRoutePath(RequiredText(ReadString(node.Config, "routePath") ?? ReadString(node.Config, "path"), "Call API 节点缺少接口路径"));
        var httpMethod = (ReadString(node.Config, "httpMethod") ?? ReadString(node.Config, "method") ?? "GET").Trim().ToUpperInvariant();
        var query = BuildApiQuery(node, variables);
        var body = BuildApiBody(node, variables);
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var service = await ResolvePublishedApiServiceAsync(appDb, workspace, routePath, httpMethod, cancellationToken);
        EnsureApiPermission(service);
        var config = ApplicationDataCenterJson.DeserializeDictionary(service.ConfigJson);
        return service.ObjectType switch
        {
            ApplicationApiServiceSourceType.Microflow => await ExecuteMicroflowApiServiceAsync(config, query, body, context, cancellationToken),
            ApplicationApiServiceSourceType.SqlQuery => await ExecuteSqlQueryApiServiceAsync(appDb, workspace, service, config, query, cancellationToken),
            ApplicationApiServiceSourceType.ExternalProxy => await ExecuteExternalProxyApiServiceAsync(config, httpMethod, query, body, cancellationToken),
            ApplicationApiServiceSourceType.Webhook => new { accepted = true, receivedAt = DateTime.UtcNow, body },
            _ => throw new ValidationException("当前接口服务类型暂不支持运行", ErrorCodes.ApplicationDataCenterRuntimeFailed)
        };
    }

    private async Task<object?> ExecuteMicroflowApiServiceAsync(
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyDictionary<string, string?> query,
        IReadOnlyDictionary<string, object?> body,
        MicroflowRuntimeExecutionContext context,
        CancellationToken cancellationToken)
    {
        var flowCode = RequiredText(ReadString(config, "flowCode"), "接口缺少微流编码");
        var nestedVariables = new Dictionary<string, object?>(body, StringComparer.OrdinalIgnoreCase);
        foreach (var item in query)
        {
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                nestedVariables[item.Key] = item.Value;
            }
        }

        return await ExecuteAsync(
            flowCode,
            new ApplicationMicroflowExecuteRequest(
                nestedVariables,
                ReadString(config, "startNodeId"),
                ReadString(config, "pageCode") ?? context.PageCode,
                ReadString(config, "previewPageId") ?? context.PreviewPageId,
                ReadString(config, "modelCode") ?? context.ModelCode,
                ReadString(config, "action") ?? context.Action),
            cancellationToken);
    }

    private async Task<ApplicationDataCenterPreviewResponse> ExecuteSqlQueryApiServiceAsync(
        ISqlSugarClient appDb,
        ApplicationDataCenterWorkspace workspace,
        ApplicationApiServiceEntity service,
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        var dataSourceId = service.SourceObjectId ?? ReadString(config, "dataSourceId");
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            throw new ValidationException("SQL 查询接口缺少数据源", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var dataSource = (await appDb.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.Id == dataSourceId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("接口数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
        var maxRows = int.TryParse(ReadQuery(query, "pageSize"), out var requestedRows) ? requestedRows : 50;
        using var sourceDb = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        return await previewReader.PreviewDatabaseAsync(
            sourceDb,
            ReadString(config, "sql"),
            ReadString(config, "tableName"),
            Math.Clamp(maxRows, 1, 200),
            cancellationToken);
    }

    private async Task<object?> ExecuteExternalProxyApiServiceAsync(
        IReadOnlyDictionary<string, object?> config,
        string httpMethod,
        IReadOnlyDictionary<string, string?> query,
        IReadOnlyDictionary<string, object?> body,
        CancellationToken cancellationToken)
    {
        var baseUrl = RequiredText(ReadString(config, "baseUrl") ?? ReadString(config, "url"), "外部代理缺少 URL");
        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(httpMethod), BuildProxyUrl(baseUrl, query));
        var token = ReadString(config, "token");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body.Count > 0 && httpMethod is not "GET" and not "DELETE")
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, ApplicationDataCenterJson.Options), Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return new
        {
            statusCode = (int)response.StatusCode,
            response.IsSuccessStatusCode,
            body = TryReadJson(text)
        };
    }

    private async Task<ApplicationApiServiceEntity> ResolvePublishedApiServiceAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string routePath,
        string httpMethod,
        CancellationToken cancellationToken)
    {
        var entity = (await db.Queryable<ApplicationApiServiceEntity>()
            .Where(item =>
                item.RoutePath == routePath &&
                item.HttpMethod == httpMethod &&
                item.Status == ApplicationDataCenterObjectStatus.Published &&
                !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        return entity ?? throw new NotFoundException("应用数据接口不存在或未发布", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private void EnsureApiPermission(ApplicationApiServiceEntity service)
    {
        if (!service.RequiresAuthentication)
        {
            return;
        }

        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired);
        }

        if (!string.IsNullOrWhiteSpace(service.PermissionCode) &&
            !currentUser.HasAsterErpPermission(service.PermissionCode))
        {
            throw new ValidationException("无权限访问该应用数据接口", ErrorCodes.PermissionDenied);
        }
    }

    private Dictionary<string, string?> BuildApiQuery(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables)
    {
        var mappings = ReadJson<List<RuntimeModelFieldMappingDto>>(node.Config, "queryMappings") ?? [];
        return mappings
            .Where(item => !string.IsNullOrWhiteSpace(item.TargetField))
            .ToDictionary(
                item => item.TargetField,
                item => item.Expression is null
                    ? null
                    : NormalizeJsonValue(EvaluateExpression(
                        item.Expression,
                        variables,
                        CreateNodeExpressionDescriptor(node, $"queryMappings.{item.TargetField}", null, null)))?.ToString(),
                StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, object?> BuildApiBody(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables)
    {
        var expression = ReadExpression(node, "bodyExpression");
        if (expression is not null)
        {
            var value = EvaluateNodeExpression(node, "bodyExpression", variables);
            return value is IReadOnlyDictionary<string, object?> dictionary
                ? new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase)
                : NormalizeJsonValue(value) is IReadOnlyDictionary<string, object?> normalizedDictionary
                    ? new Dictionary<string, object?>(normalizedDictionary, StringComparer.OrdinalIgnoreCase)
                    : [];
        }

        var mappings = ReadJson<List<ApplicationMicroflowDataMappingDefinition>>(node.Config, "bodyMappings")
            ?? ReadJson<List<ApplicationMicroflowDataMappingDefinition>>(node.Config, "fieldMappings")
            ?? [];
        return BuildMappedValues(mappings, variables, node);
    }

    private RuntimeQueryRequest BuildQuery(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        MicroflowRuntimeExecutionContext context)
    {
        var filters = ReadJson<List<RuntimeModelFilterMappingDto>>(node.Config, "filters") ?? [];
        return new RuntimeQueryRequest(
            ReadInt(node.Config, "pageIndex") ?? 1,
            ReadInt(node.Config, "pageSize") ?? 20,
            ReadString(node.Config, "keyword"),
            BuildFilterRequests(filters, variables, node),
            [],
            null,
            null);
    }

    private Dictionary<string, object?> BuildValues(ApplicationMicroflowNodeDefinition node, Dictionary<string, object?> variables)
    {
        var mappings = ReadJson<List<ApplicationMicroflowDataMappingDefinition>>(node.Config, "fieldMappings") ?? [];
        return BuildMappedValues(mappings, variables, node);
    }

    private RuntimeCompositeCreateRequest BuildCompositeCreateRequest(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        MicroflowRuntimeExecutionContext context)
    {
        var rootModelCode = RequiredString(node, "rootModelCode");
        var rootValues = BuildValues(node, variables);
        var childDefinitions = ReadJson<List<ApplicationMicroflowCompositeChildCreateDefinition>>(node.Config, "children") ?? [];
        var children = childDefinitions
            .Select((child, index) => BuildCompositeChildCreateRequest(node, child, index, variables))
            .ToArray();
        return new RuntimeCompositeCreateRequest(
            rootModelCode,
            rootValues,
            children,
            ReadString(node.Config, "pageCode") ?? context.PageCode,
            ReadString(node.Config, "previewPageId") ?? context.PreviewPageId);
    }

    private RuntimeCompositeDetailRequest BuildCompositeDetailRequest(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        MicroflowRuntimeExecutionContext context)
    {
        var rootModelCode = RequiredString(node, "rootModelCode");
        var rootId = RequiredText(EvaluateNodeExpression(node, "idExpression", variables), "Composite Detail 节点缺少主键表达式结果");
        var childDefinitions = ReadJson<List<ApplicationMicroflowCompositeChildDetailDefinition>>(node.Config, "children") ?? [];
        var children = childDefinitions
            .Select(child => BuildCompositeChildDetailRequest(child, variables))
            .ToArray();
        return new RuntimeCompositeDetailRequest(
            rootModelCode,
            rootId,
            children,
            ReadString(node.Config, "pageCode") ?? context.PageCode,
            ReadString(node.Config, "previewPageId") ?? context.PreviewPageId);
    }

    private RuntimeCompositeChildDetailRequest BuildCompositeChildDetailRequest(
        ApplicationMicroflowCompositeChildDetailDefinition child,
        Dictionary<string, object?> variables)
    {
        var modelCode = RequiredText(child.ModelCode, "Composite Detail 子对象缺少模型编码");
        var foreignKeyField = RequiredText(child.ForeignKeyField, "Composite Detail 子对象缺少外键字段");
        var query = new RuntimeQueryRequest(
            child.PageIndex <= 0 ? 1 : child.PageIndex,
            child.PageSize <= 0 ? 20 : child.PageSize,
            child.Keyword,
            BuildFilterRequests(child.Filters, variables),
            []);
        return new RuntimeCompositeChildDetailRequest(
            modelCode,
            string.IsNullOrWhiteSpace(child.ParentKeyField) ? "id" : child.ParentKeyField.Trim(),
            foreignKeyField,
            query,
            child.BindingKey);
    }

    private RuntimeCompositeChildCreateRequest BuildCompositeChildCreateRequest(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowCompositeChildCreateDefinition child,
        int childIndex,
        Dictionary<string, object?> variables)
    {
        var modelCode = RequiredText(child.ModelCode, "Composite Create 子对象缺少模型编码");
        var foreignKeyField = RequiredText(child.ForeignKeyField, "Composite Create 子对象缺少外键字段");
        var rowsEnumerable = ResolveMicroflowCollectionExpression(
            node,
            child.RowsExpression,
            variables,
            $"children[{childIndex}].rowsExpression",
            "Composite Create 子对象 rowsExpression 必须返回数组或集合",
            modelCode);

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var row in rowsEnumerable)
        {
            var rowVariables = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase)
            {
                ["item"] = row,
                ["row"] = row,
                ["currentRow"] = row
            };
            rows.Add(BuildMappedValues(child.FieldMappings, rowVariables, node, modelCode));
        }

        return new RuntimeCompositeChildCreateRequest(
            modelCode,
            string.IsNullOrWhiteSpace(child.ParentKeyField) ? "id" : child.ParentKeyField.Trim(),
            foreignKeyField,
            rows);
    }

    private RuntimeCompositeUpdateRequest BuildCompositeUpdateRequest(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        MicroflowRuntimeExecutionContext context)
    {
        var rootModelCode = RequiredString(node, "rootModelCode");
        var rootId = RequiredText(EvaluateNodeExpression(node, "idExpression", variables), "Composite Update 节点缺少主键表达式结果");
        var rootValues = BuildValues(node, variables);
        var childDefinitions = ReadJson<List<ApplicationMicroflowCompositeChildUpdateDefinition>>(node.Config, "children") ?? [];
        var children = childDefinitions
            .Select((child, index) => BuildCompositeChildUpdateRequest(node, child, index, variables))
            .ToArray();
        return new RuntimeCompositeUpdateRequest(
            rootModelCode,
            rootId,
            rootValues,
            children,
            ReadString(node.Config, "pageCode") ?? context.PageCode,
            ReadString(node.Config, "previewPageId") ?? context.PreviewPageId);
    }

    private RuntimeCompositeChildUpdateRequest BuildCompositeChildUpdateRequest(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowCompositeChildUpdateDefinition child,
        int childIndex,
        Dictionary<string, object?> variables)
    {
        var modelCode = RequiredText(child.ModelCode, "Composite Update 子对象缺少模型编码");
        var foreignKeyField = RequiredText(child.ForeignKeyField, "Composite Update 子对象缺少外键字段");
        var rowsEnumerable = ResolveMicroflowCollectionExpression(
            node,
            child.RowsExpression,
            variables,
            $"children[{childIndex}].rowsExpression",
            "Composite Update 子对象 rowsExpression 必须返回数组或集合",
            modelCode);

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var row in rowsEnumerable)
        {
            var rowVariables = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase)
            {
                ["item"] = row,
                ["row"] = row,
                ["currentRow"] = row
            };
            rows.Add(BuildMappedValues(child.FieldMappings, rowVariables, node, modelCode));
        }

        return new RuntimeCompositeChildUpdateRequest(
            modelCode,
            string.IsNullOrWhiteSpace(child.ParentKeyField) ? "id" : child.ParentKeyField.Trim(),
            foreignKeyField,
            rows,
            ResolveCompositeUpdateDeleteIds(node, child, childIndex, variables),
            child.DeleteMissing);
    }

    private IReadOnlyList<string> ResolveCompositeUpdateDeleteIds(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowCompositeChildUpdateDefinition child,
        int childIndex,
        Dictionary<string, object?> variables)
    {
        if (child.DeleteIdsExpression is null)
        {
            return [];
        }

        var descriptor = CreateNodeExpressionDescriptor(
            node,
            $"children[{childIndex}].deleteIdsExpression",
            child.ModelCode,
            null);
        var value = EvaluateWithoutExpectedType(child.DeleteIdsExpression, variables, descriptor);
        if (value is null)
        {
            return [];
        }

        if (value is string textValue)
        {
            return textValue
                .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        if (value is global::System.Collections.IEnumerable items)
        {
            return items
                .Cast<object?>()
                .Select(item => item?.ToString()?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToArray();
        }

        return [value.ToString() ?? string.Empty];
    }

    private global::System.Collections.IEnumerable ResolveMicroflowCollectionExpression(
        ApplicationMicroflowNodeDefinition node,
        RuntimeValueExpressionDto? expression,
        Dictionary<string, object?> variables,
        string expressionName,
        string message,
        string? modelCode)
    {
        if (expression is null)
        {
            return Array.Empty<object>();
        }

        var descriptor = CreateNodeExpressionDescriptor(node, expressionName, modelCode, null);
        object? value;
        try
        {
            value = EvaluateExpression(expression, variables, descriptor);
        }
        catch (ValidationException exception)
        {
            if (!IsDataTypeValidationException(exception))
            {
                throw;
            }

            value = EvaluateWithoutExpectedType(expression, variables, descriptor);
            throw new ValidationException(BuildMicroflowExpressionError(node, expression, expressionName, message, value, modelCode), exception.Code);
        }

        if (value is string || value is not global::System.Collections.IEnumerable items)
        {
            throw new ValidationException(
                BuildMicroflowExpressionError(node, expression, expressionName, message, value, modelCode),
                ErrorCodes.ParameterInvalid);
        }

        return items;
    }

    private RuntimeCompositeDeleteRequest BuildCompositeDeleteRequest(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables,
        MicroflowRuntimeExecutionContext context)
    {
        var rootModelCode = RequiredString(node, "rootModelCode");
        var rootId = RequiredText(EvaluateNodeExpression(node, "idExpression", variables), "Composite Delete 节点缺少主键表达式结果");
        var childDefinitions = ReadJson<List<ApplicationMicroflowCompositeChildDeleteDefinition>>(node.Config, "children") ?? [];
        var children = childDefinitions
            .Select((child, index) => new RuntimeCompositeChildDeleteRequest(
                RequiredText(child.ModelCode, "Composite Delete 子对象缺少模型编码"),
                string.IsNullOrWhiteSpace(child.ParentKeyField) ? "id" : child.ParentKeyField.Trim(),
                RequiredText(child.ForeignKeyField, "Composite Delete 子对象缺少外键字段"),
                child.ParentIdExpression is null
                    ? rootId
                    : RequiredText(
                        EvaluateExpression(
                            child.ParentIdExpression,
                            variables,
                            CreateNodeExpressionDescriptor(
                                node,
                                $"children[{index}].parentIdExpression",
                                child.ModelCode,
                                null)),
                        "Composite Delete 子对象缺少父级主键表达式结果"),
                child.Required))
            .ToArray();
        return new RuntimeCompositeDeleteRequest(
            rootModelCode,
            rootId,
            children,
            ReadString(node.Config, "pageCode") ?? context.PageCode,
            ReadString(node.Config, "previewPageId") ?? context.PreviewPageId);
    }

    private Dictionary<string, object?> BuildMappedValues(
        IReadOnlyList<RuntimeModelFieldMappingDto> mappings,
        Dictionary<string, object?> variables,
        ApplicationMicroflowNodeDefinition? node = null,
        string? modelCode = null)
    {
        return mappings
            .Where(item => !string.IsNullOrWhiteSpace(item.TargetField))
            .ToDictionary(
                item => item.TargetField,
                item => item.Expression is null
                    ? null
                    : NormalizeJsonValue(EvaluateExpression(
                        item.Expression,
                        variables,
                        node is null
                            ? null
                            : CreateNodeExpressionDescriptor(node, $"fieldMappings.{item.TargetField}", modelCode, null))),
                StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, object?> BuildMappedValues(
        IReadOnlyList<ApplicationMicroflowDataMappingDefinition> mappings,
        Dictionary<string, object?> variables,
        ApplicationMicroflowNodeDefinition node,
        string? modelCode = null)
    {
        return mappings
            .Where(item => !string.IsNullOrWhiteSpace(item.Target))
            .ToDictionary(
                item => item.Target,
                item => item.Expression is null
                    ? null
                    : NormalizeJsonValue(EvaluateExpression(
                        item.Expression,
                        variables,
                        CreateNodeExpressionDescriptor(node, $"fieldMappings.{item.Target}", modelCode, null))),
                StringComparer.OrdinalIgnoreCase);
    }

    private RuntimeFilterRequest[] BuildFilterRequests(
        IReadOnlyList<RuntimeModelFilterMappingDto> filters,
        Dictionary<string, object?> variables,
        ApplicationMicroflowNodeDefinition? node = null,
        string? modelCode = null)
    {
        return filters.Select(item => new RuntimeFilterRequest(
            item.Field,
            string.IsNullOrWhiteSpace(item.Operator) ? "equals" : item.Operator,
            item.ValueExpression is null
                ? null
                : NormalizeJsonValue(EvaluateExpression(
                    item.ValueExpression,
                    variables,
                    node is null ? null : CreateNodeExpressionDescriptor(node, $"filters.{item.Field}.valueExpression", modelCode, null))),
            item.ValueToExpression is null
                ? null
                : NormalizeJsonValue(EvaluateExpression(
                    item.ValueToExpression,
                    variables,
                    node is null ? null : CreateNodeExpressionDescriptor(node, $"filters.{item.Field}.valueToExpression", modelCode, null))))).ToArray();
    }

    private object? EvaluateNodeExpression(
        ApplicationMicroflowNodeDefinition node,
        string expressionName,
        Dictionary<string, object?> variables) =>
        EvaluateExpression(
            ReadExpression(node, expressionName),
            variables,
            CreateNodeExpressionDescriptor(node, expressionName, null, null));

    private bool EvaluateDecisionNode(
        ApplicationMicroflowNodeDefinition node,
        Dictionary<string, object?> variables)
    {
        var rules = ReadJson<List<DecisionConditionRuleDefinition>>(node.Config, "conditionRules") ?? [];
        if (rules.Count == 0)
        {
            throw new ValidationException(
                $"Decision 节点未配置条件规则。nodeId={node.Id}，nodeName={node.Name}，expressionName=conditionRules",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var mode = ReadString(node.Config, "conditionMode");
        var indexedRules = rules.Select((rule, index) => new { rule, index });
        var matched = string.Equals(mode, "any", StringComparison.OrdinalIgnoreCase)
            ? indexedRules.Any(item => EvaluateDecisionConditionRule(node, item.rule, item.index, variables))
            : indexedRules.All(item => EvaluateDecisionConditionRule(node, item.rule, item.index, variables));

        return ReadBool(node.Config, "conditionNot") ? !matched : matched;
    }

    private bool EvaluateDecisionConditionRule(
        ApplicationMicroflowNodeDefinition node,
        DecisionConditionRuleDefinition rule,
        int index,
        Dictionary<string, object?> variables)
    {
        var left = EvaluateExpression(
            rule.LeftExpression,
            variables,
            CreateNodeExpressionDescriptor(node, $"conditionRules[{index}].leftExpression", null, null));
        var right = EvaluateExpression(
            rule.RightExpression,
            variables,
            CreateNodeExpressionDescriptor(node, $"conditionRules[{index}].rightExpression", null, null));

        return EvaluateDecisionComparison(left, rule.Operator, right);
    }

    private object? EvaluateExpression(
        RuntimeValueExpressionDto? expression,
        Dictionary<string, object?> variables,
        RuntimeExpressionEvaluationDescriptor? descriptor) =>
        expression is null ? null : NormalizeJsonValue(expressionEvaluator.Evaluate(expression, BuildContext(variables), descriptor));

    private object? EvaluateWithoutExpectedType(
        RuntimeValueExpressionDto? expression,
        Dictionary<string, object?> variables,
        RuntimeExpressionEvaluationDescriptor? descriptor) =>
        expression is null
            ? null
            : NormalizeJsonValue(expressionEvaluator.Evaluate(expression, BuildContext(variables), descriptor));

    private static RuntimeExpressionEvaluationDescriptor CreateNodeExpressionDescriptor(
        ApplicationMicroflowNodeDefinition node,
        string expressionName,
        string? modelCode,
        string? bindingKey) =>
        new()
        {
            BindingKey = bindingKey,
            ExpressionName = expressionName,
            ModelCode = modelCode,
            OwnerId = node.Id,
            OwnerName = node.Name,
            OwnerType = $"MicroflowNode:{node.Type}"
        };

    private static bool IsDataTypeValidationException(ValidationException exception) =>
        exception.Message.StartsWith("变量表达式结果必须", StringComparison.Ordinal);

    private static string BuildMicroflowExpressionError(
        ApplicationMicroflowNodeDefinition node,
        RuntimeValueExpressionDto? expression,
        string expressionName,
        string message,
        object? actualValue,
        string? modelCode)
    {
        var actualType = actualValue is null ? "null" : actualValue.GetType().Name;
        var childModelCode = string.IsNullOrWhiteSpace(modelCode) ? "未配置" : modelCode.Trim();
        return $"{message}。nodeId={node.Id}，nodeName={node.Name}，nodeType={node.Type}，expressionName={expressionName}，childModelCode={childModelCode}，expression={FormatExpression(expression)}，actualType={actualType}";
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is JsonElement element)
        {
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

        return value;
    }

    private static IReadOnlyList<object?>? TryReadReturnFieldRows(object? value) =>
        value is not null && TryReadReturnEnumerable(value, out var items)
            ? items!.Cast<object?>().ToArray()
            : null;

    private static bool TryReadReturnEnumerable(object? value, out global::System.Collections.IEnumerable? items)
    {
        if (value is null)
        {
            items = Array.Empty<object?>();
            return true;
        }

        if (value is string ||
            value is global::System.Collections.IDictionary ||
            value is IReadOnlyDictionary<string, object?>)
        {
            items = null;
            return false;
        }

        if (value is global::System.Collections.IEnumerable enumerable)
        {
            items = enumerable;
            return true;
        }

        items = null;
        return false;
    }

    private static bool HasValueExpression(RuntimeValueExpressionDto? expression)
    {
        if (expression is null)
        {
            return false;
        }

        var kind = expression.Kind?.Trim();
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return kind.Equals("literal", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("object", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("array", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("template", StringComparison.OrdinalIgnoreCase) ||
            kind.Equals("function", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(expression.FunctionId) ||
            kind.Equals("ref", StringComparison.OrdinalIgnoreCase) && expression.Ref is not null;
    }

    private static string FormatExpression(RuntimeValueExpressionDto? expression)
    {
        if (expression is null)
        {
            return "未配置";
        }

        var kind = string.IsNullOrWhiteSpace(expression.Kind) ? "未配置" : expression.Kind.Trim();
        if (expression.Ref is not null)
        {
            var refPath = expression.Ref.FieldPath.Count == 0
                ? expression.Ref.OutputKey ?? expression.Ref.VariableId
                : $"{expression.Ref.OutputKey ?? expression.Ref.VariableId}.{string.Join('.', expression.Ref.FieldPath)}";
            return $"{kind}:{expression.Ref.SourceType}:{refPath}";
        }

        return string.IsNullOrWhiteSpace(expression.FunctionId) ? kind : $"{kind}:{expression.FunctionId}";
    }

    private static bool EvaluateDecisionComparison(object? left, string? operatorValue, object? right)
    {
        var normalizedOperator = string.IsNullOrWhiteSpace(operatorValue)
            ? "equals"
            : operatorValue.Trim();
        return normalizedOperator switch
        {
            "equals" => ValuesEqual(left, right),
            "notEquals" => !ValuesEqual(left, right),
            "contains" => ToComparableString(left).Contains(ToComparableString(right), StringComparison.OrdinalIgnoreCase),
            "gt" => CompareValues(left, right) > 0,
            "gte" => CompareValues(left, right) >= 0,
            "lt" => CompareValues(left, right) < 0,
            "lte" => CompareValues(left, right) <= 0,
            "between" => IsBetween(left, right),
            _ => false
        };
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (TryReadDecimal(left, out var leftNumber) && TryReadDecimal(right, out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        if (bool.TryParse(left.ToString(), out var leftBool) && bool.TryParse(right.ToString(), out var rightBool))
        {
            return leftBool == rightBool;
        }

        return string.Equals(ToComparableString(left), ToComparableString(right), StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareValues(object? left, object? right)
    {
        if (TryReadDecimal(left, out var leftNumber) && TryReadDecimal(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(ToComparableString(left), ToComparableString(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBetween(object? value, object? range)
    {
        var boundaries = ReadRangeBoundaries(range);
        if (boundaries is null)
        {
            return false;
        }

        return CompareValues(value, boundaries.Value.Min) >= 0 && CompareValues(value, boundaries.Value.Max) <= 0;
    }

    private static (object? Min, object? Max)? ReadRangeBoundaries(object? value)
    {
        if (value is object?[] array && array.Length >= 2)
        {
            return (array[0], array[1]);
        }

        if (value is IReadOnlyList<object?> list && list.Count >= 2)
        {
            return (list[0], list[1]);
        }

        var text = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var separator = text.Contains("..", StringComparison.Ordinal) ? ".." : ",";
        var parts = text.Split(separator, 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : null;
    }

    private static bool TryReadDecimal(object? value, out decimal result)
    {
        return value switch
        {
            decimal decimalValue => SetDecimal(decimalValue, out result),
            double doubleValue => SetDecimal(Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture), out result),
            float floatValue => SetDecimal(Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture), out result),
            int intValue => SetDecimal(intValue, out result),
            long longValue => SetDecimal(longValue, out result),
            _ => decimal.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result)
        };
    }

    private static bool SetDecimal(decimal value, out decimal result)
    {
        result = value;
        return true;
    }

    private static string ToComparableString(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private RuntimeExpressionEvaluationContext BuildContext(Dictionary<string, object?> variables)
    {
        var sources = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase)
        {
            ["inputs"] = variables,
            ["input"] = variables,
            ["variables"] = variables,
            ["vars"] = variables,
            ["sqlResult"] = variables,
            ["system"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["currentUserId"] = currentUser.GetAsterErpUserId(),
                ["tenantId"] = currentUser.GetAsterErpTenantId(),
                ["appCode"] = currentUser.GetAsterErpAppCode(),
                ["now"] = DateTime.UtcNow
            }
        };
        if (variables.TryGetValue("form", out var form))
        {
            sources["form"] = form;
        }

        if (variables.TryGetValue("currentRow", out var currentRow))
        {
            sources["currentRow"] = currentRow;
            sources["model"] = currentRow;
            sources["row"] = currentRow;
        }

        if (variables.TryGetValue("model", out var model))
        {
            sources["model"] = model;
        }

        return new RuntimeExpressionEvaluationContext(sources);
    }

    private static ApplicationMicroflowNodeDefinition ResolveStartNode(
        ApplicationMicroflowDefinition definition,
        string? startNodeId)
    {
        if (!string.IsNullOrWhiteSpace(startNodeId))
        {
            return definition.Nodes.FirstOrDefault(item => item.Id == startNodeId)
                ?? throw new ValidationException("微流起始节点不存在", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return definition.Nodes.FirstOrDefault(item => string.Equals(item.Type, "start", StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("微流缺少 Start 节点", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static ApplicationMicroflowNodeDefinition? ResolveNextNode(
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowNodeDefinition node,
        string? branch)
    {
        var edge = definition.Edges.FirstOrDefault(item =>
            item.SourceNodeId == node.Id &&
            (string.IsNullOrWhiteSpace(branch) ||
             string.IsNullOrWhiteSpace(item.Condition) ||
             string.Equals(item.Condition, branch, StringComparison.OrdinalIgnoreCase)));
        return edge is null ? null : definition.Nodes.FirstOrDefault(item => item.Id == edge.TargetNodeId);
    }

    private static RuntimeValueExpressionDto? ReadExpression(ApplicationMicroflowNodeDefinition node, string key) =>
        ReadJson<RuntimeValueExpressionDto>(node.Config, key);

    private static T? ReadJson<T>(IReadOnlyDictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value is null)
        {
            return default;
        }

        return value is JsonElement element
            ? JsonSerializer.Deserialize<T>(element.GetRawText(), ApplicationDataCenterJson.Options)
            : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, ApplicationDataCenterJson.Options), ApplicationDataCenterJson.Options);
    }

    private static void WriteTarget(ApplicationMicroflowNodeDefinition node, Dictionary<string, object?> variables, object? value)
    {
        var target = ReadString(node.Config, "targetVariable");
        if (!string.IsNullOrWhiteSpace(target))
        {
            RuntimeExpressionPathWriter.Write(variables, target, value);
        }
    }

    private static string NormalizeApiRoutePath(string value)
    {
        var normalized = value.Trim();
        const string applicationDataPrefix = "/api/application-data/";
        if (normalized.StartsWith(applicationDataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[applicationDataPrefix.Length..];
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized;
    }

    private static string BuildProxyUrl(
        string baseUrl,
        IReadOnlyDictionary<string, string?> query)
    {
        var pairs = query
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value ?? string.Empty)}")
            .ToArray();
        return pairs.Length == 0
            ? baseUrl
            : $"{baseUrl}{(baseUrl.Contains('?') ? '&' : '?')}{string.Join('&', pairs)}";
    }

    private static object? TryReadJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<object>(value, ApplicationDataCenterJson.Options);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static string? ReadQuery(IReadOnlyDictionary<string, string?> query, string key) =>
        query.TryGetValue(key, out var value) ? value : null;

    private static string RequiredString(ApplicationMicroflowNodeDefinition node, string key) =>
        RequiredText(ReadString(node.Config, key), $"节点 {node.Name} 缺少 {key}");

    private static string RequiredText(object? value, string message)
    {
        var text = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return text;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value is JsonElement element ? element.ToString() : value.ToString();
    }

    private static int? ReadInt(IReadOnlyDictionary<string, object?> source, string key) =>
        int.TryParse(ReadString(source, key), out var value) ? value : null;

    private static bool ReadBool(IReadOnlyDictionary<string, object?> source, string key) =>
        bool.TryParse(ReadString(source, key), out var value) && value;

    private static bool IsTruthy(object? value) =>
        value is bool boolean
            ? boolean
            : bool.TryParse(value?.ToString(), out var parsed) && parsed;

    private sealed record NodeOutcome(bool Stop, string? Branch, object? Result)
    {
        public static NodeOutcome Continue() => new(false, null, null);

        public static NodeOutcome ForBranch(string branch) => new(false, branch, null);

        public static NodeOutcome WithResult(object? result) => new(false, null, result);

        public static NodeOutcome StopWith(object? result) => new(true, null, result);
    }

    private sealed record ReturnFieldValue(ApplicationMicroflowFieldDefinition Field, object? Value);

    private sealed record MicroflowRuntimeExecutionContext(
        string? PageCode,
        string? PreviewPageId,
        string? ModelCode,
        string? Action);

    private sealed record DecisionConditionRuleDefinition(
        string? Id,
        RuntimeValueExpressionDto? LeftExpression,
        string? Operator,
        RuntimeValueExpressionDto? RightExpression);
}
