import type { ApiEnvelope } from '../../../core/http/apiEnvelope';
import { httpClient } from '../../../core/http/httpClient';
import { formatMessage } from '../../../core/i18n/formatMessage';
import { translateCurrentLocale } from '../../../core/i18n/I18nProvider';

const graphBasePath = '/ai/knowledge/graph';

export interface KnowledgeGraphQuery {
  direction?: 'both' | 'incoming' | 'outgoing';
  includeInactive?: boolean;
  includeOrphans?: boolean;
  keyword?: string | null;
  maxDepth?: number;
  maxNodes?: number;
  nodeType?: string | null;
  relationType?: string | null;
  sourceId?: string | null;
  status?: string | null;
}

export interface KnowledgeGraphTaskQuery {
  keyword?: string | null;
  pageIndex?: number;
  pageSize?: number;
  status?: string | null;
}

export interface KnowledgeGraphNodeDto {
  degree: number;
  description: string;
  id: string;
  isVirtual: boolean;
  label: string;
  metadata: Record<string, unknown>;
  nodeCode: string;
  nodeType: string;
  positionX: number;
  positionY: number;
  sourceId: string;
  sourceName: string;
  status: string;
  tags: string[];
  title: string;
  weight: number;
}

export interface KnowledgeGraphEdgeDto {
  description: string;
  id: string;
  label: string;
  metadata: Record<string, unknown>;
  relationCode: string;
  relationType: string;
  source: string;
  sourceLabel: string;
  sourceNodeId: string;
  status: string;
  target: string;
  targetLabel: string;
  targetNodeId: string;
  title: string;
  weight: number;
}

export interface KnowledgeGraphOverviewDto {
  edgeCount: number;
  evidenceCount: number;
  healthStatus: string;
  lastUpdatedAt: string | null;
  latestJob?: KnowledgeGraphTaskDto | null;
  metrics: Array<{ key: string; label: string; tone: 'critical' | 'info' | 'neutral' | 'success' | 'warning'; value: number | string }>;
  nodeCount: number;
  sourceCount: number;
  summary: string;
}

export interface KnowledgeGraphSnapshotDto {
  edgeTotal: number;
  edges: KnowledgeGraphEdgeDto[];
  generatedAt: string;
  nodeTotal: number;
  nodes: KnowledgeGraphNodeDto[];
  totalEdges: number;
  totalNodes: number;
  truncateReason: string | null;
  truncated: boolean;
}

export interface KnowledgeGraphTaskDto {
  completedAt: string | null;
  createdTime: string | null;
  errorCode?: string | null;
  errorMessage: string | null;
  id: string;
  progressPercent: number;
  status: string;
  summary: string;
  taskCode: string;
  taskName: string;
  taskType: string;
}

export interface KnowledgeGraphNodeUpsertRequest {
  description?: string | null;
  id?: string | null;
  metadata?: Record<string, unknown>;
  nodeCode: string;
  nodeType: string;
  positionX?: number;
  positionY?: number;
  sourceId?: string | null;
  status?: string;
  tags?: string[];
  title: string;
  weight?: number;
}

export interface KnowledgeGraphEdgeUpsertRequest {
  description?: string | null;
  id?: string | null;
  metadata?: Record<string, unknown>;
  relationCode?: string;
  relationType: string;
  sourceNodeId: string;
  status?: string;
  targetNodeId: string;
  title?: string;
  weight?: number;
}

export interface KnowledgeGraphPathAnalysisRequest {
  maxDepth: number;
  relationType?: string | null;
  sourceNodeId: string;
  targetNodeId: string;
}

export interface KnowledgeGraphPathAnalysisResultDto {
  edges: KnowledgeGraphEdgeDto[];
  nodes: KnowledgeGraphNodeDto[];
  paths: Array<{ edgeIds: string[]; edges: string[]; id: string; nodeIds: string[]; nodes: string[]; score: number; summary: string }>;
  truncated: boolean;
}

export interface KnowledgeGraphImpactAnalysisRequest {
  direction: 'both' | 'incoming' | 'outgoing';
  maxDepth: number;
  nodeId: string;
  relationType?: string | null;
}

