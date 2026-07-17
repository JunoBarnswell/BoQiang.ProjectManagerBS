import { httpClient } from '../../core/http/httpClient';

export type QueryViewConditionOperator =
  | 'contains'
  | 'eq'
  | 'ge'
  | 'gt'
  | 'in'
  | 'le'
  | 'lt'
  | 'ne';

export interface QueryViewQueryCondition {
  field: string;
  operator: QueryViewConditionOperator | string;
  value?: boolean | number | string | null;
  valueTo?: boolean | number | string | null;
}

export interface QueryViewQuerySort {
  direction: 'asc' | 'desc';
  field: string;
}

export interface QueryViewQueryRequest {
  conditions?: QueryViewQueryCondition[];
  pageIndex?: number;
  pageSize?: number;
  selectedIds?: string[];
  sorts?: QueryViewQuerySort[];
}

export interface QueryViewColumnDefinitionDto {
  alias: string;
  dataType: string;
  exportable: boolean;
  fieldName: string;
  fixed?: string | null;
  queryable: boolean;
  sortable: boolean;
  title: string;
  visible: boolean;
  width?: string | null;
}

export interface QueryViewRuntimeDefinitionDto {
  columns: QueryViewColumnDefinitionDto[];
  maxPageSize: number;
  viewCode: string;
  viewName: string;
}

export interface QueryViewQueryResponse {
  columns: QueryViewColumnDefinitionDto[];
  pageIndex: number;
  pageSize: number;
  rows: Array<Record<string, unknown>>;
  total: number;
  viewCode: string;
}

export interface QueryViewExportRequest {
  columns: string[];
  conditions: QueryViewQueryCondition[];
  exportMode: 'all' | 'currentPage' | 'selected';
  fileType: 'xlsx' | string;
  selectedRowIds?: string[];
  sorts: QueryViewQuerySort[];
}

export interface QueryViewExportResponse {
  base64Content?: string | null;
  contentType: string;
  fileName: string;
  status: string;
  taskNo: string;
  totalCount: number;
}

export function getQueryViewDefinition(viewCode: string, signal?: AbortSignal) {
  return httpClient.get<QueryViewRuntimeDefinitionDto>(`/system/query-views/${viewCode}/definition`, undefined, signal);
}

export function queryQueryView(viewCode: string, request: QueryViewQueryRequest, signal?: AbortSignal) {
  return httpClient.post<QueryViewQueryResponse, QueryViewQueryRequest>(`/system/query-views/${viewCode}/query`, request, undefined, signal);
}

export function exportQueryView(viewCode: string, request: QueryViewExportRequest) {
  return httpClient.post<QueryViewExportResponse, QueryViewExportRequest>(`/system/query-views/${viewCode}/export`, request);
}
