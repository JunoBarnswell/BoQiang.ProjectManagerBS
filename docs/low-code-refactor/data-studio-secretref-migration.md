# Data Studio SecretRef-only 持久化迁移策略

## 当前写入契约

`ApplicationDataSourceService` 和 `ApplicationApiServiceService` 的新建、更新请求必须把非敏感连接元数据放在 `ConfigJson`，把凭据放在 `SecretConfigJson`。应用服务在 `ConfigJson` 进入实体前递归检查属性名；`password`、`token`、`apiKey`、`secret`、`connectionString` 及现有同类匹配项会被拒绝，并返回 `ApplicationDataCenterInvalidConfig`。凭据只能沿现有链路保护到 `SecretConfigCipherText`，以 `SecretRef` 标识，公开响应只返回摘要。

该校验发生在实体 Insert/Update 之前，因此不是响应脱敏，也不会把明文敏感字段落入 `ConfigJson`。诊断草稿是临时连接测试输入，不是持久化入口；它不应被当作已保存数据源使用。

## 存量迁移步骤

本次代码变更不直接触碰任何用户数据库或运行时资产。上线前由受控迁移作业按当前租户/应用工作区执行以下步骤：

1. 备份应用数据库，并记录数据源/API 服务实体的总行数、租户、应用、对象 ID 和迁移批次号。
2. 只读取 `ConfigJson` JSON 对象，递归定位敏感属性；不得使用字符串 `Contains` 判断，也不得把敏感值写入日志、审计预览或迁移报告。
3. 对每一行保留非敏感 `ConfigJson`；将敏感键值合并到既有 `SecretConfigJson` 写入链并保护为 `SecretConfigCipherText`，复用已有 `SecretRef`，同时重建 `PublicConfigJson` 摘要。
4. 若同一键同时出现在公开配置和已有凭据中、JSON 不是对象、保护失败或无法确定字段归属，停止该行并进入人工隔离队列，不覆盖任一值。
5. 每批在事务中提交，提交后重新读取确认：公开配置不含敏感键和值、`SecretRef` 与密文同时存在、连接工厂只从受保护凭据取得凭据；记录成功/隔离/失败计数，计数不一致则阻断发布。
6. 迁移完成后重新启动 API，运行 Data Studio SecretRef 定向测试和真实租户/应用连接诊断；不得用响应中的掩码字段代替数据库层验证。

## 回滚与发布门禁

迁移前备份和每行变更快照是回滚依据。若保护、计数、连接诊断或审计校验失败，先停止后续批次，再按批次恢复快照/备份；禁止通过重新写入明文 `ConfigJson` 回滚。迁移报告必须包含失败行 ID 和原因摘要，但不得包含凭据值。

在存量迁移完成前，旧的公开敏感字段会被新建/更新边界拒绝，但历史行可能仍被连接工厂读取，因此该状态只能作为阻断发布的迁移 blocker，不能宣告 SecretRef-only 已在全量数据上闭环。
