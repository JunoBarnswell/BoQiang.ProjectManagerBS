# Application Console 页面统计数据源证据

日期：2026-07-13

## 根因

Application Console 的 `PageCount` 和 `PublishedPageCount` 仍从旧的
`SystemPageSchemaEntity` 投影读取。该投影已经不是最新 DesignerDocument 的正式来源，继续统计会把旧投影残留误报为现存页面。

## 最新链路

统计现在直接读取 `ApplicationDevelopmentPageEntity`，按租户、应用、删除标记和 Published 状态过滤。运行时内容仍由页面的
`ApplicationDesignerDocumentEntity.PublishedArtifactId` 指向不可变 RuntimeArtifact；Capability Reader 只负责页面数量，不读取 artifact JSON。

## 验证

`ApplicationDatabaseBaselineSeederTests.ReadCountsAsync_UsesDesignerPagesInsteadOfLegacyPageSchemas` 验证插入旧
`SystemPageSchemaEntity` 不会改变页面统计；测试结果 11/11 通过。

提交：`41f00f41`
