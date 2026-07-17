export interface ApplicationConsoleApplicationDto {
  tenantId: string;
  tenantName: string;
  appCode: string;
  appName: string;
  systemName: string;
  version?: string | null;
  defaultRoutePath?: string | null;
  status: string;
  appType: string;
  createdTime: string;
  updatedTime?: string | null;
  workspaceLevel: 'application';
}

export interface ApplicationConsoleMetricDto {
  code: string;
  name: string;
  value: string;
  unit?: string | null;
  status: 'empty' | 'ok' | string;
}

export interface ApplicationConsoleCapabilityCountsDto {
  rootMenuCount: number;
  menuCount: number;
  permissionCount: number;
  pageCount: number;
  publishedPageCount: number;
  dataModelCount: number;
  workflowModelCount: number;
  publishTaskCount: number;
}

export interface ApplicationDatabaseBindingStatusDto {
  status?: ApplicationDatabaseBindingStatus;
  isBound: boolean;
  isReachable: boolean;
  provider?: ApplicationDatabaseProvider | null;
  displayName?: string | null;
  databaseName?: string | null;
  updatedAt?: string | null;
  canManage: boolean;
  message?: string | null;
}

export type ApplicationDatabaseBindingStatus =
  | 'NotConfigured'
  | 'Ready'
  | 'InvalidConfiguration'
  | 'MigrationRequired'
  | 'PermissionDenied'
  | 'Unavailable'
  | string;

export interface ApplicationDatabaseBindingRequest {
  provider: ApplicationDatabaseProvider;
  connectionString?: string | null;
  databaseName?: string | null;
  displayName?: string | null;
}

export type ApplicationDatabaseBindingResponseDto = ApplicationDatabaseBindingStatusDto;

export type ApplicationDatabaseProvider = 'Sqlite' | 'MySql' | 'PostgreSQL' | 'SqlServer';

export interface ApplicationConsoleRecentItemDto {
  id: string;
  title: string;
  description?: string | null;
  createdTime: string;
  status?: string | null;
}

export interface ApplicationConsoleEntryTreeItemDto {
  key: string;
  title: string;
  description: string;
  icon: string;
  routePath: string;
  permissionCode: string;
  visitKind: string;
  accent: string;
  count?: number | null;
  countLabel?: string | null;
  recentTargetTitle?: string | null;
}

export interface ApplicationConsoleEntryTreeGroupDto {
  key: string;
  title: string;
  description: string;
  icon: string;
  items: ApplicationConsoleEntryTreeItemDto[];
}

export interface ApplicationConsoleDevelopmentShortcutDto {
  key: string;
  title: string;
  description: string;
  icon: string;
  routePath: string;
  permissionCode: string;
  visitKind: string;
  actionText: string;
  accent: string;
  count?: number | null;
  countLabel?: string | null;
  recentTargetTitle?: string | null;
}

export interface ApplicationConsoleRecentDevelopmentItemDto {
  id: string;
  pageId: string;
  title: string;
  pageCode: string;
  status: string;
  description: string;
  moduleName?: string | null;
  moduleCode?: string | null;
  versionId?: string | null;
  versionName?: string | null;
  versionCode?: string | null;
  continueRoutePath: string;
  previewRoutePath?: string | null;
  canContinueDesign: boolean;
  canPreview: boolean;
  canPublish: boolean;
  updatedTime: string;
  visitKind: string;
}

export interface ApplicationConsoleVersionSnapshotDto {
  id: string;
  versionName: string;
  versionCode: string;
  status: string;
  updatedTime: string;
}

export interface ApplicationConsoleVersionContextDto {
  draftVersionCount: number;
  publishedVersionCount: number;
  latestDraftVersion?: ApplicationConsoleVersionSnapshotDto | null;
  latestPublishedVersion?: ApplicationConsoleVersionSnapshotDto | null;
  latestPublishTime?: string | null;
  summary: string;
}

export interface ApplicationConsoleDraftSignalDto {
  code: string;
  title: string;
  detail: string;
  severity: string;
  count: number;
}

export interface ApplicationConsoleDraftSignalsDto {
  totalRiskCount: number;
  hasPendingPublishRisk: boolean;
  items: ApplicationConsoleDraftSignalDto[];
}

export interface ApplicationConsoleSummaryDto {
  application: ApplicationConsoleApplicationDto;
  databaseBinding: ApplicationDatabaseBindingStatusDto;
  metrics: ApplicationConsoleMetricDto[];
  capabilityCounts: ApplicationConsoleCapabilityCountsDto;
  recentPublishes: ApplicationConsoleRecentItemDto[];
  recentAudits: ApplicationConsoleRecentItemDto[];
  entryTree: ApplicationConsoleEntryTreeGroupDto[];
  developmentShortcuts: ApplicationConsoleDevelopmentShortcutDto[];
  recentDevelopmentItems: ApplicationConsoleRecentDevelopmentItemDto[];
  versionContext: ApplicationConsoleVersionContextDto;
  draftSignals: ApplicationConsoleDraftSignalsDto;
}
