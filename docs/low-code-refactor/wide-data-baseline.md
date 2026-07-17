# Wide-table and large-volume baseline

本基线把“列宽度”和“行数量”拆成两个可重复维度：20/200/1000 列只测量字段元数据、列布局和首个可交互列模型；10,000/1,000,000 行使用固定六字段投影并强制 200 行分页，避免把一百万行全部加载进浏览器或 DOM。该设计对应当前 Data Studio API 的分页边界，不能把本地 Fixture 结果冒充数据库验收。

## 现有实现入口

- 前端 `src/shared/table/tableQueryUtils.ts`：字段类型推断、筛选、排序。
- 前端 `src/shared/table/tableRuntimeUtils.ts`：运行时列模型和列设置。
- 页面 `src/pages/application-console/data-center/workbench/DataSourceTablesPanel.tsx`：表元数据、行查询和编辑入口。
- 后端 `ApplicationDataSourceTableRowService`：数据库侧 `COUNT`、筛选、排序、分页和参数化读写。
- 后端 `ApplicationDataSourceTableWorkbenchService`：建表、类型归一化和预览。

## 判定

本地基线只证明共享表格算法和有界页面数据的可重复性能。只有在重启 API、授权租户/权限和真实 Provider 数据源下完成 Route → Page → API → Application Service → ORM/DB → 回显，以及刷新、重新登录、编辑失败、导出取消和重新加载后，才可把对应 Case 判定为 `Done`。缺少真实数据源或浏览器证据时为 `Blocked`，不得使用 Mock 或匿名 401。
