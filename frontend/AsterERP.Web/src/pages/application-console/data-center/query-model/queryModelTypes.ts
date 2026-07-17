import type {
  ApplicationDataSourceColumn,
  ApplicationDataSourceTable,
  ApplicationQueryPlanParameter,
  ApplicationQueryPlanRequest
} from '../../../../api/application-data-center/applicationDataCenter.types';

export type QueryModelNodeKind = 'table' | 'view';
export type ApplicationQueryJoinType = 'inner' | 'left' | 'right' | 'full';
export type QueryModelJoinType = ApplicationQueryJoinType;
export type QueryModelAggregate = 'none' | 'count' | 'sum' | 'avg' | 'min' | 'max';

export interface QueryModelFieldReference {
  fieldResourceId: string;
  nodeId: string;
}

export interface QueryModelNode {
  alias: string;
  columns: ApplicationDataSourceColumn[];
  id: string;
  kind: QueryModelNodeKind;
  name: string;
  resourceId: string;
  x: number;
  y: number;
}

export interface QueryModelJoin {
  id: string;
  leftFieldResourceId: string;
  leftNodeId: string;
  rightFieldResourceId: string;
  rightNodeId: string;
  type: QueryModelJoinType;
}

export interface QueryModelSelection {
  aggregate: QueryModelAggregate;
  alias: string;
  fieldResourceId: string;
  id: string;
  nodeId: string;
}

export interface QueryModelPredicate {
  fieldResourceId: string;
  id: string;
  nodeId: string;
  operator: string;
  parameterResourceId: string;
  value?: unknown;
}

export interface QueryModelOrder {
  direction: 'asc' | 'desc';
  fieldResourceId: string;
  id: string;
  nodeId: string;
}

export interface QueryModelConfig {
  schemaVersion: 1;
  dataSourceId: string;
  nodes: QueryModelNode[];
  joins: QueryModelJoin[];
  selections: QueryModelSelection[];
  where: QueryModelPredicate[];
  groupBy: QueryModelFieldReference[];
  having: QueryModelPredicate[];
  orderBy: QueryModelOrder[];
  page: { index: number; size: number };
  parameters: ApplicationQueryPlanParameter[];
  timeoutSeconds: number;
  rowLimit: number;
}

export interface QueryModelDiagnostic {
  level: 'error' | 'warning';
  message: string;
}

export interface QueryModelSourceCatalog {
  tables: ApplicationDataSourceTable[];
  columns: ApplicationDataSourceColumn[];
}

export type QueryPlanDto = ApplicationQueryPlanRequest;
