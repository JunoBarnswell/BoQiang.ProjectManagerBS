import { formatMessage } from '../../../../core/i18n/formatMessage';
import { translateCurrentLocale } from '../../../../core/i18n/I18nProvider';
import type {
  KnowledgeGraphApiEdgeUpsertRequest,
  KnowledgeGraphApiExport,
  KnowledgeGraphApiExportRequest,
  KnowledgeGraphApiImpactRequest,
  KnowledgeGraphApiImpactResult,
  KnowledgeGraphApiImportRequest,
  KnowledgeGraphApiNodeUpsertRequest,
  KnowledgeGraphApiPathRequest,
  KnowledgeGraphApiPathResult,
  KnowledgeGraphApiQuery,
  KnowledgeGraphApiTaskQuery,
  KnowledgeGraphEdgeFormValue,
  KnowledgeGraphEdgeView,
  KnowledgeGraphFilterState,
  KnowledgeGraphImpactDraft,
  KnowledgeGraphImpactView,
  KnowledgeGraphMetricView,
  KnowledgeGraphNodeFormValue,
  KnowledgeGraphNodeView,
  KnowledgeGraphOverviewView,
  KnowledgeGraphPathDraft,
  KnowledgeGraphPathView,
  KnowledgeGraphSnapshotView,
  KnowledgeGraphTaskView
} from '../types';

const EMPTY_GRAPH: KnowledgeGraphSnapshotView = {
  edgeTotal: 0,
  edges: [],
  generatedAt: null,
  nodeTotal: 0,
  nodes: [],
  truncateReason: null,
  truncated: false
};

export const defaultKnowledgeGraphFilters: KnowledgeGraphFilterState = {
  direction: 'both',
  includeInactive: false,
  includeOrphans: true,
  keyword: '',
  maxDepth: 3,
  maxNodes: 300,
  nodeType: '',
  relationType: '',
  sourceId: '',
  status: ''
};

export const defaultPathDraft: KnowledgeGraphPathDraft = {
  maxDepth: 4,
  relationType: '',
  sourceNodeId: '',
  targetNodeId: ''
};

export const defaultImpactDraft: KnowledgeGraphImpactDraft = {
  direction: 'both',
  maxDepth: 3,
  nodeId: '',
  relationType: ''
};

export const defaultNodeFormValue: KnowledgeGraphNodeFormValue = {
  description: '',
  id: '',
  metadataJson: '{}',
  nodeCode: '',
  nodeType: 'term',
  positionX: 120,
  positionY: 120,
  sourceId: '',
  status: 'Enabled',
  tags: '',
  title: '',
  weight: 1
};

export const defaultEdgeFormValue: KnowledgeGraphEdgeFormValue = {
  description: '',
  id: '',
  metadataJson: '{}',
  relationCode: '',
  relationType: 'related',
  sourceNodeId: '',
  status: 'Enabled',
  targetNodeId: '',
  title: '',
  weight: 1
};

export function buildGraphQuery(filters: KnowledgeGraphFilterState): KnowledgeGraphApiQuery {
  return {
    direction: filters.direction,
    includeInactive: filters.includeInactive,
    includeOrphans: filters.includeOrphans,
    keyword: emptyToNull(filters.keyword),
    maxDepth: filters.maxDepth,
    maxNodes: filters.maxNodes,
    nodeType: emptyToNull(filters.nodeType),
    relationType: emptyToNull(filters.relationType),
    sourceId: emptyToNull(filters.sourceId),
    status: emptyToNull(filters.status)
  } as KnowledgeGraphApiQuery;
}

export function buildTaskQuery(filters: KnowledgeGraphFilterState): KnowledgeGraphApiTaskQuery {
  return {
    keyword: emptyToNull(filters.keyword),
    pageIndex: 1,
    pageSize: 20,
    status: emptyToNull(filters.status)
  } as KnowledgeGraphApiTaskQuery;
}

