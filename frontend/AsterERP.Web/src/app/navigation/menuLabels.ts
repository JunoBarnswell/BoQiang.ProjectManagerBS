import { appRoutes } from '@/app/navigation/routes';

import type { MenuTreeNodeDto } from '../../api/system/system.types';


type Translate = (key: string) => string;

const menuCodeLabelKeys: Record<string, string> = {
  flowise: 'nav.flowiseStudio',
  'flowise:evaluations-group': 'nav.flowiseEvaluationsGroup',
  'flowise:management-group': 'nav.flowiseManagementGroup',
  'flowise:others-group': 'nav.flowiseOthersGroup',
  'app-console': 'nav.applicationConsole',
  'app-center': 'nav.applicationCenter',
  'dev-center': 'nav.developmentCenter',
  'data-center': 'nav.dataCenter',
  workflow: 'nav.workflowRoot',
  'workflow:workspace': 'nav.workflowWorkspaceGroup',
  'workflow:management': 'nav.workflowManagementGroup',
  'workflow:analytics': 'nav.workflowAnalyticsGroup',
  'workflow:settings': 'nav.workflowSettingsGroup',
  system: 'sidebar.system'
};

const flowiseRouteLabelKeys: Record<string, string> = {
  '/flowise/account': 'nav.flowiseAccount',
  '/flowise/api-keys': 'nav.flowiseApiKeys',
  '/flowise/apikey': 'nav.flowiseApiKeys',
  '/flowise/assistants': 'nav.flowiseAssistants',
  '/flowise/chatflows': 'nav.flowiseChatflows',
  '/flowise/credentials': 'nav.flowiseCredentials',
  '/flowise/datasets': 'nav.flowiseDatasets',
  '/flowise/dataset_rows': 'nav.flowiseDatasets',
  '/flowise/document-stores': 'nav.flowiseDocumentStores',
  '/flowise/evaluations': 'nav.flowiseEvaluations',
  '/flowise/evaluation-results': 'nav.flowiseEvaluations',
  '/flowise/evaluation_results': 'nav.flowiseEvaluations',
  '/flowise/evaluators': 'nav.flowiseEvaluators',
  '/flowise/executions': 'nav.flowiseExecutions',
  '/flowise/login-activity': 'nav.flowiseLoginActivity',
  '/flowise/logs': 'nav.flowiseLogs',
  '/flowise/marketplaces': 'nav.flowiseMarketplaces',
  '/flowise/sso-config': 'nav.flowiseSsoConfig',
  '/flowise/tools': 'nav.flowiseTools',
  '/flowise/variables': 'nav.flowiseVariables',
  '/flowise/workflows': 'nav.flowiseWorkflows'
};

const workflowRouteLabelKeys: Record<string, string> = {
  '/system/roles?from=workflow': 'nav.workflowSettingsRoles',
  '/system/users?from=workflow': 'nav.workflowSettingsOrg',
  '/workflows/bindings': 'nav.workflowBindings',
  '/workflows/categories': 'nav.workflowCategories',
  '/workflows/calendars': 'nav.workflowCalendars',
  '/workflows/delegations': 'nav.workflowDelegations',
  '/workflows/drafts': 'nav.workflowDrafts',
  '/workflows/forms': 'nav.workflowForms',
  '/workflows/history': 'nav.workflowHistory',
  '/workflows/initiate': 'nav.workflowInitiate',
  '/workflows/instances/:processInstanceId': 'nav.workflowInstance',
  '/workflows/monitoring': 'nav.workflowMonitoring',
  '/workflows/models': 'nav.workflowModels',
  '/workflows/notifications': 'nav.workflowNotifications',
  '/workflows/reports?tab=approval': 'nav.workflowReportApproval',
  '/workflows/reports?tab=business': 'nav.workflowReportBusiness',
  '/workflows/reports?tab=efficiency': 'nav.workflowReportEfficiency',
  '/workflows/tasks?tab=cc': 'nav.workflowTaskCc',
  '/workflows/tasks?tab=done': 'nav.workflowTaskDone',
  '/workflows/tasks?tab=mine': 'nav.workflowTaskMine',
  '/workflows/tasks?tab=todo': 'nav.workflowTaskTodo'
};

function normalizeRoutePath(routePath: string): string {
  return routePath.split(/[?#]/)[0] ?? routePath;
}

export function getMenuLabelKey(menu: Pick<MenuTreeNodeDto, 'menuCode' | 'routePath'>): string | null {
  const routePath = menu.routePath?.trim();
  if (routePath) {
    const workflowLabelKey = workflowRouteLabelKeys[routePath];
    if (workflowLabelKey) {
      return workflowLabelKey;
    }

    const flowiseLabelKey = Object.entries(flowiseRouteLabelKeys).find(([routePrefix]) => routePath === routePrefix || routePath.startsWith(`${routePrefix}/`))?.[1];
    if (flowiseLabelKey) {
      return flowiseLabelKey;
    }

    const normalizedRoutePath = normalizeRoutePath(routePath);
    const route = appRoutes.find((item) => item.path === routePath || item.path === normalizedRoutePath || item.path === routePath.replace(/\?.*$/, ''));
    if (route) {
      return route.labelKey;
    }
  }

  return menuCodeLabelKeys[menu.menuCode] ?? null;
}

export function resolveMenuLabel(menu: Pick<MenuTreeNodeDto, 'menuCode' | 'menuName' | 'routePath'>, translate: Translate): string {
  const labelKey = getMenuLabelKey(menu);
  return labelKey ? translate(labelKey) : menu.menuName;
}