export interface KnowledgeGraphImpactAnalysisResultDto {
  results: Array<{
    affectedEdges: string[];
    affectedNodes: string[];
    blastRadius: number;
    id: string;
    recommendation: string;
    riskLevel: string;
    summary: string;
  }>;
}

export interface KnowledgeGraphImportRequest {
  content: string;
  fileName: string;
}

export interface KnowledgeGraphImportResultDto {
  createdCount: number;
  edgeCount: number;
  nodeCount: number;
  skippedCount: number;
  updatedCount: number;
}

export interface KnowledgeGraphExportRequest extends KnowledgeGraphQuery {
  format?: 'json' | 'mermaid';
}

export interface KnowledgeGraphExportDto {
  content: string;
  fileName: string;
  mimeType: string;
}

interface BackendOverviewDto {
  edgeCount?: number;
  evidenceCount?: number;
  lastUpdatedTime?: string | null;
  latestJob?: BackendBuildJobDto | null;
  nodeCount?: number;
  sourceCount?: number;
}

interface BackendNodeDto {
  createdTime?: string;
  displayName?: string;
  documentId?: string | null;
  documentName?: string | null;
  id: string;
  metadataJson?: string | null;
  nodeKey?: string;
  nodeType?: string;
  sourceId?: string | null;
  sourceName?: string | null;
  summary?: string | null;
  updatedTime?: string | null;
}

interface BackendEdgeDto {
  createdTime?: string;
  evidenceText?: string | null;
  fromNodeId?: string;
  id: string;
  metadataJson?: string | null;
  relationType?: string;
  sourceId?: string | null;
  toNodeId?: string;
  updatedTime?: string | null;
  weight?: number;
}

interface BackendGraphResponseDto {
  edges?: BackendEdgeDto[];
  nodes?: BackendNodeDto[];
  totalEdges?: number;
  totalNodes?: number;
  traceId?: string;
  truncated?: boolean;
}

interface BackendPathDto {
  edgeIds?: string[];
  nodeIds?: string[];
}

interface BackendPathResponseDto {
  edges?: BackendEdgeDto[];
  nodes?: BackendNodeDto[];
  paths?: BackendPathDto[];
  truncated?: boolean;
}

interface BackendImpactResponseDto {
  edges?: BackendEdgeDto[];
  nodes?: BackendNodeDto[];
  rootNodeId?: string;
  truncated?: boolean;
}

interface BackendBuildJobDto {
  createdCount?: number;
  errorCode?: string | null;
  errorMessage?: string | null;
  finishedAt?: string | null;
  id: string;
  progress?: number;
  skippedCount?: number;
  sourceId?: string | null;
  startedAt?: string | null;
  status?: string;
  updatedCount?: number;
}

interface BackendNodeUpsertRequest {
  displayName: string;
  documentId?: string | null;
  metadataJson?: string | null;
  nodeKey: string;
  nodeType: string;
  sourceId?: string | null;
  summary?: string | null;
}

interface BackendEdgeUpsertRequest {
  evidenceText?: string | null;
  fromNodeId: string;
  metadataJson?: string | null;
  relationType: string;
  sourceId?: string | null;
  toNodeId: string;
  weight: number;
}

interface BackendImportRequest {
  edges: Array<BackendEdgeUpsertRequest & { fromNodeKey?: string | null; toNodeKey?: string | null }>;
  mode: string;
  nodes: BackendNodeUpsertRequest[];
  requestId: string;
  sourceId?: string | null;
}

interface BackendExportDto {
  edges?: BackendEdgeDto[];
  evidence?: unknown[];
  exportedAt?: string;
  nodes?: BackendNodeDto[];
}

export async function fetchKnowledgeGraphOverview(signal?: AbortSignal): Promise<ApiEnvelope<KnowledgeGraphOverviewDto>> {
  const response = await httpClient.get<BackendOverviewDto>(`${graphBasePath}/overview`, undefined, signal);
  return withData(response, mapOverview(response.data));
}

export async function fetchKnowledgeGraphSnapshot(query: KnowledgeGraphQuery, signal?: AbortSignal): Promise<ApiEnvelope<KnowledgeGraphSnapshotDto>> {
  const response = await httpClient.post<BackendGraphResponseDto, unknown>(
    `${graphBasePath}/query`,
    toBackendQuery(query),
    undefined,
    signal
  );
  return withData(response, mapSnapshot(response.data));
}

