using System.Collections.Generic;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Command;

public sealed class CommandContextFlushState
{
    public IReadOnlyCollection<CommentEntity> Comments { get; init; } = [];
    public IReadOnlyCollection<string> DeletedCommentIds { get; init; } = [];
    public IReadOnlyCollection<AttachmentEntity> Attachments { get; init; } = [];
    public IReadOnlyDictionary<string, byte[]> AttachmentContents { get; init; } = new Dictionary<string, byte[]>();
    public IReadOnlyCollection<string> DeletedAttachmentIds { get; init; } = [];
    public RuntimePersistenceBatch RuntimePersistenceBatch { get; init; } = new();
}
