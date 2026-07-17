using System.Text.Json;
using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Contracts.Runtime;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class FlowiseRuntimeDataModelExecutionTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"astererp-flowise-runtime-model-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task StartAsync_executes_runtime_data_model_node_and_returns_system_configuration_answer()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<FlowiseChatFlowEntity, FlowiseExecutionEntity, FlowiseAuditLogEntity>();
        var runtimeDataModelService = new RecordingRuntimeDataModelService();
        var chatflow = new FlowiseChatFlowEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            Name = "系统配置模型编排验收",
            Type = FlowiseChatflowTypes.Agentflow,
            FlowData = BuildRuntimeDataModelFlowData()
        };
        await db.Insertable(chatflow).ExecuteCommandAsync();
        var service = CreateExecutionService(db, runtimeDataModelService);

        var execution = await service.StartAsync(new FlowiseExecutionStartRequest
        {
            ResourceId = chatflow.Id,
            Question = "请查询系统菜单配置模型",
            InputJson = "{}"
        }, CancellationToken.None);

        var persisted = await db.Queryable<FlowiseExecutionEntity>().FirstAsync(item => item.Id == execution.Id);
        Assert.True(execution.Status == "Completed", $"Expected Completed but got {execution.Status}: {persisted.ErrorMessage}");
        Assert.Equal("runtime.menu", runtimeDataModelService.LastModelCode);
        Assert.NotNull(runtimeDataModelService.LastRequest);
        Assert.Equal(1, runtimeDataModelService.LastRequest.PageIndex);
        Assert.Equal(20, runtimeDataModelService.LastRequest.PageSize);
        using var output = JsonDocument.Parse(persisted.OutputJson);
        var root = output.RootElement;
        Assert.Equal("Completed", GetProperty(root, "status").GetString());
        Assert.Equal(2, GetProperty(root, "nodeCount").GetInt32());
        Assert.Equal(1, GetProperty(root, "edgeCount").GetInt32());
        Assert.Contains("系统配置模型 runtime.menu 查询完成", GetProperty(root, "answer").GetString());
        Assert.Contains("系统菜单", GetProperty(root, "answer").GetString());
        Assert.Equal("Runtime Data Model", GetProperty(GetProperty(root, "agentExecutedData")[1], "nodeLabel").GetString());
        Assert.Contains("runtime.menu", GetProperty(GetProperty(root, "usedTools")[0], "inputJson").GetString());
        using var sourceDocuments = JsonDocument.Parse(persisted.SourceDocumentsJson);
        Assert.Contains("runtime.menu", GetProperty(sourceDocuments.RootElement[0], "content").GetString());
    }

    [Fact]
    public async Task StartAsync_resolves_runtime_data_model_query_parameters_from_form_input()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<FlowiseChatFlowEntity, FlowiseExecutionEntity, FlowiseAuditLogEntity>();
        var runtimeDataModelService = new RecordingRuntimeDataModelService();
        var chatflow = new FlowiseChatFlowEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            Name = "系统配置模型表单参数编排验收",
            Type = FlowiseChatflowTypes.Agentflow,
            FlowData = BuildRuntimeDataModelFlowData(new
            {
                modelCode = "$form.modelCode",
                keyword = "$form.keyword",
                pageIndex = "$form.pageIndex",
                pageSize = "$form.pageSize",
                filters = "$form.filters",
                sorts = "$form.sorts"
            })
        };
        await db.Insertable(chatflow).ExecuteCommandAsync();
        var service = CreateExecutionService(db, runtimeDataModelService);

        await service.StartAsync(new FlowiseExecutionStartRequest
        {
            ResourceId = chatflow.Id,
            Question = "按表单参数查询系统菜单配置模型",
            Form = new Dictionary<string, object?>
            {
                ["modelCode"] = "runtime.menu",
                ["keyword"] = "用户管理",
                ["pageIndex"] = 2,
                ["pageSize"] = 5,
                ["filters"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["field"] = "title",
                        ["operator"] = "contains",
                        ["value"] = "用户"
                    }
                },
                ["sorts"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["field"] = "title",
                        ["order"] = "asc"
                    }
                }
            }
        }, CancellationToken.None);

        Assert.Equal("runtime.menu", runtimeDataModelService.LastModelCode);
        Assert.NotNull(runtimeDataModelService.LastRequest);
        Assert.Equal("用户管理", runtimeDataModelService.LastRequest.Keyword);
        Assert.Equal(2, runtimeDataModelService.LastRequest.PageIndex);
        Assert.Equal(5, runtimeDataModelService.LastRequest.PageSize);
        Assert.Single(runtimeDataModelService.LastRequest.Filters!);
        Assert.Equal("title", runtimeDataModelService.LastRequest.Filters![0].Field);
        Assert.Equal("contains", runtimeDataModelService.LastRequest.Filters![0].Operator);
        Assert.Equal("用户", runtimeDataModelService.LastRequest.Filters![0].Value?.ToString());
        Assert.Single(runtimeDataModelService.LastRequest.Sorts!);
        Assert.Equal("title", runtimeDataModelService.LastRequest.Sorts![0].Field);
        Assert.Equal("asc", runtimeDataModelService.LastRequest.Sorts![0].Order);
    }

    [Fact]
    public async Task StreamAsync_emits_flowise_agentflow_runtime_events_for_canvas_status()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<FlowiseChatFlowEntity, FlowiseExecutionEntity, FlowiseAuditLogEntity>();
        var chatflow = new FlowiseChatFlowEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            Name = "系统配置模型流式事件验收",
            Type = FlowiseChatflowTypes.Agentflow,
            FlowData = BuildRuntimeDataModelFlowData()
        };
        await db.Insertable(chatflow).ExecuteCommandAsync();
        var service = CreateExecutionService(db, new RecordingRuntimeDataModelService());
        var events = new List<(string Name, JsonElement Data)>();

        var execution = await service.StreamAsync(new FlowiseExecutionStartRequest
        {
            ResourceId = chatflow.Id,
            Question = "请查询系统菜单配置模型",
            InputJson = "{}"
        }, (eventName, data, _) =>
        {
            events.Add((eventName, JsonSerializer.SerializeToElement(data)));
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal("Completed", execution.Status);
        Assert.Contains(events, item =>
            item.Name == "agentFlowEvent" &&
            GetProperty(item.Data, "status").GetString() == "INPROGRESS");
        Assert.Contains(events, item =>
            item.Name == "nextAgentFlow" &&
            GetProperty(item.Data, "nodeId").GetString() == "runtime-data-model_0" &&
            GetProperty(item.Data, "status").GetString() == "FINISHED");
        Assert.Contains(events, item =>
            item.Name == "agentFlowExecutedData" &&
            item.Data.ValueKind == JsonValueKind.Array &&
            item.Data.EnumerateArray().Any(node =>
                GetProperty(node, "nodeId").GetString() == "runtime-data-model_0" &&
                GetProperty(node, "status").GetString() == "FINISHED"));
        Assert.Contains(events, item =>
            item.Name == "agentFlowEvent" &&
            GetProperty(item.Data, "status").GetString() == "FINISHED");
    }

    [Fact]
    public async Task StartAsync_rejects_agentflow_without_start_node()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<FlowiseChatFlowEntity, FlowiseExecutionEntity, FlowiseAuditLogEntity>();
        var chatflow = new FlowiseChatFlowEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            Name = "缺少 Start 的工作流",
            Type = FlowiseChatflowTypes.Agentflow,
            FlowData = BuildRuntimeDataModelFlowDataWithoutStart()
        };
        await db.Insertable(chatflow).ExecuteCommandAsync();
        var service = CreateExecutionService(db, new RecordingRuntimeDataModelService());

        var execution = await service.StartAsync(new FlowiseExecutionStartRequest
        {
            ResourceId = chatflow.Id,
            Question = "缺少 Start 节点仍尝试运行"
        }, CancellationToken.None);

        Assert.Equal("Failed", execution.Status);
        Assert.Equal(ErrorCodes.ParameterInvalid.ToString(), execution.ErrorCode);
        Assert.Contains("Start", execution.ErrorMessage);
    }

    [Fact]
    public async Task PredictAsync_accepts_start_form_input_and_passes_form_to_execution()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<FlowiseChatFlowEntity, FlowiseChatMessageEntity, FlowiseFeedbackEntity, FlowiseLeadEntity>();
        var chatflow = new FlowiseChatFlowEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            Name = "表单输入编排验收",
            Type = FlowiseChatflowTypes.Agentflow,
            FlowData = BuildRuntimeDataModelFlowData()
        };
        await db.Insertable(chatflow).ExecuteCommandAsync();
        var executionService = new RecordingExecutionService();
        var service = new FlowisePredictionService(
            db,
            new AiWorkspaceContext(CreateCurrentUser()),
            executionService,
            modelRouter: null!,
            chatRuntime: null!,
            new FlowisePermissionGuard(CreateCurrentUser()));

        await service.PredictAsync(new FlowisePredictionRequest
        {
            ResourceId = chatflow.Id,
            ChatId = "form-chat",
            Form = new Dictionary<string, object?>
            {
                ["modelCode"] = "runtime.menu",
                ["keyword"] = "用户管理"
            }
        }, CancellationToken.None);

        Assert.NotNull(executionService.LastRequest);
        Assert.Contains("modelCode: runtime.menu", executionService.LastRequest.Question);
        Assert.Contains("keyword: 用户管理", executionService.LastRequest.Question);
        using var input = JsonDocument.Parse(executionService.LastRequest.InputJson!);
        Assert.Equal("runtime.menu", GetProperty(GetProperty(input.RootElement, "form"), "modelCode").GetString());
        Assert.Equal("用户管理", GetProperty(GetProperty(input.RootElement, "form"), "keyword").GetString());
        var userMessage = await db.Queryable<FlowiseChatMessageEntity>().FirstAsync(item => item.Role == "user" && item.ChatId == "form-chat");
        Assert.Contains("modelCode: runtime.menu", userMessage.Message);
    }

    [Fact]
    public async Task ClearChatAsync_soft_deletes_messages_feedback_and_chat_leads()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<FlowiseChatFlowEntity, FlowiseChatMessageEntity, FlowiseFeedbackEntity, FlowiseLeadEntity>();
        var chatflow = new FlowiseChatFlowEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            Name = "聊天清空验收",
            Type = FlowiseChatflowTypes.Agentflow,
            FlowData = BuildRuntimeDataModelFlowData()
        };
        await db.Insertable(chatflow).ExecuteCommandAsync();
        var deletedUserMessage = new FlowiseChatMessageEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            ResourceId = chatflow.Id,
            ChatId = "clear-chat",
            Role = "user",
            Message = "请查询系统菜单配置模型"
        };
        var deletedAssistantMessage = new FlowiseChatMessageEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            ResourceId = chatflow.Id,
            ChatId = "clear-chat",
            Role = "assistant",
            Message = "系统配置模型 runtime.menu 查询完成"
        };
        var keptMessage = new FlowiseChatMessageEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            ResourceId = chatflow.Id,
            ChatId = "keep-chat",
            Role = "user",
            Message = "保留会话"
        };
        await db.Insertable(new[] { deletedUserMessage, deletedAssistantMessage, keptMessage }).ExecuteCommandAsync();
        var deletedFeedback = new FlowiseFeedbackEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            MessageId = deletedAssistantMessage.Id,
            Rating = "up"
        };
        var keptFeedback = new FlowiseFeedbackEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            MessageId = keptMessage.Id,
            Rating = "down"
        };
        await db.Insertable(new[] { deletedFeedback, keptFeedback }).ExecuteCommandAsync();
        var deletedLead = new FlowiseLeadEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            ResourceId = chatflow.Id,
            ContactJson = JsonSerializer.Serialize(new { chatId = "clear-chat", name = "clear" })
        };
        var keptLead = new FlowiseLeadEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            ResourceId = chatflow.Id,
            ContactJson = JsonSerializer.Serialize(new { chatId = "keep-chat", name = "keep" })
        };
        await db.Insertable(new[] { deletedLead, keptLead }).ExecuteCommandAsync();
        var service = new FlowisePredictionService(
            db,
            new AiWorkspaceContext(CreateCurrentUser()),
            new RecordingExecutionService(),
            modelRouter: null!,
            chatRuntime: null!,
            new FlowisePermissionGuard(CreateCurrentUser()));

        var cleared = await service.ClearChatAsync(new FlowiseChatClearRequest
        {
            ResourceId = chatflow.Id,
            ChatId = "clear-chat"
        }, CancellationToken.None);

        Assert.True(cleared);
        Assert.Equal(2, await db.Queryable<FlowiseChatMessageEntity>().CountAsync(item => item.ChatId == "clear-chat" && item.IsDeleted));
        Assert.Equal(1, await db.Queryable<FlowiseChatMessageEntity>().CountAsync(item => item.ChatId == "keep-chat" && !item.IsDeleted));
        Assert.True(await db.Queryable<FlowiseFeedbackEntity>().AnyAsync(item => item.Id == deletedFeedback.Id && item.IsDeleted));
        Assert.True(await db.Queryable<FlowiseFeedbackEntity>().AnyAsync(item => item.Id == keptFeedback.Id && !item.IsDeleted));
        Assert.True(await db.Queryable<FlowiseLeadEntity>().AnyAsync(item => item.Id == deletedLead.Id && item.IsDeleted));
        Assert.True(await db.Queryable<FlowiseLeadEntity>().AnyAsync(item => item.Id == keptLead.Id && !item.IsDeleted));
    }

    [Fact]
    public async Task AbortChatAsync_cancels_active_stream_for_same_resource_and_chat()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<FlowiseChatFlowEntity, FlowiseChatMessageEntity, FlowiseExecutionEntity, FlowiseFeedbackEntity, FlowiseLeadEntity>();
        var chatflow = new FlowiseChatFlowEntity
        {
            TenantId = "tenant-system",
            AppCode = "SYSTEM",
            OwnerUserId = "admin",
            WorkspaceId = "workspace-system",
            Name = "聊天停止运行验收",
            Type = FlowiseChatflowTypes.Agentflow,
            FlowData = BuildRuntimeDataModelFlowData()
        };
        await db.Insertable(chatflow).ExecuteCommandAsync();
        var executionService = new BlockingExecutionService();
        var service = new FlowisePredictionService(
            db,
            new AiWorkspaceContext(CreateCurrentUser()),
            executionService,
            modelRouter: null!,
            chatRuntime: null!,
            new FlowisePermissionGuard(CreateCurrentUser()));
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var streamTask = service.StreamAsync(new FlowisePredictionRequest
        {
            ResourceId = chatflow.Id,
            ChatId = "abort-chat",
            Question = "请查询系统菜单配置模型"
        }, httpContext.Response, CancellationToken.None);

        await executionService.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var aborted = await service.AbortChatAsync(new FlowisePredictionAbortRequest
        {
            ResourceId = chatflow.Id,
            ChatId = "abort-chat"
        }, CancellationToken.None);

        Assert.True(aborted);
        await executionService.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await streamTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private static FlowiseExecutionService CreateExecutionService(
        ISqlSugarClient db,
        IRuntimeDataModelService runtimeDataModelService)
    {
        var currentUser = CreateCurrentUser();
        var nodeClassifier = new FlowiseRuntimeNodeClassifier();
        var nodeDataReader = new FlowiseRuntimeNodeDataReader();
        var jsonDocumentParser = new FlowiseExecutionJsonDocumentParser();
        var contentParser = new FlowiseExecutionContentParser(nodeDataReader);
        var documentStoreReferenceParser = new FlowiseDocumentStoreReferenceParser(jsonDocumentParser);
        var structuredOutputBuilder = new FlowiseStructuredOutputBuilder(contentParser);
        var agentFlowEventBuilder = new FlowiseAgentFlowEventBuilder();
        var variableResolver = new FlowiseVariableResolver();
        var outputReferenceResolver = new FlowiseOutputReferenceResolver();
        var templateResolver = new FlowiseExecutionTemplateResolver(variableResolver, outputReferenceResolver);
        var nodeMessageBuilder = new FlowiseNodeMessageBuilder(nodeDataReader, contentParser, templateResolver);
        var keyValueInputReader = new FlowiseKeyValueInputReader(nodeDataReader, variableResolver, outputReferenceResolver);
        var runtimeNodeInputResolver = new FlowiseRuntimeNodeInputResolver(nodeDataReader, variableResolver);
        var stateUpdateApplier = new FlowiseStateUpdateApplier(keyValueInputReader, templateResolver, outputReferenceResolver, variableResolver, nodeDataReader);
        var agentToolCallParser = new FlowiseAgentToolCallParser(jsonDocumentParser, templateResolver);
        var conditionEvaluator = new FlowiseConditionEvaluator(
            nodeClassifier,
            nodeDataReader,
            variableResolver);
        var executionOrderPlanner = new FlowiseExecutionOrderPlanner(nodeClassifier, conditionEvaluator);
        var executionSnapshotBuilder = new FlowiseExecutionSnapshotBuilder(nodeClassifier, nodeDataReader, executionOrderPlanner, agentFlowEventBuilder);
        var workspaceContext = new AiWorkspaceContext(currentUser);
        return new FlowiseExecutionService(
            db,
            workspaceContext,
            modelRouter: null!,
            chatRuntime: null!,
            runtimeDataModelService,
            documentStoreService: null!,
            kernelFunctionService: null!,
            httpClientFactory: null!,
            nodeClassifier,
            nodeDataReader,
            documentStoreReferenceParser,
            agentToolCallParser,
            nodeMessageBuilder,
            structuredOutputBuilder,
            new FlowiseRuntimeFlowDataParser(nodeDataReader),
            agentFlowEventBuilder,
            executionSnapshotBuilder,
            conditionEvaluator,
            templateResolver,
            keyValueInputReader,
            runtimeNodeInputResolver,
            stateUpdateApplier,
            executionOrderPlanner,
            variableResolver,
            outputReferenceResolver,
            new FlowiseExecutionResultBuilder(),
            new FlowiseExecutionTrackingService(db, workspaceContext),
            new FlowisePermissionGuard(currentUser));
    }

    private static string BuildRuntimeDataModelFlowData(object? runtimeInputs = null) =>
        JsonSerializer.Serialize(new
        {
            nodes = new object[]
            {
                new
                {
                    id = "startAgentflow_0",
                    type = "flowiseAgentFlowNode",
                    position = new { x = 100, y = 120 },
                    data = new
                    {
                        displayName = "Start Agentflow",
                        label = "Start Agentflow",
                        name = "startAgentflow",
                        nodeType = "startAgentflow",
                        inputs = new { startInputType = "chatInput" }
                    }
                },
                new
                {
                    id = "runtime-data-model_0",
                    type = "flowiseAgentFlowNode",
                    position = new { x = 420, y = 120 },
                    data = new
                    {
                        displayName = "Runtime Data Model",
                        label = "Runtime Data Model 0",
                        name = "runtime-data-model",
                        nodeType = "runtime-data-model",
                        inputs = runtimeInputs ?? new
                        {
                            modelCode = "runtime.menu",
                            pageIndex = 1,
                            pageSize = 20,
                            filters = Array.Empty<object>(),
                            sorts = Array.Empty<object>()
                        }
                    }
                }
            },
            edges = new object[]
            {
                new
                {
                    id = "startAgentflow_0-output-runtime-data-model_0-input",
                    source = "startAgentflow_0",
                    target = "runtime-data-model_0"
                }
            },
            viewport = new { x = 0, y = 0, zoom = 1 }
        });

    private static string BuildRuntimeDataModelFlowDataWithoutStart() =>
        JsonSerializer.Serialize(new
        {
            nodes = new object[]
            {
                new
                {
                    id = "runtime-data-model_0",
                    type = "flowiseWorkflowNode",
                    position = new { x = 420, y = 120 },
                    data = new
                    {
                        displayName = "Runtime Data Model",
                        label = "Runtime Data Model 0",
                        name = "runtime-data-model",
                        nodeType = "runtime-data-model",
                        inputs = new
                        {
                            modelCode = "runtime.menu",
                            pageIndex = 1,
                            pageSize = 20,
                            filters = Array.Empty<object>(),
                            sorts = Array.Empty<object>()
                        }
                    }
                }
            },
            edges = Array.Empty<object>(),
            viewport = new { x = 0, y = 0, zoom = 1 }
        });

    private SqlSugarClient CreateDb() =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    private static JsonElement GetProperty(JsonElement element, string camelCaseName)
    {
        if (element.TryGetProperty(camelCaseName, out var value))
        {
            return value;
        }

        var pascalCaseName = char.ToUpperInvariant(camelCaseName[0]) + camelCaseName[1..];
        return element.GetProperty(pascalCaseName);
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-system",
            "默认租户",
            "SYSTEM",
            "系统管理",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            ["*"],
            "ALL",
            true,
            true,
            true,
            "平台管理员"));
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new Volo.Abp.Users.CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }

    private sealed class RecordingRuntimeDataModelService : IRuntimeDataModelService
    {
        public string? LastModelCode { get; private set; }

        public RuntimeQueryRequest? LastRequest { get; private set; }

        public Task<RuntimeDataModelDefinition> GetPublishedDefinitionAsync(string modelCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeQueryResponse> QueryAsync(string modelCode, RuntimeQueryRequest request, CancellationToken cancellationToken = default)
        {
            LastModelCode = modelCode;
            LastRequest = request;
            RuntimeDataFieldResponse[] fields =
            [
                new("title", "菜单名称", "string", "title", true, true, true, true, false, null, null, null, null, 1),
                new("path", "菜单路径", "string", "path", true, true, true, true, false, null, null, null, null, 2)
            ];
            IReadOnlyDictionary<string, object?>[] rows =
            [
                new Dictionary<string, object?>
                {
                    ["title"] = "系统菜单",
                    ["path"] = "/system/menus"
                }
            ];
            return Task.FromResult(new RuntimeQueryResponse(fields, rows, 1, request.PageIndex, request.PageSize));
        }

        public Task<RuntimeDetailResponse> GetDetailAsync(string modelCode, string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeCompositeDetailResponse> GetCompositeDetailAsync(
            RuntimeCompositeDetailRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeModelOperationResponse> ExecuteOperationAsync(
            string modelCode,
            RuntimeModelOperationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeCreateResponse> CreateAsync(
            string modelCode,
            IReadOnlyDictionary<string, object?> values,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeCompositeCreateResponse> CreateCompositeAsync(
            RuntimeCompositeCreateRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeCompositeUpdateResponse> UpdateCompositeAsync(
            RuntimeCompositeUpdateRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateFieldsAsync(string modelCode, string id, IReadOnlyDictionary<string, object?> updates, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeDeleteResponse> DeleteAsync(string modelCode, string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeCompositeDeleteResponse> DeleteCompositeAsync(
            RuntimeCompositeDeleteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingExecutionService : IFlowiseExecutionService
    {
        public FlowiseExecutionStartRequest LastRequest { get; private set; } = new();

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FlowiseExecutionDto> GetAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GridPageResult<FlowiseExecutionDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FlowiseExecutionDto> StartAsync(FlowiseExecutionStartRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new FlowiseExecutionDto
            {
                Id = "execution-form",
                ResourceId = request.ResourceId,
                Status = "Completed",
                OutputJson = JsonSerializer.Serialize(new
                {
                    answer = "表单输入已进入工作流",
                    agentExecutedData = Array.Empty<object>(),
                    agentReasoning = Array.Empty<object>(),
                    artifacts = Array.Empty<object>(),
                    usedTools = Array.Empty<object>()
                }),
                SourceDocumentsJson = "[]",
                TraceId = "trace-form"
            });
        }

        public Task<FlowiseExecutionDto> StartMcpAsync(FlowiseChatFlowEntity chatflow, string inputJson, string? question, string? idempotencyKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FlowiseExecutionDto> StreamAsync(FlowiseExecutionStartRequest request, Func<string, object?, CancellationToken, Task> emitAsync, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class BlockingExecutionService : IFlowiseExecutionService
    {
        public TaskCompletionSource<FlowiseExecutionStartRequest> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Cancelled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FlowiseExecutionDto> GetAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GridPageResult<FlowiseExecutionDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FlowiseExecutionDto> StartAsync(FlowiseExecutionStartRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FlowiseExecutionDto> StartMcpAsync(FlowiseChatFlowEntity chatflow, string inputJson, string? question, string? idempotencyKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async Task<FlowiseExecutionDto> StreamAsync(
            FlowiseExecutionStartRequest request,
            Func<string, object?, CancellationToken, Task> emitAsync,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult(request);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult(true);
                return new FlowiseExecutionDto
                {
                    Id = "execution-aborted",
                    ResourceId = request.ResourceId,
                    Status = "Cancelled",
                    ErrorCode = "FLOWISE_CHAT_ABORTED",
                    ErrorMessage = "Flowise chat run aborted.",
                    OutputJson = "{}",
                    SourceDocumentsJson = "[]",
                    TraceId = "trace-abort"
                };
            }

            throw new InvalidOperationException("Blocking execution should only finish when the stream is aborted.");
        }
    }
}
