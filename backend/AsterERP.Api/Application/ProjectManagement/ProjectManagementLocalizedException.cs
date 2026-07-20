using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// A stable project-management error descriptor. The transport renders the key in
/// the request culture while clients can safely render the same descriptor locally.
/// </summary>
public sealed class ProjectManagementLocalizedException : BusinessException
{
    public ProjectManagementLocalizedException(
        string messageKey,
        int code,
        IReadOnlyDictionary<string, string>? messageArguments = null)
        : base(code, ProjectManagementText.Resolve(messageKey, messageArguments))
    {
        MessageKey = messageKey;
        MessageArguments = messageArguments;
    }

    public string MessageKey { get; }

    public IReadOnlyDictionary<string, string>? MessageArguments { get; }
}