export function normalizeKnowledgeGraphOverview(input: unknown): KnowledgeGraphOverviewView {
  const source = toRecord(input);
  const metricsInput = readArray(source, 'metrics', 'summaryItems');
  const metrics = metricsInput.length > 0
    ? metricsInput.map((item, index) => normalizeMetric(item, index))
    : [
        normalizeKnownMetric(source, 'nodeCount', translateCurrentLocale('kg.metrics.nodeCount'), 'info'),
        normalizeKnownMetric(source, 'edgeCount', translateCurrentLocale('kg.metrics.edgeCount'), 'success'),
        normalizeKnownMetric(source, 'activeNodeCount', translateCurrentLocale('kg.metrics.activeNodeCount'), 'neutral'),
        normalizeKnownMetric(source, 'pendingTaskCount', translateCurrentLocale('kg.metrics.pendingTaskCount'), 'warning')
      ];

  return {
    healthStatus: readString(source, ['healthStatus', 'status'], translateCurrentLocale('kg.overview.unknown')),
    lastUpdatedAt: readNullableString(source, ['lastUpdatedAt', 'updatedTime', 'generatedAt']),
    metrics,
    summary: readString(source, ['summary', 'description'], translateCurrentLocale('kg.overview.summary'))
  };
}

export function normalizeKnowledgeGraphSnapshot(input: unknown): KnowledgeGraphSnapshotView {
  if (!input) {
    return EMPTY_GRAPH;
  }

  const source = toRecord(input);
  const nodeInputs = readArray(source, 'nodes', 'graphNodes', 'vertices');
  const edgeInputs = readArray(source, 'edges', 'graphEdges', 'links');
  const nodes = nodeInputs.map((item, index) => normalizeNode(item, index));
  const nodeLabelById = new Map(nodes.map((node) => [node.id, node.label]));
  const edges = edgeInputs.map((item, index) => normalizeEdge(item, nodeLabelById, index));

  return {
    edgeTotal: readNumber(source, ['edgeTotal', 'totalEdges', 'totalEdgeCount', 'edgeCount'], edges.length),
    edges,
    generatedAt: readNullableString(source, ['generatedAt', 'createdTime', 'updatedTime']),
    nodeTotal: readNumber(source, ['nodeTotal', 'totalNodes', 'totalNodeCount', 'nodeCount'], nodes.length),
    nodes,
    truncateReason: readNullableString(source, ['truncateReason', 'truncatedReason', 'limitReason']),
    truncated: readBoolean(source, ['truncated', 'isTruncated'], false)
  };
}

export function normalizeKnowledgeGraphTasks(input: unknown): KnowledgeGraphTaskView[] {
  const source = toRecord(input);
  const rows = Array.isArray(input) ? input : readArray(source, 'items', 'rows', 'tasks');
  return rows.map((item, index) => {
    const record = toRecord(item);
    const id = readString(record, ['id', 'taskId', 'taskCode'], `task_${index}`);
    return {
      completedAt: readNullableString(record, ['completedAt', 'finishedAt']),
      createdTime: readNullableString(record, ['createdTime', 'createdAt']),
      errorMessage: readNullableString(record, ['errorMessage', 'failureReason']),
      id,
      progressPercent: readNumber(record, ['progressPercent', 'percent', 'progress'], 0),
      status: readString(record, ['status'], translateCurrentLocale('kg.task.status.pending')),
      summary: readString(record, ['summary', 'description'], ''),
      taskCode: readString(record, ['taskCode', 'code'], id),
      taskName: readString(record, ['taskName', 'name', 'title'], id),
      taskType: readString(record, ['taskType', 'type'], translateCurrentLocale('kg.task.type.graphTask'))
    };
  });
}

export function normalizePathAnalysisResult(input: KnowledgeGraphApiPathResult | unknown): KnowledgeGraphPathView[] {
  const source = toRecord(input);
  const rows = Array.isArray(input) ? input : readArray(source, 'paths', 'items', 'results');
  return rows.map((item, index) => {
    const record = toRecord(item);
    return {
      edges: readStringArray(record, ['edges', 'edgeIds', 'relationIds']),
      id: readString(record, ['id', 'pathId'], `path_${index}`),
      nodes: readStringArray(record, ['nodes', 'nodeIds']),
      riskLevel: readString(record, ['riskLevel', 'risk'], 'normal'),
      score: readNumber(record, ['score', 'weight'], 0),
      summary: readString(record, ['summary', 'description'], translateCurrentLocale('kg.analysis.path.resultFallback'))
    };
  });
}

