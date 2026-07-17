import {
  Activity,
  ArrowRight,
  Bell,
  Building2,
  CheckCircle2,
  Clock3,
  FileClock,
  FolderTree,
  RefreshCw,
  Server,
  ShieldCheck,
  UserCog,
  Users
} from 'lucide-react';
import { useCallback, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';

import { systemAnnouncementApi, type SystemAnnouncementListItemDto } from '../../api/system/announcements.api';
import { systemOperationLogApi, type OperationLogListItemDto } from '../../api/system/operation-logs.api';
import { systemScheduledJobApi, type ScheduledJobSummaryDto } from '../../api/system/scheduled-jobs.api';
import { getDepartments, getMenus, getRoles, getUsers } from '../../api/system/system-management.api';
import type { MenuListItemDto } from '../../api/system/system.types';
import {
  getWorkflowTaskSummary,
  getWorkflowTodoTasks,
  type WorkflowTaskListItemDto,
  type WorkflowTaskSummaryDto
} from '../../api/workflow/workflows.api';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiQueries } from '../../core/query/useApiQueries';
import { useMenuStore, usePermissionStore, useWorkspaceStore } from '../../core/state';
import { CrudStateView } from '../../shared/components/crud-page/CrudStateView';

import {
  countMenuNodes,
  flattenFlowiseMenus,
  flattenFlowiseMenuRows,
  formatDateTime,
  formatOperationLogTitle,
  formatWorkflowBusiness,
  formatWorkflowTaskTitle,
  getEnvelopeData,
  getGridItems,
  getGridTotal,
  getMetricValue,
  getQueryResult,
  getScopedQueryKey,
  getSummaryMetricValue,
  listSize,
  pageSize,
  type DashboardMetric,
  type DashboardQueryResult,
  type ShortcutItem
} from './dashboardModel';
import { MetricCard } from './MetricCard';

