export interface RuntimePageSchemaDto {
  id: string;
  tenantId: string;
  appCode: string;
  pageCode: string;
  pageName: string;
  pageType: string;
  modelCode?: string | null;
  permissionCode?: string | null;
  versionNo: number;
  artifactJson: string;
}

export interface RuntimeModelQueryRequest {
  pageIndex: number;
  pageSize: number;
  keyword?: string | null;
  filters?: Array<{ field: string; operator: string; value?: unknown; valueTo?: unknown }>;
  sorts?: Array<{ field: string; order: string }>;
  pageCode?: string | null;
  previewPageId?: string | null;
}

export interface RuntimeModelFieldDto {
  fieldCode: string;
  fieldName: string;
  dataType: string;
  binding: string;
  visible: boolean;
  queryable: boolean;
  sortable: boolean;
  exportable: boolean;
  writable: boolean;
  renderer?: string | null;
  dictType?: string | null;
  width?: string | null;
  fixed?: string | null;
  order: number;
  required?: boolean;
}

export interface RuntimeModelQueryResponse {
  fields: RuntimeModelFieldDto[];
  rows: Array<Record<string, unknown>>;
  total: number;
  pageIndex: number;
  pageSize: number;
  keyField?: string | null;
}

/**
 * The only runtime page envelope accepted by the latest runtime path.
 *
 * The API still transports the envelope as JSON text for compatibility with
 * the existing endpoint, but the parsed value is an artifact envelope rather
 * than an editor-only schema model.
 */
export interface RuntimePageArtifactEnvelope {
  artifactHash: string;
  compilerVersion: string;
  document: Record<string, unknown>;
  id: string;
  manifestTypes: string[];
  revision: number;
  signature: string;
}

export interface RuntimeExpressionHelperDto {
  args?: Record<string, unknown>;
  name: string;
}

export interface RuntimeVariableExpressionDto {
  expectedType?: 'array' | 'boolean' | 'date' | 'number' | 'object' | 'string' | string | null;
  fallback?: unknown;
  helpers?: RuntimeExpressionHelperDto[];
  modelCode?: string | null;
  path?: string | null;
  source: 'api' | 'component' | 'constant' | 'currentRow' | 'form' | 'microflow' | 'page' | 'system' | 'tableRow' | 'variables' | 'workflow' | string;
  value?: unknown;
}
