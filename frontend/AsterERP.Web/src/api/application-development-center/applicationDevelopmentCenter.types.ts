export interface ApplicationDevelopmentOverview {
  draftPageCount: number;
  draftVersionCount: number;
  previewMenuCount: number;
  publishedPageCount: number;
  publishedVersionCount: number;
  totalModuleCount: number;
}

export interface ApplicationDevelopmentAppConfig {
  defaultDataSourceId?: string | null;
  description?: string | null;
  logoIcon?: string | null;
  primaryColor?: string | null;
  sqlProtectionEnabled: boolean;
  systemFullName?: string | null;
  systemShortName?: string | null;
}

export interface ApplicationDevelopmentAppConfigRequest {
  defaultDataSourceId?: string | null;
  description?: string | null;
  logoIcon?: string | null;
  primaryColor?: string | null;
  sqlProtectionEnabled: boolean;
  systemFullName?: string | null;
  systemShortName?: string | null;
}

export interface ApplicationDevelopmentVersion {
  createdTime: string;
  defaultPageId?: string | null;
  id: string;
  sourceDataSourceId?: string | null;
  status: string;
  updatedTime?: string | null;
  versionCode: string;
  versionName: string;
}

export interface ApplicationDevelopmentVersionUpsertRequest {
  defaultPageId?: string | null;
  remark?: string | null;
  sourceDataSourceId?: string | null;
  status: string;
  versionCode: string;
  versionName: string;
}

export interface ApplicationDevelopmentModuleTreeNode {
  children: ApplicationDevelopmentModuleTreeNode[];
  id: string;
  moduleCode: string;
  moduleName: string;
  pageCount: number;
  parentModuleId?: string | null;
  sortOrder: number;
  versionId: string;
}

export interface ApplicationDevelopmentModuleUpsertRequest {
  moduleCode: string;
  moduleName: string;
  parentModuleId?: string | null;
  remark?: string | null;
  sortOrder: number;
  versionId: string;
}

export interface ApplicationDevelopmentPageListItem {
  createdTime: string;
  id: string;
  moduleId?: string | null;
  pageCode: string;
  pageName: string;
  parentPageId?: string | null;
  pageParameters?: ApplicationDevelopmentPageParameter[];
  pageParametersJson?: string | null;
  pageType: ApplicationDevelopmentPageType;
  previewMenuCode?: string | null;
  previewRoutePath?: string | null;
  publishedMenuCode?: string | null;
  publishedArtifactId?: string | null;
  publishedRoutePath?: string | null;
  sortOrder: number;
  status: string;
  templateCode: string;
  updatedTime?: string | null;
  versionId: string;
}

export type ApplicationDevelopmentPageType = 'dialog' | 'drawer' | 'standard';
export type ApplicationDevelopmentPageParameterDirection = 'input' | 'output';

export interface ApplicationDevelopmentPageParameter {
  code: string;
  defaultValue?: unknown;
  direction: ApplicationDevelopmentPageParameterDirection;
  name: string;
  required?: boolean;
  valueType: 'array' | 'boolean' | 'date' | 'json' | 'number' | 'object' | 'string';
}

export interface ApplicationDevelopmentPageDetail extends ApplicationDevelopmentPageListItem {
  designerMode: string;
  documentJson: string;
  permissionConfigJson: string;
  publishedMenuId?: string | null;
  publishedArtifactId?: string | null;
  publishedArtifactJson?: string | null;
  publishedSchemaUpdatedTime?: string | null;
  publishedArtifactHash?: string | null;
  publishedArtifactRevision?: number | null;
  publishedManifestHash?: string | null;
}

export interface ApplicationDevelopmentPageCreateRequest {
  moduleId?: string | null;
  pageCode?: string | null;
  pageName: string;
  parentPageId?: string | null;
  pageParameters?: ApplicationDevelopmentPageParameter[];
  pageType: ApplicationDevelopmentPageType;
  sortOrder: number;
  versionId: string;
}

export interface ApplicationDevelopmentPageUpsertRequest {
  designerMode: string;
  expectedUpdatedTime?: string | null;
  documentJson: string;
  moduleId?: string | null;
  pageCode: string;
  pageName: string;
  parentPageId?: string | null;
  pageParameters?: ApplicationDevelopmentPageParameter[];
  pageType: ApplicationDevelopmentPageType;
  permissionConfigJson: string;
  remark?: string | null;
  sortOrder: number;
  templateCode: string;
  versionId: string;
}

