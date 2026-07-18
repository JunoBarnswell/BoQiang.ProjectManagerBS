import { projectManagementQueryKeys } from './projectManagementQueryKeys';

const queryKeyRoot = ['astererp'] as const;

export const queryKeys = {
  all: queryKeyRoot,
  dashboard: {
    health: () => [...queryKeyRoot, 'dashboard', 'health'] as const,
    echo: (message: string) => [...queryKeyRoot, 'dashboard', 'echo', message] as const
  },
  dict: {
    byType: (dictType: string) => [...queryKeyRoot, 'dict', dictType] as const
  },
  systemDicts: {
    all: [...queryKeyRoot, 'system-dicts'] as const,
    items: (dictTypeId: string) => [...queryKeyRoot, 'system-dicts', 'items', dictTypeId] as const,
    types: (pageIndex: number, pageSize: number, keyword: string, sorts: unknown = []) =>
      [...queryKeyRoot, 'system-dicts', 'types', pageIndex, pageSize, keyword, sorts] as const
  },
  systemParameters: {
    root: () => [...queryKeyRoot, 'system-parameters'] as const,
    list: (pageIndex: number, pageSize: number, keyword: string, category: string, status: string, sorts: unknown = []) =>
      [...queryKeyRoot, 'system-parameters', 'list', pageIndex, pageSize, keyword, category, status, sorts] as const
  },
  systemFiles: {
    root: () => [...queryKeyRoot, 'system-files'] as const,
    list: (pageIndex: number, pageSize: number, keyword: string, sorts: unknown = [], tableQuery: unknown = null) =>
      [...queryKeyRoot, 'system-files', 'list', pageIndex, pageSize, keyword, sorts, tableQuery] as const,
    formats: () => [...queryKeyRoot, 'system-files', 'formats'] as const
  },
  abpInfrastructureSettings: {
    root: () => [...queryKeyRoot, 'abp-infrastructure-settings'] as const,
    detail: () => [...queryKeyRoot, 'abp-infrastructure-settings', 'detail'] as const,
    messageLogs: (params: unknown) => [...queryKeyRoot, 'abp-infrastructure-settings', 'message-logs', params] as const
  },
  systemManagement: {
    departmentsRoot: () => [...queryKeyRoot, 'system-management', 'departments'] as const,
    departmentTree: () => [...queryKeyRoot, 'system-management', 'department-tree'] as const,
    departments: (pageIndex: number, pageSize: number, keyword: string, status: string, parentId: string, includeDescendants = false, sorts: unknown = []) =>
      [...queryKeyRoot, 'system-management', 'departments', pageIndex, pageSize, keyword, status, parentId, includeDescendants, sorts] as const,
    menusRoot: () => [...queryKeyRoot, 'system-management', 'menus'] as const,
    menuTree: (tenantId = '', appCode = '') => [...queryKeyRoot, 'system-management', 'menu-tree', tenantId, appCode] as const,
    menus: (pageIndex: number, pageSize: number, keyword: string, parentId = '', menuType = '', status = '', includeDescendants = false, tenantId = '', appCode = '', sorts: unknown = []) =>
      [...queryKeyRoot, 'system-management', 'menus', pageIndex, pageSize, keyword, parentId, menuType, status, includeDescendants, tenantId, appCode, sorts] as const,
    permissionCatalog: () => [...queryKeyRoot, 'system-management', 'permission-catalog'] as const,
    permissionTree: (tenantId = '', appCode = '') => [...queryKeyRoot, 'system-management', 'permission-tree', tenantId, appCode] as const,
    positionsRoot: () => [...queryKeyRoot, 'system-management', 'positions'] as const,
    positions: (pageIndex: number, pageSize: number, keyword: string, status: string, deptId: string, sorts: unknown = []) =>
      [...queryKeyRoot, 'system-management', 'positions', pageIndex, pageSize, keyword, status, deptId, sorts] as const,
    rolesRoot: () => [...queryKeyRoot, 'system-management', 'roles'] as const,
    roles: (pageIndex: number, pageSize: number, keyword: string, status = '', tenantId = '', appCode = '', sorts: unknown = []) =>
      [...queryKeyRoot, 'system-management', 'roles', pageIndex, pageSize, keyword, status, tenantId, appCode, sorts] as const,
    usersRoot: () => [...queryKeyRoot, 'system-management', 'users'] as const,
    users: (pageIndex: number, pageSize: number, keyword: string, status = '', deptId = '', positionId = '', roleId = '', includeDescendants = false) =>
      [...queryKeyRoot, 'system-management', 'users', pageIndex, pageSize, keyword, status, deptId, positionId, roleId, includeDescendants] as const
  },
  platform: {
    tenantsRoot: () => [...queryKeyRoot, 'platform', 'tenants'] as const,
    applicationsRoot: () => [...queryKeyRoot, 'platform', 'applications'] as const,
    applicationPublishTask: (taskId: string) => [...queryKeyRoot, 'platform', 'application-publish-task', taskId] as const,
    applicationPublishTasks: (appId: string, pageIndex: number, pageSize: number) => [...queryKeyRoot, 'platform', 'applications', appId, 'publish-tasks', pageIndex, pageSize] as const,
    applicationPublishLogs: (taskId: string, pageIndex: number, pageSize: number) => [...queryKeyRoot, 'platform', 'application-publish-tasks', taskId, 'logs', pageIndex, pageSize] as const,
    applicationPublishArtifacts: (appId: string, pageIndex: number, pageSize: number) => [...queryKeyRoot, 'platform', 'applications', appId, 'publish-artifacts', pageIndex, pageSize] as const,
    tenantAppsRoot: () => [...queryKeyRoot, 'platform', 'tenant-apps'] as const,
    userTenantsRoot: () => [...queryKeyRoot, 'platform', 'user-tenants'] as const,
    userAppRolesRoot: () => [...queryKeyRoot, 'platform', 'user-app-roles'] as const
  },
  tenant: {
    appsCatalog: () => [...queryKeyRoot, 'tenant', 'apps', 'catalog'] as const,
    appsInstalled: () => [...queryKeyRoot, 'tenant', 'apps', 'installed'] as const
  },
  applicationConsole: {
    databaseBindingStatus: (tenantId = '', appCode = '') => [...queryKeyRoot, 'application-console', 'database-binding-status', tenantId, appCode] as const,
    summary: (tenantId = '', appCode = '') => [...queryKeyRoot, 'application-console', 'summary', tenantId, appCode] as const
  },
  applicationDataCenter: {
    workspace: (tenantId = '', appCode = '', moduleKey = 'all', dataSourceId = 'none') =>
      [...queryKeyRoot, 'application-data-center', 'workspace', tenantId, appCode, moduleKey, dataSourceId] as const,
    workspaceSwitcher: (tenantId = '', appCode = '') =>
      [...queryKeyRoot, 'application-data-center', 'workspace-switcher', tenantId, appCode, 'data-sources'] as const
  },
  projectManagement: projectManagementQueryKeys,
  runtime: {
    expressionFunctions: (scope = 'all') => [...queryKeyRoot, 'runtime', 'expression-functions', scope] as const,
    gridView: (pageCode: string, previewPageId = '') => [...queryKeyRoot, 'runtime', 'grid-view', pageCode, previewPageId] as const,
    modelRows: (modelCode: string, requestKey: string) => [...queryKeyRoot, 'runtime', 'model-rows', modelCode, requestKey] as const,
    modelRowsRoot: (modelCode: string) => [...queryKeyRoot, 'runtime', 'model-rows', modelCode] as const,
    pageSchema: (pageCode: string, previewPageId = '') => [...queryKeyRoot, 'runtime', 'page-schema', pageCode, previewPageId] as const,
    pageSchemaScoped: (tenantId: string, appCode: string, pageCode: string, previewPageId = '') =>
      [...queryKeyRoot, 'runtime', 'page-schema', tenantId, appCode, pageCode, previewPageId] as const
  },
  page: {
    byName: (pageName: string) => [...queryKeyRoot, 'page', pageName] as const
  }
};
