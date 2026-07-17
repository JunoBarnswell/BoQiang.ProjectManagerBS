namespace AsterERP.Workflow.Api.Shared;

public class WorkflowApiException : Exception
{
    public int StatusCode { get; }

    public WorkflowApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public WorkflowApiException(int statusCode, string message, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
