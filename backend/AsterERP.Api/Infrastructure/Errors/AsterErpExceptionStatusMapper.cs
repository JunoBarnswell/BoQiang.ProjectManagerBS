using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Approval.Api.Exceptions;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Api.Shared;

namespace AsterERP.Api.Infrastructure.Errors;

public static class AsterErpExceptionStatusMapper
{
    public static (string Message, int Code, int StatusCode) Map(Exception? exception) =>
        exception switch
        {
            WorkflowApprovalException flowException =>
                (flowException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status400BadRequest),
            WorkflowEngineException workflowEngineException =>
                (workflowEngineException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status400BadRequest),
            WorkflowNotFoundException workflowNotFoundException =>
                (workflowNotFoundException.Message, ErrorCodes.WorkflowProcessDefinitionNotFound, StatusCodes.Status404NotFound),
            WorkflowForbiddenException workflowForbiddenException =>
                (workflowForbiddenException.Message, ErrorCodes.PermissionDenied, StatusCodes.Status403Forbidden),
            WorkflowConflictException workflowConflictException =>
                (workflowConflictException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status409Conflict),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode == StatusCodes.Status401Unauthorized =>
                (workflowApiException.Message, ErrorCodes.AuthenticationRequired, StatusCodes.Status401Unauthorized),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode == StatusCodes.Status404NotFound =>
                (workflowApiException.Message, ErrorCodes.WorkflowTaskNotFound, StatusCodes.Status404NotFound),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode == StatusCodes.Status403Forbidden =>
                (workflowApiException.Message, ErrorCodes.PermissionDenied, StatusCodes.Status403Forbidden),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode == StatusCodes.Status409Conflict =>
                (workflowApiException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status409Conflict),
            WorkflowApiException workflowApiException when workflowApiException.StatusCode >= StatusCodes.Status500InternalServerError =>
                (workflowApiException.Message, ErrorCodes.InternalError, StatusCodes.Status500InternalServerError),
            WorkflowApiException workflowApiException =>
                (workflowApiException.Message, ErrorCodes.WorkflowActionInvalid, StatusCodes.Status400BadRequest),
            ValidationException validationException when validationException.Code == ErrorCodes.AuthenticationRequired =>
                (validationException.Message, validationException.Code, StatusCodes.Status401Unauthorized),
            ValidationException validationException when validationException.Code == ErrorCodes.PasswordResetRequired =>
                (validationException.Message, validationException.Code, StatusCodes.Status428PreconditionRequired),
            ValidationException validationException when validationException.Code == ErrorCodes.PermissionDenied =>
                (validationException.Message, validationException.Code, StatusCodes.Status403Forbidden),
            ValidationException validationException when validationException.Code == ErrorCodes.DesignerSchemaInvalid =>
                (validationException.Message, validationException.Code, StatusCodes.Status422UnprocessableEntity),
            ValidationException validationException when validationException.Code == ErrorCodes.SchemaOrPayloadTooLarge =>
                (validationException.Message, validationException.Code, StatusCodes.Status413PayloadTooLarge),
            ValidationException validationException =>
                (validationException.Message, validationException.Code, StatusCodes.Status400BadRequest),
            NotFoundException notFoundException =>
                (notFoundException.Message, notFoundException.Code, StatusCodes.Status404NotFound),
            BusinessException businessException =>
                (businessException.Message, businessException.Code, StatusCodes.Status400BadRequest),
            _ =>
                ("系统繁忙，请稍后重试", ErrorCodes.InternalError, StatusCodes.Status500InternalServerError)
        };
}