export function normalizeImpactAnalysisResult(input: KnowledgeGraphApiImpactResult | unknown): KnowledgeGraphImpactView[] {
  const source = toRecord(input);
  const rows = Array.isArray(input) ? input : readArray(source, 'impacts', 'items', 'results');
  return rows.map((item, index) => {
    const record = toRecord(item);
    return {
      affectedEdges: readStringArray(record, ['affectedEdges', 'edgeIds', 'relations']),
      affectedNodes: readStringArray(record, ['affectedNodes', 'nodeIds', 'nodes']),
      blastRadius: readNumber(record, ['blastRadius', 'radius', 'affectedCount'], 0),
      id: readString(record, ['id', 'impactId'], `impact_${index}`),
      recommendation: readString(record, ['recommendation', 'suggestion'], ''),
      riskLevel: readString(record, ['riskLevel', 'risk'], 'normal'),
      summary: readString(record, ['summary', 'description'], translateCurrentLocale('kg.analysis.impact.resultFallback'))
    };
  });
}

export function buildNodeFormValue(node?: KnowledgeGraphNodeView | null): KnowledgeGraphNodeFormValue {
  if (!node) {
    return defaultNodeFormValue;
  }

  return {
    description: node.description,
    id: node.id,
    metadataJson: stableJson(node.metadata),
    nodeCode: node.nodeCode,
    nodeType: node.nodeType,
    positionX: node.position.x,
    positionY: node.position.y,
    sourceId: node.sourceId,
    status: node.status,
    tags: node.tags.join(', '),
    title: node.label,
    weight: node.weight
  };
}

export function buildEdgeFormValue(edge?: KnowledgeGraphEdgeView | null): KnowledgeGraphEdgeFormValue {
  if (!edge) {
    return defaultEdgeFormValue;
  }

  return {
    description: edge.description,
    id: edge.id,
    metadataJson: stableJson(edge.metadata),
    relationCode: edge.relationCode,
    relationType: edge.relationType,
    sourceNodeId: edge.source,
    status: edge.status,
    targetNodeId: edge.target,
    title: edge.label,
    weight: edge.weight
  };
}

export function buildNodeUpsertRequest(value: KnowledgeGraphNodeFormValue): KnowledgeGraphApiNodeUpsertRequest {
  return {
    description: emptyToNull(value.description),
    id: emptyToNull(value.id),
    metadata: parseJsonObject(value.metadataJson),
    nodeCode: value.nodeCode.trim(),
    nodeType: value.nodeType.trim(),
    positionX: finiteOr(value.positionX, defaultNodeFormValue.positionX),
    positionY: finiteOr(value.positionY, defaultNodeFormValue.positionY),
    sourceId: emptyToNull(value.sourceId),
    status: value.status,
    tags: splitTags(value.tags),
    title: value.title.trim(),
    weight: finiteOr(value.weight, 1)
  } as KnowledgeGraphApiNodeUpsertRequest;
}

export function buildEdgeUpsertRequest(value: KnowledgeGraphEdgeFormValue): KnowledgeGraphApiEdgeUpsertRequest {
  return {
    description: emptyToNull(value.description),
    id: emptyToNull(value.id),
    metadata: parseJsonObject(value.metadataJson),
    relationCode: value.relationCode.trim(),
    relationType: value.relationType.trim(),
    sourceNodeId: value.sourceNodeId,
    status: value.status,
    targetNodeId: value.targetNodeId,
    title: value.title.trim(),
    weight: finiteOr(value.weight, 1)
  } as KnowledgeGraphApiEdgeUpsertRequest;
}

export function buildNodePositionRequest(node: KnowledgeGraphNodeView, x: number, y: number): KnowledgeGraphApiNodeUpsertRequest {
  return {
    ...toRecord(node.raw),
    description: node.description,
    id: node.id,
    metadata: node.metadata,
    nodeCode: node.nodeCode,
    nodeType: node.nodeType,
    positionX: finiteOr(x, node.position.x),
    positionY: finiteOr(y, node.position.y),
    sourceId: emptyToNull(node.sourceId),
    status: node.status,
    tags: node.tags,
    title: node.label,
    weight: node.weight
  } as KnowledgeGraphApiNodeUpsertRequest;
}

export function buildPathRequest(value: KnowledgeGraphPathDraft): KnowledgeGraphApiPathRequest {
  return {
    maxDepth: finiteOr(value.maxDepth, defaultPathDraft.maxDepth),
    relationType: emptyToNull(value.relationType),
    sourceNodeId: value.sourceNodeId,
    targetNodeId: value.targetNodeId
  } as KnowledgeGraphApiPathRequest;
}

export function buildImpactRequest(value: KnowledgeGraphImpactDraft): KnowledgeGraphApiImpactRequest {
  return {
    direction: value.direction,
    maxDepth: finiteOr(value.maxDepth, defaultImpactDraft.maxDepth),
    nodeId: value.nodeId,
    relationType: emptyToNull(value.relationType)
  } as KnowledgeGraphApiImpactRequest;
}