export async function fetchKnowledgeGraphTasks(query: KnowledgeGraphTaskQuery, signal?: AbortSignal): Promise<ApiEnvelope<KnowledgeGraphTaskDto[]>> {
  const response = await httpClient.get<BackendOverviewDto>(`${graphBasePath}/overview`, undefined, signal);
  const latestJob = response.data.latestJob ? mapTask(response.data.latestJob) : null;
  const status = query.status?.trim();
  const tasks = latestJob && (!status || latestJob.status === status) ? [latestJob] : [];
  return withData(response, tasks);
}

export async function saveKnowledgeGraphNode(request: KnowledgeGraphNodeUpsertRequest): Promise<ApiEnvelope<KnowledgeGraphNodeDto>> {
  const backendRequest = toBackendNodeRequest(request);
  const response = request.id
    ? await httpClient.put<BackendNodeDto, BackendNodeUpsertRequest>(`${graphBasePath}/nodes/${encodeURIComponent(request.id)}`, backendRequest)
    : await httpClient.post<BackendNodeDto, BackendNodeUpsertRequest>(`${graphBasePath}/nodes`, backendRequest);
  return withData(response, mapNode(response.data, new Map(), 0));
}

export async function saveKnowledgeGraphEdge(request: KnowledgeGraphEdgeUpsertRequest): Promise<ApiEnvelope<KnowledgeGraphEdgeDto>> {
  const backendRequest = toBackendEdgeRequest(request);
  const response = request.id
    ? await httpClient.put<BackendEdgeDto, BackendEdgeUpsertRequest>(`${graphBasePath}/edges/${encodeURIComponent(request.id)}`, backendRequest)
    : await httpClient.post<BackendEdgeDto, BackendEdgeUpsertRequest>(`${graphBasePath}/edges`, backendRequest);
  return withData(response, mapEdge(response.data, new Map()));
}

export function deleteKnowledgeGraphNode(nodeId: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`${graphBasePath}/nodes/${encodeURIComponent(nodeId)}?cascade=true`);
}

export function deleteKnowledgeGraphEdge(edgeId: string): Promise<ApiEnvelope<boolean>> {
  return httpClient.delete<boolean>(`${graphBasePath}/edges/${encodeURIComponent(edgeId)}`);
}

export async function analyzeKnowledgeGraphPath(request: KnowledgeGraphPathAnalysisRequest): Promise<ApiEnvelope<KnowledgeGraphPathAnalysisResultDto>> {
  const response = await httpClient.post<BackendPathResponseDto, unknown>(`${graphBasePath}/paths`, {
    fromNodeId: request.sourceNodeId,
    limit: 20,
    maxDepth: clampInteger(request.maxDepth, 1, 6, 4),
    relationTypes: request.relationType ? [request.relationType] : [],
    toNodeId: request.targetNodeId
  });
  return withData(response, mapPathResult(response.data));
}

export async function analyzeKnowledgeGraphImpact(request: KnowledgeGraphImpactAnalysisRequest): Promise<ApiEnvelope<KnowledgeGraphImpactAnalysisResultDto>> {
  const response = await httpClient.post<BackendImpactResponseDto, unknown>(`${graphBasePath}/impact`, {
    direction: toBackendDirection(request.direction),
    limit: 200,
    maxDepth: clampInteger(request.maxDepth, 1, 6, 4),
    nodeId: request.nodeId
  });
  return withData(response, mapImpactResult(response.data));
}

export async function importKnowledgeGraph(request: KnowledgeGraphImportRequest): Promise<ApiEnvelope<KnowledgeGraphImportResultDto>> {
  const backendRequest = toBackendImportRequest(request);
  const response = await httpClient.post<
    { createdCount?: number; skippedCount?: number; updatedCount?: number },
    BackendImportRequest
  >(`${graphBasePath}/import`, backendRequest);
  return withData(response, {
    createdCount: response.data.createdCount ?? 0,
    edgeCount: backendRequest.edges.length,
    nodeCount: backendRequest.nodes.length,
    skippedCount: response.data.skippedCount ?? 0,
    updatedCount: response.data.updatedCount ?? 0
  });
}

