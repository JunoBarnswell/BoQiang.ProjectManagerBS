using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public interface IMailSender
{
    Task SendAsync(MailMessage mailMessage, CancellationToken cancellationToken = default);
}

public class MailMessage
{
    public string? To { get; set; }
    public string? From { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string? Subject { get; set; }
    public string? Text { get; set; }
    public string? Html { get; set; }
    public string? Charset { get; set; }
    public List<string> AttachmentPaths { get; set; } = new();
}

public class MailServerConfiguration
{
    public string? Host { get; set; }
    public int Port { get; set; } = 25;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public bool UseTls { get; set; }
    public string? DefaultFrom { get; set; }
}

public interface IWebServiceInvoker
{
    Task<WebServiceResponse> InvokeAsync(WebServiceRequest request, CancellationToken cancellationToken = default);
}

public class WebServiceRequest
{
    public string? Endpoint { get; set; }
    public string? Method { get; set; } = "GET";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public int Timeout { get; set; } = 30000;
}

public class WebServiceResponse
{
    public int StatusCode { get; set; }
    public string? Body { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode < 300;
}

public class MailActivityBehavior : FlowNodeActivityBehavior
{
    public string? To { get; set; }
    public string? From { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string? Subject { get; set; }
    public string? Text { get; set; }
    public string? Html { get; set; }
    public string? Charset { get; set; }
    public string? TextVar { get; set; }
    public string? HtmlVar { get; set; }
    public string? Attachments { get; set; }
    public string? IgnoreException { get; set; }
    public string? ExceptionVariableName { get; set; }
    public string? ResultVariable { get; set; }

    protected IExpressionManager? ExpressionManager { get; set; }
    protected IMailSender? MailSender { get; set; }

    public MailActivityBehavior() { }

    public MailActivityBehavior(
        IExpressionManager? expressionManager = null,
        IMailSender? mailSender = null)
    {
        ExpressionManager = expressionManager;
        MailSender = mailSender;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var doIgnoreException = bool.TryParse(ResolveField(IgnoreException, execution), out var ignore) && ignore;
        var exceptionVariable = ResolveField(ExceptionVariableName, execution);

        try
        {
            var resolvedTo = ResolveField(To, execution);
            var resolvedFrom = ResolveField(From, execution);
            var resolvedCc = ResolveField(Cc, execution);
            var resolvedBcc = ResolveField(Bcc, execution);
            var resolvedSubject = ResolveField(Subject, execution);
            var resolvedText = ResolveText(execution);
            var resolvedHtml = ResolveHtml(execution);
            var resolvedCharset = ResolveField(Charset, execution);

            if (string.IsNullOrEmpty(resolvedTo) && string.IsNullOrEmpty(resolvedCc) && string.IsNullOrEmpty(resolvedBcc))
            {
                throw new WorkflowEngineException("No mail recipients could be resolved for mail activity");
            }

            if (string.IsNullOrEmpty(resolvedText) && string.IsNullOrEmpty(resolvedHtml))
            {
                throw new WorkflowEngineException("'html' or 'text' is required to be defined when using the mail activity");
            }

            var mailMessage = new MailMessage
            {
                To = resolvedTo,
                From = resolvedFrom,
                Cc = resolvedCc,
                Bcc = resolvedBcc,
                Subject = resolvedSubject ?? string.Empty,
                Text = resolvedText,
                Html = resolvedHtml,
                Charset = resolvedCharset
            };

            ResolveAttachments(mailMessage, execution);

            if (MailSender != null)
            {
                await MailSender.SendAsync(mailMessage, cancellationToken);
            }

            execution.SetVariable("_mailSent", true);
            execution.SetVariable("_mailSentTimestamp", AbpTimeIdProvider.UtcNow);

            if (!string.IsNullOrEmpty(ResultVariable))
            {
                execution.SetVariable(ResultVariable, true);
            }
        }
        catch (WorkflowEngineException ex)
        {
            HandleException(execution, ex.Message, ex, doIgnoreException, exceptionVariable);
        }
        catch (Exception ex)
        {
            HandleException(execution, $"Could not send e-mail in execution {execution.Id}", ex, doIgnoreException, exceptionVariable);
        }

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual string? ResolveText(ExecutionEntity execution)
    {
        if (!string.IsNullOrEmpty(TextVar))
        {
            var varValue = execution.GetVariable(TextVar);
            if (varValue != null) return varValue.ToString();
        }
        return ResolveField(Text, execution);
    }

    protected virtual string? ResolveHtml(ExecutionEntity execution)
    {
        if (!string.IsNullOrEmpty(HtmlVar))
        {
            var varValue = execution.GetVariable(HtmlVar);
            if (varValue != null) return varValue.ToString();
        }
        return ResolveField(Html, execution);
    }

    protected virtual void ResolveAttachments(MailMessage mailMessage, ExecutionEntity execution)
    {
        var resolvedAttachments = ResolveField(Attachments, execution);
        if (string.IsNullOrEmpty(resolvedAttachments)) return;

        foreach (var path in resolvedAttachments.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (System.IO.File.Exists(path))
            {
                mailMessage.AttachmentPaths.Add(path);
            }
        }
    }

    protected virtual string? ResolveField(string? field, ExecutionEntity execution)
    {
        if (string.IsNullOrEmpty(field)) return field;

        if (ExpressionManager != null && (field.StartsWith("${") || field.StartsWith("#{")))
        {
            var result = ExpressionManager.Evaluate(field, execution.Variables);
            return result?.ToString();
        }

        return field;
    }

    protected virtual void HandleException(
        ExecutionEntity execution,
        string message,
        Exception exception,
        bool doIgnoreException,
        string? exceptionVariable)
    {
        if (doIgnoreException)
        {
            if (!string.IsNullOrEmpty(exceptionVariable))
            {
                execution.SetVariable(exceptionVariable, message);
            }
        }
        else
        {
            if (exception is WorkflowEngineException workflowEngineEx) throw workflowEngineEx;
            throw new WorkflowEngineException(message, exception);
        }
    }
}

public class WebServiceActivityBehavior : FlowNodeActivityBehavior
{
    public string? Implementation { get; set; }
    public string? OperationRef { get; set; }
    public string? Endpoint { get; set; }
    public string? Method { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public string? ResultVariable { get; set; }
    public int Timeout { get; set; } = 30000;

    protected IExpressionManager? ExpressionManager { get; set; }
    protected IWebServiceInvoker? WebServiceInvoker { get; set; }

    public WebServiceActivityBehavior() { }

    public WebServiceActivityBehavior(
        IExpressionManager? expressionManager = null,
        IWebServiceInvoker? webServiceInvoker = null)
    {
        ExpressionManager = expressionManager;
        WebServiceInvoker = webServiceInvoker;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var resolvedEndpoint = ResolveField(Endpoint, execution);
        var resolvedMethod = ResolveField(Method, execution) ?? "GET";
        var resolvedBody = ResolveField(Body, execution);

        execution.SetVariable("_webServiceEndpoint", resolvedEndpoint);
        execution.SetVariable("_webServiceMethod", resolvedMethod);
        execution.SetVariable("_webServiceInvoked", true);
        execution.SetVariable("_webServiceTimestamp", AbpTimeIdProvider.UtcNow);

        if (!string.IsNullOrEmpty(resolvedBody))
        {
            execution.SetVariable("_webServiceRequestBody", resolvedBody);
        }

        if (WebServiceInvoker != null && !string.IsNullOrEmpty(resolvedEndpoint))
        {
            try
            {
                var request = new WebServiceRequest
                {
                    Endpoint = resolvedEndpoint,
                    Method = resolvedMethod,
                    Body = resolvedBody,
                    Timeout = Timeout
                };

                foreach (var header in Headers)
                {
                    request.Headers[header.Key] = ResolveField(header.Value, execution) ?? header.Value;
                }

                var response = await WebServiceInvoker.InvokeAsync(request, cancellationToken);

                if (!string.IsNullOrEmpty(ResultVariable))
                {
                    execution.SetVariable(ResultVariable, response.Body);
                }

                execution.SetVariable("_webServiceStatusCode", response.StatusCode);
            }
            catch (Exception ex)
            {
                execution.SetVariable("_webServiceError", ex.Message);
                if (!string.IsNullOrEmpty(ResultVariable))
                {
                    execution.SetVariable(ResultVariable, $"WebService error from {resolvedEndpoint}: {ex.Message}");
                }
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(ResultVariable))
            {
                execution.SetVariable(ResultVariable, $"WebService result from {resolvedEndpoint}");
            }
        }

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual string? ResolveField(string? field, ExecutionEntity execution)
    {
        if (string.IsNullOrEmpty(field)) return field;

        if (ExpressionManager != null && (field.StartsWith("${") || field.StartsWith("#{")))
        {
            var result = ExpressionManager.Evaluate(field, execution.Variables);
            return result?.ToString();
        }

        return field;
    }
}

