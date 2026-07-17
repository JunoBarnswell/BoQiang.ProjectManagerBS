using System.Text;

namespace AsterERP.Api.Infrastructure.Publishing;

public sealed class ApplicationPublishFrontendTargetWriter
{
    public async Task WriteAsync(
        string sourceRoot,
        ApplicationPublishDependencySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (string.Equals(snapshot.PublishMode, "Full", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var frontendRoot = Path.Combine(sourceRoot, "frontend", "AsterERP.Web", "src");
        await WriteWorkspaceRoutesAsync(frontendRoot, snapshot.FrontendRoutes, cancellationToken);
        await WriteNavigationRoutesAsync(frontendRoot, snapshot.FrontendRoutes, cancellationToken);
    }

    private static Task WriteWorkspaceRoutesAsync(
        string frontendRoot,
        IReadOnlyList<string> frontendRoutes,
        CancellationToken cancellationToken)
    {
        var routePaths = frontendRoutes.Select(NormalizeRoutePath).ToArray();
        var includeRuntimeRoute = routePaths.Any(IsRuntimePageRoute);
        var includeWorkflowRoutes = routePaths.Any(route => route.StartsWith("/workflows/", StringComparison.OrdinalIgnoreCase));
        var includeFlowiseRoutes = routePaths.Any(route => route.StartsWith("/flowise/", StringComparison.OrdinalIgnoreCase));
        var builder = new StringBuilder();
        builder.AppendLine("import { lazy, Suspense, type ReactNode } from 'react';");
        builder.AppendLine("import { Navigate, type RouteObject } from 'react-router-dom';");
        builder.AppendLine();
        builder.AppendLine("import { Page403 } from '../../shared/status/Page403';");
        builder.AppendLine("import { Page404 } from '../../shared/status/Page404';");
        builder.AppendLine("import { PageLoading } from '../../shared/status/PageLoading';");
        builder.AppendLine();
        builder.AppendLine("const DashboardPage = lazy(() => import('@/pages/dashboard/DashboardPage'));");
        if (includeRuntimeRoute)
        {
            builder.AppendLine("const RuntimePage = lazy(() => import('../../pages/runtime/RuntimePage').then((module) => ({ default: module.RuntimePage })));");
        }
        if (includeWorkflowRoutes)
        {
            AppendWorkflowLazyImports(builder);
        }
        if (includeFlowiseRoutes)
        {
            AppendFlowiseLazyImports(builder);
        }

        builder.AppendLine();
        builder.AppendLine("function lazyPage(children: ReactNode) {");
        builder.AppendLine("  return <Suspense fallback={<PageLoading />}>{children}</Suspense>;");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("export const workspaceRoutes: RouteObject[] = [");
        builder.AppendLine("  {");
        builder.AppendLine("    index: true,");
        builder.AppendLine("    element: <Navigate replace to=\"/home\" />");
        builder.AppendLine("  },");
        builder.AppendLine("  {");
        builder.AppendLine("    path: 'home',");
        builder.AppendLine("    element: lazyPage(<DashboardPage />)");
        builder.AppendLine("  },");
        builder.AppendLine("  {");
        builder.AppendLine("    path: 'dashboard',");
        builder.AppendLine("    element: lazyPage(<DashboardPage />)");
        builder.AppendLine("  },");
        if (includeRuntimeRoute)
        {
            builder.AppendLine("  {");
            builder.AppendLine("    path: 'pages/:pageCode',");
            builder.AppendLine("    element: lazyPage(<RuntimePage />)");
            builder.AppendLine("  },");
        }
        if (includeWorkflowRoutes)
        {
            AppendWorkflowWorkspaceRoutes(builder);
        }
        if (includeFlowiseRoutes)
        {
            AppendFlowiseWorkspaceRoutes(builder);
        }

        builder.AppendLine("  {");
        builder.AppendLine("    path: '403',");
        builder.AppendLine("    element: <Page403 />");
        builder.AppendLine("  },");
        builder.AppendLine("  {");
        builder.AppendLine("    path: '*',");
        builder.AppendLine("    element: <Page404 />");
        builder.AppendLine("  }");
        builder.AppendLine("];");

        var path = Path.Combine(frontendRoot, "app", "router", "workspaceRoutes.target.tsx");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, builder.ToString(), cancellationToken);
    }

    private static Task WriteNavigationRoutesAsync(
        string frontendRoot,
        IReadOnlyList<string> frontendRoutes,
        CancellationToken cancellationToken)
    {
        var routePaths = frontendRoutes.Select(NormalizeRoutePath).ToArray();
        var includeRuntimeRoute = routePaths.Any(IsRuntimePageRoute);
        var includeWorkflowRoutes = routePaths.Any(route => route.StartsWith("/workflows/", StringComparison.OrdinalIgnoreCase));
        var includeFlowiseRoutes = routePaths.Any(route => route.StartsWith("/flowise/", StringComparison.OrdinalIgnoreCase));
        var builder = new StringBuilder();
        builder.AppendLine("export interface AppRouteMeta {");
        builder.AppendLine("  breadcrumbKey: string;");
        builder.AppendLine("  cachePolicy?: 'none' | 'tab-alive';");
        builder.AppendLine("  iconKey: string;");
        builder.AppendLine("  labelKey: string;");
        builder.AppendLine("  layoutVariant?: 'app' | 'login';");
        builder.AppendLine("  path: string;");
        builder.AppendLine("  presentation?: 'drawer' | 'modal' | 'page' | 'subtab';");
        builder.AppendLine("  tabMode?: 'detail' | 'menu' | 'none';");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("export const appRoutes: AppRouteMeta[] = [");
        AppendRoute(builder, "/", "breadcrumbs.home", "home", "nav.home", "none");
        AppendRoute(builder, "/home", "breadcrumbs.dashboard", "dashboard", "nav.dashboard", "tab-alive");
        AppendRoute(builder, "/dashboard", "breadcrumbs.dashboard", "dashboard", "nav.dashboard", "tab-alive");
        if (includeRuntimeRoute)
        {
            AppendRoute(builder, "/pages/:pageCode", "breadcrumbs.runtime", "module", "nav.runtime", "tab-alive");
        }
        if (includeWorkflowRoutes)
        {
            AppendWorkflowNavigationRoutes(builder);
        }
        if (includeFlowiseRoutes)
        {
            AppendFlowiseNavigationRoutes(builder);
        }

        AppendRoute(builder, "/login", "breadcrumbs.login", "users", "nav.login", "none", "login", isLast: true);
        builder.AppendLine("];");
        builder.AppendLine();
        builder.AppendLine("export function findRouteMeta(pathname: string): AppRouteMeta {");
        builder.AppendLine("  if (pathname.startsWith('/pages/')) {");
        builder.AppendLine("    return appRoutes.find((route) => route.path === '/pages/:pageCode') ?? appRoutes[0];");
        builder.AppendLine("  }");
        builder.AppendLine();
        builder.AppendLine("  return appRoutes.find((route) => route.path === pathname || matchesRoutePattern(route.path, pathname)) ?? appRoutes[0];");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("function matchesRoutePattern(pattern: string, pathname: string): boolean {");
        builder.AppendLine("  if (!pattern.includes(':')) return false;");
        builder.AppendLine("  const patternParts = pattern.split('/').filter(Boolean);");
        builder.AppendLine("  const pathParts = pathname.split('/').filter(Boolean);");
        builder.AppendLine("  if (patternParts.length !== pathParts.length) return false;");
        builder.AppendLine("  return patternParts.every((part, index) => part.startsWith(':') || part === pathParts[index]);");
        builder.AppendLine("}");

        var path = Path.Combine(frontendRoot, "app", "navigation", "routes.target.ts");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, builder.ToString(), cancellationToken);
    }

