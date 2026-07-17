import type {
  ApplicationConsoleSummaryDto,
  ApplicationDatabaseBindingStatusDto
} from '../../api/application-console/applicationConsole.types';
import type { CurrentWorkspaceDto } from '../../api/platform/auth.types';

export function buildWorkspaceFallbackSummary(
  workspace: CurrentWorkspaceDto,
  databaseBinding: ApplicationDatabaseBindingStatusDto
): ApplicationConsoleSummaryDto {
  return {
    application: {
      appCode: workspace.appCode,
      appName: workspace.appName,
      appType: 'Business',
      createdTime: '1970-01-01T00:00:00.000Z',
      defaultRoutePath: workspace.defaultRoutePath ?? null,
      status: 'Enabled',
      systemName: workspace.systemName,
      tenantId: workspace.tenantId,
      tenantName: workspace.tenantName,
      updatedTime: null,
      version: null,
      workspaceLevel: 'application'
    },
    capabilityCounts: {
      dataModelCount: 0,
      menuCount: 0,
      pageCount: 0,
      permissionCount: 0,
      publishedPageCount: 0,
      publishTaskCount: 0,
      rootMenuCount: 0,
      workflowModelCount: 0
    },
    databaseBinding,
    developmentShortcuts: [],
    draftSignals: {
      hasPendingPublishRisk: false,
      items: [],
      totalRiskCount: 0
    },
    entryTree: [],
    metrics: [],
    recentAudits: [],
    recentDevelopmentItems: [],
    recentPublishes: [],
    versionContext: {
      draftVersionCount: 0,
      latestDraftVersion: null,
      latestPublishedVersion: null,
      latestPublishTime: null,
      publishedVersionCount: 0,
      summary: ''
    }
  };
}
