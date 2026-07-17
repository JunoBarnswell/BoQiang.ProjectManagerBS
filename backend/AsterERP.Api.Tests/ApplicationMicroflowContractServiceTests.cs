using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationMicroflowContractServiceTests : IDisposable
{
    private const string TenantId = "tenant-a";
    private const string AppCode = "MES";

    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-microflow-contract-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task GetAsync_ReturnsPublishedInputsOutputsAndFields()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity>();
        await InsertMicroflowAsync(
            db,
            "queryCustomers",
            QueryCustomersDefinition(),
            ApplicationDataCenterObjectStatus.Published);
        var service = CreateService(db);

        var contract = await service.GetAsync("queryCustomers", CancellationToken.None);

        Assert.Equal("queryCustomers", contract.FlowCode);
        Assert.Equal("查询客户", contract.FlowName);
        Assert.Equal(3, contract.VersionNo);
        var input = Assert.Single(contract.Inputs);
        Assert.Equal("keyword", input.VariableCode);
        var output = Assert.Single(contract.Outputs);
        Assert.Equal("rows", output.VariableCode);
        Assert.Equal("array", output.ValueType);
        Assert.Equal(["id", "name"], output.Fields.Select(item => item.FieldCode).ToArray());
    }

    [Fact]
    public async Task GetAsync_RejectsDraftMicroflow()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity>();
        await InsertMicroflowAsync(
            db,
            "queryCustomers",
            QueryCustomersDefinition(),
            ApplicationDataCenterObjectStatus.Normal);
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAsync("queryCustomers", CancellationToken.None));
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

    private ApplicationMicroflowContractService CreateService(ISqlSugarClient db)
    {
        var currentUser = CreateCurrentUser();
        return new ApplicationMicroflowContractService(
            new TestWorkspaceDatabaseAccessor(db),
            new ApplicationDataCenterWorkspaceResolver(currentUser),
            new ApplicationMicroflowOutputSchemaSynchronizer());
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
        ApplicationMicroflowDefinition definition,
        string status)
    {
        await db.Insertable(new ApplicationMicroflowEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = TenantId,
            AppCode = AppCode,
            ModuleKey = ApplicationDataCenterModuleKey.Microflow,
            ObjectCode = flowCode,
            ObjectName = "查询客户",
            ObjectType = "Microflow",
            Status = status,
            VersionNo = 3,
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
            Outputs =
            [
                new ApplicationMicroflowVariableDefinition
                {
                    Fields =
                    [
                        new ApplicationMicroflowFieldDefinition
                        {
                            DataType = "string",
                            FieldCode = "stale",
                            FieldName = "旧字段"
                        }
                    ],
                    VariableCode = "rows",
                    VariableName = "旧输出",
                    ValueType = "array"
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
