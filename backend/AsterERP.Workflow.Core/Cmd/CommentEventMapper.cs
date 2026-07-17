namespace AsterERP.Workflow.Core.Cmd;

internal static class CommentEventMapper
{
    public static EventEntity ToEvent(CommentEntity comment)
    {
        return new EventEntity
        {
            Id = comment.Id,
            Action = comment.Action,
            Message = comment.Message,
            UserId = comment.UserId,
            TaskId = comment.TaskId,
            ProcessInstanceId = comment.ProcessInstanceId,
            Time = comment.Time,
            Type = comment.Type
        };
    }
}
