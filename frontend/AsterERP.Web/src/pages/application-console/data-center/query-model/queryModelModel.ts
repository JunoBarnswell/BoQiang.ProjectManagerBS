import type { ApplicationDataSourceColumn, ApplicationQueryPlanRequest } from '../../../../api/application-data-center/applicationDataCenter.types';

import type {
  QueryModelConfig,
  QueryModelDiagnostic,
  QueryModelFieldReference,
  QueryModelNode,
  QueryModelSelection,
  QueryModelJoinType
} from './queryModelTypes';

const allJoinTypes: QueryModelJoinType[] = ['inner', 'left', 'right', 'full'];

/** A resource can be placed more than once, so its resource ID is not a node identity. */
export function createNodeId(dataSourceId: string, resourceId: string, instance = 1): string {
  return `node:${dataSourceId}:${resourceId}:instance:${Math.max(1, Math.trunc(instance))}`;
}
export function createSelectionId(nodeId: string, fieldResourceId: string): string { return `selection:${nodeId}:${fieldResourceId}`; }
export function selectNextJoinTarget<T extends { resourceId: string }>(sourceTables: readonly T[], nodes: readonly Pick<QueryModelNode, 'resourceId'>[]): T | undefined {
  const rootResourceId = nodes[0]?.resourceId;
  return sourceTables.find((table) => table.resourceId !== rootResourceId && !nodes.some((node) => node.resourceId === table.resourceId))
    ?? sourceTables.find((table) => table.resourceId === rootResourceId)
    ?? sourceTables[0];
}
export function createInitialQueryModel(dataSourceId = ''): QueryModelConfig {
  return { schemaVersion: 1, dataSourceId, nodes: [], selections: [], joins: [], where: [], groupBy: [], having: [], orderBy: [], page: { index: 1, size: 20 }, parameters: [], timeoutSeconds: 30, rowLimit: 20 };
}

export function getSupportedQueryModelJoinTypes(provider: string | undefined): QueryModelJoinType[] {
  const normalized = provider?.trim().toLowerCase();
  if (normalized === 'postgresql' || normalized === 'sqlserver') return [...allJoinTypes];
  if (normalized === 'mysql' || normalized === 'sqlite' || normalized === 'applicationdatabase') return ['inner', 'left', 'right'];
  return [];
}

export function normalizeQueryModel(value: unknown, fallbackDataSourceId = ''): QueryModelConfig {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return createInitialQueryModel(fallbackDataSourceId);
  const source = value as Partial<QueryModelConfig> & { groupBy?: unknown };
  const base = createInitialQueryModel(typeof source.dataSourceId === 'string' ? source.dataSourceId : fallbackDataSourceId);
  const nodes = Array.isArray(source.nodes) ? source.nodes as QueryModelNode[] : [];
  return {
    ...base,
    ...source,
    schemaVersion: 1,
    nodes,
    joins: Array.isArray(source.joins) ? source.joins : [],
    selections: normalizeSelections(source.selections),
    where: normalizePredicates(source.where),
    groupBy: normalizeGroupBy(source.groupBy),
    having: normalizePredicates(source.having),
    orderBy: normalizeOrderBy(source.orderBy),
    page: { ...base.page, ...(source.page ?? {}) },
    parameters: Array.isArray(source.parameters) ? source.parameters : []
  };
}

