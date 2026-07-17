namespace AsterERP.Workflow.Core.Variable;

public class VariableInstanceEntity
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? TextValue { get; set; }
    public string? TextValue2 { get; set; }
    public long? LongValue { get; set; }
    public double? DoubleValue { get; set; }
    public byte[]? ByteValue { get; set; }
    public string? ExecutionId { get; set; }
    public string? ProcessInstanceId { get; set; }
    public string? TaskId { get; set; }
    public DateTime? CreateTime { get; set; }
    public DateTime? LastUpdatedTime { get; set; }

    public object? Value
    {
        get
        {
            if (TextValue != null) return TextValue;
            if (LongValue.HasValue) return LongValue.Value;
            if (DoubleValue.HasValue) return DoubleValue.Value;
            if (ByteValue != null) return ByteValue;
            return null;
        }
        set
        {
            switch (value)
            {
                case string s:
                    TextValue = s;
                    break;
                case long l:
                    LongValue = l;
                    break;
                case int i:
                    LongValue = i;
                    break;
                case double d:
                    DoubleValue = d;
                    break;
                case float f:
                    DoubleValue = f;
                    break;
                case byte[] b:
                    ByteValue = b;
                    break;
                default:
                    TextValue = value?.ToString();
                    break;
            }
        }
    }
}