export interface ApplicationDevelopmentPreviewSchemaResponse {
  pageCode: string;
  pageName: string;
  artifactJson: string;
}

export interface ApplicationDevelopmentPreviewArtifactRequest {
  documentJson: string;
}

export interface ApplicationDevelopmentEnvironmentCheckRequest {
  documentJson: string;
}

export interface ApplicationDevelopmentEnvironmentDiagnostic {
  category: 'binding' | 'database' | 'microflow' | 'page' | string;
  code: string;
  dataSourceId?: string | null;
  fixHint?: string | null;
  flowCode?: string | null;
  message: string;
  path?: string | null;
  severity: 'error' | 'warning';
  tableName?: string | null;
}

export interface ApplicationDevelopmentEnvironmentCheckResponse {
  diagnostics: ApplicationDevelopmentEnvironmentDiagnostic[];
  passed: boolean;
}

export interface ApplicationDevelopmentSharedResourceListItem {
  createdTime: string;
  id: string;
  resourceCode: string;
  resourceName: string;
  resourceType: string;
  status: string;
  updatedTime?: string | null;
  versionId?: string | null;
}

export interface ApplicationDevelopmentSharedResourceDetail extends ApplicationDevelopmentSharedResourceListItem {
  contentJson: string;
  contentText?: string | null;
}

export interface ApplicationDevelopmentSharedResourceUpsertRequest {
  contentJson: string;
  contentText?: string | null;
  resourceCode: string;
  resourceName: string;
  resourceType: string;
  status: string;
  versionId?: string | null;
}

export interface ApplicationDevelopmentMenuOption {
  menuCode: string;
  menuName: string;
}

export interface ApplicationDevelopmentRoleOption {
  roleCode: string;
  roleId: string;
  roleName: string;
}

export interface ApplicationDevelopmentPermissionOptions {
  menuOptions: ApplicationDevelopmentMenuOption[];
  roleOptions: ApplicationDevelopmentRoleOption[];
}

export const APPLICATION_DEVELOPMENT_DRAFT_CONFLICT_CODE = 42065;

export function isApplicationDevelopmentDraftConflictCode(code?: number, status?: number): boolean {
  return code === APPLICATION_DEVELOPMENT_DRAFT_CONFLICT_CODE || code === 42301 || status === 409;
}

export interface ApplicationDevelopmentPublishResponse {
  diagnostics?: ApplicationDevelopmentPublishDiagnostic[];
  generatedPermissionCodes: string[];
  publishedMenuCode?: string | null;
  publishedMenuId?: string | null;
  publishedArtifactId?: string | null;
  publishedArtifactHash?: string | null;
  publishedArtifactRevision?: number | null;
  publishedManifestHash?: string | null;
  publishedRoutePath?: string | null;
  publishedSchemaUpdatedTime?: string | null;
  versionId: string;
}

export interface ApplicationDevelopmentPublishDiagnostic {
  actionId?: string | null;
  code: string;
  elementId?: string | null;
  fixHint?: string | null;
  message: string;
  pageCode?: string | null;
  pageId?: string | null;
  path?: string | null;
  severity: 'error' | 'warning';
}

export interface ApplicationDevelopmentArtifactRollbackRequest {
  artifactHash: string;
  artifactId: string;
  operationId: string;
  reason: string;
}

export interface ApplicationDevelopmentArtifactRollbackResponse {
  artifactHash: string;
  artifactId: string;
  auditId: string;
  documentId: string;
  pageId: string;
  previousArtifactId: string;
  publishedArtifactId: string;
  status: string;
}

export interface ApplicationDevelopmentWorkspace {
  overview: ApplicationDevelopmentOverview;
  selectedVersionId?: string | null;
  selectedVersion?: ApplicationDevelopmentVersion | null;
  versions: ApplicationDevelopmentVersion[];
  modules: ApplicationDevelopmentModuleTreeNode[];
  pages: ApplicationDevelopmentPageListItem[];
  sharedResources: ApplicationDevelopmentSharedResourceListItem[];
  recentPages: ApplicationDevelopmentPageListItem[];
}
