using System.Text.Json;
using AsterERP.Workflow.Persistence;
using AsterERP.Workflow.Core.Variable;
using PersistentVariableInstanceEntity = AsterERP.Workflow.Persistence.Entities.VariableInstanceEntity;
using PersistentHistoricVariableInstanceEntity = AsterERP.Workflow.Persistence.Entities.HistoricVariableInstanceEntity;
using PersistentHistoricDetailEntity = AsterERP.Workflow.Persistence.Entities.HistoricDetailEntity;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

internal readonly record struct RuntimeVariableMapping(PersistentVariableInstanceEntity Row, byte[]? Bytes);

internal static class PersistenceVariableCodec
{
    private static readonly VariableTypes VariableTypes = new DefaultVariableTypes();

    public static PersistentVariableInstanceEntity ToRuntimeVariableEntity(
        string executionId,
        string? processInstanceId,
        string? activityId,
        KeyValuePair<string, object?> variable)
    {
        return EncodeRuntimeVariable(executionId, processInstanceId, activityId, variable).Row;
    }

    public static RuntimeVariableMapping EncodeRuntimeVariable(
        string executionId,
        string? processInstanceId,
        string? activityId,
        KeyValuePair<string, object?> variable)
    {
        var runtimeVariable = new VariableInstanceEntity
        {
            Id = $"{executionId}:{variable.Key}",
            Name = variable.Key,
            ExecutionId = executionId,
            ProcessInstanceId = processInstanceId,
            TaskId = null,
            CreateTime = AbpTimeIdProvider.UtcNow,
            LastUpdatedTime = AbpTimeIdProvider.UtcNow
        };

        var variableType = VariableTypes.FindVariableType(variable.Value);
        runtimeVariable.Type = variableType.TypeName;
        variableType.SetValue(variable.Value, runtimeVariable);

        var byteArrayId = runtimeVariable.ByteValue != null ? runtimeVariable.Id : null;

        var row = new PersistentVariableInstanceEntity
        {
            Id = runtimeVariable.Id!,
            Name = runtimeVariable.Name,
            Type = runtimeVariable.Type,
            ExecutionId = runtimeVariable.ExecutionId,
            ProcessInstanceId = runtimeVariable.ProcessInstanceId,
            TaskId = runtimeVariable.TaskId,
            ActivityId = activityId,
            ByteArrayId = byteArrayId,
            TextValue = runtimeVariable.TextValue,
            TextValue2 = runtimeVariable.TextValue2,
            LongValue = runtimeVariable.LongValue,
            DoubleValue = runtimeVariable.DoubleValue,
            CreateTime = runtimeVariable.CreateTime,
            LastUpdatedTime = runtimeVariable.LastUpdatedTime,
            IsActive = true
        };

        return new RuntimeVariableMapping(row, runtimeVariable.ByteValue);
    }

    public static PersistentHistoricVariableInstanceEntity ToHistoricVariableEntity(
        string id,
        string? processInstanceId,
        string? taskId,
        string? name,
        object? value,
        DateTime? createTime,
        DateTime? lastUpdatedTime)
    {
        var runtimeVariable = new VariableInstanceEntity
        {
            Id = id,
            Name = name,
            ProcessInstanceId = processInstanceId,
            TaskId = taskId,
            CreateTime = createTime,
            LastUpdatedTime = lastUpdatedTime
        };

        var variableType = VariableTypes.FindVariableType(value);
        runtimeVariable.Type = variableType.TypeName;
        variableType.SetValue(value, runtimeVariable);

        return new PersistentHistoricVariableInstanceEntity
        {
            Id = runtimeVariable.Id!,
            Name = runtimeVariable.Name,
            ProcessInstanceId = runtimeVariable.ProcessInstanceId,
            TaskId = runtimeVariable.TaskId,
            Type = runtimeVariable.Type,
            TextValue = runtimeVariable.TextValue,
            TextValue2 = runtimeVariable.TextValue2,
            LongValue = runtimeVariable.LongValue,
            DoubleValue = runtimeVariable.DoubleValue,
            CreateTime = runtimeVariable.CreateTime,
            LastUpdatedTime = runtimeVariable.LastUpdatedTime
        };
    }

    public static PersistentHistoricDetailEntity ToHistoricDetailEntity(
        string id,
        string? processInstanceId,
        string? taskId,
        string? name,
        object? value,
        DateTime? time)
    {
        var runtimeVariable = new VariableInstanceEntity
        {
            Id = id,
            Name = name,
            ProcessInstanceId = processInstanceId,
            TaskId = taskId,
            LastUpdatedTime = time
        };

        var variableType = VariableTypes.FindVariableType(value);
        runtimeVariable.Type = variableType.TypeName;
        variableType.SetValue(value, runtimeVariable);

        return new PersistentHistoricDetailEntity
        {
            Id = runtimeVariable.Id!,
            Type = "VariableUpdate",
            ProcessInstanceId = processInstanceId,
            VariableId = runtimeVariable.Id,
            VariableInstanceId = runtimeVariable.Id,
            Name = name,
            VariableType = runtimeVariable.Type,
            Time = time,
            TextValue = runtimeVariable.TextValue,
            TextValue2 = runtimeVariable.TextValue2,
            LongValue = runtimeVariable.LongValue,
            DoubleValue = runtimeVariable.DoubleValue
        };
    }

    public static object? ReadValue(
        string? type,
        string? textValue,
        string? textValue2,
        long? longValue,
        double? doubleValue)
    {
        return ReadValue(type, textValue, textValue2, longValue, doubleValue, null);
    }

    public static object? ReadValue(
        string? type,
        string? textValue,
        string? textValue2,
        long? longValue,
        double? doubleValue,
        byte[]? bytes)
    {
        var runtimeVariable = new VariableInstanceEntity
        {
            Type = type,
            TextValue = textValue,
            TextValue2 = textValue2,
            LongValue = longValue,
            DoubleValue = doubleValue,
            ByteValue = bytes
        };

        if (!string.IsNullOrWhiteSpace(type))
        {
            var variableType = VariableTypes.GetVariableType(type);
            if (variableType != null)
            {
                return variableType.GetValue(runtimeVariable);
            }
        }

        if (!string.IsNullOrWhiteSpace(textValue2) && LooksLikeJson(textValue2))
        {
            try
            {
                return JsonSerializer.Deserialize<object>(textValue2);
            }
            catch (JsonException)
            {
            }
        }

        if (textValue != null)
        {
            return textValue;
        }

        if (longValue.HasValue)
        {
            return longValue.Value;
        }

        if (doubleValue.HasValue)
        {
            return doubleValue.Value;
        }

        return null;
    }

    private static bool LooksLikeJson(string value)
    {
        return value.StartsWith("{", StringComparison.Ordinal)
            || value.StartsWith("[", StringComparison.Ordinal)
            || value.StartsWith("\"", StringComparison.Ordinal);
    }
}