    private static void AppendRoute(
        StringBuilder builder,
        string path,
        string breadcrumbKey,
        string iconKey,
        string labelKey,
        string cachePolicy,
        string layoutVariant = "app",
        bool isLast = false)
    {
        builder.AppendLine("  {");
        builder.AppendLine($"    breadcrumbKey: '{breadcrumbKey}',");
        builder.AppendLine($"    cachePolicy: '{cachePolicy}',");
        builder.AppendLine($"    iconKey: '{iconKey}',");
        builder.AppendLine($"    labelKey: '{labelKey}',");
        builder.AppendLine($"    layoutVariant: '{layoutVariant}',");
        builder.AppendLine($"    path: '{path}'");
        builder.AppendLine(isLast ? "  }" : "  },");
    }

    private static string NormalizeRoutePath(string route)
    {
        var index = route.IndexOf('?', StringComparison.Ordinal);
        return index >= 0 ? route[..index] : route;
    }

    private static bool IsRuntimePageRoute(string routePath) =>
        routePath.StartsWith("/pages/", StringComparison.OrdinalIgnoreCase) ||
        routePath.StartsWith("/runtime/", StringComparison.OrdinalIgnoreCase);

    private static void AppendWorkflowLazyImports(StringBuilder builder)
    {
        builder.AppendLine("const WorkflowCalendarsPage = lazy(() => import('../../pages/workflows/WorkflowCalendarsPage').then((module) => ({ default: module.WorkflowCalendarsPage })));");
        builder.AppendLine("const WorkflowBindingsPage = lazy(() => import('../../pages/workflows/WorkflowBindingsPage').then((module) => ({ default: module.WorkflowBindingsPage })));");
        builder.AppendLine("const WorkflowCategoriesPage = lazy(() => import('../../pages/workflows/WorkflowCategoriesPage').then((module) => ({ default: module.WorkflowCategoriesPage })));");
        builder.AppendLine("const WorkflowDelegationsPage = lazy(() => import('../../pages/workflows/WorkflowDelegationsPage').then((module) => ({ default: module.WorkflowDelegationsPage })));");
        builder.AppendLine("const WorkflowDesignerPage = lazy(() => import('../../pages/workflows/WorkflowDesignerPage').then((module) => ({ default: module.WorkflowDesignerPage })));");
        builder.AppendLine("const WorkflowDraftsPage = lazy(() => import('../../pages/workflows/WorkflowDraftsPage').then((module) => ({ default: module.WorkflowDraftsPage })));");
        builder.AppendLine("const WorkflowFormsPage = lazy(() => import('../../pages/workflows/WorkflowFormsPage').then((module) => ({ default: module.WorkflowFormsPage })));");
        builder.AppendLine("const WorkflowHistoryPage = lazy(() => import('../../pages/workflows/WorkflowHistoryPage').then((module) => ({ default: module.WorkflowHistoryPage })));");
        builder.AppendLine("const WorkflowInitiatePage = lazy(() => import('../../pages/workflows/WorkflowInitiatePage').then((module) => ({ default: module.WorkflowInitiatePage })));");
        builder.AppendLine("const WorkflowInstancePage = lazy(() => import('../../pages/workflows/WorkflowInstancePage').then((module) => ({ default: module.WorkflowInstancePage })));");
        builder.AppendLine("const WorkflowMonitoringPage = lazy(() => import('../../pages/workflows/WorkflowMonitoringPage').then((module) => ({ default: module.WorkflowMonitoringPage })));");
        builder.AppendLine("const WorkflowModelsPage = lazy(() => import('../../pages/workflows/WorkflowModelsPage').then((module) => ({ default: module.WorkflowModelsPage })));");
        builder.AppendLine("const WorkflowNotificationsPage = lazy(() => import('../../pages/workflows/WorkflowNotificationsPage').then((module) => ({ default: module.WorkflowNotificationsPage })));");
        builder.AppendLine("const WorkflowReportsPage = lazy(() => import('../../pages/workflows/WorkflowReportsPage').then((module) => ({ default: module.WorkflowReportsPage })));");
        builder.AppendLine("const WorkflowTasksPage = lazy(() => import('../../pages/workflows/WorkflowTasksPage').then((module) => ({ default: module.WorkflowTasksPage })));");
    }

