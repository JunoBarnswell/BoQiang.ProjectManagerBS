using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Workflows.Callbacks;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Runtime;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowCallbackExecutorTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-workflow-callback-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ExecuteAsync_UsesStructuredConfigAndLogsSuccess()
    {
        using var db = CreateDb();
        var runtimeDataModelService = new RecordingRuntimeDataModelService();
        var parser = new WorkflowCallbackConfigParser();
        var executor = CreateExecutor(db, runtimeDataModelService, parser);
        var config = new WorkflowCallbackConfigDto(
        [
            new WorkflowCallbackRuleDto(
                "complete-status",
                true,
                WorkflowCallbackTriggers.ProcessCompleted,
                null,
                new WorkflowCallbackTargetDto("purchase.order", WorkflowCallbackValueSources.BusinessKey, null),
                [
                    new WorkflowCallbackAssignmentDto(
                        "approvalStatus",
                        WorkflowCallbackValueSources.Constant,
                        "Approved",
                        null)
                ],
                0)
        ]);

        await InsertBindingAsync(db, parser.Serialize(config), statusField: null);

        await executor.ExecuteAsync(new WorkflowCallbackContext(
            BuildInstance("Completed"),
            WorkflowCallbackTriggers.ProcessCompleted,
            null,
            null,
            null,
            "approver-001",
            new Dictionary<string, object?>(),
            DateTime.UtcNow));

        var update = Assert.Single(runtimeDataModelService.Updates);
        Assert.Equal("purchase.order", update.ModelCode);
        Assert.Equal("po-001", update.Id);
        Assert.Equal("Approved", Convert.ToString(update.Fields["approvalStatus"], System.Globalization.CultureInfo.InvariantCulture));

        var log = await db.Queryable<WorkflowCallbackLogEntity>().FirstAsync();
        Assert.Equal("Success", log.Status);
        Assert.Equal("complete-status", log.RuleId);
        Assert.Equal("purchase.order", log.TargetModelCode);
        Assert.Equal("po-001", log.TargetKey);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSynthesizeLegacyStatusCallbackWhenConfigMissing()
    {
        using var db = CreateDb();
        var runtimeDataModelService = new RecordingRuntimeDataModelService();
        var parser = new WorkflowCallbackConfigParser();
        var executor = CreateExecutor(db, runtimeDataModelService, parser);
        await InsertBindingAsync(db, bindingConfigJson: null, statusField: "approvalStatus");

        await executor.ExecuteAsync(new WorkflowCallbackContext(
            BuildInstance("Completed"),
            WorkflowCallbackTriggers.ProcessCompleted,
            null,
            null,
            null,
            "approver-001",
            new Dictionary<string, object?>(),
            DateTime.UtcNow));

        Assert.Empty(runtimeDataModelService.Updates);
    }

    [Fact]
    public void ParsePersisted_RejectsCorruptedConfigInsteadOfFallingBack()
    {
        var parser = new WorkflowCallbackConfigParser();

        var exception = Assert.Throws<ValidationException>(() => parser.ParsePersisted("{not-json"));

        Assert.Contains("MigrationBlocked", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsePersisted_RejectsUnversionedCallbackConfigAsMigrationBlocked()
    {
        var parser = new WorkflowCallbackConfigParser();

        var exception = Assert.Throws<ValidationException>(() => parser.ParsePersisted("{\"rules\":[{\"ruleId\":\"legacy\",\"enabled\":true,\"trigger\":\"process-completed\",\"sortOrder\":0}]}"));

        Assert.Contains("requires migration", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_RejectsAnOutdatedVersionEvenWhenItHasNoRules()
    {
        var parser = new WorkflowCallbackConfigParser();

        var exception = Assert.Throws<ValidationException>(() =>
            parser.Serialize(new WorkflowCallbackConfigDto([], "legacy")));

        Assert.Contains("must be latest", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnwritableField()
    {
        var runtimeDataModelService = new RecordingRuntimeDataModelService
        {
            Definition = BuildDefinition(writable: false)
        };
        var validator = new WorkflowCallbackConfigValidator(runtimeDataModelService);
        var config = new WorkflowCallbackConfigDto(
        [
            new WorkflowCallbackRuleDto(
                "complete-status",
                true,
                WorkflowCallbackTriggers.ProcessCompleted,
                null,
                new WorkflowCallbackTargetDto("purchase.order", WorkflowCallbackValueSources.BusinessKey, null),
                [
                    new WorkflowCallbackAssignmentDto(
                        "approvalStatus",
                        WorkflowCallbackValueSources.Constant,
                        "Approved",
                        null)
                ],
                0)
        ]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            validator.ValidateAsync(config, "purchase.order"));
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

    private WorkflowCallbackExecutor CreateExecutor(
        ISqlSugarClient db,
        IRuntimeDataModelService runtimeDataModelService,
        WorkflowCallbackConfigParser parser)
    {
        return new WorkflowCallbackExecutor(
            new FixedWorkspaceDatabaseAccessor(db),
            runtimeDataModelService,
            parser,
            new WorkflowCallbackValueResolver(),
            NullLogger<WorkflowCallbackExecutor>.Instance,
            new TestGuidGenerator(),
            new TestClock());
    }

    private sealed class FixedWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;

        public ISqlSugarClient GetCurrentDb() => db;

        public ISqlSugarClient RequireApplicationDb() => db;

        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);

        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private async Task InsertBindingAsync(
        ISqlSugarClient db,
        string? bindingConfigJson,
        string? statusField)
    {
        await db.Insertable(new WorkflowBindingEntity
        {
            TenantId = "tenant-a",
            AppCode = "WMS",
            MenuCode = "purchase.order",
            BusinessType = "purchase.order",
            ProcessDefinitionKey = "purchaseApproval",
            ModelCode = "purchase.order",
            KeyField = "id",
            StatusField = statusField,
            BindingConfigJson = bindingConfigJson,
            IsEnabled = true
        }).ExecuteCommandAsync();
    }

    private SqlSugarClient CreateDb()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={_databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });
        db.CodeFirst.InitTables<WorkflowBindingEntity, WorkflowCallbackLogEntity>();
        return db;
    }

    private static WorkflowBusinessInstanceEntity BuildInstance(string status)
    {
        return new WorkflowBusinessInstanceEntity
        {
            TenantId = "tenant-a",
            AppCode = "WMS",
            MenuCode = "purchase.order",
            BusinessType = "purchase.order",
            BusinessKey = "po-001",
            ProcessInstanceId = "proc-001",
            ProcessDefinitionKey = "purchaseApproval",
            Status = status,
            StartedBy = "starter-001",
            StartedAt = DateTime.UtcNow,
            VariableSnapshotJson = "{}",
            SubmittedFormJson = "{}"
        };
    }

    private static RuntimeDataModelDefinition BuildDefinition(bool writable)
    {
        return new RuntimeDataModelDefinition(
            "model-001",
            "tenant-a",
            "WMS",
            "purchase.order",
            "采购订单",
            "test.purchase-orders",
            "id",
            RuntimeModelIdGeneration.Guid,
            "runtime:data:query",
            [
                new RuntimeDataFieldDefinition
                {
                    FieldCode = "id",
                    FieldName = "ID",
                    DataType = "text",
                    Binding = "id"
                },
                new RuntimeDataFieldDefinition
                {
                    FieldCode = "approvalStatus",
                    FieldName = "审批状态",
                    DataType = "text",
                    Binding = "approvalStatus",
                    Writable = writable
                }
            ]);
    }

    private sealed class RecordingRuntimeDataModelService : IRuntimeDataModelService
    {
        public RuntimeDataModelDefinition Definition { get; set; } = BuildDefinition(writable: true);

        public List<RuntimeDataModelUpdate> Updates { get; } = [];

        public Task<RuntimeDataModelDefinition> GetPublishedDefinitionAsync(
            string modelCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Definition);

        public Task<RuntimeQueryResponse> QueryAsync(
            string modelCode,
            RuntimeQueryRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeDetailResponse> GetDetailAsync(
            string modelCode,
            string id,
            CancellationToken cancellationToken = default) =>
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

        public Task UpdateFieldsAsync(
            string modelCode,
            string id,
            IReadOnlyDictionary<string, object?> updates,
            CancellationToken cancellationToken = default)
        {
            Updates.Add(new RuntimeDataModelUpdate(modelCode, id, updates));
            return Task.CompletedTask;
        }

        public Task<RuntimeDeleteResponse> DeleteAsync(
            string modelCode,
            string id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeCompositeDeleteResponse> DeleteCompositeAsync(
            RuntimeCompositeDeleteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed record RuntimeDataModelUpdate(
        string ModelCode,
        string Id,
        IReadOnlyDictionary<string, object?> Fields);

    private sealed class TestClock : IClock
    {
        public DateTime Now => DateTime.UtcNow;

        public DateTimeKind Kind => DateTimeKind.Utc;

        public bool SupportsMultipleTimezone => false;

        public DateTime Normalize(DateTime dateTime)
        {
            return dateTime;
        }

        public DateTime ConvertToUserTime(DateTime dateTime)
        {
            return dateTime;
        }

        public DateTimeOffset ConvertToUserTime(DateTimeOffset dateTime)
        {
            return dateTime;
        }

        public DateTime ConvertToUtc(DateTime dateTime)
        {
            return dateTime;
        }
    }

    private sealed class TestGuidGenerator : IGuidGenerator
    {
        public Guid Create()
        {
            return Guid.NewGuid();
        }
    }
}
