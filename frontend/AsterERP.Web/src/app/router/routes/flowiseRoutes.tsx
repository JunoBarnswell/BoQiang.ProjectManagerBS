import { lazy, type ReactNode } from 'react';
import { Navigate, type RouteObject } from 'react-router-dom';

import { routeMeta } from '../../navigation/routeMeta';
import { PermissionRoute } from '../PermissionRoute';

import { lazyPage, ParamRedirect } from './routeElements';

import '../../../features/flowise-studio/styles/flowise-studio.css';
import '../../../features/flowise-studio/styles/flowise-pages.css';
import '../../../features/flowise-studio/styles/flowise-canvas.css';
import '../../../features/flowise-studio/styles/flowise-dialogs.css';

const FlowiseChatflowsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseChatflowsPage })));
const FlowiseWorkflowsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseWorkflowsPage })));
const FlowiseExecutionsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseExecutionsPage').then((module) => ({ default: module.FlowiseExecutionsPage })));
const FlowiseAssistantsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseAssistantsPage })));
const FlowiseAssistantDetailPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseAssistantDetailPage').then((module) => ({ default: module.FlowiseAssistantDetailPage })));
const FlowiseMarketplacesPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseMarketplacesPage })));
const FlowiseToolsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseToolsPage })));
const FlowiseCredentialsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseCredentialsPage })));
const FlowiseVariablesPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseVariablesPage })));
const FlowiseApiKeysPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseApiKeysPage })));
const FlowiseDocumentStoresPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseDocumentStoresPage })));
const FlowiseDocumentStoreDetailPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseDocumentStoreDetailPage').then((module) => ({ default: module.FlowiseDocumentStoreDetailPage })));
const FlowiseDatasetsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseDatasetsPage })));
const FlowiseDatasetRowsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseDatasetRowsPage').then((module) => ({ default: module.FlowiseDatasetRowsPage })));
const FlowiseEvaluatorsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseEvaluatorsPage })));
const FlowiseEvaluationsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseEvaluationsPage })));
const FlowiseEvaluationResultPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseEvaluationResultPage').then((module) => ({ default: module.FlowiseEvaluationResultPage })));
const FlowiseSsoConfigPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseSsoConfigPage })));
const FlowiseLoginActivityPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseLoginActivityPage })));
const FlowiseLogsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseMenuPages').then((module) => ({ default: module.FlowiseLogsPage })));
const FlowiseAccountSettingsPage = lazy(() => import('../../../features/flowise-studio/pages/FlowiseAccountSettingsPage').then((module) => ({ default: module.FlowiseAccountSettingsPage })));
const FlowiseCanvasPage = lazy(() => import('../../../features/flowise-studio/canvas/FlowiseCanvasPage').then((module) => ({ default: module.FlowiseCanvasPage })));
const FlowiseAgentflowV2CanvasPage = lazy(() => import('../../../features/flowise-studio/canvas/FlowiseAgentflowV2CanvasPage').then((module) => ({ default: module.FlowiseAgentflowV2CanvasPage })));
const FlowiseMarketplaceTemplateCanvasPage = lazy(() => import('../../../features/flowise-studio/native/views/agentflows/MarketplaceCanvas').then((module) => ({ default: module.MarketplaceCanvasTemplate })));

function resolveFlowiseLabelKey(path: string): string {
  if (path.includes('agentcanvas') || path.includes('workflows') || path.includes('agentflows')) return 'nav.flowiseWorkflows';
  if (path.includes('canvas') || path.includes('chatflows')) return 'nav.flowiseChatflows';
  if (path.includes('executions')) return 'nav.flowiseExecutions';
  if (path.includes('assistants')) return 'nav.flowiseAssistants';
  if (path.includes('marketplace')) return 'nav.flowiseMarketplaces';
  if (path.includes('tools')) return 'nav.flowiseTools';
  if (path.includes('credentials')) return 'nav.flowiseCredentials';
  if (path.includes('variables')) return 'nav.flowiseVariables';
  if (path.includes('api-keys') || path.includes('apikey')) return 'nav.flowiseApiKeys';
  if (path.includes('document-stores')) return 'nav.flowiseDocumentStores';
  if (path.includes('datasets') || path.includes('dataset_rows')) return 'nav.flowiseDatasets';
  if (path.includes('evaluators')) return 'nav.flowiseEvaluators';
  if (path.includes('evaluations') || path.includes('evaluation_') || path.includes('evaluation-')) return 'nav.flowiseEvaluations';
  if (path.includes('sso')) return 'nav.flowiseSsoConfig';
  if (path.includes('login-activity')) return 'nav.flowiseLoginActivity';
  if (path.includes('logs')) return 'nav.flowiseLogs';
  if (path.includes('account')) return 'nav.flowiseAccount';
  return 'nav.flowiseStudio';
}