    private static void AppendFlowiseLazyImports(StringBuilder builder)
    {
        var pages = new[]
        {
            "FlowiseChatflowsPage", "FlowiseAgentflowsPage", "FlowiseExecutionsPage", "FlowiseAssistantsPage",
            "FlowiseMarketplacesPage", "FlowiseToolsPage", "FlowiseCredentialsPage", "FlowiseVariablesPage",
            "FlowiseApiKeysPage", "FlowiseDocumentStoresPage", "FlowiseDatasetsPage", "FlowiseEvaluatorsPage",
            "FlowiseEvaluationsPage", "FlowiseSsoConfigPage", "FlowiseLoginActivityPage", "FlowiseLogsPage", "FlowiseAccountSettingsPage",
            "FlowiseCanvasPage"
        };
        foreach (var page in pages)
        {
            builder.AppendLine($"const {page} = lazy(() => import('../../features/flowise-studio').then((module) => ({{ default: module.{page} }})));");
        }
    }

    private static void AppendWorkflowWorkspaceRoutes(StringBuilder builder)
    {
        AppendWorkspaceRoute(builder, "workflows/initiate", "WorkflowInitiatePage");
        AppendWorkspaceRoute(builder, "workflows/forms", "WorkflowFormsPage");
        AppendWorkspaceRoute(builder, "workflows/models", "WorkflowModelsPage");
        AppendWorkspaceRoute(builder, "workflows/models/:modelId/designer", "WorkflowDesignerPage");
        AppendWorkspaceRoute(builder, "workflows/categories", "WorkflowCategoriesPage");
        AppendWorkspaceRoute(builder, "workflows/monitoring", "WorkflowMonitoringPage");
        AppendWorkspaceRoute(builder, "workflows/bindings", "WorkflowBindingsPage");
        AppendWorkspaceRoute(builder, "workflows/tasks", "WorkflowTasksPage");
        AppendWorkspaceRoute(builder, "workflows/drafts", "WorkflowDraftsPage");
        AppendWorkspaceRoute(builder, "workflows/instances/:processInstanceId", "WorkflowInstancePage");
        AppendWorkspaceRoute(builder, "workflows/history", "WorkflowHistoryPage");
        AppendWorkspaceRoute(builder, "workflows/reports", "WorkflowReportsPage");
        AppendWorkspaceRoute(builder, "workflows/notifications", "WorkflowNotificationsPage");
        AppendWorkspaceRoute(builder, "workflows/delegations", "WorkflowDelegationsPage");
        AppendWorkspaceRoute(builder, "workflows/calendars", "WorkflowCalendarsPage");
    }

