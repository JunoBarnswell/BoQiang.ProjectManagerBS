using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.System.Logs;
using AsterERP.Contracts.Logs;

namespace AsterERP.Api.Application.Auth;

public sealed class LoginLogWriter(
    IRepository<SystemLoginLogEntity> loginLogRepository,
    ILogger<LoginLogWriter> logger) : ILoginLogWriter
{
    private const int MaxTextLength = 512;

    public async Task WriteAsync(LoginLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        var loginResult = ResolveLoginResult(request);
        var loginTime = DateTime.UtcNow;
        var entity = new SystemLoginLogEntity
        {
            TraceId = NormalizeTraceId(request.TraceId),
            UserName = NormalizeUserName(request.UserName),
            UserId = NormalizeOptional(request.UserId),
            LoginTime = loginTime,
            IsSuccess = request.IsSuccess,
            LoginResult = loginResult,
            FailureReason = NormalizeOptional(request.FailureReason),
            ClientIp = NormalizeOptional(request.ClientIp, 64),
            UserAgent = NormalizeOptional(request.UserAgent, MaxTextLength),
            CreatedTime = loginTime
        };

        try
        {
            await loginLogRepository.InsertAsync(entity, cancellationToken);
            logger.LogInformation(
                "Login logged: {UserName} -> {LoginResult} ({TraceId})",
                entity.UserName,
                loginResult,
                entity.TraceId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to write login log for {UserName} ({TraceId})",
                entity.UserName,
                entity.TraceId);
        }
    }

    private static string NormalizeUserName(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : Truncate(normalized, MaxTextLength);
    }

    private static string NormalizeTraceId(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : Truncate(normalized, MaxTextLength);
    }

    private static string? NormalizeOptional(string? value, int maxLength = MaxTextLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Truncate(value.Trim(), maxLength);
    }

    private static string ResolveLoginResult(LoginLogWriteRequest request)
    {
        return ResolveLoginResult(request.IsSuccess, request.FailureReason);
    }

    private static string ResolveLoginResult(bool isSuccess, string? failureReason)
    {
        return isSuccess
            ? LoginLogResults.Success
            : failureReason?.Trim() switch
            {
                "账号不存在" => LoginLogResults.AccountNotFound,
                "账号错误" => LoginLogResults.AccountNotFound,
                "账号已停用" => LoginLogResults.AccountDisabled,
                "密码错误" => LoginLogResults.PasswordError,
                _ => LoginLogResults.PasswordError
            };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