export async function exportKnowledgeGraph(request: KnowledgeGraphExportRequest): Promise<ApiEnvelope<KnowledgeGraphExportDto>> {
  const response = await httpClient.post<BackendExportDto, unknown>(`${graphBasePath}/export`, {
    includeEvidence: true,
    nodeTypes: request.nodeType ? [request.nodeType] : [],
    relationTypes: request.relationType ? [request.relationType] : [],
    sourceIds: request.sourceId ? [request.sourceId] : []
  });
  const fileName = `knowledge-graph-${new Date().toISOString().slice(0, 10)}.${request.format === 'mermaid' ? 'mmd' : 'json'}`;
  return withData(response, {
    content: request.format === 'mermaid' ? toMermaid(response.data) : JSON.stringify(response.data, null, 2),
    fileName,
    mimeType: request.format === 'mermaid' ? 'text/plain' : 'application/json'
  });
}

export async function rebuildKnowledgeGraph(query: KnowledgeGraphQuery): Promise<ApiEnvelope<KnowledgeGraphTaskDto>> {
  const response = await httpClient.post<BackendBuildJobDto, unknown>(`${graphBasePath}/reindex`, {
    documentIds: [],
    mode: 'Rebuild',
    requestId: createRequestId(),
    sourceId: query.sourceId || null
  });
  return withData(response, mapTask(response.data));
}

function withData<TInput, TOutput>(response: ApiEnvelope<TInput>, data: TOutput): ApiEnvelope<TOutput> {
  return {
    ...response,
    data
  };
}

function toBackendQuery(query: KnowledgeGraphQuery) {
  return {
    depth: clampInteger(query.maxDepth, 1, 3, 1),
    keyword: query.keyword || null,
    limit: clampInteger(query.maxNodes, 1, 500, 200),
    nodeTypes: query.nodeType ? [query.nodeType] : [],
    relationTypes: query.relationType ? [query.relationType] : [],
    sourceIds: query.sourceId ? [query.sourceId] : []
  };
}

function toBackendNodeRequest(request: KnowledgeGraphNodeUpsertRequest): BackendNodeUpsertRequest {
  const metadata = {
    ...(request.metadata ?? {}),
    position: {
      x: request.positionX ?? 120,
      y: request.positionY ?? 120
    },
    status: request.status || 'Enabled',
    tags: request.tags ?? [],
    weight: request.weight ?? 1
  };
  return {
    displayName: request.title.trim(),
    metadataJson: JSON.stringify(metadata),
    nodeKey: request.nodeCode.trim(),
    nodeType: request.nodeType.trim(),
    sourceId: request.sourceId || null,
    summary: request.description || null
  };
}

function toBackendEdgeRequest(request: KnowledgeGraphEdgeUpsertRequest): BackendEdgeUpsertRequest {
  const metadata = {
    ...(request.metadata ?? {}),
    relationCode: request.relationCode || null,
    status: request.status || 'Enabled',
    title: request.title || null
  };
  return {
    evidenceText: request.description || null,
    fromNodeId: request.sourceNodeId,
    metadataJson: JSON.stringify(metadata),
    relationType: request.relationType.trim(),
    sourceId: null,
    toNodeId: request.targetNodeId,
    weight: clampDecimal(request.weight ?? 1, 0, 1)
  };
}

function mapOverview(input: BackendOverviewDto): KnowledgeGraphOverviewDto {
  const nodeCount = input.nodeCount ?? 0;
  const edgeCount = input.edgeCount ?? 0;
  const sourceCount = input.sourceCount ?? 0;
  const evidenceCount = input.evidenceCount ?? 0;
  const latestJob = input.latestJob ? mapTask(input.latestJob) : null;
  return {
    edgeCount,
    evidenceCount,
    healthStatus: latestJob?.status === 'Failed'
      ? translateGraph('ai.knowledgeGraph.health.warning')
      : translateGraph('ai.knowledgeGraph.health.healthy'),
    lastUpdatedAt: input.lastUpdatedTime ?? latestJob?.completedAt ?? null,
    latestJob,
    metrics: [
      { key: 'nodeCount', label: translateGraph('ai.knowledgeGraph.metric.nodeCount'), tone: 'info', value: nodeCount },
      { key: 'edgeCount', label: translateGraph('ai.knowledgeGraph.metric.edgeCount'), tone: 'success', value: edgeCount },
      { key: 'sourceCount', label: translateGraph('ai.knowledgeGraph.metric.sourceCount'), tone: 'neutral', value: sourceCount },
      { key: 'evidenceCount', label: translateGraph('ai.knowledgeGraph.metric.evidenceCount'), tone: 'warning', value: evidenceCount }
    ],
    nodeCount,
    sourceCount,
    summary: latestJob
      ? formatMessage(translateGraph('ai.knowledgeGraph.summary.latestJob'), { status: latestJob.status })
      : translateGraph('ai.knowledgeGraph.summary.empty')
  };
}

