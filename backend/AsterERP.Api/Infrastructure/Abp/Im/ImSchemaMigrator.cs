using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.Im;

public sealed class ImSchemaMigrator
{
    public Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);
        CreateTables(schema);
        CreateIndexes(schema);
        return Task.CompletedTask;
    }

    private static void CreateTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS im_account_bindings (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, UserId TEXT NOT NULL,
    ImAccountId TEXT NOT NULL, DisplayName TEXT NOT NULL, Status TEXT NOT NULL DEFAULT 'Enabled',
    BoundAt TEXT NOT NULL, LastSyncedAt TEXT NOT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS im_conversations (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, ConversationKey TEXT NOT NULL,
    ConversationType TEXT NOT NULL DEFAULT 'Direct', Title TEXT NULL, Status TEXT NOT NULL DEFAULT 'Active',
    ParticipantAUserId TEXT NOT NULL, ParticipantBUserId TEXT NOT NULL, LastMessageId TEXT NULL,
    LastMessagePreview TEXT NULL, LastMessageAt TEXT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.EnsureColumn("im_conversations", "ConversationType", "TEXT NOT NULL DEFAULT 'Direct'");
        schema.EnsureColumn("im_conversations", "Title", "TEXT NULL");
        schema.EnsureColumn("im_conversations", "Status", "TEXT NOT NULL DEFAULT 'Active'");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS im_conversation_participants (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, ConversationId TEXT NOT NULL,
    UserId TEXT NOT NULL, UnreadCount INTEGER NOT NULL DEFAULT 0, LastReadMessageId TEXT NULL,
    LastReadAt TEXT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS im_messages (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, ConversationId TEXT NOT NULL,
    SenderUserId TEXT NOT NULL, ReceiverUserId TEXT NOT NULL, MessageType TEXT NOT NULL DEFAULT 'Text',
    Content TEXT NOT NULL, Status TEXT NOT NULL DEFAULT 'Sent', ClientMessageId TEXT NULL,
    SourceAppCode TEXT NULL, CloudImMessageId TEXT NULL, SentAt TEXT NOT NULL, CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL,
    DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS im_message_delivery_logs (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, ConversationId TEXT NOT NULL,
    MessageId TEXT NULL, Channel TEXT NOT NULL DEFAULT 'SignalR', TargetUserId TEXT NOT NULL,
    Result TEXT NOT NULL DEFAULT 'Pending', ErrorMessage TEXT NULL, TraceId TEXT NULL, CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL,
    DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
    }

    private static void CreateIndexes(SqliteSchemaExecutor schema)
    {
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_im_account_bindings_user ON im_account_bindings(TenantId, UserId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_im_account_bindings_account ON im_account_bindings(ImAccountId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_im_conversations_direct ON im_conversations(TenantId, ConversationKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_im_conversations_status_last ON im_conversations(TenantId, Status, LastMessageAt, CreatedTime) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_im_participants_user ON im_conversation_participants(TenantId, ConversationId, UserId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_im_participants_lookup ON im_conversation_participants(TenantId, UserId, UnreadCount) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_im_messages_client ON im_messages(TenantId, ConversationId, SenderUserId, ClientMessageId) WHERE IsDeleted = 0 AND ClientMessageId IS NOT NULL;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_im_messages_history ON im_messages(TenantId, ConversationId, SentAt) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_im_delivery_logs_message ON im_message_delivery_logs(TenantId, ConversationId, MessageId, TargetUserId);");
    }
}
