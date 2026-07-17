using AsterERP.Api.Infrastructure.Errors;
using AsterERP.Workflow.Approval.Api.Exceptions;
using AsterERP.Shared;
using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Common;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowExceptionStatusMapperTests
{
    [Theory]
    [MemberData(nameof(MapCases))]
    public void Map_should_return_expected_error_code_and_http_status(Exception exception, int expectedCode, int expectedStatus)
    {
        var (message, code, statusCode) = AsterErpExceptionStatusMapper.Map(exception);

        Assert.Equal(expectedCode, code);
        Assert.Equal(expectedStatus, statusCode);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    public static IEnumerable<object[]> MapCases()
    {
        yield return
        [
            new WorkflowApprovalException("flow invalid"),
            ErrorCodes.WorkflowActionInvalid,
            StatusCodes.Status400BadRequest
        ];
        yield return
        [
            new WorkflowEngineException("workflow invalid"),
            ErrorCodes.WorkflowActionInvalid,
            StatusCodes.Status400BadRequest
        ];
        yield return
        [
            new WorkflowNotFoundException("not found"),
            ErrorCodes.WorkflowProcessDefinitionNotFound,
            StatusCodes.Status404NotFound
        ];
        yield return
        [
            new WorkflowForbiddenException("forbidden"),
            ErrorCodes.PermissionDenied,
            StatusCodes.Status403Forbidden
        ];
        yield return
        [
            new WorkflowConflictException("conflict"),
            ErrorCodes.WorkflowActionInvalid,
            StatusCodes.Status409Conflict
        ];
        yield return
        [
            new WorkflowApiException(StatusCodes.Status503ServiceUnavailable, "backend unavailable"),
            ErrorCodes.InternalError,
            StatusCodes.Status500InternalServerError
        ];
    }
}