    private static void AppendFlowiseWorkspaceRoutes(StringBuilder builder)
    {
        AppendWorkspaceRoute(builder, "flowise/chatflows", "FlowiseChatflowsPage");
        AppendWorkspaceRoute(builder, "flowise/agentflows", "FlowiseAgentflowsPage");
        AppendWorkspaceRoute(builder, "flowise/executions", "FlowiseExecutionsPage");
        AppendWorkspaceRoute(builder, "flowise/assistants", "FlowiseAssistantsPage");
        AppendWorkspaceRoute(builder, "flowise/marketplaces", "FlowiseMarketplacesPage");
        AppendWorkspaceRoute(builder, "flowise/tools", "FlowiseToolsPage");
        AppendWorkspaceRoute(builder, "flowise/credentials", "FlowiseCredentialsPage");
        AppendWorkspaceRoute(builder, "flowise/variables", "FlowiseVariablesPage");
        AppendWorkspaceRoute(builder, "flowise/api-keys", "FlowiseApiKeysPage");
        AppendWorkspaceRoute(builder, "flowise/document-stores", "FlowiseDocumentStoresPage");
        AppendWorkspaceRoute(builder, "flowise/datasets", "FlowiseDatasetsPage");
        AppendWorkspaceRoute(builder, "flowise/evaluators", "FlowiseEvaluatorsPage");
        AppendWorkspaceRoute(builder, "flowise/evaluations", "FlowiseEvaluationsPage");
        AppendWorkspaceRoute(builder, "flowise/sso-config", "FlowiseSsoConfigPage");
        AppendWorkspaceRoute(builder, "flowise/login-activity", "FlowiseLoginActivityPage");
        AppendWorkspaceRoute(builder, "flowise/logs", "FlowiseLogsPage");
        AppendWorkspaceRoute(builder, "flowise/account", "FlowiseAccountSettingsPage");
        AppendWorkspaceRoute(builder, "flowise/canvas/:resourceId", "FlowiseCanvasPage");
        AppendWorkspaceRoute(builder, "flowise/agentcanvas/:resourceId", "FlowiseCanvasPage");
    }

    private static void AppendWorkspaceRoute(StringBuilder builder, string path, string componentName, bool include = true)
    {
        if (!include)
        {
            return;
        }

        builder.AppendLine("  {");
        builder.AppendLine($"    path: '{path}',");
        builder.AppendLine($"    element: lazyPage(<{componentName} />)");
        builder.AppendLine("  },");
    }