function mapSnapshot(input: BackendGraphResponseDto): KnowledgeGraphSnapshotDto {
  const nodesInput = input.nodes ?? [];
  const edgesInput = input.edges ?? [];
  const nodeLabelMap = new Map(nodesInput.map((node) => [node.id, node.displayName || node.nodeKey || node.id]));
  const degreeMap = new Map<string, number>();
  for (const edge of edgesInput) {
    if (edge.fromNodeId) {
      degreeMap.set(edge.fromNodeId, (degreeMap.get(edge.fromNodeId) ?? 0) + 1);
    }
    if (edge.toNodeId) {
      degreeMap.set(edge.toNodeId, (degreeMap.get(edge.toNodeId) ?? 0) + 1);
    }
  }

  const nodes = nodesInput.map((node, index) => mapNode(node, degreeMap, index));
  const edges = edgesInput.map((edge) => mapEdge(edge, nodeLabelMap));
  return {
    edgeTotal: input.totalEdges ?? edges.length,
    edges,
    generatedAt: new Date().toISOString(),
    nodeTotal: input.totalNodes ?? nodes.length,
    nodes,
    totalEdges: input.totalEdges ?? edges.length,
    totalNodes: input.totalNodes ?? nodes.length,
    truncateReason: input.truncated ? translateGraph('ai.knowledgeGraph.truncateReason') : null,
    truncated: input.truncated === true
  };
}

function mapNode(input: BackendNodeDto, degreeMap: Map<string, number>, index: number): KnowledgeGraphNodeDto {
  const metadata = parseJsonObject(input.metadataJson);
  const position = readPosition(metadata, index);
  const nodeCode = input.nodeKey || input.id;
  return {
    degree: degreeMap.get(input.id) ?? 0,
    description: input.summary ?? '',
    id: input.id,
    isVirtual: false,
    label: input.displayName || nodeCode,
    metadata,
    nodeCode,
    nodeType: input.nodeType || 'term',
    positionX: position.x,
    positionY: position.y,
    sourceId: input.sourceId ?? '',
    sourceName: input.sourceName ?? '',
    status: readString(metadata.status, 'Enabled'),
    tags: readStringArray(metadata.tags),
    title: input.displayName || nodeCode,
    weight: readNumber(metadata.weight, 1)
  };
}

function mapEdge(input: BackendEdgeDto, nodeLabelMap: Map<string, string>): KnowledgeGraphEdgeDto {
  const metadata = parseJsonObject(input.metadataJson);
  const relationType = input.relationType || 'related';
  const title = readString(metadata.title, relationType);
  const source = input.fromNodeId ?? '';
  const target = input.toNodeId ?? '';
  return {
    description: input.evidenceText ?? '',
    id: input.id,
    label: title,
    metadata,
    relationCode: readString(metadata.relationCode, input.id),
    relationType,
    source,
    sourceLabel: nodeLabelMap.get(source) ?? source,
    sourceNodeId: source,
    status: readString(metadata.status, 'Enabled'),
    target,
    targetLabel: nodeLabelMap.get(target) ?? target,
    targetNodeId: target,
    title,
    weight: Number(input.weight ?? 1)
  };
}

function mapTask(input: BackendBuildJobDto): KnowledgeGraphTaskDto {
  const status = input.status || 'Pending';
  return {
    completedAt: input.finishedAt ?? null,
    createdTime: input.startedAt ?? null,
    errorCode: input.errorCode ?? null,
    errorMessage: input.errorMessage ?? null,
    id: input.id,
    progressPercent: input.progress ?? 0,
    status,
    summary: input.errorMessage || formatMessage(translateGraph('ai.knowledgeGraph.task.summary'), {
      createdCount: input.createdCount ?? 0,
      skippedCount: input.skippedCount ?? 0,
      updatedCount: input.updatedCount ?? 0
    }),
    taskCode: input.id,
    taskName: input.sourceId
      ? formatMessage(translateGraph('ai.knowledgeGraph.task.rebuildSource'), { sourceId: input.sourceId })
      : translateGraph('ai.knowledgeGraph.task.rebuildGraph'),
    taskType: 'Reindex'
  };
}

