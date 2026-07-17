# Database Provider Capability Matrix

该矩阵是 Data Studio 的事实型能力契约，不是新的 Adapter、Bridge 或业务代理层。Provider 差异必须落在现有 `ApplicationDataCenter` 分层；矩阵只描述实现边界、验收证据和未完成能力。

## 当前实现边界

- `ApplicationDataSourceConnectionFactory` 负责连接配置解析、Provider 映射和连接字符串生成。
- `ApplicationDataSourceService` 负责四种数据库的 Catalog 查询。
- `ApplicationDataSourceTableWorkbenchService` 负责建表和表预览。
- `ApplicationDataSourceTableRowService` 负责分页、筛选、排序和行级读写。
- `ApplicationDataSourceViewWorkbenchService` 负责物理视图创建、删除和更新。
- `ApplicationDataCenterSqlScriptEngine` 负责 SQL 计划、参数化、事务边界、取消信号传递和审计记录。

## 验收规则

每个 Provider 必须提供授权真实连接，验证 connection、catalog、DDL、事务、视图、分页、数据类型、注入防护、Secret 脱敏、取消、审计和 traceId。四种数据库的环境变量约定为：

```text
ASTERERP_TEST_SQLSERVER_CONNECTION
ASTERERP_TEST_MYSQL_CONNECTION
ASTERERP_TEST_POSTGRESQL_CONNECTION
ASTERERP_TEST_SQLITE_CONNECTION
```

缺少凭据、Docker 或真实数据库时，测试只能输出 `Blocked` 及恢复条件；不得用 Mock、匿名 `401` 或文档声明代替真实通过。当前矩阵明确标记了视图替换的非原子边界、行级乐观并发待 HAO-78、SQLite 路径 Sandbox 待 HAO-88，以及外部授权证据要求。
