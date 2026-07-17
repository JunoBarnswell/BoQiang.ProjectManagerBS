using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Command;

public interface ICommandContextSession : ISession
{
    bool HasActiveTransaction { get; }
    Task BeginAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task<string?> FindProcessInstanceIdByExecutionIdAsync(string executionId, CancellationToken cancellationToken = default);
    Task<ExecutionEntity?> LoadExecutionTreeAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<TaskImplementation?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskImplementation>> GetTasksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ExecutionRecord>> GetExecutionsAsync(CancellationToken cancellationToken = default);
    Task<AttachmentEntity?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default);
    Task<byte[]?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AttachmentEntity>> GetAttachmentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CommentEntity>> GetCommentsAsync(CancellationToken cancellationToken = default);
    Task FlushAsync(CommandContextFlushState state, CancellationToken cancellationToken = default);
}
