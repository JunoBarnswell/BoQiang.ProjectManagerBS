import type { QueryViewQueryCondition, QueryViewQuerySort } from '../../api/runtime/query-views.api';

export type PrintScene = 'detail' | 'list';
export type PrintMode = 'allFiltered' | 'currentPage' | 'selected';

export interface PrintVariableNodeDto {
  children: PrintVariableNodeDto[];
  id: string;
  isArray: boolean;
  label: string;
}

export interface PrintTargetOptionDto {
  defaultTitle: string;
  menuCode: string;
  menuName: string;
  routePath?: string | null;
  supportedScenes: PrintScene[];
  supportsAssets: boolean;
}

export interface PrintTargetDetailDto extends PrintTargetOptionDto {
  activeScene: PrintScene;
  availableVariables: PrintVariableNodeDto[];
  detailProviderKey?: string | null;
  listViewCode?: string | null;
  testData?: Record<string, unknown> | null;
}

export interface PrintTemplateListItemDto {
  ext?: Record<string, unknown> | null;
  id: string;
  isDefault: boolean;
  menuCode: string;
  menuName: string;
  name: string;
  permissions?: Record<string, unknown> | null;
  remark?: string | null;
  routePath?: string | null;
  scene: PrintScene;
  status: string;
  templateCode: string;
  updatedAt: number;
}

export interface PrintTemplateDetailDto extends PrintTemplateListItemDto {
  data?: Record<string, unknown> | null;
}

export interface PrintCustomElementListItemDto {
  ext?: Record<string, unknown> | null;
  id: string;
  name: string;
  permissions?: Record<string, unknown> | null;
  updatedAt: number;
}

export interface PrintCustomElementDetailDto extends PrintCustomElementListItemDto {
  element?: Record<string, unknown> | null;
}

export interface PrintTemplateUpsertRequest {
  data?: Record<string, unknown> | null;
  ext?: Record<string, unknown> | null;
  id?: string | null;
  menuCode?: string | null;
  name: string;
  permissions?: Record<string, unknown> | null;
  remark?: string | null;
  scene?: PrintScene | null;
  templateCode?: string | null;
  updatedAt?: number | null;
}

export interface PrintCustomElementUpsertRequest {
  element?: Record<string, unknown> | null;
  ext?: Record<string, unknown> | null;
  id?: string | null;
  name: string;
  permissions?: Record<string, unknown> | null;
  updatedAt?: number | null;
}

export interface PrintRuntimeResolveRequest {
  conditions: QueryViewQueryCondition[];
  detailId?: string | null;
  menuCode: string;
  mode?: PrintMode | null;
  pageIndex: number;
  pageSize: number;
  scene: PrintScene;
  selectedIds: string[];
  sorts: QueryViewQuerySort[];
  templateId?: string | null;
}

export interface PrintRuntimeResolveResponse {
  availableVariables: PrintVariableNodeDto[];
  data?: Record<string, unknown> | null;
  scene: PrintScene;
  suggestedFileName: string;
  supportsAssets: boolean;
  templateCode: string;
  templateId: string;
  templateName: string;
  testData?: Record<string, unknown> | null;
  variables?: Record<string, unknown> | null;
}

export interface PrintLaunchRequest {
  conditions: QueryViewQueryCondition[];
  detailId?: string | null;
  menuCode: string;
  pageIndex: number;
  pageSize: number;
  scene: PrintScene;
  selectedIds: string[];
  sorts: QueryViewQuerySort[];
}

export interface PrintDesignerCrudContext {
  menuCode: string;
  scene: PrintScene;
}