    private static void AppendWorkflowNavigationRoutes(StringBuilder builder)
    {
        AppendRoute(builder, "/workflows/initiate", "breadcrumbs.workflowInitiate", "activity", "nav.workflowInitiate", "tab-alive");
        AppendRoute(builder, "/workflows/forms", "breadcrumbs.workflowForms", "menu", "nav.workflowForms", "tab-alive");
        AppendRoute(builder, "/workflows/models", "breadcrumbs.workflowModels", "activity", "nav.workflowModels", "tab-alive");
        AppendRoute(builder, "/workflows/models/:modelId/designer", "breadcrumbs.workflowDesigner", "wrench", "nav.workflowDesigner", "none");
        AppendRoute(builder, "/workflows/categories", "breadcrumbs.workflowCategories", "menu", "nav.workflowCategories", "tab-alive");
        AppendRoute(builder, "/workflows/monitoring", "breadcrumbs.workflowMonitoring", "activity", "nav.workflowMonitoring", "tab-alive");
        AppendRoute(builder, "/workflows/bindings", "breadcrumbs.workflowBindings", "menu", "nav.workflowBindings", "tab-alive");
        AppendRoute(builder, "/workflows/tasks", "breadcrumbs.workflowTasks", "list", "nav.workflowTasks", "tab-alive");
        AppendRoute(builder, "/workflows/drafts", "breadcrumbs.workflowDrafts", "list", "nav.workflowDrafts", "tab-alive");
        AppendRoute(builder, "/workflows/instances/:processInstanceId", "breadcrumbs.workflowInstance", "activity", "nav.workflowInstance", "none");
        AppendRoute(builder, "/workflows/history", "breadcrumbs.workflowHistory", "activity", "nav.workflowHistory", "tab-alive");
        AppendRoute(builder, "/workflows/reports", "breadcrumbs.workflowReports", "dashboard", "nav.workflowReports", "tab-alive");
        AppendRoute(builder, "/workflows/notifications", "breadcrumbs.workflowNotifications", "activity", "nav.workflowNotifications", "tab-alive");
        AppendRoute(builder, "/workflows/delegations", "breadcrumbs.workflowDelegations", "users", "nav.workflowDelegations", "tab-alive");
        AppendRoute(builder, "/workflows/calendars", "breadcrumbs.workflowCalendars", "activity", "nav.workflowCalendars", "tab-alive");
    }

    private static void AppendFlowiseNavigationRoutes(StringBuilder builder)
    {
        AppendRoute(builder, "/flowise/chatflows", "breadcrumbs.flowiseStudio", "module", "nav.flowiseChatflows", "tab-alive");
        AppendRoute(builder, "/flowise/agentflows", "breadcrumbs.flowiseStudio", "module", "nav.flowiseAgentflows", "tab-alive");
        AppendRoute(builder, "/flowise/executions", "breadcrumbs.flowiseStudio", "module", "nav.flowiseExecutions", "tab-alive");
        AppendRoute(builder, "/flowise/assistants", "breadcrumbs.flowiseStudio", "module", "nav.flowiseAssistants", "tab-alive");
        AppendRoute(builder, "/flowise/marketplaces", "breadcrumbs.flowiseStudio", "module", "nav.flowiseMarketplaces", "tab-alive");
        AppendRoute(builder, "/flowise/tools", "breadcrumbs.flowiseStudio", "wrench", "nav.flowiseTools", "tab-alive");
        AppendRoute(builder, "/flowise/credentials", "breadcrumbs.flowiseStudio", "shield", "nav.flowiseCredentials", "tab-alive");
        AppendRoute(builder, "/flowise/variables", "breadcrumbs.flowiseStudio", "braces", "nav.flowiseVariables", "tab-alive");
        AppendRoute(builder, "/flowise/api-keys", "breadcrumbs.flowiseStudio", "key", "nav.flowiseApiKeys", "tab-alive");
        AppendRoute(builder, "/flowise/document-stores", "breadcrumbs.flowiseStudio", "database", "nav.flowiseDocumentStores", "tab-alive");
        AppendRoute(builder, "/flowise/datasets", "breadcrumbs.flowiseStudio", "database", "nav.flowiseDatasets", "tab-alive");
        AppendRoute(builder, "/flowise/evaluators", "breadcrumbs.flowiseStudio", "activity", "nav.flowiseEvaluators", "tab-alive");
        AppendRoute(builder, "/flowise/evaluations", "breadcrumbs.flowiseStudio", "activity", "nav.flowiseEvaluations", "tab-alive");
        AppendRoute(builder, "/flowise/sso-config", "breadcrumbs.flowiseStudio", "shield", "nav.flowiseSsoConfig", "tab-alive");
        AppendRoute(builder, "/flowise/login-activity", "breadcrumbs.flowiseStudio", "clipboard", "nav.flowiseLoginActivity", "tab-alive");
        AppendRoute(builder, "/flowise/logs", "breadcrumbs.flowiseStudio", "activity", "nav.flowiseLogs", "tab-alive");
        AppendRoute(builder, "/flowise/account", "breadcrumbs.flowiseStudio", "settings", "nav.flowiseAccount", "tab-alive");
        AppendRoute(builder, "/flowise/canvas/:resourceId", "breadcrumbs.flowiseStudio", "module", "nav.flowiseChatflows", "none");
        AppendRoute(builder, "/flowise/agentcanvas/:resourceId", "breadcrumbs.flowiseStudio", "module", "nav.flowiseAgentflows", "none");
    }
}