function mapPathResult(input: BackendPathResponseDto): KnowledgeGraphPathAnalysisResultDto {
  const snapshot = mapSnapshot({ edges: input.edges ?? [], nodes: input.nodes ?? [], truncated: input.truncated });
  return {
    edges: snapshot.edges,
    nodes: snapshot.nodes,
    paths: (input.paths ?? []).map((path, index) => ({
      edgeIds: path.edgeIds ?? [],
      edges: path.edgeIds ?? [],
      id: `path_${index + 1}`,
      nodeIds: path.nodeIds ?? [],
      nodes: path.nodeIds ?? [],
      score: path.edgeIds?.length ? 1 / path.edgeIds.length : 0,
      summary: path.nodeIds?.length
        ? formatMessage(translateGraph('ai.knowledgeGraph.path.summary'), { count: path.nodeIds.length })
        : translateGraph('ai.knowledgeGraph.path.empty')
    })),
    truncated: input.truncated === true
  };
}

function mapImpactResult(input: BackendImpactResponseDto): KnowledgeGraphImpactAnalysisResultDto {
  return {
    results: [
      {
        affectedEdges: (input.edges ?? []).map((edge) => edge.id),
        affectedNodes: (input.nodes ?? []).map((node) => node.id),
        blastRadius: input.nodes?.length ?? 0,
        id: input.rootNodeId || 'impact',
        recommendation: input.truncated ? translateGraph('ai.knowledgeGraph.impact.recommendation') : '',
        riskLevel: input.truncated ? 'warning' : 'normal',
        summary: formatMessage(translateGraph('ai.knowledgeGraph.impact.summary'), {
          edges: input.edges?.length ?? 0,
          nodes: input.nodes?.length ?? 0
        })
      }
    ]
  };
}

function toBackendImportRequest(request: KnowledgeGraphImportRequest): BackendImportRequest {
  const parsed = parseImportContent(request.content);
  const nodeRecords = Array.isArray(parsed.nodes) ? parsed.nodes.map(toRecord) : [];
  const nodes = nodeRecords.map(toBackendImportNode).filter((node): node is BackendNodeUpsertRequest => node !== null);
  const nodeKeyById = new Map<string, string>();
  nodeRecords.forEach((node) => {
    const id = readString(node.id, '');
    const key = readString(node.nodeKey ?? node.nodeCode ?? node.key, '');
    if (id && key) {
      nodeKeyById.set(id, key);
    }
  });

  const edges = (Array.isArray(parsed.edges) ? parsed.edges.map(toRecord) : [])
    .map((edge) => toBackendImportEdge(edge, nodeKeyById))
    .filter((edge): edge is BackendImportRequest['edges'][number] => edge !== null);
  return {
    edges,
    mode: readString(parsed.mode, 'Upsert'),
    nodes,
    requestId: readString(parsed.requestId, createRequestId()),
    sourceId: readNullableString(parsed.sourceId)
  };
}

function translateGraph(key: string): string {
  return translateCurrentLocale(key);
}

function toBackendImportNode(record: Record<string, unknown>): BackendNodeUpsertRequest | null {
  const nodeKey = readString(record.nodeKey ?? record.nodeCode ?? record.key, '');
  const nodeType = readString(record.nodeType ?? record.type, 'term');
  const displayName = readString(record.displayName ?? record.title ?? record.label ?? record.name, nodeKey);
  if (!nodeKey || !displayName) {
    return null;
  }

  return {
    displayName,
    documentId: readNullableString(record.documentId),
    metadataJson: stringifyMetadata(record.metadataJson, record.metadata),
    nodeKey,
    nodeType,
    sourceId: readNullableString(record.sourceId),
    summary: readNullableString(record.summary ?? record.description)
  };
}

