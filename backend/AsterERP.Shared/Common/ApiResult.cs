namespace AsterERP.Shared;

public sealed record ApiResult<T>(int Code, string Message, T? Data, string TraceId)
{
    public static ApiResult<T> Ok(T data, string traceId, string message = "success")
    {
        return new ApiResult<T>(ErrorCodes.Success, message, data, traceId);
    }

    public static ApiResult<T> Fail(string message, string traceId, int code)
    {
        return new ApiResult<T>(code, message, default, traceId);
    }
}

public static class ApiResultFactory
{
    public static ApiResult<T> Ok<T>(T data, string traceId, string message = "success")
    {
        return ApiResult<T>.Ok(data, traceId, message);
    }

    public static ApiResult<T> Fail<T>(string message, string traceId, int code)
    {
        return ApiResult<T>.Fail(message, traceId, code);
    }
}