export function buildQueryPlanRequest(model: QueryModelConfig): ApplicationQueryPlanRequest {
  return {
    accessMode: 'readOnly',
    auditId: null,
    columns: model.selections.map((item) => ({ fieldResourceId: item.fieldResourceId, nodeId: item.nodeId, ...(item.alias ? { alias: item.alias } : {}), ...(item.aggregate !== 'none' ? { aggregate: item.aggregate } : {}) })),
    dataSourceId: model.dataSourceId,
    filters: model.where.map((item) => ({ fieldResourceId: item.fieldResourceId, nodeId: item.nodeId, operator: item.operator, parameterResourceId: item.parameterResourceId })),
    groupBy: model.groupBy.filter((item) => Boolean(item.fieldResourceId && item.nodeId)).map((item) => ({ fieldResourceId: item.fieldResourceId, nodeId: item.nodeId })),
    having: model.having.map((item) => ({ fieldResourceId: item.fieldResourceId, nodeId: item.nodeId, operator: item.operator, parameterResourceId: item.parameterResourceId })),
    joins: model.joins.map((item) => ({ type: item.type, leftNodeId: item.leftNodeId, leftFieldResourceId: item.leftFieldResourceId, rightNodeId: item.rightNodeId, rightFieldResourceId: item.rightFieldResourceId })),
    nodes: model.nodes.map((item) => ({ alias: item.alias, id: item.id, kind: item.kind, resourceId: item.resourceId })),
    page: { index: Math.max(1, model.page.index), size: Math.max(1, model.page.size) },
    parameters: model.parameters,
    riskConfirmed: false,
    riskConfirmationId: null,
    rowLimit: Math.max(1, model.rowLimit),
    sorts: model.orderBy.map((item) => ({ fieldResourceId: item.fieldResourceId, direction: item.direction, nodeId: item.nodeId })),
    timeoutSeconds: Math.max(1, model.timeoutSeconds)
  };
}

export function validateQueryModel(model: QueryModelConfig, provider?: string): QueryModelDiagnostic[] {
  const result: QueryModelDiagnostic[] = [];
  if (!model.dataSourceId) result.push({ level: 'error', message: 'Select a data source.' });
  if (model.nodes.length === 0 || model.nodes.some((node) => !node.name || !node.resourceId)) result.push({ level: 'error', message: 'Every query node must select a source table or view.' });
  if (model.selections.length === 0) result.push({ level: 'error', message: 'Select at least one output field.' });
  const nodes = new Map(model.nodes.map((node) => [node.id, node]));
  model.selections.forEach((item) => validateFieldReference(item, `Selection ${item.id}`, nodes, result));
  const parameters = new Set(model.parameters.map((item) => item.resourceId));
  [...model.where, ...model.having].forEach((item) => {
    if (!item.fieldResourceId) result.push({ level: 'error', message: 'WHERE contains a predicate without a field Resource ID.' });
    if (item.fieldResourceId) validateFieldReference(item, `Predicate ${item.id}`, nodes, result);
    if (!item.parameterResourceId && !['isNull', 'isNotNull'].includes(item.operator)) result.push({ level: 'error', message: `Predicate ${item.fieldResourceId || '(unnamed)'} needs a parameter Resource ID.` });
    if (item.parameterResourceId && !parameters.has(item.parameterResourceId)) result.push({ level: 'warning', message: `Parameter ${item.parameterResourceId} is not defined.` });
  });
  if (model.nodes.length > 1 && model.joins.length === 0) result.push({ level: 'error', message: 'Every additional node must be connected by a JOIN.' });
  if (new Set(model.nodes.map((node) => node.id)).size !== model.nodes.length) result.push({ level: 'error', message: 'Query node IDs must be unique.' });
  const supportedJoinTypes = provider ? getSupportedQueryModelJoinTypes(provider) : allJoinTypes;
  model.joins.forEach((join) => {
    const left = nodes.get(join.leftNodeId);
    const right = nodes.get(join.rightNodeId);
    if (!supportedJoinTypes.includes(join.type)) result.push({ level: 'error', message: `Provider does not support ${join.type.toUpperCase()} JOIN.` });
    if (!left || !right) result.push({ level: 'error', message: `JOIN ${join.id} references an unknown node.` });
    if (join.leftNodeId === join.rightNodeId) result.push({ level: 'error', message: 'A JOIN cannot connect a node to itself.' });
    if (!join.leftFieldResourceId || !left?.columns.some((field) => field.resourceId === join.leftFieldResourceId)) result.push({ level: 'error', message: `JOIN ${join.id} has an invalid left field.` });
    if (!join.rightFieldResourceId || !right?.columns.some((field) => field.resourceId === join.rightFieldResourceId)) result.push({ level: 'error', message: `JOIN ${join.id} has an invalid right field.` });
  });
  model.groupBy.forEach((field) => { if (field.fieldResourceId) validateFieldReference(field, `GROUP BY field ${field.fieldResourceId}`, nodes, result); });
  model.orderBy.forEach((item) => {
    if (!item.fieldResourceId) result.push({ level: 'error', message: 'ORDER BY requires a field Resource ID.' });
    else validateFieldReference(item, `ORDER BY field ${item.fieldResourceId}`, nodes, result);
  });
  if (model.page.size > model.rowLimit) result.push({ level: 'warning', message: 'Page size exceeds the row limit.' });
  return result;
}

