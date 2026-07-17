# 开发中心页面列表最新链路证据

## 根因

`/development-center/pages` 仍然加载业务对象旧页面，旧页面直接请求 `/business-objects`，并解析 `schema.sections`，用户从入口进入后绕过最新 `ApplicationDevelopmentPage`、`DesignerDocument` 和统一发布链。

## 本次实现

- `ApplicationDevelopmentPagesPage` 使用 `getApplicationDevelopmentWorkspace` 获取版本、模块和页面资源。
- 页面创建、元数据编辑、预览菜单刷新、发布分别调用 Application Development Center API。
- 进入 Designer 统一导航到 `DesignerRoutePage`；预览不再解析 `schema.sections`。
- 删除 `businessObjectApi.ts` 和旧 `BusinessObjectDesignPage` 入口，路由改为 `ApplicationDevelopmentPagesPage`。

## 验证

```text
npm run typecheck
npm run test -- --run src/pages/application-console/development-center/DevelopmentCenterDesignerPage.test.ts
npm run test -- --run src/pages/application-console/development-center/low-code-studio/canvas/DesignerCanvas.test.tsx
npm run build
```

结果：typecheck 通过；页面列表守卫 6/6、画布 21/21；生产构建通过。

浏览器验收还需在本地已登录会话中确认页面列表不产生 `/business-objects` 请求，并能进入 Designer、预览和发布。
