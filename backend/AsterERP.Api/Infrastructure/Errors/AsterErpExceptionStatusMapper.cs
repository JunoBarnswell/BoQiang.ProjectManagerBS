using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Approval.Api.Exceptions;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Api.Shared;

namespace AsterERP.Api.Infrastructure.Errors;

public sealed record AsterErpExceptionMapping(
    string Message,
    int Code,
    int StatusCode,
    string? MessageKey = null,
    IReadOnlyDictionary<string, string>? MessageArguments = null);

public static class AsterErpExceptionStatusMapper
{
    public static AsterErpExceptionMapping Map(Exception? exception) =>
        exception switch
        {
            ProjectManagementLocalizedException projectManagementException =>
                new(projectManagementException.Message, projectManagementException.Code, ResolveStatusCode(projectManagementException.Code), projectManagementException.MessageKey, projectManagementException.MessageArguments),
            WorkflowApprovalException flowException =>
                new(flowException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status400BadRequest),
            WorkflowEngineException workflowEngineException =>
                new(workflowEngineException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status400BadRequest),
            WorkflowNotFoundException workflowNotFoundException =>
                new(workflowNotFoundException.Message, ErrorCodes.WorkflowProcessDefinitionNotFound, StatusCodes.Status404NotFound),
            WorkflowForbiddenException workflowForbiddenException =>
                new(workflowForbiddenException.Message, ErrorCodes.PermissionDenied, StatusCodes.Status403Forbidden),
            WorkflowConflictException workflowConflictException =>
                new(workflowConflictException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status409Conflict),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode == StatusCodes.Status401Unauthorized =>
                new(workflowApiException.Message, ErrorCodes.AuthenticationRequired, StatusCodes.Status401Unauthorized),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode == StatusCodes.Status404NotFound =>
                new(workflowApiException.Message, ErrorCodes.WorkflowTaskNotFound, StatusCodes.Status404NotFound),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode == StatusCodes.Status403Forbidden =>
                new(workflowApiException.Message, ErrorCodes.PermissionDenied, StatusCodes.Status403Forbidden),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode == StatusCodes.Status409Conflict =>
                new(workflowApiException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status409Conflict),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode >= StatusCodes.Status500InternalServerError =>
                new(workflowApiException.Message, ErrorCodes.InternalError, StatusCodes.Status500InternalServerError),
            WorkflowApiException workflowApiException =>
                new(workflowApiException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status400BadRequest),
            ValidationException validationException when validationException.Code == ErrorCodes.AuthenticationRequired =>
                new(validationException.Message, validationException.Code, StatusCodes.Status401Unauthorized),
            ValidationException validationException when validationException.Code == ErrorCodes.PasswordResetRequired =>
                new(validationException.Message, validationException.Code, StatusCodes.Status428PreconditionRequired),
            ValidationException validationException when validationException.Code == ErrorCodes.PermissionDenied =>
                new(validationException.Message, validationException.Code, StatusCodes.Status403Forbidden),
            ValidationException validationException when validationException.Code == ErrorCodes.DesignerSchemaInvalid =>
                new(validationException.Message, validationException.Code, StatusCodes.Status422UnprocessableEntity),
            ValidationException validationException when validationException.Code == ErrorCodes.SchemaOrPayloadTooLarge =>
                new(validationException.Message, validationException.Code, StatusCodes.Status413PayloadTooLarge),
            ValidationException validationException =>
                new(validationException.Message, validationException.Code, StatusCodes.Status400BadRequest),
            NotFoundException notFoundException =>
                new(notFoundException.Message, notFoundException.Code, StatusCodes.Status404NotFound),
            BusinessException businessException =>
                new(businessException.Message, businessException.Code, StatusCodes.Status400BadRequest),
            _ =>
                new(ProjectManagementText.Resolve("projectManagement.api.internalError"), ErrorCodes.InternalError, StatusCodes.Status500InternalServerError, "projectManagement.api.internalError")
        };

    private static int ResolveStatusCode(int code) => code switch
    {
        ErrorCodes.AuthenticationRequired => StatusCodes.Status401Unauthorized,
        ErrorCodes.PermissionDenied => StatusCodes.Status403Forbidden,
        ErrorCodes.PlatformResourceNotFound => StatusCodes.Status404NotFound,
        ErrorCodes.ApplicationDevelopmentPageRevisionConflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
