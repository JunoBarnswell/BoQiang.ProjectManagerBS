using AsterERP.Api.Infrastructure.Errors;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Workflow.Approval.Api.Exceptions;
using AsterERP.Shared;
using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Common;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowExceptionStatusMapperTests
{
    [Fact]
    public void Project_management_localized_exception_keeps_a_machine_readable_descriptor()
    {
        var exception = new ProjectManagementLocalizedException("projectManagement.api.task.statusUpdateFailed", ErrorCodes.ParameterInvalid);

        var mapped = AsterErpExceptionStatusMapper.Map(exception);

        Assert.Equal("projectManagement.api.task.statusUpdateFailed", mapped.MessageKey);
        Assert.Equal(ErrorCodes.ParameterInvalid, mapped.Code);
        Assert.False(string.IsNullOrWhiteSpace(mapped.Message));
    }

    [Fact]
    public void Project_management_text_uses_the_current_request_culture()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            var chinese = ProjectManagementText.Resolve("projectManagement.api.task.statusUpdateFailed");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var english = ProjectManagementText.Resolve("projectManagement.api.task.statusUpdateFailed");

            Assert.NotEqual(chinese, english);
            Assert.Contains("task", english, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Theory]
    [MemberData(nameof(MapCases))]
    public void Map_should_return_expected_error_code_and_http_status(Exception exception, int expectedCode, int expectedStatus)
    {
        var mapped = AsterErpExceptionStatusMapper.Map(exception);

        Assert.Equal(expectedCode, mapped.Code);
        Assert.Equal(expectedStatus, mapped.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(mapped.Message));
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
