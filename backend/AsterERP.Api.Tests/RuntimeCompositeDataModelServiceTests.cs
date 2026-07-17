using System.Globalization;
using System.Text.Json;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class RuntimeCompositeDataModelServiceTests : IDisposable
{
    private const string TenantId = "tenant-a";
    private const string AppCode = "MES";
    private const string ProviderKey = "test.memory";

    private readonly string databasePath = Path.Combine(
        Path.GetTempPath(),
        $"astererp-runtime-composite-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task DeleteCompositeAsync_DeletesAllChildPagesBeforeRoot()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("name", "Root"))]);
        provider.Seed(
            "child_model",
            Enumerable.Range(1, 201)
                .Select(index => Row($"child-{index}", ("parent_id", "root-1"), ("name", $"Child {index}")))
                .ToArray());

        var service = CreateService(db, provider);
        var response = await service.DeleteCompositeAsync(new RuntimeCompositeDeleteRequest(
            "root_model",
            "root-1",
            [
                new RuntimeCompositeChildDeleteRequest(
                    "child_model",
                    "id",
                    "parent_id",
                    "root-1",
                    Required: true)
            ]));

        Assert.True(response.Root.Deleted);
        Assert.Equal(201, response.Children.Single().DeletedCount);
        Assert.Empty(provider.Rows("child_model"));
        Assert.Empty(provider.Rows("root_model"));
    }

    [Fact]
    public async Task DeleteCompositeAsync_UsesConfiguredParentKeyField()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("code", writable: true, queryable: true),
            Field("name", writable: true)
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("root_code", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("code", "ROOT-CODE"), ("name", "Root"))]);
        provider.Seed("child_model", [
            Row("child-1", ("root_code", "ROOT-CODE"), ("name", "Child 1")),
            Row("child-2", ("root_code", "OTHER"), ("name", "Child 2"))
        ]);

        var service = CreateService(db, provider);
        var response = await service.DeleteCompositeAsync(new RuntimeCompositeDeleteRequest(
            "root_model",
            "root-1",
            [
                new RuntimeCompositeChildDeleteRequest(
                    "child_model",
                    "code",
                    "root_code",
                    null,
                    Required: true)
            ]));

        Assert.True(response.Root.Deleted);
        Assert.Equal(1, response.Children.Single().DeletedCount);
        Assert.DoesNotContain(provider.Rows("child_model"), row => string.Equals(row["id"]?.ToString(), "child-1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(provider.Rows("child_model"), row => string.Equals(row["id"]?.ToString(), "child-2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateCompositeAsync_RollsBackRootAndCreatedChildrenWhenChildFails()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider
        {
            FailCreateModelCode = "child_model",
            FailCreateName = "fail"
        };
        var service = CreateService(db, provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCompositeAsync(new RuntimeCompositeCreateRequest(
            "root_model",
            new Dictionary<string, object?> { ["name"] = "Root" },
            [
                new RuntimeCompositeChildCreateRequest(
                    "child_model",
                    "id",
                    "parent_id",
                    [
                        new Dictionary<string, object?> { ["name"] = "Child 1" },
                        new Dictionary<string, object?> { ["name"] = "fail" }
                    ])
            ])));

        Assert.Empty(provider.Rows("child_model"));
        Assert.Empty(provider.Rows("root_model"));
    }

    [Fact]
    public async Task CreateCompositeAsync_RollsBackSqlTablesInOneTransactionWhenChildInsertFails()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE root_items (id TEXT PRIMARY KEY, name TEXT NOT NULL)");
        db.Ado.ExecuteCommand("CREATE TABLE child_items (id TEXT PRIMARY KEY, root_id TEXT NOT NULL, name TEXT NOT NULL)");
        await InsertPublishedModelAsync(
            db,
            "root_sql_model",
            "主对象 SQL",
            "id",
            [
                Field("id", writable: false),
                Field("name", writable: true)
            ],
            providerKey: "application-data-center.sql-table",
            source: new Dictionary<string, object?> { ["tableName"] = "root_items" });
        await InsertPublishedModelAsync(
            db,
            "child_sql_model",
            "子对象 SQL",
            "id",
            [
                Field("id", writable: false),
                Field("root_id", writable: true, queryable: true),
                Field("name", writable: true)
            ],
            providerKey: "application-data-center.sql-table",
            source: new Dictionary<string, object?> { ["tableName"] = "child_items" });

        var service = CreateService(db, CreateSqlProvider(db));

        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateCompositeAsync(new RuntimeCompositeCreateRequest(
            "root_sql_model",
            new Dictionary<string, object?> { ["name"] = "Root" },
            [
                new RuntimeCompositeChildCreateRequest(
                    "child_sql_model",
                    "id",
                    "root_id",
                    [
                        new Dictionary<string, object?> { ["name"] = "Child 1" },
                        new Dictionary<string, object?> { ["name"] = null }
                    ])
            ])));

        Assert.Equal(0, Convert.ToInt32(db.Ado.GetScalar("SELECT COUNT(1) FROM root_items")));
        Assert.Equal(0, Convert.ToInt32(db.Ado.GetScalar("SELECT COUNT(1) FROM child_items")));
    }

    [Fact]
    public async Task UpdateFieldsAsync_UsesSqlRuntimeKeyWithoutRequiringPrimaryKeyInPayload()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE editable_items (id TEXT PRIMARY KEY, name TEXT NOT NULL)");
        db.Ado.ExecuteCommand("INSERT INTO editable_items (id, name) VALUES ('row-1', 'Old')");
        await InsertPublishedModelAsync(
            db,
            "editable_sql_model",
            "可编辑 SQL",
            "id",
            [
                Field("id", writable: false),
                Field("name", writable: true)
            ],
            providerKey: "application-data-center.sql-table",
            source: new Dictionary<string, object?> { ["tableName"] = "editable_items" });

        var service = CreateService(db, CreateSqlProvider(db));

        await service.UpdateFieldsAsync(
            "editable_sql_model",
            "row-1",
            new Dictionary<string, object?> { ["name"] = "Updated" });

        Assert.Equal("Updated", db.Ado.GetString("SELECT name FROM editable_items WHERE id = 'row-1'"));
    }

    [Fact]
    public async Task UpdateCompositeAsync_UpdatesCreatesDeletesChildrenAndRejectsCrossParentRows()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [
            Row("root-1", ("name", "Root 1")),
            Row("root-2", ("name", "Root 2"))
        ]);
        provider.Seed("child_model", [
            Row("child-1", ("parent_id", "root-1"), ("name", "Old child")),
            Row("child-delete", ("parent_id", "root-1"), ("name", "Delete me")),
            Row("child-other", ("parent_id", "root-2"), ("name", "Other parent"))
        ]);

        var service = CreateService(db, provider);
        var response = await service.UpdateCompositeAsync(new RuntimeCompositeUpdateRequest(
            "root_model",
            "root-1",
            new Dictionary<string, object?> { ["name"] = "Root updated" },
            [
                new RuntimeCompositeChildUpdateRequest(
                    "child_model",
                    "id",
                    "parent_id",
                    [
                        new Dictionary<string, object?>
                        {
                            ["id"] = "child-1",
                            ["name"] = "Child updated"
                        },
                        new Dictionary<string, object?>
                        {
                            ["id"] = null,
                            ["name"] = "Child created"
                        }
                    ],
                    ["child-delete"],
                    DeleteMissing: false)
            ]));

        Assert.True(response.Root.Success);
        Assert.Single(response.Children);
        Assert.Single(response.Children[0].CreatedRows);
        Assert.Single(response.Children[0].UpdatedRows);
        Assert.Equal(1, response.Children[0].DeletedCount);
        var childRows = provider.Rows("child_model");
        Assert.Contains(childRows, row => string.Equals(row["id"]?.ToString(), "child-1", StringComparison.OrdinalIgnoreCase) && string.Equals(row["name"]?.ToString(), "Child updated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(childRows, row => string.Equals(row["parent_id"]?.ToString(), "root-1", StringComparison.OrdinalIgnoreCase) && string.Equals(row["name"]?.ToString(), "Child created", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(childRows, row => string.Equals(row["id"]?.ToString(), "child-delete", StringComparison.OrdinalIgnoreCase));

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateCompositeAsync(new RuntimeCompositeUpdateRequest(
            "root_model",
            "root-1",
            new Dictionary<string, object?> { ["name"] = "Root should rollback" },
            [
                new RuntimeCompositeChildUpdateRequest(
                    "child_model",
                    "id",
                    "parent_id",
                    [
                        new Dictionary<string, object?>
                        {
                            ["id"] = "child-other",
                            ["name"] = "Illegal update"
                        }
                    ],
                    [],
                    DeleteMissing: false)
            ])));

        Assert.Contains(provider.Rows("child_model"), row => string.Equals(row["id"]?.ToString(), "child-other", StringComparison.OrdinalIgnoreCase) && string.Equals(row["name"]?.ToString(), "Other parent", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(provider.Rows("root_model"), row => string.Equals(row["id"]?.ToString(), "root-1", StringComparison.OrdinalIgnoreCase) && string.Equals(row["name"]?.ToString(), "Root updated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteOperationAsync_CanCreateCompositeRowsFromVariables()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ], operations:
        [
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "saveComposite",
                OperationName = "复合保存",
                OperationType = "compositeCreate",
                FieldMappings =
                [
                    new RuntimeModelFieldMappingDto
                    {
                        TargetField = "name",
                        Expression = Expression("form", "name", "string")
                    }
                ],
                Children =
                [
                    new RuntimeModelCompositeChildDefinitionDto
                    {
                        ModelCode = "child_model",
                        ParentKeyField = "id",
                        ForeignKeyField = "parent_id",
                        RowsExpression = Expression("variables", "children", "array")
                    }
                ]
            }
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider();
        var service = CreateService(db, provider);

        var response = await service.ExecuteOperationAsync(
            "root_model",
            new RuntimeModelOperationRequest(
                "saveComposite",
                new Dictionary<string, object?>
                {
                    ["form"] = new Dictionary<string, object?> { ["name"] = "Root" },
                    ["children"] = new[]
                    {
                        new Dictionary<string, object?> { ["name"] = "Child 1" },
                        new Dictionary<string, object?> { ["name"] = "Child 2" }
                    }
                },
                null,
                null));

        Assert.Equal("compositeCreate", response.OperationType);
        var result = Assert.IsType<RuntimeCompositeCreateResponse>(response.Result);
        Assert.Equal("Root", result.Root.Row["name"]);
        Assert.Equal(2, result.Children.Single().Rows.Count);
        var rootId = result.Root.Id;
        Assert.All(provider.Rows("child_model"), row => Assert.Equal(rootId, row["parent_id"]));
    }

    [Fact]
    public async Task ExecuteOperationAsync_MapsCompositeChildRowsWithChildCurrentRowContext()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ], operations:
        [
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "saveMappedComposite",
                OperationName = "映射复合保存",
                OperationType = "compositeCreate",
                FieldMappings =
                [
                    new RuntimeModelFieldMappingDto
                    {
                        TargetField = "name",
                        Expression = Expression("form", "name", "string")
                    }
                ],
                Children =
                [
                    new RuntimeModelCompositeChildDefinitionDto
                    {
                        ModelCode = "child_model",
                        ParentKeyField = "id",
                        ForeignKeyField = "parent_id",
                        RowsExpression = Expression("variables", "children", "array"),
                        FieldMappings =
                        [
                            new RuntimeModelFieldMappingDto
                            {
                                TargetField = "name",
                                Expression = FunctionExpression("trim", "string", Expression("currentRow", "rawName", "string"))
                            },
                            new RuntimeModelFieldMappingDto
                            {
                                TargetField = "amount",
                                Expression = Expression("item", "rawAmount", "number")
                            }
                        ]
                    }
                ]
            }
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true),
            Field("amount", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider();
        var service = CreateService(db, provider);

        var response = await service.ExecuteOperationAsync(
            "root_model",
            new RuntimeModelOperationRequest(
                "saveMappedComposite",
                new Dictionary<string, object?>
                {
                    ["form"] = new Dictionary<string, object?> { ["name"] = "Root" },
                    ["currentRow"] = new Dictionary<string, object?> { ["rawName"] = "Root row must not leak", ["rawAmount"] = 999 },
                    ["children"] = new[]
                    {
                        new Dictionary<string, object?> { ["rawName"] = " Child A ", ["rawAmount"] = 12 },
                        new Dictionary<string, object?> { ["rawName"] = " Child B ", ["rawAmount"] = 34 }
                    }
                },
                null,
                null));

        Assert.Equal("compositeCreate", response.OperationType);
        var childRows = provider.Rows("child_model");
        Assert.Contains(childRows, row => string.Equals(row["name"]?.ToString(), "Child A", StringComparison.OrdinalIgnoreCase) && string.Equals(row["amount"]?.ToString(), "12", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(childRows, row => string.Equals(row["name"]?.ToString(), "Child B", StringComparison.OrdinalIgnoreCase) && string.Equals(row["amount"]?.ToString(), "34", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(childRows, row => string.Equals(row["name"]?.ToString(), "Root row must not leak", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteOperationAsync_CanUpdateCompositeRowsFromVariables()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ], operations:
        [
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "updateComposite",
                OperationName = "复合编辑",
                OperationType = "compositeUpdate",
                IdExpression = Expression("currentRow", "__runtimeKey", "string"),
                FieldMappings =
                [
                    new RuntimeModelFieldMappingDto
                    {
                        TargetField = "name",
                        Expression = Expression("form", "name", "string")
                    }
                ],
                Children =
                [
                    new RuntimeModelCompositeChildDefinitionDto
                    {
                        ModelCode = "child_model",
                        ParentKeyField = "id",
                        ForeignKeyField = "parent_id",
                        RowsExpression = Expression("variables", "children", "array"),
                        DeleteIdsExpression = Expression("variables", "deleteIds", "array")
                    }
                ]
            }
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("name", "Before"))]);
        provider.Seed("child_model", [
            Row("child-1", ("parent_id", "root-1"), ("name", "Before child")),
            Row("child-delete", ("parent_id", "root-1"), ("name", "Delete child"))
        ]);
        var service = CreateService(db, provider);

        var response = await service.ExecuteOperationAsync(
            "root_model",
            new RuntimeModelOperationRequest(
                "updateComposite",
                new Dictionary<string, object?>
                {
                    ["currentRow"] = new Dictionary<string, object?> { ["__runtimeKey"] = "root-1" },
                    ["form"] = new Dictionary<string, object?> { ["name"] = "After" },
                    ["children"] = new[]
                    {
                        new Dictionary<string, object?> { ["id"] = "child-1", ["name"] = "After child" },
                        new Dictionary<string, object?> { ["name"] = "New child" }
                    },
                    ["deleteIds"] = new[]
                    {
                        new Dictionary<string, object?> { ["__runtimeKey"] = "child-delete" }
                    }
                },
                null,
                null));

        Assert.Equal("compositeUpdate", response.OperationType);
        var result = Assert.IsType<RuntimeCompositeUpdateResponse>(response.Result);
        Assert.True(result.Root.Success);
        Assert.Single(result.Children.Single().CreatedRows);
        Assert.Single(result.Children.Single().UpdatedRows);
        Assert.Equal(1, result.Children.Single().DeletedCount);
        Assert.Contains(provider.Rows("root_model"), row => string.Equals(row["name"]?.ToString(), "After", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(provider.Rows("child_model"), row => string.Equals(row["id"]?.ToString(), "child-1", StringComparison.OrdinalIgnoreCase) && string.Equals(row["name"]?.ToString(), "After child", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(provider.Rows("child_model"), row => string.Equals(row["parent_id"]?.ToString(), "root-1", StringComparison.OrdinalIgnoreCase) && string.Equals(row["name"]?.ToString(), "New child", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(provider.Rows("child_model"), row => string.Equals(row["id"]?.ToString(), "child-delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteOperationAsync_DeletesCompositeRowsWhenRowsExpressionIsEmpty()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ], operations:
        [
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "updateComposite",
                OperationName = "复合编辑",
                OperationType = "compositeUpdate",
                IdExpression = Expression("currentRow", "__runtimeKey", "string"),
                FieldMappings =
                [
                    new RuntimeModelFieldMappingDto
                    {
                        TargetField = "name",
                        Expression = Expression("form", "name", "string")
                    }
                ],
                Children =
                [
                    new RuntimeModelCompositeChildDefinitionDto
                    {
                        ModelCode = "child_model",
                        ParentKeyField = "id",
                        ForeignKeyField = "parent_id",
                        RowsExpression = LiteralExpression(Array.Empty<object>(), "array"),
                        DeleteIdsExpression = Expression("variables", "deleteIds", "array")
                    }
                ]
            }
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("name", "Before"))]);
        provider.Seed("child_model", [
            Row("child-delete", ("parent_id", "root-1"), ("name", "Delete child"))
        ]);
        var service = CreateService(db, provider);

        var response = await service.ExecuteOperationAsync(
            "root_model",
            new RuntimeModelOperationRequest(
                "updateComposite",
                new Dictionary<string, object?>
                {
                    ["currentRow"] = new Dictionary<string, object?> { ["__runtimeKey"] = "root-1" },
                    ["form"] = new Dictionary<string, object?> { ["name"] = "After" },
                    ["deleteIds"] = new[] { "child-delete" }
                },
                null,
                null));

        var result = Assert.IsType<RuntimeCompositeUpdateResponse>(response.Result);
        Assert.Empty(result.Children.Single().CreatedRows);
        Assert.Empty(result.Children.Single().UpdatedRows);
        Assert.Equal(1, result.Children.Single().DeletedCount);
        Assert.DoesNotContain(provider.Rows("child_model"), row => string.Equals(row["id"]?.ToString(), "child-delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteOperationAsync_ReportsCompositeRowsExpressionContextWhenRowsAreNotArray()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ], operations:
        [
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "saveComposite",
                OperationName = "复合保存",
                OperationType = "compositeCreate",
                FieldMappings =
                [
                    new RuntimeModelFieldMappingDto
                    {
                        TargetField = "name",
                        Expression = Expression("form", "name", "string")
                    }
                ],
                Children =
                [
                    new RuntimeModelCompositeChildDefinitionDto
                    {
                        ModelCode = "child_model",
                        ParentKeyField = "id",
                        ForeignKeyField = "parent_id",
                        RowsExpression = Expression("variables", "children", "array")
                    }
                ]
            }
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var service = CreateService(db, new InMemoryDataModelProvider());

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.ExecuteOperationAsync(
            "root_model",
            new RuntimeModelOperationRequest(
                "saveComposite",
                new Dictionary<string, object?>
                {
                    ["form"] = new Dictionary<string, object?> { ["name"] = "Root" },
                    ["children"] = "not-array"
                },
                null,
                null)));

        Assert.Contains("modelCode=child_model", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expressionName=rowsExpression", exception.Message, StringComparison.Ordinal);
        Assert.Contains("kind=ref", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dataType=array", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ref=global:children:(root)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("actualType=String", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteOperationAsync_CanDeleteCompositeRowsFromVariables()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ], operations:
        [
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "deleteComposite",
                OperationName = "复合删除",
                OperationType = "compositeDelete",
                IdExpression = Expression("currentRow", "__runtimeKey", "string"),
                Children =
                [
                    new RuntimeModelCompositeChildDefinitionDto
                    {
                        ModelCode = "child_model",
                        ForeignKeyField = "parent_id",
                        ParentIdExpression = Expression("currentRow", "__runtimeKey", "string"),
                        Required = true
                    }
                ]
            }
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);

        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("name", "Root"))]);
        provider.Seed("child_model", [
            Row("child-1", ("parent_id", "root-1"), ("name", "Child 1")),
            Row("child-2", ("parent_id", "root-1"), ("name", "Child 2"))
        ]);
        var service = CreateService(db, provider);

        var response = await service.ExecuteOperationAsync(
            "root_model",
            new RuntimeModelOperationRequest(
                "deleteComposite",
                new Dictionary<string, object?>
                {
                    ["currentRow"] = new Dictionary<string, object?> { ["__runtimeKey"] = "root-1" }
                },
                null,
                null));

        Assert.Equal("compositeDelete", response.OperationType);
        var result = Assert.IsType<RuntimeCompositeDeleteResponse>(response.Result);
        Assert.True(result.Root.Deleted);
        Assert.Equal(2, result.Children.Single().DeletedCount);
        Assert.Empty(provider.Rows("root_model"));
        Assert.Empty(provider.Rows("child_model"));
    }

    [Fact]
    public async Task MicroflowCallApiNode_ExecutesPublishedMicroflowEndpoint()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertMicroflowAsync(db, "target_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = ReturnConfig(
                        "result",
                        "返回结果",
                        "object",
                        null,
                        ReturnField("message", "消息", "string", "variables", "message", "string"))
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-return",
                    SourceNodeId = "start",
                    TargetNodeId = "return"
                }
            ]
        });
        await InsertApiServiceAsync(db, "target-flow-api", "/api/app/target-flow", "POST", "target_flow");
        await InsertMicroflowAsync(db, "caller_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "call",
                    Name = "Call API",
                    Type = "callApi",
                    Config = new Dictionary<string, object?>
                    {
                        ["routePath"] = "/api/app/target-flow",
                        ["httpMethod"] = "POST",
                        ["targetVariable"] = "apiResult",
                        ["bodyMappings"] = new[]
                        {
                            new ApplicationMicroflowDataMappingDefinition
                            {
                                Target = "message",
                                Expression = Expression("variables", "message", "string")
                            }
                        }
                    }
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-call",
                    SourceNodeId = "start",
                    TargetNodeId = "call"
                }
            ]
        });
        var runtime = CreateMicroflowRuntimeService(db);

        var response = await runtime.ExecuteAsync(
            "caller_flow",
            new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?> { ["message"] = "hello" }, null));

        Assert.Equal("caller_flow", response.FlowCode);
        var resultResponse = Assert.IsType<ApplicationMicroflowExecuteResponse>(response.Result);
        Assert.Equal("target_flow", resultResponse.FlowCode);
        var resultPayload = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(resultResponse.Result);
        Assert.Equal("hello", resultPayload["message"]);
        Assert.True(response.Variables.TryGetValue("apiResult", out var apiResult));
        var nestedResponse = Assert.IsType<ApplicationMicroflowExecuteResponse>(apiResult);
        Assert.Equal("target_flow", nestedResponse.FlowCode);
        var nestedPayload = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(nestedResponse.Result);
        Assert.Equal("hello", nestedPayload["message"]);
    }

    [Fact]
    public async Task MicroflowReturnNode_ProjectsParameterizedSqlScript()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationDataSourceEntity, ApplicationSqlScriptAuditEntity>();
        db.Ado.ExecuteCommand("CREATE TABLE return_orders (id TEXT NOT NULL, customer_name TEXT NOT NULL, total_amount REAL NOT NULL)");
        db.Ado.ExecuteCommand(
            "INSERT INTO return_orders (id, customer_name, total_amount) VALUES (@id, @customer, @amount)",
            new SugarParameter("@id", "order-1"),
            new SugarParameter("@customer", "客户A"),
            new SugarParameter("@amount", 128.5m));
        db.Ado.ExecuteCommand(
            "INSERT INTO return_orders (id, customer_name, total_amount) VALUES (@id, @customer, @amount)",
            new SugarParameter("@id", "order-2"),
            new SugarParameter("@customer", "客户B"),
            new SugarParameter("@amount", 256m));
        await InsertSqliteDataSourceAsync(db, "return-sql-source");
        await InsertMicroflowAsync(db, "return_sql_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = new Dictionary<string, object?>
                    {
                        ["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
                        {
                            Fields =
                            [
                                SqlRowReturnField("orderId", "订单ID", "string", "id"),
                                SqlRowReturnField("customerName", "客户名称", "string", "customer_name"),
                                SqlRowReturnField("totalAmount", "总金额", "number", "total_amount")
                            ],
                            SourceMode = "sqlScript",
                            SqlScript = new ApplicationMicroflowSqlScriptDefinition
                            {
                                DataSourceId = "return-sql-source",
                                LocalVariables = [],
                                MaxRows = 10,
                                Parameters =
                                [
                                    new ApplicationMicroflowSqlScriptParameterDefinition
                                    {
                                        DataType = "string",
                                        Name = "customerName",
                                        Expression = Expression("variables", "customerName", "string")
                                    }
                                ],
                                ResultShape = new ApplicationMicroflowSqlScriptResultShapeDefinition
                                {
                                    Fields =
                                    [
                                        ReturnFieldMetadata("id", "订单ID", "string"),
                                        ReturnFieldMetadata("customer_name", "客户名称", "string"),
                                        ReturnFieldMetadata("total_amount", "总金额", "number")
                                    ],
                                    ValueType = "object"
                                },
                                Script = "RETURN SELECT id, customer_name, total_amount FROM return_orders WHERE customer_name = @customerName"
                            },
                            ValueType = "object",
                            VariableCode = "sqlReturn",
                            VariableName = "SQL 返回"
                        }
                    }
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-return",
                    SourceNodeId = "start",
                    TargetNodeId = "return"
                }
            ]
        });
        var runtime = CreateMicroflowRuntimeService(db);

        var response = await runtime.ExecuteAsync(
            "return_sql_flow",
            new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?> { ["customerName"] = "客户B" }, null));

        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Result);
        Assert.Equal("order-2", result["orderId"]);
        Assert.Equal("客户B", result["customerName"]);
        Assert.Equal(256d, Convert.ToDouble(result["totalAmount"], CultureInfo.InvariantCulture));
        Assert.DoesNotContain("sqlRows", result.Keys);
        Assert.True(response.Variables.TryGetValue("sqlReturn", out var sqlReturn));
        Assert.Equal(result, sqlReturn);

        var audit = await db.Queryable<ApplicationSqlScriptAuditEntity>().SingleAsync();
        Assert.True(audit.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(audit.TraceId));
        Assert.Equal("MicroflowReturnSql", audit.SourceKind);
        Assert.Equal("return", audit.SourceId);
        Assert.Equal("return-sql-source", audit.DataSourceId);
        Assert.Equal(1, audit.ReturnedRows);
        Assert.Contains("RETURN SELECT", audit.StatementSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customerName", audit.ParameterSummaryJson, StringComparison.Ordinal);
        Assert.DoesNotContain("客户B", audit.ParameterSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MicroflowDetailNode_LoadsModelDetailIntoTargetVariable()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertMicroflowAsync(db, "detail_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "detail",
                    Name = "Detail",
                    Type = "detail",
                    Config = new Dictionary<string, object?>
                    {
                        ["modelCode"] = "root_model",
                        ["targetVariable"] = "detail",
                        ["idExpression"] = Expression("variables", "id", "string")
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = ReturnConfig(
                        "detailResult",
                        "详情结果",
                        "object",
                        null,
                        ReturnField("id", "ID", "string", "variables", "detail.id", "string"),
                        ReturnField("name", "名称", "string", "variables", "detail.name", "string"))
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-detail",
                    SourceNodeId = "start",
                    TargetNodeId = "detail"
                },
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-detail-return",
                    SourceNodeId = "detail",
                    TargetNodeId = "return"
                }
            ]
        });
        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("name", "Root detail"))]);
        var runtime = CreateMicroflowRuntimeService(db, provider);

        var response = await runtime.ExecuteAsync(
            "detail_flow",
            new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?> { ["id"] = "root-1" }, null));

        var row = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Result);
        Assert.Equal("root-1", row["id"]);
        Assert.Equal("Root detail", row["name"]);
        Assert.True(response.Variables.TryGetValue("detail", out var variableValue));
        var variableRow = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(variableValue);
        Assert.Equal("root-1", variableRow["id"]);
    }

    [Fact]
    public async Task MicroflowTargetVariable_CanWriteNestedVariablePath()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertMicroflowAsync(db, "nested_detail_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "detail",
                    Name = "Detail",
                    Type = "detail",
                    Config = new Dictionary<string, object?>
                    {
                        ["modelCode"] = "root_model",
                        ["targetVariable"] = "order.detail",
                        ["idExpression"] = Expression("variables", "id", "string")
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = ReturnConfig(
                        "result",
                        "返回结果",
                        "object",
                        null,
                        ReturnField("name", "名称", "string", "variables", "order.detail.name", "string"))
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-detail-return",
                    SourceNodeId = "detail",
                    TargetNodeId = "return"
                }
            ]
        });
        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("name", "Nested detail"))]);
        var runtime = CreateMicroflowRuntimeService(db, provider);

        var response = await runtime.ExecuteAsync(
            "nested_detail_flow",
            new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?> { ["id"] = "root-1" }, "detail"));

        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Result);
        Assert.Equal("Nested detail", result["name"]);
        Assert.True(response.Variables.TryGetValue("order", out var order));
        var orderObject = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(order);
        var detail = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(orderObject["detail"]);
        Assert.Equal("root-1", detail["id"]);
    }

    [Fact]
    public async Task MicroflowLoopNode_CanIterateQueryRowsFromVariablesItems()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "order_model", "订单", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertMicroflowAsync(db, "loop_query_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "query",
                    Name = "Query Orders",
                    Type = "query",
                    Config = new Dictionary<string, object?>
                    {
                        ["modelCode"] = "order_model",
                        ["targetVariable"] = "items",
                        ["pageIndex"] = 1,
                        ["pageSize"] = 20
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "loop",
                    Name = "Loop Orders",
                    Type = "loop",
                    Config = new Dictionary<string, object?>
                    {
                        ["collectionExpression"] = Expression("variables", "items", "array"),
                        ["itemVariable"] = "item",
                        ["bodyNodeId"] = "assign"
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "assign",
                    Name = "Assign Last Name",
                    Type = "setVariable",
                    Config = new Dictionary<string, object?>
                    {
                        ["variableCode"] = "lastName",
                        ["valueExpression"] = Expression("item", "name", "string")
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = ReturnConfig(
                        "result",
                        "返回结果",
                        "object",
                        null,
                        ReturnField("lastName", "最后订单名称", "string", "variables", "lastName", "string"))
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-query-loop",
                    SourceNodeId = "query",
                    TargetNodeId = "loop"
                },
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-loop-return",
                    SourceNodeId = "loop",
                    TargetNodeId = "return",
                    Condition = "done"
                }
            ]
        });
        var provider = new InMemoryDataModelProvider();
        provider.Seed("order_model", [
            Row("order-1", ("name", "Order 1")),
            Row("order-2", ("name", "Order 2"))
        ]);
        var runtime = CreateMicroflowRuntimeService(db, provider);

        var response = await runtime.ExecuteAsync(
            "loop_query_flow",
            new ApplicationMicroflowExecuteRequest(null, "query"));

        var loopResult = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Result);
        Assert.Equal("Order 2", loopResult["lastName"]);
        var items = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(response.Variables["items"]);
        Assert.Equal(2, items.Count);
        Assert.Contains("loop:loop", response.Trace);
    }

    [Fact]
    public async Task MicroflowQueryNode_BindsInputVariableToFilterExpression()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "order_model", "订单", "id", [
            Field("id", writable: false),
            Field("customerName", writable: true, queryable: true)
        ]);
        await InsertMicroflowAsync(db, "input_filter_flow", new ApplicationMicroflowDefinition
        {
            Inputs =
            [
                new ApplicationMicroflowVariableDefinition
                {
                    VariableCode = "customerName",
                    VariableName = "客户名称",
                    ValueType = "string"
                }
            ],
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "query",
                    Name = "Query Orders",
                    Type = "query",
                    Config = new Dictionary<string, object?>
                    {
                        ["modelCode"] = "order_model",
                        ["targetVariable"] = "items",
                        ["pageIndex"] = 1,
                        ["pageSize"] = 20,
                        ["filters"] = new List<RuntimeModelFilterMappingDto>
                        {
                            new()
                            {
                                Field = "customerName",
                                Operator = "equals",
                                ValueExpression = Expression("inputs", "customerName", "string")
                            }
                        }
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = ReturnConfig(
                        "itemsResult",
                        "订单列表",
                        "array",
                        Expression("variables", "items", "array"),
                        ReturnField("id", "ID", "string", "currentRow", "id", "string"),
                        ReturnField("customerName", "客户名称", "string", "currentRow", "customerName", "string"))
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-query-return",
                    SourceNodeId = "query",
                    TargetNodeId = "return"
                }
            ]
        });
        var provider = new InMemoryDataModelProvider();
        provider.Seed("order_model", [
            Row("order-1", ("customerName", "客户A")),
            Row("order-2", ("customerName", "客户B"))
        ]);
        var runtime = CreateMicroflowRuntimeService(db, provider);

        var customerAResponse = await runtime.ExecuteAsync(
            "input_filter_flow",
            new ApplicationMicroflowExecuteRequest(
                Variables: new Dictionary<string, object?> { ["customerName"] = "客户A" },
                StartNodeId: "query"));
        var customerBResponse = await runtime.ExecuteAsync(
            "input_filter_flow",
            new ApplicationMicroflowExecuteRequest(
                Variables: new Dictionary<string, object?> { ["customerName"] = "客户B" },
                StartNodeId: "query"));

        var customerARows = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(customerAResponse.Result);
        var customerBRows = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(customerBResponse.Result);
        Assert.Single(customerARows);
        Assert.Single(customerBRows);
        Assert.Equal("客户A", customerARows[0]["customerName"]);
        Assert.Equal("客户B", customerBRows[0]["customerName"]);
    }

    [Fact]
    public async Task MicroflowLoopNode_ReportsNodePathAndActualTypeWhenCollectionIsNotArray()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertMicroflowAsync(db, "loop_error_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "loop",
                    Name = "Loop Orders",
                    Type = "loop",
                    Config = new Dictionary<string, object?>
                    {
                        ["collectionExpression"] = Expression("variables", "items", "array"),
                        ["itemVariable"] = "item",
                        ["bodyNodeId"] = "assign"
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "assign",
                    Name = "Assign",
                    Type = "setVariable",
                    Config = new Dictionary<string, object?>
                    {
                        ["variableCode"] = "lastName",
                        ["valueExpression"] = Expression("item", "name", "string")
                    }
                }
            ]
        });
        var runtime = CreateMicroflowRuntimeService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            runtime.ExecuteAsync(
                "loop_error_flow",
                new ApplicationMicroflowExecuteRequest(
                    new Dictionary<string, object?> { ["items"] = "not-array" },
                    "loop")));

        Assert.Contains("nodeId=loop", exception.Message, StringComparison.Ordinal);
        Assert.Contains("nodeName=Loop Orders", exception.Message, StringComparison.Ordinal);
        Assert.Contains("kind=ref", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dataType=array", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ref=global:items:(root)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("actualType=String", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MicroflowReturnNode_ReportsExpressionContextWhenArrayResultIsNotArray()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertMicroflowAsync(db, "return_array_error_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return Items",
                    Type = "return",
                    Config = ReturnConfig(
                        "itemsResult",
                        "列表",
                        "array",
                        Expression("variables", "items", "array"),
                        ReturnField("id", "ID", "string", "currentRow", "id", "string"))
                }
            ]
        });
        var runtime = CreateMicroflowRuntimeService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            runtime.ExecuteAsync(
                "return_array_error_flow",
                new ApplicationMicroflowExecuteRequest(
                    new Dictionary<string, object?> { ["items"] = "not-array" },
                    "return")));

        Assert.Contains("nodeId=return", exception.Message, StringComparison.Ordinal);
        Assert.Contains("nodeName=Return Items", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expressionName=outputSchema.arrayExpression", exception.Message, StringComparison.Ordinal);
        Assert.Contains("kind=ref", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dataType=array", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ref=global:items:(root)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("actualType=String", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MicroflowCompositeCreateNode_ReportsChildRowsExpressionContextWhenRowsAreNotArray()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertMicroflowAsync(db, "composite_rows_error_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "save",
                    Name = "Composite Save",
                    Type = "compositeCreate",
                    Config = new Dictionary<string, object?>
                    {
                        ["rootModelCode"] = "root_model",
                        ["children"] = new List<ApplicationMicroflowCompositeChildCreateDefinition>
                        {
                            new()
                            {
                                ModelCode = "child_model",
                                ParentKeyField = "id",
                                ForeignKeyField = "parent_id",
                                RowsExpression = Expression("variables", "children", "array")
                            }
                        }
                    }
                }
            ]
        });
        var runtime = CreateMicroflowRuntimeService(db);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            runtime.ExecuteAsync(
                "composite_rows_error_flow",
                new ApplicationMicroflowExecuteRequest(
                    new Dictionary<string, object?> { ["children"] = "not-array" },
                    "save")));

        Assert.Contains("nodeId=save", exception.Message, StringComparison.Ordinal);
        Assert.Contains("nodeName=Composite Save", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expressionName=children[0].rowsExpression", exception.Message, StringComparison.Ordinal);
        Assert.Contains("modelCode=child_model", exception.Message, StringComparison.Ordinal);
        Assert.Contains("kind=ref", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dataType=array", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ref=global:children:(root)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("actualType=String", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MicroflowCompositeDetailNode_LoadsRootAndLinkedChildRows()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);
        await InsertMicroflowAsync(db, "composite_detail_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "detail",
                    Name = "Composite Detail",
                    Type = "compositeDetail",
                    Config = new Dictionary<string, object?>
                    {
                        ["rootModelCode"] = "root_model",
                        ["targetVariable"] = "detailResult",
                        ["idExpression"] = Expression("variables", "id", "string"),
                        ["children"] = new[]
                        {
                            new ApplicationMicroflowCompositeChildDetailDefinition
                            {
                                ModelCode = "child_model",
                                ParentKeyField = "id",
                                ForeignKeyField = "parent_id",
                                BindingKey = "children",
                                PageSize = 50
                            }
                        }
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = ReturnConfig(
                        "result",
                        "返回结果",
                        "object",
                        null,
                        ReturnField("rootName", "主对象名称", "string", "variables", "detailResult.Root.Row.name", "string"),
                        ReturnField(
                            "childGroupCount",
                            "子对象组数",
                            "number",
                            "variables",
                            "detailResult.Children",
                            "number",
                            new RuntimeExpressionHelperDto { Name = "count" }))
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-detail",
                    SourceNodeId = "start",
                    TargetNodeId = "detail"
                },
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-detail-return",
                    SourceNodeId = "detail",
                    TargetNodeId = "return"
                }
            ]
        });
        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("name", "Root"))]);
        provider.Seed("child_model", [
            Row("child-1", ("parent_id", "root-1"), ("name", "Child 1")),
            Row("child-2", ("parent_id", "root-1"), ("name", "Child 2")),
            Row("child-other", ("parent_id", "root-2"), ("name", "Other child"))
        ]);
        var runtime = CreateMicroflowRuntimeService(db, provider);

        var response = await runtime.ExecuteAsync(
            "composite_detail_flow",
            new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?> { ["id"] = "root-1" }, null));

        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Result);
        Assert.Equal("Root", result["rootName"]);
        Assert.Equal(1, result["childGroupCount"]);
        var detailResult = Assert.IsType<RuntimeCompositeDetailResponse>(response.Variables["detailResult"]);
        var childGroup = Assert.Single(detailResult.Children);
        Assert.Equal("children", childGroup.BindingKey);
        Assert.Equal(2, childGroup.Data.Rows.Count);
        Assert.DoesNotContain(childGroup.Data.Rows, row => string.Equals(row["id"]?.ToString(), "child-other", StringComparison.OrdinalIgnoreCase));
        Assert.True(response.Variables.TryGetValue("detailResult", out var variableValue));
        Assert.IsType<RuntimeCompositeDetailResponse>(variableValue);
    }

    [Fact]
    public async Task MicroflowCompositeCreateNode_CreatesRootAndChildRowsFromVariables()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);
        await InsertMicroflowAsync(db, "composite_create_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "create",
                    Name = "Composite Create",
                    Type = "compositeCreate",
                    Config = new Dictionary<string, object?>
                    {
                        ["rootModelCode"] = "root_model",
                        ["targetVariable"] = "saveResult",
                        ["fieldMappings"] = new[]
                        {
                            new ApplicationMicroflowDataMappingDefinition
                            {
                                Target = "name",
                                Expression = Expression("form", "name", "string")
                            }
                        },
                        ["children"] = new[]
                        {
                            new ApplicationMicroflowCompositeChildCreateDefinition
                            {
                                ModelCode = "child_model",
                                ParentKeyField = "id",
                                ForeignKeyField = "parent_id",
                                RowsExpression = Expression("variables", "children", "array"),
                                FieldMappings =
                                [
                                    new ApplicationMicroflowDataMappingDefinition
                                    {
                                        Target = "name",
                                        Expression = Expression("currentRow", "name", "string")
                                    }
                                ]
                            }
                        }
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = ReturnConfig(
                        "result",
                        "返回结果",
                        "object",
                        null,
                        ReturnField("rootId", "主对象 ID", "string", "variables", "saveResult.Root.Id", "string"),
                        ReturnField(
                            "childGroupCount",
                            "子对象组数",
                            "number",
                            "variables",
                            "saveResult.Children",
                            "number",
                            new RuntimeExpressionHelperDto { Name = "count" }))
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-create",
                    SourceNodeId = "start",
                    TargetNodeId = "create"
                },
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-create-return",
                    SourceNodeId = "create",
                    TargetNodeId = "return"
                }
            ]
        });
        var provider = new InMemoryDataModelProvider();
        var runtime = CreateMicroflowRuntimeService(db, provider);

        var response = await runtime.ExecuteAsync(
            "composite_create_flow",
            new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?>
            {
                ["form"] = new Dictionary<string, object?> { ["name"] = "Root via microflow" },
                ["children"] = new[]
                {
                    new Dictionary<string, object?> { ["name"] = "Child A" },
                    new Dictionary<string, object?> { ["name"] = "Child B" }
                }
            }, null));

        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(result["rootId"]?.ToString()));
        Assert.Equal(1, result["childGroupCount"]);
        Assert.True(response.Variables.TryGetValue("saveResult", out var variableValue));
        var variableResult = Assert.IsType<RuntimeCompositeCreateResponse>(variableValue);
        Assert.Equal(result["rootId"], variableResult.Root.Id);
        Assert.Equal("Root via microflow", variableResult.Root.Row["name"]);
        Assert.Equal(2, variableResult.Children.Single().Rows.Count);
        Assert.All(provider.Rows("child_model"), row => Assert.Equal(variableResult.Root.Id, row["parent_id"]));
    }

    [Fact]
    public async Task MicroflowCompositeDeleteNode_DeletesLinkedChildRows()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity, ApplicationApiServiceEntity, SystemDataModelEntity>();
        await InsertPublishedModelAsync(db, "root_model", "主对象", "id", [
            Field("id", writable: false),
            Field("name", writable: true)
        ]);
        await InsertPublishedModelAsync(db, "child_model", "子对象", "id", [
            Field("id", writable: false),
            Field("parent_id", writable: true, queryable: true),
            Field("name", writable: true)
        ]);
        await InsertMicroflowAsync(db, "composite_delete_flow", new ApplicationMicroflowDefinition
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "delete",
                    Name = "Composite Delete",
                    Type = "compositeDelete",
                    Config = new Dictionary<string, object?>
                    {
                        ["rootModelCode"] = "root_model",
                        ["targetVariable"] = "deleteResult",
                        ["idExpression"] = Expression("variables", "id", "string"),
                        ["children"] = new[]
                        {
                            new ApplicationMicroflowCompositeChildDeleteDefinition
                            {
                                ModelCode = "child_model",
                                ForeignKeyField = "parent_id",
                                ParentIdExpression = Expression("variables", "id", "string"),
                                Required = true
                            }
                        }
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = ReturnConfig(
                        "result",
                        "返回结果",
                        "object",
                        null,
                        ReturnField("rootDeleted", "主对象已删除", "boolean", "variables", "deleteResult.Root.Deleted", "boolean"),
                        ReturnField("deletedChildCount", "已删除子对象数", "number", "variables", "deleteResult.Children.0.DeletedCount", "number"))
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-delete",
                    SourceNodeId = "start",
                    TargetNodeId = "delete"
                },
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-delete-return",
                    SourceNodeId = "delete",
                    TargetNodeId = "return"
                }
            ]
        });
        var provider = new InMemoryDataModelProvider();
        provider.Seed("root_model", [Row("root-1", ("name", "Root"))]);
        provider.Seed("child_model", [
            Row("child-1", ("parent_id", "root-1"), ("name", "Child 1")),
            Row("child-2", ("parent_id", "root-1"), ("name", "Child 2")),
            Row("child-other", ("parent_id", "root-2"), ("name", "Other child"))
        ]);
        var runtime = CreateMicroflowRuntimeService(db, provider);

        var response = await runtime.ExecuteAsync(
            "composite_delete_flow",
            new ApplicationMicroflowExecuteRequest(new Dictionary<string, object?> { ["id"] = "root-1" }, null));

        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Result);
        Assert.True((bool)result["rootDeleted"]!);
        Assert.Equal(2, result["deletedChildCount"]);
        Assert.True(response.Variables.TryGetValue("deleteResult", out var variableValue));
        var variableResult = Assert.IsType<RuntimeCompositeDeleteResponse>(variableValue);
        Assert.True(variableResult.Root.Deleted);
        Assert.Empty(provider.Rows("root_model"));
        Assert.Single(provider.Rows("child_model"));
        Assert.Equal("child-other", provider.Rows("child_model").Single()["id"]);
    }

    [Fact]
    public async Task ExecuteAsync_InitializesGlobalVariableNodeDefaultsWithoutTrace()
    {
        using var db = CreateDb();
        db.CodeFirst.InitTables<ApplicationMicroflowEntity>();
        await InsertMicroflowAsync(db, "global_variable_flow", new ApplicationMicroflowDefinition
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "globals",
                    Name = "全局变量",
                    Type = "globalVariables",
                    Config = new Dictionary<string, object?>
                    {
                        ["variables"] = new List<ApplicationMicroflowVariableDefinition>
                        {
                            new()
                            {
                                DefaultValue = 42,
                                ValueType = "number",
                                VariableCode = "threshold",
                                VariableName = "阈值"
                            }
                        }
                    }
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = new Dictionary<string, object?>
                    {
                        ["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
                        {
                            VariableCode = "result",
                            VariableName = "返回结果",
                            ValueType = "object",
                            Fields =
                            [
                                new ApplicationMicroflowFieldDefinition
                                {
                                    DataType = "number",
                                    FieldCode = "threshold",
                                    FieldName = "阈值",
                                    Expression = Expression("variables", "threshold", "number")
                                }
                            ]
                        }
                    }
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-return",
                    SourceNodeId = "start",
                    TargetNodeId = "return"
                }
            ]
        });
        var runtime = CreateMicroflowRuntimeService(db);

        var response = await runtime.ExecuteAsync("global_variable_flow", new ApplicationMicroflowExecuteRequest());

        var result = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(response.Result);
        Assert.Equal(42, ReadInt32(result["threshold"]));
        Assert.Equal(42, ReadInt32(response.Variables["threshold"]));
        Assert.DoesNotContain(response.Trace, item => item.StartsWith("globalVariables:", StringComparison.OrdinalIgnoreCase));
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

    private RuntimeDataModelService CreateService(ISqlSugarClient db, IDataModelProvider provider)
    {
        var currentUser = CreateCurrentUser();
        return new RuntimeDataModelService(
            new FixedWorkspaceDatabaseAccessor(db),
            currentUser,
            new DataModelProviderRegistry([provider]),
            new UnusedRuntimeGridViewService(),
            new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog()),
            NullLogger<RuntimeDataModelService>.Instance);
    }

    private ApplicationMicroflowRuntimeService CreateMicroflowRuntimeService(
        ISqlSugarClient db,
        IDataModelProvider? provider = null)
    {
        var currentUser = CreateCurrentUser();
        var hostRoot = Path.GetDirectoryName(databasePath) ?? Path.GetTempPath();
        var expressionEvaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());
        var workspaceResolver = new ApplicationDataCenterWorkspaceResolver(currentUser);
        var databaseAccessor = new FixedWorkspaceDatabaseAccessor(db);
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "test-sql-script-trace"
            }
        };
        var connectionFactory = new ApplicationDataSourceConnectionFactory(
            new TestHostEnvironment(hostRoot),
            new NoopApplicationDataSecretProtector(),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance));
        var sqlParser = new ApplicationDataCenterSqlScriptParser();
        var functionCatalog = new RuntimeExpressionFunctionCatalog();
        var tokenizer = new ApplicationDataCenterSqlScriptExpressionTokenizer();
        var expressionParser = new ApplicationDataCenterSqlScriptExpressionParser(tokenizer);
        var scriptExpressionEvaluator = new ApplicationDataCenterSqlScriptExpressionEvaluator(
            expressionParser,
            functionCatalog,
            new RuntimeExpressionHelperCatalog(),
            new ApplicationDataCenterSqlRbacFunctionEvaluator(currentUser));
        var functionParameterizer = new ApplicationDataCenterSqlScriptFunctionParameterizer(
            expressionParser,
            scriptExpressionEvaluator);
        var sqlValidator = new ApplicationDataCenterSqlScriptValidator(sqlParser, functionParameterizer);
        var sqlEngine = new ApplicationDataCenterSqlScriptEngine(
            databaseAccessor,
            workspaceResolver,
            expressionEvaluator,
            connectionFactory,
            new ApplicationDataCenterSqlBuiltInVariableProvider(currentUser),
            sqlParser,
            sqlValidator,
            scriptExpressionEvaluator,
            functionParameterizer,
            new ApplicationDataCenterSqlScriptIfElseBlockReader(),
            new ApplicationDataCenterSqlScriptResultProjector(),
            new ApplicationDataCenterSqlScriptAuditWriter(
                databaseAccessor,
                workspaceResolver,
                currentUser,
                NullLogger<ApplicationDataCenterSqlScriptAuditWriter>.Instance),
            httpContextAccessor,
            NullLogger<ApplicationDataCenterSqlScriptEngine>.Instance);
        return new ApplicationMicroflowRuntimeService(
            databaseAccessor,
            workspaceResolver,
            CreateService(db, provider ?? new InMemoryDataModelProvider()),
            expressionEvaluator,
            sqlEngine,
            sqlValidator,
            connectionFactory,
            new ApplicationDataPreviewReader(CreateProviderRegistry()),
            currentUser,
            new NoopHttpClientFactory(),
            NullLogger<ApplicationMicroflowRuntimeService>.Instance);
    }

    private static ApplicationDataSourceProviderRegistry CreateProviderRegistry() =>
        new([
            new SqliteApplicationDataSourceProvider(),
            new MySqlApplicationDataSourceProvider(),
            new PostgreSqlApplicationDataSourceProvider(),
            new SqlServerApplicationDataSourceProvider()
        ]);

    private static int ReadInt32(object? value)
    {
        return value is JsonElement element
            ? element.GetInt32()
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

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
            ConfigJson = JsonSerializer.Serialize(definition, ApplicationDataCenterJson.Options)
        }).ExecuteCommandAsync();
    }

    private async Task InsertSqliteDataSourceAsync(
        ISqlSugarClient db,
        string dataSourceId)
    {
        await db.Insertable(new ApplicationDataSourceEntity
        {
            Id = dataSourceId,
            TenantId = TenantId,
            AppCode = AppCode,
            ModuleKey = ApplicationDataCenterModuleKey.DataSource,
            ObjectCode = dataSourceId,
            ObjectName = dataSourceId,
            ObjectType = ApplicationDataSourceType.Sqlite,
            Status = ApplicationDataCenterObjectStatus.Normal,
            ConfigJson = ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
            {
                ["databaseName"] = databasePath
            })
        }).ExecuteCommandAsync();
    }

    private static async Task InsertApiServiceAsync(
        ISqlSugarClient db,
        string objectCode,
        string routePath,
        string httpMethod,
        string flowCode)
    {
        await db.Insertable(new ApplicationApiServiceEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = TenantId,
            AppCode = AppCode,
            ModuleKey = ApplicationDataCenterModuleKey.ApiService,
            ObjectCode = objectCode,
            ObjectName = objectCode,
            ObjectType = ApplicationApiServiceSourceType.Microflow,
            Status = ApplicationDataCenterObjectStatus.Published,
            VersionNo = 1,
            HttpMethod = httpMethod,
            RoutePath = routePath,
            RequiresAuthentication = false,
            ConfigJson = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
            ["flowCode"] = flowCode
            }, ApplicationDataCenterJson.Options)
        }).ExecuteCommandAsync();
    }

    private static RuntimeValueExpressionDto Expression(string source, string path, string dataType) =>
        ReferenceExpression(source, path, dataType);

    private static RuntimeValueExpressionDto FunctionExpression(
        string functionId,
        string dataType,
        params RuntimeValueExpressionDto[] args) =>
        new()
        {
            Args = args.ToList(),
            DataType = dataType,
            FunctionId = functionId,
            Kind = "function"
        };

    private static RuntimeValueExpressionDto LiteralExpression(object? value, string dataType) =>
        new()
        {
            DataType = dataType,
            Kind = "literal",
            Value = value
        };

    private static RuntimeValueExpressionDto ReferenceExpression(
        string source,
        string path,
        string dataType)
    {
        var parts = path
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var sourceType = source.Equals("variables", StringComparison.OrdinalIgnoreCase)
            ? "global"
            : source.Equals("inputs", StringComparison.OrdinalIgnoreCase)
                ? "trigger"
                : source.Trim();
        var outputKey = parts.FirstOrDefault() ?? string.Empty;
        var fieldPath = parts.Skip(1).ToList();
        if (!source.Equals("variables", StringComparison.OrdinalIgnoreCase) &&
            !source.Equals("inputs", StringComparison.OrdinalIgnoreCase) &&
            (source.Equals("item", StringComparison.OrdinalIgnoreCase) ||
             source.Equals("currentRow", StringComparison.OrdinalIgnoreCase) ||
             source.Equals("lineItem", StringComparison.OrdinalIgnoreCase)))
        {
            fieldPath = parts;
            outputKey = string.Empty;
        }

        return new RuntimeValueExpressionDto
        {
            DataType = dataType,
            Kind = "ref",
            Ref = new RuntimeVariableRefDto
            {
                DataType = dataType,
                FieldPath = fieldPath,
                Label = string.IsNullOrWhiteSpace(outputKey)
                    ? string.Join('.', fieldPath)
                    : string.Join('.', new[] { outputKey }.Concat(fieldPath)),
                OutputKey = outputKey,
                SourceType = sourceType,
                VariableId = string.IsNullOrWhiteSpace(outputKey) ? source : outputKey
            }
        };
    }

    private static Dictionary<string, object?> ReturnConfig(
        string variableCode,
        string variableName,
        string valueType,
        RuntimeValueExpressionDto? arrayExpression,
        params ApplicationMicroflowFieldDefinition[] fields) =>
        new()
        {
            ["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
            {
                ArrayExpression = arrayExpression,
                Fields = fields.ToList(),
                ValueType = valueType,
                VariableCode = variableCode,
                VariableName = variableName
            }
        };

    private static ApplicationMicroflowFieldDefinition ReturnField(
        string fieldCode,
        string fieldName,
        string dataType,
        string source,
        string path,
        string expressionDataType,
        params RuntimeExpressionHelperDto[] helpers) =>
        new()
        {
            DataType = dataType,
            FieldCode = fieldCode,
            FieldName = fieldName,
            Expression = helpers.Aggregate(
                Expression(source, path, expressionDataType),
                (current, helper) => FunctionExpression(
                    helper.Name,
                    expressionDataType,
                    [current, .. helper.Args.Select(item => LiteralExpression(item.Value, InferLiteralType(item.Value)))])),
            Visible = true,
            Writable = false
        };

    private static ApplicationMicroflowFieldDefinition SqlRowReturnField(
        string fieldCode,
        string fieldName,
        string dataType,
        string sqlFieldCode) =>
        new()
        {
            DataType = dataType,
            FieldCode = fieldCode,
            FieldName = fieldName,
            Expression = new RuntimeValueExpressionDto
            {
                DataType = dataType,
                Kind = "ref",
                Ref = new RuntimeVariableRefDto
                {
                    DataType = dataType,
                    FieldPath = [sqlFieldCode],
                    Label = $"SQL结果.{sqlFieldCode}",
                    OutputKey = "sqlRow",
                    SourceType = "sqlResult",
                    VariableId = "sqlRow"
                }
            },
            Visible = true,
            Writable = false
        };

    private static ApplicationMicroflowFieldDefinition ReturnFieldMetadata(
        string fieldCode,
        string fieldName,
        string dataType) =>
        new()
        {
            DataType = dataType,
            FieldCode = fieldCode,
            FieldName = fieldName,
            Visible = true,
            Writable = false
        };

    private static string InferLiteralType(object? value) =>
        value switch
        {
            bool => "boolean",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "number",
            global::System.Collections.IEnumerable when value is not string => "array",
            _ => "string"
        };

    private async Task InsertPublishedModelAsync(
        ISqlSugarClient db,
        string modelCode,
        string modelName,
        string keyField,
        IReadOnlyList<RuntimeDataFieldDefinition> fields,
        string? providerKey = null,
        IReadOnlyDictionary<string, object?>? source = null,
        IReadOnlyList<RuntimeModelOperationDefinitionDto>? operations = null)
    {
        var schema = new RuntimeDataModelSchema
        {
            Fields = fields.ToList(),
            IdGeneration = RuntimeModelIdGeneration.Guid,
            Operations = operations?.ToList() ?? [],
            Source = source is null
                ? []
                : new Dictionary<string, object?>(source, StringComparer.OrdinalIgnoreCase)
        };
        await db.Insertable(new SystemDataModelEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = TenantId,
            AppCode = AppCode,
            ModelCode = modelCode,
            ModelName = modelName,
            ProviderKey = providerKey ?? ProviderKey,
            KeyField = keyField,
            Status = "Published",
            VersionNo = 1,
            SchemaJson = JsonSerializer.Serialize(schema, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }).ExecuteCommandAsync();
    }

    private SqlSugarClient CreateDb() =>
        new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            InitKeyType = InitKeyType.Attribute,
            IsAutoCloseConnection = true
        });

    private ApplicationDataCenterSqlDataModelProvider CreateSqlProvider(ISqlSugarClient db)
    {
        var currentUser = CreateCurrentUser();
        return new ApplicationDataCenterSqlDataModelProvider(
            new FixedWorkspaceDatabaseAccessor(db),
            new ApplicationDataCenterWorkspaceResolver(currentUser),
            new ApplicationDataSourceConnectionFactory(
                new TestHostEnvironment(Path.GetDirectoryName(databasePath) ?? Path.GetTempPath()),
                new NoopApplicationDataSecretProtector(),
                new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance)),
            new RuntimeSnowflakeIdGenerator());
    }

    private static RuntimeDataFieldDefinition Field(
        string fieldCode,
        bool writable,
        bool queryable = false) =>
        new()
        {
            Binding = fieldCode,
            DataType = "string",
            Exportable = true,
            FieldCode = fieldCode,
            FieldName = fieldCode,
            Order = 1,
            Queryable = queryable,
            Sortable = false,
            Visible = true,
            Writable = writable
        };

    private static Dictionary<string, object?> Row(string id, params (string Key, object? Value)[] values)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id
        };
        foreach (var value in values)
        {
            row[value.Key] = value.Value;
        }

        return row;
    }

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
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }

    private sealed class InMemoryDataModelProvider : ITransactionalDataModelProvider
    {
        private readonly Dictionary<string, List<Dictionary<string, object?>>> rows = new(StringComparer.OrdinalIgnoreCase);

        public string ProviderKey => RuntimeCompositeDataModelServiceTests.ProviderKey;

        public string? FailCreateModelCode { get; set; }

        public string? FailCreateName { get; set; }

        public async Task<T> ExecuteInTransactionAsync<T>(
            IReadOnlyList<RuntimeDataModelDefinition> models,
            Func<Task<T>> action,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = rows.ToDictionary(
                item => item.Key,
                item => item.Value.Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)).ToList(),
                StringComparer.OrdinalIgnoreCase);
            try
            {
                var result = await action();
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            catch
            {
                rows.Clear();
                foreach (var item in snapshot)
                {
                    rows[item.Key] = item.Value;
                }

                throw;
            }
        }

        public Task<RuntimeDataModelQueryResult> QueryAsync(
            RuntimeDataModelDefinition model,
            RuntimeDataModelQuery query,
            CancellationToken cancellationToken = default)
        {
            var sourceRows = Rows(model.ModelCode)
                .Where(row => MatchesFilters(row, query.Filters))
                .Skip((Math.Max(query.PageIndex, 1) - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase) as IReadOnlyDictionary<string, object?>)
                .ToArray();
            var total = Rows(model.ModelCode).Count(row => MatchesFilters(row, query.Filters));
            return Task.FromResult(new RuntimeDataModelQueryResult(sourceRows, total));
        }

        public Task<IReadOnlyDictionary<string, object?>?> GetDetailAsync(
            RuntimeDataModelDefinition model,
            string id,
            CancellationToken cancellationToken = default)
        {
            var row = Rows(model.ModelCode).FirstOrDefault(item => string.Equals(item["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(row is null ? null : new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase) as IReadOnlyDictionary<string, object?>);
        }

        public Task<IReadOnlyDictionary<string, object?>?> CreateAsync(
            RuntimeDataModelDefinition model,
            IReadOnlyList<RuntimeDataModelFieldUpdate> values,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(model.ModelCode, FailCreateModelCode, StringComparison.OrdinalIgnoreCase) &&
                values.Any(value => string.Equals(value.Value?.ToString(), FailCreateName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("模拟子对象创建失败");
            }

            var row = values.ToDictionary(
                value => value.Field.FieldCode,
                value => value.Value,
                StringComparer.OrdinalIgnoreCase);
            row.TryAdd("id", $"{model.ModelCode}-{Guid.NewGuid():N}");
            RowsForWrite(model.ModelCode).Add(row);
            IReadOnlyDictionary<string, object?> created = new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyDictionary<string, object?>?>(created);
        }

        public Task<bool> UpdateFieldsAsync(
            RuntimeDataModelDefinition model,
            string id,
            IReadOnlyList<RuntimeDataModelFieldUpdate> updates,
            CancellationToken cancellationToken = default)
        {
            var row = Rows(model.ModelCode).FirstOrDefault(item => string.Equals(item["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                return Task.FromResult(false);
            }

            foreach (var update in updates)
            {
                row[update.Field.FieldCode] = update.Value;
            }

            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(
            RuntimeDataModelDefinition model,
            string id,
            CancellationToken cancellationToken = default)
        {
            var removed = rows.TryGetValue(model.ModelCode, out var modelRows) &&
                modelRows.RemoveAll(item => string.Equals(item["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase)) > 0;
            return Task.FromResult(removed);
        }

        public IReadOnlyList<Dictionary<string, object?>> Rows(string modelCode) =>
            rows.TryGetValue(modelCode, out var modelRows) ? modelRows : [];

        public void Seed(string modelCode, IReadOnlyList<Dictionary<string, object?>> seedRows)
        {
            rows[modelCode] = seedRows.Select(row => new Dictionary<string, object?>(row, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        private List<Dictionary<string, object?>> RowsForWrite(string modelCode)
        {
            if (!rows.TryGetValue(modelCode, out var modelRows))
            {
                modelRows = [];
                rows[modelCode] = modelRows;
            }

            return modelRows;
        }

        private static bool MatchesFilters(
            IReadOnlyDictionary<string, object?> row,
            IReadOnlyList<RuntimeDataModelFilter> filters) =>
            filters.All(filter =>
                !string.Equals(filter.Operator, "equals", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.GetValueOrDefault(filter.Field.FieldCode)?.ToString(), filter.Value?.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FixedWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;

        public ISqlSugarClient GetCurrentDb() => db;

        public ISqlSugarClient RequireApplicationDb() => db;

        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);

        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class UnusedRuntimeGridViewService : IRuntimeGridViewService
    {
        public Task<RuntimeGridViewResponse> GetAsync(string pageCode, string? previewPageId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeGridViewResponse> SaveTenantDefaultAsync(string pageCode, RuntimeGridViewSaveRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeGridViewResponse> SaveUserViewAsync(string pageCode, RuntimeGridViewSaveRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RuntimeGridViewResponse> ResetUserViewAsync(string pageCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoopApplicationDataSecretProtector : IApplicationDataSecretProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string cipherText) => cipherText;

        public string BuildPublicSecretSummary(string? cipherText) =>
            string.IsNullOrWhiteSpace(cipherText) ? "{}" : "{\"configured\":true}";

        public string BuildPublicSecretSummary(string? cipherText, string secretRef, DateTime? updatedAt) =>
            BuildPublicSecretSummary(cipherText);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "AsterERP.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class NoopHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
