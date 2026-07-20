namespace AsterERP.Shared;

public sealed record ApiResult<T>(
    int Code,
    string Message,
    T? Data,
    string TraceId,
    string? MessageKey = null,
    IReadOnlyDictionary<string, string>? MessageArguments = null)
{
    public static ApiResult<T> Ok(
        T data,
        string traceId,
        string message = "success",
        string? messageKey = null,
        IReadOnlyDictionary<string, string>? messageArguments = null)
    {
        return new ApiResult<T>(ErrorCodes.Success, message, data, traceId, messageKey, messageArguments);
    }

    public static ApiResult<T> Fail(
        string message,
        string traceId,
        int code,
        string? messageKey = null,
        IReadOnlyDictionary<string, string>? messageArguments = null)
    {
        return new ApiResult<T>(code, message, default, traceId, messageKey, messageArguments);
    }
}

public static class ApiResultFactory
{
    public static ApiResult<T> Ok<T>(
        T data,
        string traceId,
        string message = "success",
        string? messageKey = null,
        IReadOnlyDictionary<string, string>? messageArguments = null)
    {
        return ApiResult<T>.Ok(data, traceId, message, messageKey, messageArguments);
    }

    public static ApiResult<T> Fail<T>(
        string message,
        string traceId,
        int code,
        string? messageKey = null,
        IReadOnlyDictionary<string, string>? messageArguments = null)
    {
        return ApiResult<T>.Fail(message, traceId, code, messageKey, messageArguments);
    }
}