export function DashboardPage() {
  const navigate = useNavigate();
  const { translate, locale } = useI18n();
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const menus = useMenuStore((state) => state.menus);
  const permissionCodes = usePermissionStore((state) => state.permissionCodes);
  const availableWorkspaces = useWorkspaceStore((state) => state.availableWorkspaces);
  const hasPermission = usePermissionStore((state) => state.hasPermission);

  const systemKey = currentWorkspace
    ? currentWorkspace.systemId || currentWorkspace.workspaceId || `${currentWorkspace.tenantId}:${currentWorkspace.appCode}`
    : 'none';
  const systemName = currentWorkspace?.systemName || currentWorkspace?.appName || translate('page.dashboard.noSystemSelected');
  const tenantName = currentWorkspace?.tenantName || '-';
  const isSystemWorkspace = currentWorkspace?.appCode?.toUpperCase() === 'SYSTEM';
  const workspacePathPrefix = currentWorkspace?.workspaceLevel === 'application' && currentWorkspace.tenantId && currentWorkspace.appCode
    ? `/tenants/${encodeURIComponent(currentWorkspace.tenantId)}/apps/${currentWorkspace.appCode.toUpperCase()}/admin`
    : '';
  const resolveWorkspacePath = useCallback((path: string) => {
    const normalized = path.startsWith('/') ? path : `/${path}`;
    if (!workspacePathPrefix || normalized.startsWith('/apps/') || normalized.startsWith('/tenants/')) {
      return normalized;
    }

    return `${workspacePathPrefix}${normalized}`;
  }, [workspacePathPrefix]);
  const visibleMenuCount = useMemo(() => countMenuNodes(menus), [menus]);
  const flowiseHomeMenus = useMemo(
    () =>
      flattenFlowiseMenus(menus).map((item) => ({
        desc: translate(item.desc),
        path: resolveWorkspacePath(item.path),
        title: translate(item.title)
      })),
    [menus, resolveWorkspacePath, translate]
  );
  const availableSystemCount = useMemo(
    () => availableWorkspaces.filter((workspace) => workspace.isAvailable !== false).length,
    [availableWorkspaces]
  );

  const noAccessText = translate('page.dashboard.noAccess');
  const loadingText = translate('page.dashboard.loadingValue');
  const approvalTaskText = translate('page.dashboard.approvalTask');
  const businessDocumentText = translate('page.dashboard.businessDocument');
  const workflowListText = translate('page.dashboard.workflowListQuery');
  const workflowOperationText = translate('page.dashboard.workflowOperation');

  const canViewUsers = isSystemWorkspace && hasPermission('system:user:query');
  const canViewRoles = isSystemWorkspace && hasPermission('system:role:query');
  const canViewDepartments = isSystemWorkspace && hasPermission('system:dept:query');
  const canViewMenus = isSystemWorkspace && hasPermission('system:menu:query');
  const canViewAnnouncements = isSystemWorkspace && hasPermission('system:announcement:query');
  const canViewOperationLogs = isSystemWorkspace && hasPermission('system:operation-log:query');
  const canViewScheduledJobs = isSystemWorkspace && hasPermission('system:scheduled-job:query');
  const canViewDicts = isSystemWorkspace && hasPermission('system:dict:query');
  const canViewParameters = isSystemWorkspace && hasPermission('system:parameter:query');
  const canQueryViews = isSystemWorkspace && hasPermission('system:query-view:query');
  const canViewWorkflowTasks = hasPermission('workflow:task:query');
  const canViewWorkflowHistory = hasPermission('workflow:history:query');
  const canViewWorkflowModels = hasPermission('workflow:model:query');
  const canViewWorkflowBindings = hasPermission('workflow:binding:query');
  const canQueryUsers = canViewUsers && canQueryViews;
  const canQueryRoles = canViewRoles && canQueryViews;
  const canQueryDepartments = canViewDepartments && canQueryViews;
  const hasSystem = Boolean(currentWorkspace);

  const overviewQueries = useApiQueries(
    useMemo(() => {
      const tenantId = currentWorkspace?.tenantId ?? '';
      const appCode = currentWorkspace?.appCode ?? '';
      const queries: Parameters<typeof useApiQueries>[0] = {};

      if (hasSystem && canQueryUsers) {
        queries.users = {
          queryFn: ({ signal }) => getUsers({ pageIndex: 1, pageSize }, signal),
          queryKey: getScopedQueryKey(queryKeys.systemManagement.users(1, pageSize, '', '', '', '', ''), systemKey)
        };
      }

      if (hasSystem && canQueryRoles) {
        queries.roles = {
          queryFn: ({ signal }) => getRoles({ appCode, pageIndex: 1, pageSize, tenantId }, signal),
          queryKey: getScopedQueryKey(queryKeys.systemManagement.roles(1, pageSize, '', '', tenantId, appCode), systemKey)
        };
      }

      if (hasSystem && canQueryDepartments) {
        queries.departments = {
          queryFn: ({ signal }) => getDepartments({ pageIndex: 1, pageSize }, signal),
          queryKey: getScopedQueryKey(queryKeys.systemManagement.departments(1, pageSize, '', '', ''), systemKey)
        };
      }

      if (hasSystem && canViewMenus) {
        queries.menus = {
          queryFn: ({ signal }) => getMenus({ appCode, pageIndex: 1, pageSize: 200, tenantId }, signal),
          queryKey: getScopedQueryKey(queryKeys.systemManagement.menus(1, 200, '', '', '', '', false, tenantId, appCode), systemKey)
        };
      }

      if (hasSystem && canViewAnnouncements) {
        queries.announcements = {
          queryFn: ({ signal }) =>
            systemAnnouncementApi.list(
              {
                pageIndex: 1,
                pageSize: listSize,
                sorts: [{ field: 'publishedAt', order: 'desc' }],
                status: 'Published'
              },
              signal
            ),
          queryKey: ['system-announcements', 'home', systemKey, 'published'] as const
        };
      }

      if (hasSystem && canViewOperationLogs) {
        queries.operationLogs = {
          queryFn: ({ signal }) =>
            systemOperationLogApi.list(
              {
                pageIndex: 1,
                pageSize: listSize,
                sorts: [{ field: 'createdTime', order: 'desc' }]
              },
              signal
            ),
          queryKey: ['system-operation-logs', 'home', systemKey] as const
        };
      }

      if (hasSystem && canViewScheduledJobs) {
        queries.scheduledJobs = {
          queryFn: () => systemScheduledJobApi.summary(),
          queryKey: ['system-scheduled-jobs', 'home-summary', systemKey] as const
        };
      }

      if (hasSystem && canViewWorkflowTasks) {
        queries.workflowSummary = {
          queryFn: ({ signal }) => getWorkflowTaskSummary(signal),
          queryKey: ['workflows', 'home-summary', systemKey] as const
        };
        queries.workflowTodo = {
          queryFn: ({ signal }) => getWorkflowTodoTasks({ pageIndex: 1, pageSize: listSize }, signal),
          queryKey: ['workflows', 'home-todo', systemKey] as const
        };
      }

      return queries;
    }, [
      canViewAnnouncements,
      canQueryDepartments,
      canQueryRoles,
      canQueryUsers,
      canViewMenus,
      canViewOperationLogs,
      canViewScheduledJobs,
      canViewWorkflowTasks,
      currentWorkspace?.appCode,
      currentWorkspace?.tenantId,
      hasSystem,
      systemKey
    ])
  );

  const queryResults = overviewQueries.results as Record<string, DashboardQueryResult>;
  const usersResult = getQueryResult(queryResults, 'users');
  const rolesResult = getQueryResult(queryResults, 'roles');
  const departmentsResult = getQueryResult(queryResults, 'departments');
  const menusResult = getQueryResult(queryResults, 'menus');
  const announcementsResult = getQueryResult(queryResults, 'announcements');
  const operationLogsResult = getQueryResult(queryResults, 'operationLogs');
  const scheduledJobsResult = getQueryResult(queryResults, 'scheduledJobs');
  const workflowSummaryResult = getQueryResult(queryResults, 'workflowSummary');
  const workflowTodoResult = getQueryResult(queryResults, 'workflowTodo');
  const firstError = Object.values(queryResults).find((query) => query.isError)?.error ?? null;

  const menuRows = getGridItems<MenuListItemDto>(menusResult.data);
  const announcements = getGridItems<SystemAnnouncementListItemDto>(announcementsResult.data);
  const operationLogs = getGridItems<OperationLogListItemDto>(operationLogsResult.data);
  const scheduledJobSummary = getEnvelopeData<ScheduledJobSummaryDto>(scheduledJobsResult.data);
  const workflowSummary = getEnvelopeData<WorkflowTaskSummaryDto>(workflowSummaryResult.data);
  const workflowTodoTasks = getGridItems<WorkflowTaskListItemDto>(workflowTodoResult.data);
  const flowiseMenuRows = useMemo(() => flattenFlowiseMenuRows(menuRows), [menuRows]);
  const homeFlowiseMenus = (flowiseHomeMenus.length > 0 ? flowiseHomeMenus : flowiseMenuRows).map((item) => ({
    desc: translate(item.desc),
    path: resolveWorkspacePath(item.path),
    title: translate(item.title)
  }));

  const workspaceMetrics: DashboardMetric[] = [
    {
      desc: formatMessage(translate('page.dashboard.currentSystemDescription'), { tenantName }),
      icon: Server,
      title: translate('page.dashboard.currentSystemTitle'),
      tone: 'blue',
      value: systemName
    },
    {
      desc: translate('page.dashboard.visibleMenusDescription'),
      icon: FolderTree,
      title: translate('page.dashboard.visibleMenusTitle'),
      tone: 'rose',
      value: visibleMenuCount
    },
    {
      desc: translate('page.dashboard.permissionCodesDescription'),
      icon: ShieldCheck,
      title: translate('page.dashboard.permissionCodesTitle'),
      tone: 'teal',
      value: permissionCodes.length
    },
    {
      desc: translate('page.dashboard.switchableSystemsDescription'),
      icon: Building2,
      title: translate('page.dashboard.switchableSystemsTitle'),
      tone: 'amber',
      value: availableSystemCount
    }
  ];
  const workflowMetrics: DashboardMetric[] = [
    {
      desc: translate('page.dashboard.pendingApprovalsDescription'),
      icon: FileClock,
      isLoading: workflowSummaryResult.isLoading,
      noAccess: !canViewWorkflowTasks,
      title: translate('page.dashboard.pendingApprovalsTitle'),
      tone: 'rose',
      value: getSummaryMetricValue(workflowSummary, 'todo', workflowSummaryResult, !canViewWorkflowTasks, noAccessText, loadingText)
    },
    {
      desc: translate('page.dashboard.completedApprovalsDescription'),
      icon: CheckCircle2,
      isLoading: workflowSummaryResult.isLoading,
      noAccess: !canViewWorkflowTasks,
      title: translate('page.dashboard.completedApprovalsTitle'),
      tone: 'emerald',
      value: getSummaryMetricValue(workflowSummary, 'done', workflowSummaryResult, !canViewWorkflowTasks, noAccessText, loadingText)
    },
    {
      desc: translate('page.dashboard.overdueApprovalsDescription'),
      icon: Clock3,
      isLoading: workflowSummaryResult.isLoading,
      noAccess: !canViewWorkflowTasks,
      title: translate('page.dashboard.overdueApprovalsTitle'),
      tone: 'amber',
      value: getSummaryMetricValue(workflowSummary, 'timeout', workflowSummaryResult, !canViewWorkflowTasks, noAccessText, loadingText)
    },
    {
      desc: translate('page.dashboard.ccApprovalsDescription'),
      icon: Bell,
      isLoading: workflowSummaryResult.isLoading,
      noAccess: !canViewWorkflowTasks,
      title: translate('page.dashboard.ccApprovalsTitle'),
      tone: 'teal',
      value: getSummaryMetricValue(workflowSummary, 'cc', workflowSummaryResult, !canViewWorkflowTasks, noAccessText, loadingText)
    }
  ];
  const systemMetrics: DashboardMetric[] = [
    {
      desc: translate('page.dashboard.usersDescription'),
      icon: Users,
      isLoading: usersResult.isLoading,
      noAccess: !canQueryUsers,
      title: translate('page.dashboard.usersTitle'),
      tone: 'blue',
      value: getMetricValue(getGridTotal(usersResult.data), usersResult, !canQueryUsers, noAccessText, loadingText)
    },
    {
      desc: translate('page.dashboard.rolesDescription'),
      icon: ShieldCheck,
      isLoading: rolesResult.isLoading,
      noAccess: !canQueryRoles,
      title: translate('page.dashboard.rolesTitle'),
      tone: 'teal',
      value: getMetricValue(getGridTotal(rolesResult.data), rolesResult, !canQueryRoles, noAccessText, loadingText)
    },
    {
      desc: translate('page.dashboard.departmentsDescription'),
      icon: Building2,
      isLoading: departmentsResult.isLoading,
      noAccess: !canQueryDepartments,
      title: translate('page.dashboard.departmentsTitle'),
      tone: 'amber',
      value: getMetricValue(getGridTotal(departmentsResult.data), departmentsResult, !canQueryDepartments, noAccessText, loadingText)
    },
    {
      desc: translate('page.dashboard.menusDescription'),
      icon: FolderTree,
      isLoading: menusResult.isLoading,
      noAccess: !canViewMenus,
      title: translate('page.dashboard.menusTitle'),
      tone: 'rose',
      value: getMetricValue(getGridTotal(menusResult.data), menusResult, !canViewMenus, noAccessText, loadingText)
    }
  ];
  const metrics = [
    ...workspaceMetrics,
    ...(canViewWorkflowTasks ? workflowMetrics : []),
    ...systemMetrics.filter((metric) => isSystemWorkspace || !metric.noAccess)
  ];

  const quickShortcuts = useMemo<ShortcutItem[]>(
    () =>
      [
        canViewWorkflowTasks
          ? {
              desc: translate('page.dashboard.approvalWorkbenchDescription'),
              icon: FileClock,
              path: resolveWorkspacePath('/workflows/tasks'),
              title: translate('page.dashboard.approvalWorkbenchTitle'),
              tone: 'border-l-rose-500'
            }
          : null,
        canViewWorkflowHistory
          ? {
              desc: translate('page.dashboard.approvalRecordDescription'),
              icon: Clock3,
              path: resolveWorkspacePath('/workflows/history'),
              title: translate('page.dashboard.approvalRecordTitle'),
              tone: 'border-l-amber-500'
            }
          : null,
        canViewWorkflowModels
          ? {
              desc: translate('page.dashboard.approvalTemplateDescription'),
              icon: CheckCircle2,
              path: resolveWorkspacePath('/workflows/models'),
              title: translate('page.dashboard.approvalTemplateTitle'),
              tone: 'border-l-emerald-500'
            }
          : null,
        canViewWorkflowBindings
          ? {
              desc: translate('page.dashboard.approvalConfigDescription'),
              icon: FolderTree,
              path: resolveWorkspacePath('/workflows/bindings'),
              title: translate('page.dashboard.approvalConfigTitle'),
              tone: 'border-l-teal-500'
            }
          : null,
        canViewUsers
          ? {
              desc: translate('page.dashboard.userAuthorizationDescription'),
              icon: UserCog,
              path: resolveWorkspacePath('/system/users'),
              title: translate('page.dashboard.userAuthorizationTitle'),
              tone: 'border-l-blue-500'
            }
          : null,
        canViewRoles
          ? {
              desc: translate('page.dashboard.roleScopeDescription'),
              icon: ShieldCheck,
              path: resolveWorkspacePath('/system/roles'),
              title: translate('page.dashboard.roleScopeTitle'),
              tone: 'border-l-teal-500'
            }
          : null,
        canViewDepartments
          ? {
              desc: translate('page.dashboard.orgStructureDescription'),
              icon: Building2,
              path: resolveWorkspacePath('/system/departments'),
              title: translate('page.dashboard.orgStructureTitle'),
              tone: 'border-l-amber-500'
            }
          : null,
        canViewMenus
          ? {
              desc: translate('page.dashboard.menuPermissionDescription'),
              icon: FolderTree,
              path: resolveWorkspacePath('/system/menus'),
              title: translate('page.dashboard.menuPermissionTitle'),
              tone: 'border-l-rose-500'
            }
          : null,
        canViewDicts
          ? {
              desc: translate('page.dashboard.dictionaryDescription'),
              icon: FolderTree,
              path: resolveWorkspacePath('/system/dicts'),
              title: translate('page.dashboard.dictionaryTitle'),
              tone: 'border-l-emerald-500'
            }
          : null,
        canViewParameters
          ? {
              desc: translate('page.dashboard.parameterDescription'),
              icon: Activity,
              path: resolveWorkspacePath('/system/parameters'),
              title: translate('page.dashboard.parameterTitle'),
              tone: 'border-l-sky-500'
            }
          : null
      ].filter((item): item is ShortcutItem => Boolean(item)),
    [
      canViewDepartments,
      canViewDicts,
      canViewMenus,
      canViewParameters,
      canViewRoles,
      canViewUsers,
      canViewWorkflowBindings,
      canViewWorkflowHistory,
      canViewWorkflowModels,
      canViewWorkflowTasks,
      resolveWorkspacePath,
      translate
    ]
  );

  return (
    <div className="flex-1 p-3 overflow-y-auto overflow-x-hidden flex flex-col gap-3 bg-gray-50/30">
      <div className="erp-panel p-3 flex flex-col gap-3 shrink-0 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-lg font-bold text-gray-800">
            <span className="h-5 w-1 rounded-sm bg-primary-500"></span>
            {translate('home.workbench.title')}
          </h1>
          <p className="mt-1 pl-3 text-xs text-gray-500">
            {formatMessage(translate('page.dashboard.currentSystemLabel'), { systemName })}
          </p>
        </div>
        <button
          className="flex items-center justify-center gap-1 rounded border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 shadow-sm transition-colors hover:bg-gray-50 hover:text-primary-600 disabled:cursor-not-allowed disabled:opacity-60"
          disabled={overviewQueries.isAnyFetching}
          type="button"
          onClick={() => void overviewQueries.refetchAll()}
        >
          <RefreshCw size={14} className={overviewQueries.isAnyFetching ? 'animate-spin text-primary-500' : 'text-gray-500'} />
          {translate('page.dashboard.refresh')}
        </button>
      </div>

      <CrudStateView
        emptyText={translate('page.dashboard.empty')}
        error={firstError}
        isEmpty={false}
        isError={Boolean(firstError)}
        isLoading={overviewQueries.isAnyLoading}
        onRetry={() => void overviewQueries.refetchAll()}
      >
        <div className="flex flex-col gap-3">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {metrics.map((metric) => (
              <MetricCard key={metric.title} {...metric} />
            ))}
          </div>

          <div className={isSystemWorkspace ? 'grid gap-3 lg:grid-cols-3' : 'grid gap-3'}>
            <div className={isSystemWorkspace ? 'flex flex-col gap-3 lg:col-span-2' : 'flex flex-col gap-3'}>
              <section className="erp-panel p-5">
                <div className="mb-4 flex items-center justify-between gap-3">
                  <h3 className="flex items-center gap-2 text-sm font-bold text-gray-800">
                    <ArrowRight className="text-primary-500" size={16} /> {translate('page.dashboard.quickEntryTitle')}
                  </h3>
                  <span className="text-xs text-gray-400">{translate('page.dashboard.quickEntryHint')}</span>
                </div>
                {quickShortcuts.length === 0 ? (
                  <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                    {translate('page.dashboard.noQuickEntry')}
                  </div>
                ) : (
                  <div className="grid gap-3 sm:grid-cols-2">
                    {quickShortcuts.map((item) => {
                      const Icon = item.icon;
                      return (
                        <button
                          key={item.path}
                          className={`flex items-start gap-3 rounded border border-gray-100 bg-gray-50/50 p-3 text-left transition-all hover:-translate-y-0.5 hover:border-gray-200 hover:bg-gray-50 hover:shadow-sm border-l-4 ${item.tone}`}
                          type="button"
                          onClick={() => navigate(item.path)}
                        >
                          <span className="mt-0.5 flex h-9 w-9 items-center justify-center rounded border border-gray-100 bg-white text-gray-600 shadow-sm">
                            <Icon size={16} />
                          </span>
                          <span className="min-w-0 flex-1">
                            <span className="flex items-center justify-between gap-2 text-xs font-bold text-gray-800">
                              {item.title}
                              <ArrowRight size={12} className="text-gray-400" />
                            </span>
                            <span className="mt-1 block truncate text-[11px] text-gray-500">{item.desc}</span>
                          </span>
                        </button>
                      );
                    })}
                  </div>
                )}
              </section>

              {homeFlowiseMenus.length > 0 ? (
                <section className="erp-panel p-5">
                  <div className="mb-4 flex items-center justify-between gap-3">
                    <h3 className="flex items-center gap-2 text-sm font-bold text-gray-800">
                      <FolderTree className="text-blue-500" size={16} /> {translate('page.dashboard.flowiseMenuTitle')}
                    </h3>
                    <span className="text-xs text-gray-400">{translate('page.dashboard.flowiseMenuHint')}</span>
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                    {homeFlowiseMenus.map((item) => (
                      <button
                        key={item.path}
                        className="flex items-start gap-3 rounded border border-gray-100 bg-blue-50/40 p-3 text-left transition-all hover:-translate-y-0.5 hover:border-blue-100 hover:bg-blue-50 hover:shadow-sm"
                        type="button"
                        onClick={() => navigate(item.path)}
                      >
                        <span className="mt-0.5 flex h-9 w-9 items-center justify-center rounded border border-blue-100 bg-white text-blue-600 shadow-sm">
                          <FolderTree size={16} />
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="flex items-center justify-between gap-2 text-xs font-bold text-gray-800">
                            {item.title}
                            <ArrowRight size={12} className="text-blue-400" />
                          </span>
                          <span className="mt-1 block truncate text-[11px] text-gray-500">{item.desc}</span>
                        </span>
                      </button>
                    ))}
                  </div>
                </section>
              ) : null}

              <section className="erp-panel min-h-[220px]">
                <div className="flex items-center justify-between border-b border-gray-100 p-4">
                  <h3 className="flex items-center gap-2 text-sm font-bold text-gray-800">
                    <FileClock className="text-rose-500" size={16} /> {translate('page.dashboard.myApprovalsTitle')}
                  </h3>
                  <button
                    className="text-xs font-semibold text-primary-600 transition-colors hover:text-primary-700 disabled:text-gray-400"
                    disabled={!canViewWorkflowTasks}
                    type="button"
                    onClick={() => navigate(resolveWorkspacePath('/workflows/tasks'))}
                  >
                    {translate('page.dashboard.enterWorkbench')}
                  </button>
                </div>
                <div className="grid gap-3 p-4">
                  {!canViewWorkflowTasks ? (
                    <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                      {translate('page.dashboard.noApprovalPermission')}
                    </div>
                  ) : workflowTodoResult.isLoading ? (
                    <div className="text-sm text-gray-500">{translate('page.dashboard.approvalLoading')}</div>
                  ) : workflowTodoTasks.length === 0 ? (
                    <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                      {translate('page.dashboard.noPendingApprovals')}
                    </div>
                  ) : (
                    workflowTodoTasks.map((task) => (
                      <button
                        key={task.id}
                        className="rounded border border-gray-100 bg-gray-50 px-3 py-2 text-left transition-colors hover:border-primary-100 hover:bg-primary-50/40"
                        type="button"
                        onClick={() => navigate(resolveWorkspacePath('/workflows/tasks'))}
                      >
                        <div className="flex items-center justify-between gap-3">
                          <span className="truncate text-xs font-semibold text-gray-800">{formatWorkflowTaskTitle(task, approvalTaskText)}</span>
                          <span className={task.isOverdue ? 'shrink-0 text-[11px] font-semibold text-rose-600' : 'shrink-0 text-[11px] text-gray-400'}>
                            {task.isOverdue ? translate('page.dashboard.overdue') : formatDateTime(task.createdAt, locale)}
                          </span>
                        </div>
                        <p className="mt-1 truncate text-[11px] text-gray-500">{formatWorkflowBusiness(task, businessDocumentText)}</p>
                      </button>
                    ))
                  )}
                </div>
              </section>

              {isSystemWorkspace ? (
                <section className="erp-panel min-h-[260px] flex-1">
                  <div className="flex items-center justify-between border-b border-gray-100 p-4">
                    <h3 className="flex items-center gap-2 text-sm font-bold text-gray-800">
                      <Bell className="text-teal-500" size={16} /> {translate('page.dashboard.systemAnnouncementsTitle')}
                    </h3>
                    <span className="text-xs text-gray-400">{translate('page.dashboard.announcementSourceHint')}</span>
                  </div>
                  <div className="grid gap-2 p-4">
                    {!canViewAnnouncements ? (
                      <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                        {translate('page.dashboard.noAnnouncementPermission')}
                      </div>
                    ) : announcementsResult.isLoading ? (
                      <div className="text-sm text-gray-500">{translate('page.dashboard.announcementLoading')}</div>
                    ) : announcements.length === 0 ? (
                      <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                        {translate('page.dashboard.noAnnouncements')}
                      </div>
                    ) : (
                      announcements.map((announcement) => (
                        <div key={announcement.id} className="rounded border border-gray-100 bg-gray-50 px-3 py-2">
                          <div className="flex items-center justify-between gap-3">
                            <span className="truncate text-xs font-semibold text-gray-800">{announcement.title}</span>
                            <span className="shrink-0 text-[11px] text-gray-400">{formatDateTime(announcement.publishedAt, locale)}</span>
                          </div>
                          <p className="mt-1 line-clamp-2 text-[11px] text-gray-500">{announcement.content}</p>
                        </div>
                      ))
                    )}
                  </div>
                </section>
              ) : null}
            </div>

            {isSystemWorkspace ? (
            <div className="flex flex-col gap-3">
              <section className="erp-panel p-5">
                <h3 className="mb-4 flex items-center gap-2 text-sm font-bold text-gray-800">
                  <Activity className="text-emerald-500" size={16} /> {translate('page.dashboard.operationLogsTitle')}
                </h3>
                {!canViewOperationLogs ? (
                  <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                    {translate('page.dashboard.noLogPermission')}
                  </div>
                ) : operationLogsResult.isLoading ? (
                  <div className="text-sm text-gray-500">{translate('page.dashboard.logLoading')}</div>
                ) : operationLogs.length === 0 ? (
                  <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                    {translate('page.dashboard.noLogs')}
                  </div>
                ) : (
                  <div className="grid gap-3">
                    {operationLogs.map((log) => (
                      <div key={log.id} className="border-b border-gray-100 pb-2 last:border-b-0 last:pb-0">
                        <div className="flex items-center justify-between gap-2">
                          <span className="truncate text-xs font-semibold text-gray-700">{formatOperationLogTitle(log, workflowListText, workflowOperationText)}</span>
                          <span className={log.isSuccess ? 'text-[11px] font-semibold text-emerald-600' : 'text-[11px] font-semibold text-rose-600'}>
                            {log.statusCode}
                          </span>
                        </div>
                        <div className="mt-1 flex items-center justify-between gap-2 text-[11px] text-gray-400">
                          <span className="truncate">{log.userName || '-'}</span>
                          <span>{formatDateTime(log.createdTime, locale)}</span>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </section>

              <section className="erp-panel p-5">
                <h3 className="mb-4 flex items-center gap-2 text-sm font-bold text-gray-800">
                  <Server className="text-blue-500" size={16} /> {translate('page.dashboard.taskSchedulingTitle')}
                </h3>
                {!canViewScheduledJobs ? (
                  <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                    {translate('page.dashboard.noTaskPermission')}
                  </div>
                ) : scheduledJobsResult.isLoading ? (
                  <div className="text-sm text-gray-500">{translate('page.dashboard.taskSummaryLoading')}</div>
                ) : scheduledJobSummary ? (
                  <div className="grid grid-cols-2 gap-3">
                    <div className="rounded border border-gray-100 bg-gray-50 px-3 py-2">
                      <span className="text-[11px] text-gray-400">{translate('page.dashboard.allTasks')}</span>
                      <strong className="mt-1 block text-lg text-gray-800">{scheduledJobSummary.total}</strong>
                    </div>
                    <div className="rounded border border-gray-100 bg-gray-50 px-3 py-2">
                      <span className="text-[11px] text-gray-400">{translate('page.dashboard.enabled')}</span>
                      <strong className="mt-1 block text-lg text-emerald-600">{scheduledJobSummary.enabled}</strong>
                    </div>
                    <div className="rounded border border-gray-100 bg-gray-50 px-3 py-2">
                      <span className="text-[11px] text-gray-400">{translate('page.dashboard.paused')}</span>
                      <strong className="mt-1 block text-lg text-amber-600">{scheduledJobSummary.paused}</strong>
                    </div>
                    <div className="rounded border border-gray-100 bg-gray-50 px-3 py-2">
                      <span className="text-[11px] text-gray-400">{translate('page.dashboard.failed')}</span>
                      <strong className="mt-1 block text-lg text-rose-600">{scheduledJobSummary.failed}</strong>
                    </div>
                  </div>
                ) : (
                  <div className="rounded border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-500">
                    {translate('page.dashboard.noTaskSummary')}
                  </div>
                )}
              </section>
            </div>
            ) : null}
          </div>
        </div>
      </CrudStateView>
    </div>
  );
}

export default DashboardPage;
