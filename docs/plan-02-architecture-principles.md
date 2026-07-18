# Plan 02 - Overall Architecture Principles

本计划对应方案第 2 章“总体建设原则”，用于约束后续代码推进顺序。

## 职责边界清单

### 后端模块划分

- `AsterERP.Shared`: 基础结果、分页查询、错误码、权限属性、当前用户契约（零业务 I/O，可被 Api 与未来模块引用）。
- `AsterERP.Contracts`: 请求与响应 DTO，只表达接口契约；仅引用 Shared。
- `AsterERP.Domain`: `EntityBase` 等 ORM 耦合基类（依赖 SqlSugar，不进入 Shared）。
- `AsterERP.Api.Tests`: xUnit 测试工程，引用 Api + Shared + Contracts + Domain。
- `Application`: 应用服务编排，例如健康检查、回声示例，后续承载系统管理用例。
- `Infrastructure`: 数据库、仓储、事务、日志、安全、字典、编码规则等技术底座。
- `Modules`: 按业务域存放实体和模块内规则，目前已有 `System` 子域，并新增 `ProjectManagement` 原生 ABP 业务域。
- `ProjectManagement`: 项目管理的实体、领域规则、应用服务、权限和前端功能必须保持独立边界，依赖现有工作区、权限、数据过滤、文件、通知和审计能力，不复制基础设施。
- `AsterERP.Workflow.Approval.Core` / `AsterERP.Workflow.Forms.Core` / `AsterERP.Workflow.Core` / `AsterERP.Workflow.Persistence` / `AsterERP.Workflow.DependencyInjection`: Workflow 审批子系统的独立程序集，统一接入 ABP `Core` / `Timing` / `Guids` / `Uow` 等基础能力，同时保留原生工作流业务主链路。
- `Endpoints`: Minimal API 路由入口，只做 HTTP 适配。

### 前端模块划分

- `src/core`: http、query、responsive、ui-engine 等运行时能力。
- `src/shared`: 响应式页面、表格、表单、字典、权限等生产力组件。
- `src/pages`: 页面装配和业务交互状态，不重复封装底层控件。
- `src/app`: Provider、路由、布局、导航。

## 类与函数职责映射

### 后端

- `AsterErpAbpHostModule` / 各领域 ABP 模块：按模块边界注册应用层与基础设施服务。
- `InfrastructureServiceCollectionExtensions`: 注册基础设施服务。
- `Program.cs`: 组合根，只负责装配服务、中间件、路由和初始化。
- `WorkspaceSqlSugarRepository<TEntity>`: 工作区通用仓储，不承载具体业务分支。
- `GridQuery` / `GridPageResult`: ERP 列表页统一查询与分页协议。
- `SqlSugarUnitOfWork`: 事务边界。
- `RequestDiagnosticsMiddleware`: TraceId 与请求诊断。
- `OperationLogMiddleware`: 操作日志采集。
- `GlobalExceptionHandler`: 统一异常输出。

### 前端

- `AppProviders`: 组合 Query、Responsive、I18n、Theme 等 Provider。
- `AppLayout`: 路由状态编排和壳层组合。
- `BasicLayout`: 应用壳、侧栏、顶部栏、标签与面包屑。
- `HeaderBar` / `SidebarMenu` / `TabsView` / `BreadcrumbView`: 壳层拆分的独立 UI 单元。
- `ResponsivePage`: 页面容器、标题区、查询区、工具栏和内容区。
- `AdvancedDataGrid`: 查询区、工具栏、自动高度表格的统一组合层。
- `DataTable` / `AutoHeightTable`: 表格展示和响应式列处理。
- `FormRenderer` / `ModalForm`: Schema 表单渲染与弹窗编辑。
- `PermissionButton`: 权限按钮。
- `DictSelect` / `DictTag`: 字典输入和展示。

## 依赖方向

后端只允许从入口层向内组合，不允许业务模块绕过基础设施契约：

```txt
Program.cs -> Application / Infrastructure / Endpoints
AsterERP.Api -> AsterERP.Shared / AsterERP.Contracts / AsterERP.Domain
AsterERP.Contracts -> AsterERP.Shared
AsterERP.Domain -> SqlSugarCore（不引用 Shared）
AsterERP.Api.Tests -> AsterERP.Api / AsterERP.Shared / AsterERP.Contracts / AsterERP.Domain
Endpoints -> Application / Infrastructure / AsterERP.Contracts / AsterERP.Shared
Application -> Infrastructure / Modules / AsterERP.Contracts / AsterERP.Shared
Infrastructure -> Modules / AsterERP.Domain / AsterERP.Shared
Modules -> AsterERP.Domain / AsterERP.Shared
```

前端页面只装配能力，不重新实现核心机制：

```txt
app -> pages -> shared -> core
```

## 第 2 章落地验收

- 已有架构契约文档，可作为后续任务准入条件。
- 后端服务注册按层拆分，`Program.cs` 不继续堆积具体服务。
- 后端统一 Grid 查询协议存在，并可被仓储层复用。
- 后端 Grid 查询协议已支持基础排序透传。
- Flow / Workflow 子系统已具备独立 ABP 模块入口，当前用户、时间、GUID、UnitOfWork、后台轮询均可复用基础设施能力。
- 前端统一页面生产力组件存在，并可直接装配到业务页。
- 前端应用壳已拆分为独立布局单元，符合低耦合原则。
- 现有前后端目录与职责边界对齐。
- 后续 P0 功能必须优先复用这些边界。
- ProjectManagement M0 骨架必须先通过模块注册、发布裁剪映射、权限前缀和架构守卫测试，才能进入项目/任务业务实现。