export function mergeNodeColumns(node: QueryModelNode, columns: ApplicationDataSourceColumn[]): QueryModelNode { return { ...node, columns }; }
export function createSelections(node: QueryModelNode): QueryModelSelection[] { return node.columns.map((column) => ({ aggregate: 'none', alias: '', fieldResourceId: column.resourceId, id: createSelectionId(node.id, column.resourceId), nodeId: node.id })); }

function validateFieldReference(reference: QueryModelFieldReference, label: string, nodes: Map<string, QueryModelNode>, result: QueryModelDiagnostic[]) {
  const node = nodes.get(reference.nodeId);
  if (!reference.nodeId || !node) {
    result.push({ level: 'error', message: `${label} references an unknown node.` });
    return;
  }
  if (!reference.fieldResourceId || !node.columns.some((column) => column.resourceId === reference.fieldResourceId)) result.push({ level: 'error', message: `${label} references a field outside the loaded node catalog.` });
}

function normalizeSelections(value: unknown): QueryModelSelection[] {
  if (!Array.isArray(value)) return [];
  return value.map((item, index) => {
    const row = item as Partial<QueryModelSelection>;
    const fieldResourceId = typeof row.fieldResourceId === 'string' ? row.fieldResourceId : '';
    return { aggregate: row.aggregate ?? 'none', alias: row.alias ?? '', fieldResourceId, id: row.id ?? `selection:${index + 1}`, nodeId: typeof row.nodeId === 'string' ? row.nodeId : '' };
  });
}

function normalizePredicates(value: unknown) {
  if (!Array.isArray(value)) return [];
  return value.map((item, index) => {
    const row = item as QueryModelConfig['where'][number];
    return { ...row, id: row.id ?? `predicate:${index + 1}`, fieldResourceId: row.fieldResourceId ?? '', nodeId: typeof row.nodeId === 'string' ? row.nodeId : '', operator: row.operator ?? 'eq', parameterResourceId: row.parameterResourceId ?? '' };
  });
}

function normalizeOrderBy(value: unknown) {
  if (!Array.isArray(value)) return [];
  return value.map((item, index) => {
    const row = item as QueryModelConfig['orderBy'][number];
    return { ...row, id: row.id ?? `order:${index + 1}`, fieldResourceId: row.fieldResourceId ?? '', nodeId: typeof row.nodeId === 'string' ? row.nodeId : '', direction: row.direction === 'desc' ? 'desc' as const : 'asc' as const };
  });
}

function normalizeGroupBy(value: unknown): QueryModelFieldReference[] {
  if (!Array.isArray(value)) return [];
  return value.map((item) => {
    if (typeof item === 'string') return { fieldResourceId: item, nodeId: '' };
    const row = item as Partial<QueryModelFieldReference>;
    return { fieldResourceId: row.fieldResourceId ?? '', nodeId: typeof row.nodeId === 'string' ? row.nodeId : '' };
  });
}
