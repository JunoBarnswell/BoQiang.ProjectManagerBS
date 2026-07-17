using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.CoreShell;

public sealed class CoreShellSchemaMigrator
{
    public async Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);
        CreateAuthSessionTable(schema);
        CreateIdentityTables(schema);
        CreateOrganizationTables(schema);
        CreateMenuTable(schema);
        CreateAuditLogTables(schema);
        await RenameLatestColumnsAsync(schema, cancellationToken);
        EnsureCompatibilityColumns(schema);
        CreateIndexes(schema);
    }

    private static void CreateAuthSessionTable(SqliteSchemaExecutor schema) => schema.Execute("""
CREATE TABLE IF NOT EXISTS system_auth_sessions (
    Id TEXT NOT NULL PRIMARY KEY, UserId TEXT NOT NULL, TokenHash TEXT NOT NULL,
    SessionVersion INTEGER NOT NULL DEFAULT 1, CsrfSecretHash TEXT NULL, ExpiresAt TEXT NOT NULL,
    RevokedAt TEXT NULL, ClientIp TEXT NULL, UserAgent TEXT NULL, LastSeenTime TEXT NULL,
    CurrentTenantId TEXT NULL, CurrentAppCode TEXT NULL, WorkspaceSwitchedAt TEXT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");

    private static void CreateIdentityTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_users (
    Id TEXT NOT NULL PRIMARY KEY, UserName TEXT NOT NULL, DisplayName TEXT NOT NULL, PasswordHash TEXT NOT NULL,
    PhoneNumber TEXT NULL, Email TEXT NULL, DeptId TEXT NULL, PositionId TEXT NULL,
    IsAdmin INTEGER NOT NULL DEFAULT 0, Status TEXT NOT NULL DEFAULT 'Enabled',
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_roles (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NULL, AppCode TEXT NULL, RoleName TEXT NOT NULL, RoleCode TEXT NOT NULL,
    DataScope TEXT NOT NULL DEFAULT 'ALL', IsEnabled INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_user_roles (
    Id TEXT NOT NULL PRIMARY KEY, UserId TEXT NOT NULL, RoleId TEXT NOT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_user_employments (
    Id TEXT NOT NULL PRIMARY KEY, UserId TEXT NOT NULL, TenantId TEXT NOT NULL DEFAULT 'tenant-system',
    AppCode TEXT NOT NULL DEFAULT 'SYSTEM', DeptId TEXT NOT NULL, PositionId TEXT NOT NULL,
    EmploymentName TEXT NOT NULL, IsPrimary INTEGER NOT NULL DEFAULT 0, Status TEXT NOT NULL DEFAULT 'Enabled',
    SortOrder INTEGER NOT NULL DEFAULT 0, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_permission_codes (
    Id TEXT NOT NULL PRIMARY KEY, ModuleName TEXT NOT NULL, PermissionCode TEXT NOT NULL, PermissionName TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_role_permissions (
    Id TEXT NOT NULL PRIMARY KEY, RoleId TEXT NOT NULL, PermissionCodeId TEXT NOT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
    }

    private static void CreateOrganizationTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_departments (
    Id TEXT NOT NULL PRIMARY KEY, DeptCode TEXT NOT NULL, DeptName TEXT NOT NULL, ParentId TEXT NULL,
    ManagerName TEXT NULL, LeaderUserIdsJson TEXT NULL, PhoneNumber TEXT NULL, SortOrder INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'Enabled', CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.EnsureColumn("system_departments", "LeaderUserIdsJson", "TEXT NULL");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_positions (
    Id TEXT NOT NULL PRIMARY KEY, PositionCode TEXT NOT NULL, PositionName TEXT NOT NULL, DeptId TEXT NOT NULL,
    PositionLevel TEXT NULL, SortOrder INTEGER NOT NULL DEFAULT 0, Status TEXT NOT NULL DEFAULT 'Enabled',
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
    }

    private static void CreateMenuTable(SqliteSchemaExecutor schema) => schema.Execute("""
CREATE TABLE IF NOT EXISTS system_menus (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL DEFAULT 'tenant-system', AppCode TEXT NOT NULL DEFAULT 'SYSTEM',
    MenuName TEXT NOT NULL, MenuCode TEXT NOT NULL, ParentCode TEXT NULL, RoutePath TEXT NULL,
    ComponentName TEXT NULL, PageCode TEXT NULL, ArtifactId TEXT NULL, ScopeType TEXT NULL, ConfigJson TEXT NULL,
    MenuType TEXT NOT NULL DEFAULT 'Menu', SortOrder INTEGER NOT NULL DEFAULT 0, Visible INTEGER NOT NULL DEFAULT 1,
    PermissionCode TEXT NULL, Icon TEXT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");

    private static void CreateAuditLogTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_login_logs (
    Id TEXT NOT NULL PRIMARY KEY, TraceId TEXT NOT NULL, UserName TEXT NOT NULL, UserId TEXT NULL,
    UserDisplayName TEXT NULL, LoginTime TEXT NOT NULL, LoginResult TEXT NOT NULL, IsSuccess INTEGER NOT NULL DEFAULT 0,
    FailureReason TEXT NULL, ClientIp TEXT NULL, UserAgent TEXT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_operation_logs (
    Id TEXT NOT NULL PRIMARY KEY, TraceId TEXT NOT NULL, CorrelationId TEXT NULL, RequestPath TEXT NOT NULL,
    RequestMethod TEXT NOT NULL, RouteDisplayName TEXT NULL, ModuleName TEXT NULL, OperationType TEXT NULL,
    ActionName TEXT NULL, RequestQuery TEXT NULL, ClientIp TEXT NULL, UserId TEXT NULL, UserName TEXT NULL,
    ErrorMessage TEXT NULL, ExceptionSummary TEXT NULL, StatusCode INTEGER NOT NULL DEFAULT 0,
    DurationMs INTEGER NOT NULL DEFAULT 0, IsSuccess INTEGER NOT NULL DEFAULT 0, CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
    }

    private static void EnsureCompatibilityColumns(SqliteSchemaExecutor schema)
    {
        schema.EnsureColumn("system_users", "PositionId", "TEXT NULL");
        schema.EnsureColumn("system_users", "PasswordResetRequired", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("system_users", "PasswordFormatVersion", "TEXT NOT NULL DEFAULT 'legacy-unknown'");
        schema.EnsureColumn("system_user_employments", "TenantId", "TEXT NOT NULL DEFAULT 'tenant-system'");
        schema.EnsureColumn("system_user_employments", "AppCode", "TEXT NOT NULL DEFAULT 'SYSTEM'");
        schema.EnsureColumn("system_user_employments", "EmploymentName", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("system_user_employments", "SortOrder", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("system_roles", "TenantId", "TEXT NULL");
        schema.EnsureColumn("system_roles", "AppCode", "TEXT NULL");
        schema.EnsureColumn("system_menus", "TenantId", "TEXT NOT NULL DEFAULT 'tenant-system'");
        schema.EnsureColumn("system_menus", "AppCode", "TEXT NOT NULL DEFAULT 'SYSTEM'");
        schema.EnsureColumn("system_menus", "PageCode", "TEXT NULL");
        schema.EnsureColumn("system_menus", "ArtifactId", "TEXT NULL");
        schema.EnsureColumn("system_menus", "ScopeType", "TEXT NULL");
        schema.EnsureColumn("system_menus", "ConfigJson", "TEXT NULL");
        schema.EnsureColumn("system_auth_sessions", "CurrentTenantId", "TEXT NULL");
        schema.EnsureColumn("system_auth_sessions", "CurrentAppCode", "TEXT NULL");
        schema.EnsureColumn("system_auth_sessions", "WorkspaceSwitchedAt", "TEXT NULL");
        schema.EnsureColumn("system_auth_sessions", "SessionVersion", "INTEGER NOT NULL DEFAULT 1");
        schema.EnsureColumn("system_auth_sessions", "CsrfSecretHash", "TEXT NULL");
        schema.EnsureColumn("system_login_logs", "TraceId", "TEXT NOT NULL DEFAULT 'unknown'");
        schema.EnsureColumn("system_login_logs", "UserDisplayName", "TEXT NULL");
        schema.EnsureColumn("system_login_logs", "LoginTime", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");
        schema.EnsureColumn("system_login_logs", "LoginResult", "TEXT NOT NULL DEFAULT 'Success'");
        schema.EnsureColumn("system_operation_logs", "CorrelationId", "TEXT NULL");
        schema.EnsureColumn("system_operation_logs", "ActionName", "TEXT NULL");
        schema.EnsureColumn("system_operation_logs", "ExceptionSummary", "TEXT NULL");
        schema.EnsureColumn("system_roles", "Remark", "TEXT NULL");
        schema.EnsureColumn("system_users", "Remark", "TEXT NULL");
        schema.EnsureColumn("system_menus", "Remark", "TEXT NULL");
        schema.Execute("UPDATE system_menus SET TenantId = 'tenant-system' WHERE TenantId IS NULL OR TenantId = '';");
        schema.Execute("UPDATE system_menus SET AppCode = 'SYSTEM' WHERE AppCode IS NULL OR AppCode = '';");
        schema.Execute("""
INSERT INTO system_user_employments (Id, UserId, TenantId, AppCode, DeptId, PositionId, EmploymentName, IsPrimary, Status, SortOrder, CreatedBy, CreatedTime, UpdatedBy, UpdatedTime, DeletedBy, DeletedTime, IsDeleted, Remark)
SELECT lower(hex(randomblob(16))), u.Id, 'tenant-system', 'SYSTEM', u.DeptId, u.PositionId,
       coalesce(d.DeptName, u.DeptId) || '/' || coalesce(p.PositionName, u.PositionId), 1, u.Status, 1,
       u.CreatedBy, u.CreatedTime, u.UpdatedBy, u.UpdatedTime, NULL, NULL, 0, 'Migrated from system_users.DeptId/PositionId'
FROM system_users u
LEFT JOIN system_departments d ON d.Id = u.DeptId AND d.IsDeleted = 0
LEFT JOIN system_positions p ON p.Id = u.PositionId AND p.IsDeleted = 0
WHERE u.IsDeleted = 0 AND u.DeptId IS NOT NULL AND u.DeptId <> '' AND u.PositionId IS NOT NULL AND u.PositionId <> ''
  AND NOT EXISTS (SELECT 1 FROM system_user_employments e WHERE e.UserId = u.Id AND e.TenantId = 'tenant-system' AND e.AppCode = 'SYSTEM' AND e.DeptId = u.DeptId AND e.PositionId = u.PositionId AND e.IsDeleted = 0);
""");
    }

    private static async Task RenameLatestColumnsAsync(SqliteSchemaExecutor schema, CancellationToken cancellationToken)
    {
        await schema.RenameColumnIfExistsAsync("system_menus", "PageSchemaId", "ArtifactId", cancellationToken);
    }

    private static void CreateIndexes(SqliteSchemaExecutor schema)
    {
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_auth_sessions_token_hash ON system_auth_sessions(TokenHash);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_auth_sessions_user ON system_auth_sessions(UserId);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_auth_sessions_expires ON system_auth_sessions(ExpiresAt);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_auth_sessions_last_seen ON system_auth_sessions(LastSeenTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_auth_sessions_revoked ON system_auth_sessions(RevokedAt);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_auth_sessions_online ON system_auth_sessions(ExpiresAt, RevokedAt, LastSeenTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_users_user_name ON system_users(UserName) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_users_status ON system_users(Status);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_roles_workspace_code ON system_roles(TenantId, AppCode, RoleCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_permission_codes_code ON system_permission_codes(PermissionCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_menus_workspace_code ON system_menus(TenantId, AppCode, MenuCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_user_roles_unique ON system_user_roles(UserId, RoleId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_user_employments_unique ON system_user_employments(UserId, TenantId, AppCode, DeptId, PositionId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_user_employments_user ON system_user_employments(UserId, TenantId, AppCode, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_user_employments_dept ON system_user_employments(DeptId, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_user_employments_position ON system_user_employments(PositionId, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_role_permissions_unique ON system_role_permissions(RoleId, PermissionCodeId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_departments_code ON system_departments(DeptCode);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_departments_parent ON system_departments(ParentId);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_positions_code ON system_positions(PositionCode);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_positions_dept ON system_positions(DeptId);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_menus_workspace_parent ON system_menus(TenantId, AppCode, ParentCode);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_menus_workspace_page ON system_menus(TenantId, AppCode, PageCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_menus_list_order ON system_menus(IsDeleted, TenantId, AppCode, ParentCode, SortOrder, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_login_logs_time ON system_login_logs(CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_login_logs_login_time ON system_login_logs(LoginTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_login_logs_user_name ON system_login_logs(UserName);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_login_logs_result ON system_login_logs(LoginResult);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_login_logs_success ON system_login_logs(IsSuccess);");
        schema.CreateIndexIfColumnsExist("system_operation_logs", "idx_system_operation_logs_list_created", "IsDeleted", "CreatedTime");
        schema.CreateIndexIfColumnsExist("system_operation_logs", "idx_system_operation_logs_method_status", "IsDeleted", "RequestMethod", "StatusCode");
        schema.CreateIndexIfColumnsExist("system_operation_logs", "idx_system_operation_logs_user_module", "IsDeleted", "UserName", "ModuleName");
    }
}