function flowisePage(path: string, permissionCode: string, children: ReactNode, tabMode: 'detail' | 'menu' = 'menu'): RouteObject {
  return {
    path,
    handle: routeMeta({
      breadcrumbKey: 'breadcrumbs.flowiseStudio',
      cachePolicy: tabMode === 'detail' ? 'none' : 'tab-alive',
      iconKey: 'module',
      labelKey: resolveFlowiseLabelKey(path),
      layoutVariant: 'app',
      path: `/${path}`,
      tabMode
    }),
    element: (
      <PermissionRoute permissionCode={permissionCode}>
        {lazyPage(children)}
      </PermissionRoute>
    )
  };
}

export const flowiseRoutes: RouteObject[] = [
  flowisePage('flowise/chatflows', 'flowise:chatflows:view', <FlowiseChatflowsPage />),
  flowisePage('flowise/workflows', 'flowise:agentflows:view', <FlowiseWorkflowsPage />),
  flowisePage('flowise/workflows/:resourceId', 'flowise:agentflows:view', <FlowiseCanvasPage />, 'detail'),
  flowisePage('flowise/executions', 'flowise:executions:view', <FlowiseExecutionsPage />),
  flowisePage('flowise/assistants', 'flowise:assistants:view', <FlowiseAssistantsPage />),
  flowisePage('flowise/assistants/custom', 'flowise:assistants:view', <FlowiseAssistantsPage />),
  flowisePage('flowise/assistants/custom/:id', 'flowise:assistants:view', <FlowiseAssistantDetailPage />, 'detail'),
  flowisePage('flowise/assistants/openai', 'flowise:assistants:view', <FlowiseAssistantsPage />),
  flowisePage('flowise/marketplaces', 'flowise:marketplaces:view', <FlowiseMarketplacesPage />),
  flowisePage('flowise/marketplace/:resourceId', 'flowise:marketplaces:view', <FlowiseCanvasPage />, 'detail'),
  flowisePage('flowise/v2/marketplace/:resourceId', 'flowise:marketplaces:view', <FlowiseMarketplaceTemplateCanvasPage />, 'detail'),
  flowisePage('flowise/tools', 'flowise:tools:view', <FlowiseToolsPage />),
  flowisePage('flowise/credentials', 'flowise:credentials:view', <FlowiseCredentialsPage />),
  flowisePage('flowise/variables', 'flowise:variables:view', <FlowiseVariablesPage />),
  flowisePage('flowise/api-keys', 'flowise:api-keys:view', <FlowiseApiKeysPage />),
  flowisePage('flowise/apikey', 'flowise:api-keys:view', <FlowiseApiKeysPage />),
  flowisePage('flowise/document-stores', 'flowise:document-stores:view', <FlowiseDocumentStoresPage />),
  flowisePage('flowise/document-stores/:storeId', 'flowise:document-stores:view', <FlowiseDocumentStoreDetailPage />, 'detail'),
  flowisePage('flowise/document-stores/:storeId/:name', 'flowise:document-stores:view', <FlowiseDocumentStoreDetailPage />, 'detail'),
  flowisePage('flowise/document-stores/chunks/:storeId/:fileId', 'flowise:document-stores:view', <FlowiseDocumentStoreDetailPage />, 'detail'),
  flowisePage('flowise/document-stores/vector/:storeId', 'flowise:document-stores:view', <FlowiseDocumentStoreDetailPage />, 'detail'),
  flowisePage('flowise/document-stores/vector/:storeId/:docId', 'flowise:document-stores:view', <FlowiseDocumentStoreDetailPage />, 'detail'),
  flowisePage('flowise/document-stores/query/:storeId', 'flowise:document-stores:view', <FlowiseDocumentStoreDetailPage />, 'detail'),
  flowisePage('flowise/datasets', 'flowise:datasets:view', <FlowiseDatasetsPage />),
  flowisePage('flowise/dataset_rows/:id', 'flowise:datasets:view', <FlowiseDatasetRowsPage />, 'detail'),
  flowisePage('flowise/evaluators', 'flowise:evaluators:view', <FlowiseEvaluatorsPage />),
  flowisePage('flowise/evaluations', 'flowise:evaluations:view', <FlowiseEvaluationsPage />),
  flowisePage('flowise/evaluation_results/:id', 'flowise:evaluations:view', <FlowiseEvaluationResultPage />, 'detail'),
  flowisePage('flowise/evaluation-results/:id', 'flowise:evaluations:view', <FlowiseEvaluationResultPage />, 'detail'),
  flowisePage('flowise/sso-config', 'flowise:sso:manage', <FlowiseSsoConfigPage />),
  flowisePage('flowise/sso-success', 'flowise:sso:manage', <FlowiseSsoConfigPage />, 'detail'),
  flowisePage('flowise/login-activity', 'flowise:login-activity:view', <FlowiseLoginActivityPage />),
  flowisePage('flowise/logs', 'flowise:logs:view', <FlowiseLogsPage />),
  flowisePage('flowise/account', 'flowise:account:view', <FlowiseAccountSettingsPage />),
  flowisePage('flowise/canvas', 'flowise:chatflows:view', <FlowiseCanvasPage />, 'detail'),
  flowisePage('flowise/canvas/:resourceId', 'flowise:chatflows:view', <FlowiseCanvasPage />, 'detail'),
  { path: 'flowise/agentcanvas', element: <Navigate replace to="/flowise/workflows" /> },
  { path: 'flowise/agentcanvas/:resourceId', element: <ParamRedirect to="/flowise/workflows" /> },
  flowisePage('flowise/v2/agentcanvas', 'flowise:agentflows:view', <FlowiseAgentflowV2CanvasPage />, 'detail'),
  flowisePage('flowise/v2/agentcanvas/:resourceId', 'flowise:agentflows:view', <FlowiseAgentflowV2CanvasPage />, 'detail'),
  { path: 'chatflows', element: <Navigate replace to="/flowise/chatflows" /> },
  { path: 'flowise/agentflows', element: <Navigate replace to="/flowise/workflows" /> },
  { path: 'agentflows', element: <Navigate replace to="/flowise/workflows" /> },
  { path: 'workflows', element: <Navigate replace to="/flowise/workflows" /> },
  { path: 'executions', element: <Navigate replace to="/flowise/executions" /> },
  { path: 'assistants', element: <Navigate replace to="/flowise/assistants" /> },
  { path: 'marketplaces', element: <Navigate replace to="/flowise/marketplaces" /> },
  { path: 'tools', element: <Navigate replace to="/flowise/tools" /> },
  { path: 'credentials', element: <Navigate replace to="/flowise/credentials" /> },
  { path: 'variables', element: <Navigate replace to="/flowise/variables" /> },
  { path: 'apikey', element: <Navigate replace to="/flowise/api-keys" /> },
  { path: 'document-stores', element: <Navigate replace to="/flowise/document-stores" /> },
  { path: 'datasets', element: <Navigate replace to="/flowise/datasets" /> },
  { path: 'evaluators', element: <Navigate replace to="/flowise/evaluators" /> },
  { path: 'evaluations', element: <Navigate replace to="/flowise/evaluations" /> },
  { path: 'sso-config', element: <Navigate replace to="/flowise/sso-config" /> },
  { path: 'login-activity', element: <Navigate replace to="/flowise/login-activity" /> },
  { path: 'logs', element: <Navigate replace to="/flowise/logs" /> },
  { path: 'account', element: <Navigate replace to="/flowise/account" /> },
  { path: 'canvas/:resourceId', element: <ParamRedirect to="/flowise/canvas" /> },
  { path: 'agentcanvas/:resourceId', element: <ParamRedirect to="/flowise/agentcanvas" /> }
];