export function buildImportRequest(content: string, fileName: string): KnowledgeGraphApiImportRequest {
  return {
    content,
    fileName: fileName.trim() || translateCurrentLocale('kg.exchange.fileName.importFallback')
  } as KnowledgeGraphApiImportRequest;
}

export function buildExportText(result: KnowledgeGraphApiExport | unknown): { content: string; fileName: string; mimeType: string } {
  const source = toRecord(result);
  const content = readString(source, ['content', 'data', 'json'], '');
  const base64 = readNullableString(source, ['contentBase64', 'base64Content']);
  return {
    content: content || (base64 ? decodeBase64(base64) : stableJson(source)),
    fileName: readString(source, ['fileName', 'name'], translateCurrentLocale('kg.exchange.fileName.exportFallback')),
    mimeType: readString(source, ['mimeType', 'contentType'], 'application/json')
  };
}

export function buildExportRequest(filters: KnowledgeGraphFilterState, format: 'json' | 'mermaid'): KnowledgeGraphApiExportRequest {
  return {
    ...buildGraphQuery(filters),
    format
  } as KnowledgeGraphApiExportRequest;
}

export function downloadTextFile(fileName: string, content: string, mimeType: string): void {
  const blob = new Blob([content], { type: mimeType || 'application/json' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

export function formatDateTime(value?: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

export function formatPercent(value: number): string {
  if (!Number.isFinite(value)) {
    return '0%';
  }

  return `${Math.max(0, Math.min(100, Math.round(value)))}%`;
}

function normalizeNode(input: unknown, index: number): KnowledgeGraphNodeView {
  const record = toRecord(input);
  const id = readString(record, ['id', 'nodeId', 'key', 'nodeKey', 'nodeCode'], `node_${index}`);
  const metadata = readMetadata(record);
  return {
    degree: readNumber(record, ['degree', 'edgeCount', 'linkCount'], 0),
    description: readString(record, ['description', 'summary'], ''),
    id,
    isVirtual: readBoolean(record, ['isVirtual', 'virtual'], false),
    label: readString(record, ['title', 'displayName', 'label', 'name', 'nodeName'], id),
    metadata,
    nodeCode: readString(record, ['nodeCode', 'nodeKey', 'code', 'key'], id),
    nodeType: readString(record, ['nodeType', 'type', 'category'], 'term'),
    position: readPosition(record, index),
    raw: input as KnowledgeGraphNodeView['raw'],
    sourceId: readString(record, ['sourceId'], ''),
    sourceName: readString(record, ['sourceName', 'source'], ''),
    status: readString(record, ['status'], 'Enabled'),
    tags: readStringArray(record, ['tags', 'tagNames']),
    weight: readNumber(record, ['weight', 'score'], 1)
  };
}

function normalizeEdge(input: unknown, nodeLabelById: Map<string, string>, index: number): KnowledgeGraphEdgeView {
  const record = toRecord(input);
  const source = readString(record, ['source', 'sourceNodeId', 'fromNodeId', 'from'], '');
  const target = readString(record, ['target', 'targetNodeId', 'toNodeId', 'to'], '');
  const id = readString(record, ['id', 'edgeId', 'relationCode'], `edge_${source}_${target}_${index}`);
  return {
    description: readString(record, ['description', 'summary'], ''),
    id,
    label: readString(record, ['title', 'label', 'name', 'relationName'], readString(record, ['relationType'], translateCurrentLocale('kg.details.edge.relationFallback'))),
    metadata: readMetadata(record),
    raw: input as KnowledgeGraphEdgeView['raw'],
    relationCode: readString(record, ['relationCode', 'code'], id),
    relationType: readString(record, ['relationType', 'type'], 'related'),
    source,
    sourceLabel: readString(record, ['sourceLabel', 'sourceName'], nodeLabelById.get(source) ?? source),
    status: readString(record, ['status'], 'Enabled'),
    target,
    targetLabel: readString(record, ['targetLabel', 'targetName'], nodeLabelById.get(target) ?? target),
    weight: readNumber(record, ['weight', 'score'], 1)
  };
}

function normalizeMetric(input: unknown, index: number): KnowledgeGraphMetricView {
  const record = toRecord(input);
  return {
    key: readString(record, ['key', 'code'], `metric_${index}`),
    label: readString(record, ['label', 'name'], formatMessage(translateCurrentLocale('kg.metrics.fallback'), { index: index + 1 })),
    tone: normalizeTone(readString(record, ['tone', 'status'], 'neutral')),
    value: readNumberOrString(record, ['value', 'count'], 0)
  };
}

function normalizeKnownMetric(
  source: Record<string, unknown>,
  key: string,
  label: string,
  tone: KnowledgeGraphMetricView['tone']
): KnowledgeGraphMetricView {
  return {
    key,
    label,
    tone,
    value: readNumber(source, [key], 0)
  };
}

function normalizeTone(value: string): KnowledgeGraphMetricView['tone'] {
  const normalized = value.toLowerCase();
  if (normalized.includes('critical') || normalized.includes('danger') || normalized.includes('error')) {
    return 'critical';
  }
  if (normalized.includes('success') || normalized.includes('healthy')) {
    return 'success';
  }
  if (normalized.includes('warning') || normalized.includes('pending')) {
    return 'warning';
  }
  if (normalized.includes('info')) {
    return 'info';
  }
  return 'neutral';
}

function readPosition(record: Record<string, unknown>, index: number): { x: number; y: number } {
  const nested = toRecord(record.position);
  const fallbackColumn = index % 5;
  const fallbackRow = Math.floor(index / 5);
  return {
    x: readNumber(record, ['positionX', 'x'], readNumber(nested, ['x'], 80 + fallbackColumn * 220)),
    y: readNumber(record, ['positionY', 'y'], readNumber(nested, ['y'], 80 + fallbackRow * 140))
  };
}

function readMetadata(record: Record<string, unknown>): Record<string, unknown> {
  const direct = toRecord(record.metadata);
  if (Object.keys(direct).length > 0) {
    return direct;
  }

  return parseJsonObject(readString(record, ['metadataJson', 'extraJson'], '{}'));
}

function readArray(record: Record<string, unknown>, ...keys: string[]): unknown[] {
  for (const key of keys) {
    const value = record[key];
    if (Array.isArray(value)) {
      return value;
    }
  }
  return [];
}

function readString(record: Record<string, unknown>, keys: string[], fallback: string): string {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === 'string' && value.trim().length > 0) {
      return value;
    }
    if (typeof value === 'number' || typeof value === 'boolean') {
      return String(value);
    }
  }
  return fallback;
}

function readNullableString(record: Record<string, unknown>, keys: string[]): string | null {
  const value = readString(record, keys, '');
  return value || null;
}

function readNumber(record: Record<string, unknown>, keys: string[], fallback: number): number {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === 'number' && Number.isFinite(value)) {
      return value;
    }
    if (typeof value === 'string' && value.trim()) {
      const parsed = Number(value);
      if (Number.isFinite(parsed)) {
        return parsed;
      }
    }
  }
  return fallback;
}

