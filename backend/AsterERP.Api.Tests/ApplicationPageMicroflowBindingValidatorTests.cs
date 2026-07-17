using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationPageMicroflowBindingValidatorTests : IDisposable
{
    private const string TenantId = "tenant-a";
    private const string AppCode = "MES";

    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-page-microflow-validator-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ValidateAsync_RejectsMissingOutputVariable()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity>();
        await InsertMicroflowAsync(db, "queryCustomers", QueryCustomersDefinition());
        var validator = new ApplicationPageMicroflowBindingValidator(new ApplicationMicroflowOutputSchemaSynchronizer());
        var workspace = ResolveWorkspace();
        var documentJson = """
            {
              "pageMicroflows": [
                {
                  "id": "binding_customers",
                  "alias": "customers",
                  "flowCode": "queryCustomers",
                  "trigger": "pageLoad",
                  "action": "query",
                  "inputMappings": [],
                  "outputMappings": [
                    {
                      "outputVariable": "missingRows",
                      "resultPath": "missingRows",
                      "writeTo": "microflows.customers.data"
                    }
                  ],
                  "refreshOnChangePaths": [],
                  "errorPolicy": "blockDependents"
                }
              ]
            }
            """;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            validator.ValidateAsync(db, workspace, documentJson, CancellationToken.None));

        Assert.Contains("输出不存在", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_RejectsMicroflowDependencyCycle()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity>();
        await InsertMicroflowAsync(db, "flowA", QueryCustomersDefinition());
        await InsertMicroflowAsync(db, "flowB", QueryCustomersDefinition());
        var validator = new ApplicationPageMicroflowBindingValidator(new ApplicationMicroflowOutputSchemaSynchronizer());
        var workspace = ResolveWorkspace();
        var documentJson = """
            {
              "pageMicroflows": [
                {
                  "id": "binding_a",
                  "alias": "a",
                  "flowCode": "flowA",
                  "trigger": "pageLoad",
                  "action": "query",
                  "inputMappings": [
                    {
                      "targetVariable": "keyword",
                      "sourceExpression": { "source": "microflow", "path": "b.data.id" }
                    }
                  ],
                  "outputMappings": [
                    {
                      "outputVariable": "rows",
                      "resultPath": "rows",
                      "writeTo": "microflows.a.data"
                    }
                  ],
                  "refreshOnChangePaths": [],
                  "errorPolicy": "blockDependents"
                },
                {
                  "id": "binding_b",
                  "alias": "b",
                  "flowCode": "flowB",
                  "trigger": "pageLoad",
                  "action": "query",
                  "inputMappings": [
                    {
                      "targetVariable": "keyword",
                      "sourceExpression": { "source": "microflow", "path": "a.data.id" }
                    }
                  ],
                  "outputMappings": [
                    {
                      "outputVariable": "rows",
                      "resultPath": "rows",
                      "writeTo": "microflows.b.data"
                    }
                  ],
                  "refreshOnChangePaths": [],
                  "errorPolicy": "blockDependents"
                }
              ]
            }
            """;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            validator.ValidateAsync(db, workspace, documentJson, CancellationToken.None));

        Assert.Contains("依赖存在循环", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
        catch (IOException)
        {
        }
    }

    private SqlSugarClient CreateDb() =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    private static async Task InsertMicroflowAsync(
        ISqlSugarClient db,
        string flowCode,
        ApplicationMicroflowDefinition definition)
    {
        await db.Insertable(new ApplicationMicroflowEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = TenantId,
            AppCode = AppCode,
            ModuleKey = ApplicationDataCenterModuleKey.Microflow,
            ObjectCode = flowCode,
            ObjectName = flowCode,
            ObjectType = "Microflow",
            Status = ApplicationDataCenterObjectStatus.Published,
            VersionNo = 1,
            ConfigJson = ApplicationDataCenterJson.Serialize(definition),
            CreatedBy = "admin",
            CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();
    }

    private static ApplicationMicroflowDefinition QueryCustomersDefinition() =>
        new()
        {
            Inputs =
            [
                new ApplicationMicroflowVariableDefinition
                {
                    VariableCode = "keyword",
                    VariableName = "关键字",
                    ValueType = "string"
                }
            ],
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return-customers",
                    Name = "返回客户",
                    Type = "return",
                    Config = new Dictionary<string, object?>
                    {
                        ["outputSchema"] = CustomerRowsSchema()
                    }
                }
            ]
        };

    private static ApplicationMicroflowOutputSchemaDefinition CustomerRowsSchema() =>
        new()
        {
            Fields =
            [
                Field("id", "ID", "string"),
                Field("name", "名称", "string")
            ],
            ValueType = "array",
            VariableCode = "rows",
            VariableName = "客户行"
        };

    private static ApplicationMicroflowFieldDefinition Field(string code, string name, string dataType) =>
        new()
        {
            DataType = dataType,
            FieldCode = code,
            FieldName = name,
            Visible = true,
            Writable = false,
            Expression = new RuntimeValueExpressionDto
            {
                DataType = dataType,
                Kind = "ref",
                Ref = new RuntimeVariableRefDto
                {
                    DataType = dataType,
                    FieldPath = [code],
                    Label = $"结果.{code}",
                    OutputKey = "sqlRow",
                    SourceType = "sqlResult",
                    VariableId = "sqlRow"
                }
            }
        };

    private static ApplicationDataCenterWorkspace ResolveWorkspace() =>
        new ApplicationDataCenterWorkspaceResolver(CreateCurrentUser()).Resolve();

    private static ICurrentUser CreateCurrentUser()
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            TenantId,
            "客户A",
            AppCode,
            "客户A MES",
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
        var currentUser = new FixedAsterErpCurrentUser(principal);
        Assert.True(currentUser.IsAsterErpAuthenticated());
        return currentUser;
    }
}
