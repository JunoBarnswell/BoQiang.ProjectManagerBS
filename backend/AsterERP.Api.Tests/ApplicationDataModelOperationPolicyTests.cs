using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataModelOperationPolicyTests
{
    [Fact]
    public void ValidateForPublish_RejectsUnsupportedOperationType()
    {
        var operations = new[]
        {
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "archive_order",
                OperationType = "archive"
            }
        };

        var exception = Assert.Throws<ValidationException>(() =>
            ApplicationDataModelOperationPolicy.ValidateForPublish(operations, "order_model"));

        Assert.Contains("模型操作类型不支持", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForPublish_RejectsDuplicateOperationCodes()
    {
        var operations = new[]
        {
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "save_order",
                OperationType = "create"
            },
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "save_order",
                OperationType = "update"
            }
        };

        var exception = Assert.Throws<ValidationException>(() =>
            ApplicationDataModelOperationPolicy.ValidateForPublish(operations, "order_model"));

        Assert.Contains("模型操作编码重复", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForPublish_AllowsCrudOperationsAndNormalizesQueryPaging()
    {
        var operations = new[]
        {
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "query_order",
                OperationType = "query",
                PageIndex = 0,
                PageSize = 0
            },
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "create_order",
                OperationType = "create"
            },
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "update_order",
                OperationType = "update"
            },
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "delete_order",
                OperationType = "delete"
            }
        };

        ApplicationDataModelOperationPolicy.ValidateForPublish(operations, "order_model");

        Assert.Equal(1, operations[0].PageIndex);
        Assert.Equal(20, operations[0].PageSize);
    }

    [Fact]
    public void ValidateForPublish_RejectsCompositeOperationWithoutChildren()
    {
        var operations = new[]
        {
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "save_composite",
                OperationType = "compositeCreate"
            }
        };

        var exception = Assert.Throws<ValidationException>(() =>
            ApplicationDataModelOperationPolicy.ValidateForPublish(operations, "order_model"));

        Assert.Contains("至少需要配置一个子对象", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateForPublish_AllowsCompositeOperationsWithChildContract()
    {
        var operations = new[]
        {
            new RuntimeModelOperationDefinitionDto
            {
                OperationCode = "save_composite",
                OperationType = "compositeCreate",
                Children =
                [
                    new RuntimeModelCompositeChildDefinitionDto
                    {
                        ModelCode = "order_line",
                        ForeignKeyField = "order_id",
                        ParentKeyField = "id"
                    }
                ]
            }
        };

        ApplicationDataModelOperationPolicy.ValidateForPublish(operations, "order_model");
    }
}