function readNumberOrString(record: Record<string, unknown>, keys: string[], fallback: number): number | string {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === 'number' || typeof value === 'string') {
      return value;
    }
  }
  return fallback;
}

function readBoolean(record: Record<string, unknown>, keys: string[], fallback: boolean): boolean {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === 'boolean') {
      return value;
    }
    if (typeof value === 'string') {
      const normalized = value.toLowerCase();
      if (normalized === 'true') {
        return true;
      }
      if (normalized === 'false') {
        return false;
      }
    }
  }
  return fallback;
}

function readStringArray(record: Record<string, unknown>, keys: string[]): string[] {
  for (const key of keys) {
    const value = record[key];
    if (Array.isArray(value)) {
      return value.map((item) => String(item)).filter(Boolean);
    }
    if (typeof value === 'string' && value.trim()) {
      return value.split(',').map((item) => item.trim()).filter(Boolean);
    }
  }
  return [];
}

function parseJsonObject(value: string): Record<string, unknown> {
  try {
    const parsed = JSON.parse(value || '{}') as unknown;
    return toRecord(parsed);
  } catch {
    return {};
  }
}

function stableJson(value: Record<string, unknown>): string {
  return JSON.stringify(value, null, 2);
}

function splitTags(value: string): string[] {
  return value.split(',').map((item) => item.trim()).filter(Boolean);
}

function finiteOr(value: number, fallback: number): number {
  return Number.isFinite(value) ? value : fallback;
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
}

function decodeBase64(value: string): string {
  try {
    return decodeURIComponent(escape(window.atob(value)));
  } catch {
    return '';
  }
}

function toRecord(value: unknown): Record<string, unknown> {
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }
  return {};
}
