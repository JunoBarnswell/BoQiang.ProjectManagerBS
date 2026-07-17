using System.Linq.Expressions;
using AsterERP.Api.Application.Auth;
using AsterERP.Api.Infrastructure.Errors;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.System.Logs;
using AsterERP.Contracts.Logs;
using AsterERP.Domain.Common;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AuthenticationErrorHandlingTests
{
    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/application-auth/tenants/tenant-a/apps/WMS/login")]
    public async Task Api_exception_filter_should_normalize_login_authentication_failures(string path)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-auth-failed"
        };
        httpContext.Request.Path = path;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor(),
            new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        var executingContext = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller: new object());
        var filter = CreateFilter();

        await filter.OnActionExecutionAsync(
            executingContext,
            () => throw new ValidationException("密码错误", ErrorCodes.AuthenticationRequired));

        var objectResult = Assert.IsType<ObjectResult>(executingContext.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);

        var apiResult = Assert.IsType<ApiResult<object?>>(objectResult.Value);
        Assert.Equal(ErrorCodes.AuthenticationRequired, apiResult.Code);
        Assert.Equal(AuthenticationLoginFailureResponsePolicy.GenericMessage, apiResult.Message);
        Assert.Equal("trace-auth-failed", apiResult.TraceId);
        Assert.Null(apiResult.Data);
    }

    [Fact]
    public async Task Api_exception_filter_should_preserve_non_login_authentication_message()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-session-failed"
        };
        httpContext.Request.Path = "/api/auth/me";

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor(),
            new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        var executingContext = new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            controller: new object());
        var filter = CreateFilter();

        await filter.OnActionExecutionAsync(
            executingContext,
            () => throw new ValidationException("请先登录", ErrorCodes.AuthenticationRequired));

        var objectResult = Assert.IsType<ObjectResult>(executingContext.Result);
        var apiResult = Assert.IsType<ApiResult<object?>>(objectResult.Value);
        Assert.Equal("请先登录", apiResult.Message);
    }

    [Fact]
    public void Login_failure_delay_is_configurable_with_safe_bounds()
    {
        Assert.Equal(
            TimeSpan.FromMilliseconds(350),
            AuthenticationLoginFailureResponsePolicy.ResolveMinimumDelay(new ConfigurationBuilder().Build()));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:LoginFailureMinimumMilliseconds"] = "5000"
            })
            .Build();
        Assert.Equal(
            TimeSpan.FromMilliseconds(2000),
            AuthenticationLoginFailureResponsePolicy.ResolveMinimumDelay(configuration));
    }

    [Theory]
    [InlineData("账号不存在", LoginLogResults.AccountNotFound)]
    [InlineData("账号错误", LoginLogResults.AccountNotFound)]
    [InlineData("账号已停用", LoginLogResults.AccountDisabled)]
    [InlineData("密码错误", LoginLogResults.PasswordError)]
    public async Task Login_log_writer_should_classify_authentication_failures(string failureReason, string expectedResult)
    {
        var repository = new CapturingRepository<SystemLoginLogEntity>();
        var writer = new LoginLogWriter(repository, NullLogger<LoginLogWriter>.Instance);

        await writer.WriteAsync(new LoginLogWriteRequest(
            "admin",
            "user-1",
            false,
            failureReason,
            "127.0.0.1",
            "test-agent",
            "trace-login"));

        Assert.NotNull(repository.Inserted);
        Assert.Equal(expectedResult, repository.Inserted.LoginResult);
        Assert.Equal(failureReason, repository.Inserted.FailureReason);
    }

    private static AsterErpApiExceptionFilter CreateFilter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:LoginFailureMinimumMilliseconds"] = "100"
            })
            .Build();
        return new AsterErpApiExceptionFilter(
            NullLogger<AsterErpApiExceptionFilter>.Instance,
            configuration);
    }

    private sealed class CapturingRepository<TEntity> : IRepository<TEntity>
        where TEntity : EntityBase
    {
        public TEntity? Inserted { get; private set; }

        public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ISugarQueryable<TEntity> Query(bool includeDeleted = false)
            => throw new NotSupportedException();

        public Task<PageResult<TEntity>> PageAsync(PageQuery pageQuery, Expression<Func<TEntity, bool>>? predicate = null, bool includeDeleted = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<GridPageResult<TEntity>> GridPageAsync(GridQuery gridQuery, Expression<Func<TEntity, bool>>? predicate = null, bool includeDeleted = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, bool includeDeleted = false, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            Inserted = entity;
            return Task.FromResult(entity);
        }

        public Task<int> InsertRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> DeletePhysicalAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
