using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static class ApplicationDataModelOperationPolicy
{
    private static readonly HashSet<string> SupportedOperationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "query",
        "create",
        "update",
        "delete",
        "compositeCreate",
        "compositeUpdate",
        "compositeDelete"
    };

    public static void ValidateForPublish(IReadOnlyList<RuntimeModelOperationDefinitionDto> operations, string fallbackModelCode)
    {
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operation in operations)
        {
            var operationCode = operation.OperationCode?.Trim();
            if (string.IsNullOrWhiteSpace(operationCode))
            {
                throw new ValidationException("模型操作编码不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (!seenCodes.Add(operationCode))
            {
                throw new ValidationException($"模型操作编码重复：{operationCode}", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var operationType = operation.OperationType?.Trim();
            if (string.IsNullOrWhiteSpace(operationType) || !SupportedOperationTypes.Contains(operationType))
            {
                throw new ValidationException($"模型操作类型不支持：{operation.OperationType}", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var targetModelCode = string.IsNullOrWhiteSpace(operation.ModelCode)
                ? fallbackModelCode
                : operation.ModelCode.Trim();
            if (string.IsNullOrWhiteSpace(targetModelCode))
            {
                throw new ValidationException($"模型操作 {operationCode} 缺少目标模型编码", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (operationType.Equals("query", StringComparison.OrdinalIgnoreCase))
            {
                operation.PageIndex = operation.PageIndex <= 0 ? 1 : operation.PageIndex;
                operation.PageSize = operation.PageSize <= 0 ? 20 : Math.Min(operation.PageSize, 200);
            }

            if (operationType.StartsWith("composite", StringComparison.OrdinalIgnoreCase))
            {
                ValidateCompositeOperation(operation, operationCode);
            }
        }
    }

    private static void ValidateCompositeOperation(RuntimeModelOperationDefinitionDto operation, string operationCode)
    {
        if (operation.Children.Count == 0)
        {
            throw new ValidationException($"复合模型操作 {operationCode} 至少需要配置一个子对象", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        foreach (var child in operation.Children)
        {
            if (string.IsNullOrWhiteSpace(child.ModelCode))
            {
                throw new ValidationException($"复合模型操作 {operationCode} 的子对象模型编码不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (string.IsNullOrWhiteSpace(child.ForeignKeyField))
            {
                throw new ValidationException($"复合模型操作 {operationCode} 的子对象外键字段不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }
        }
    }
}