function toBackendImportEdge(record: Record<string, unknown>, nodeKeyById: Map<string, string>): BackendImportRequest['edges'][number] | null {
  const fromNodeId = readString(record.fromNodeId ?? record.sourceNodeId ?? record.source, '');
  const toNodeId = readString(record.toNodeId ?? record.targetNodeId ?? record.target, '');
  const fromNodeKey = readNullableString(record.fromNodeKey) ?? nodeKeyById.get(fromNodeId) ?? null;
  const toNodeKey = readNullableString(record.toNodeKey) ?? nodeKeyById.get(toNodeId) ?? null;
  const relationType = readString(record.relationType ?? record.type, 'related');
  if ((!fromNodeId && !fromNodeKey) || (!toNodeId && !toNodeKey)) {
    return null;
  }

  return {
    evidenceText: readNullableString(record.evidenceText ?? record.description ?? record.summary),
    fromNodeId,
    fromNodeKey,
    metadataJson: stringifyMetadata(record.metadataJson, record.metadata),
    relationType,
    sourceId: readNullableString(record.sourceId),
    toNodeId,
    toNodeKey,
    weight: clampDecimal(readNumber(record.weight, 1), 0, 1)
  };
}

function toMermaid(input: BackendExportDto): string {
  const nodes = input.nodes ?? [];
  const lines = ['graph LR'];
  for (const node of nodes) {
    const id = sanitizeMermaidId(node.id);
    lines.push(`  ${id}["${sanitizeMermaidLabel(node.displayName || node.nodeKey || node.id)}"]`);
  }
  for (const edge of input.edges ?? []) {
    const from = sanitizeMermaidId(edge.fromNodeId || '');
    const to = sanitizeMermaidId(edge.toNodeId || '');
    if (!from || !to) {
      continue;
    }
    const label = sanitizeMermaidLabel(edge.relationType || 'related');
    lines.push(`  ${from} -->|${label}| ${to}`);
  }
  return lines.join('\n');
}

function toBackendDirection(direction?: 'both' | 'incoming' | 'outgoing'): string {
  if (direction === 'incoming') {
    return 'Incoming';
  }
  if (direction === 'outgoing') {
    return 'Outgoing';
  }
  return 'Both';
}

function parseImportContent(content: string): Record<string, unknown> {
  try {
    return toRecord(JSON.parse(content) as unknown);
  } catch {
    return {};
  }
}

function parseJsonObject(value?: string | null): Record<string, unknown> {
  if (!value) {
    return {};
  }
  try {
    return toRecord(JSON.parse(value) as unknown);
  } catch {
    return {};
  }
}

function stringifyMetadata(metadataJson: unknown, metadata: unknown): string | null {
  if (typeof metadataJson === 'string' && metadataJson.trim()) {
    return metadataJson.trim();
  }
  const record = toRecord(metadata);
  return Object.keys(record).length > 0 ? JSON.stringify(record) : null;
}

function readPosition(metadata: Record<string, unknown>, index: number): { x: number; y: number } {
  const position = toRecord(metadata.position);
  return {
    x: readNumber(position.x, 80 + (index % 5) * 220),
    y: readNumber(position.y, 80 + Math.floor(index / 5) * 140)
  };
}

function readString(value: unknown, fallback: string): string {
  return typeof value === 'string' && value.trim() ? value.trim() : fallback;
}

function readNullableString(value: unknown): string | null {
  const result = readString(value, '');
  return result || null;
}

function readNumber(value: unknown, fallback: number): number {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }
  return fallback;
}

function readStringArray(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value.map((item) => String(item)).filter(Boolean);
  }
  if (typeof value === 'string' && value.trim()) {
    return value.split(',').map((item) => item.trim()).filter(Boolean);
  }
  return [];
}

function toRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function clampInteger(value: unknown, min: number, max: number, fallback: number): number {
  const numberValue = readNumber(value, fallback);
  return Math.max(min, Math.min(max, Math.round(numberValue)));
}

function clampDecimal(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function createRequestId(): string {
  return window.crypto?.randomUUID?.() ?? `kg-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function sanitizeMermaidId(value: string): string {
  return value ? `N_${value.replace(/[^a-zA-Z0-9_]/g, '_')}` : '';
}

function sanitizeMermaidLabel(value: string): string {
  return value.replace(/"/g, '\\"').replace(/\r?\n/g, ' ');
}
