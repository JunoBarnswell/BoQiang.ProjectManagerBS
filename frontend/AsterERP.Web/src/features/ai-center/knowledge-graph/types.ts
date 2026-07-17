import type {
  KnowledgeGraphEdgeDto,
  KnowledgeGraphEdgeUpsertRequest,
  KnowledgeGraphExportDto,
  KnowledgeGraphExportRequest,
  KnowledgeGraphImportRequest,
  KnowledgeGraphImportResultDto,
  KnowledgeGraphImpactAnalysisRequest,
  KnowledgeGraphImpactAnalysisResultDto,
  KnowledgeGraphNodeDto,
  KnowledgeGraphNodeUpsertRequest,
  KnowledgeGraphOverviewDto,
  KnowledgeGraphPathAnalysisRequest,
  KnowledgeGraphPathAnalysisResultDto,
  KnowledgeGraphQuery,
  KnowledgeGraphSnapshotDto,
  KnowledgeGraphTaskDto,
  KnowledgeGraphTaskQuery
} from '../api/knowledgeGraph.api';

export type KnowledgeGraphApiEdge = KnowledgeGraphEdgeDto;
export type KnowledgeGraphApiExport = KnowledgeGraphExportDto;
export type KnowledgeGraphApiExportRequest = KnowledgeGraphExportRequest;
export type KnowledgeGraphApiImportRequest = KnowledgeGraphImportRequest;
export type KnowledgeGraphApiImportResult = KnowledgeGraphImportResultDto;
export type KnowledgeGraphApiImpactRequest = KnowledgeGraphImpactAnalysisRequest;
export type KnowledgeGraphApiImpactResult = KnowledgeGraphImpactAnalysisResultDto;
export type KnowledgeGraphApiNode = KnowledgeGraphNodeDto;
export type KnowledgeGraphApiNodeUpsertRequest = KnowledgeGraphNodeUpsertRequest;
export type KnowledgeGraphApiEdgeUpsertRequest = KnowledgeGraphEdgeUpsertRequest;
export type KnowledgeGraphApiOverview = KnowledgeGraphOverviewDto;
export type KnowledgeGraphApiPathRequest = KnowledgeGraphPathAnalysisRequest;
export type KnowledgeGraphApiPathResult = KnowledgeGraphPathAnalysisResultDto;
export type KnowledgeGraphApiQuery = KnowledgeGraphQuery;
export type KnowledgeGraphApiSnapshot = KnowledgeGraphSnapshotDto;
export type KnowledgeGraphApiTask = KnowledgeGraphTaskDto;
export type KnowledgeGraphApiTaskQuery = KnowledgeGraphTaskQuery;

export type KnowledgeGraphDirection = 'both' | 'incoming' | 'outgoing';
export type KnowledgeGraphPanelKey = 'details' | 'analysis' | 'tasks';
export type KnowledgeGraphSelectionKind = 'edge' | 'node';
export type KnowledgeGraphModalKey = 'edgeForm' | 'exchange' | 'nodeForm' | null;

export interface KnowledgeGraphFilterState {
  direction: KnowledgeGraphDirection;
  includeInactive: boolean;
  includeOrphans: boolean;
  keyword: string;
  maxDepth: number;
  maxNodes: number;
  nodeType: string;
  relationType: string;
  sourceId: string;
  status: string;
}

export interface KnowledgeGraphMetricView {
  key: string;
  label: string;
  tone: 'critical' | 'info' | 'neutral' | 'success' | 'warning';
  value: number | string;
}

export interface KnowledgeGraphOverviewView {
  metrics: KnowledgeGraphMetricView[];
  lastUpdatedAt: string | null;
  healthStatus: string;
  summary: string;
}

export interface KnowledgeGraphNodeView {
  degree: number;
  description: string;
  id: string;
  isVirtual: boolean;
  label: string;
  metadata: Record<string, unknown>;
  nodeCode: string;
  nodeType: string;
  position: { x: number; y: number };
  raw: KnowledgeGraphApiNode | null;
  sourceId: string;
  sourceName: string;
  status: string;
  tags: string[];
  weight: number;
}

export interface KnowledgeGraphEdgeView {
  description: string;
  id: string;
  label: string;
  metadata: Record<string, unknown>;
  raw: KnowledgeGraphApiEdge | null;
  relationCode: string;
  relationType: string;
  source: string;
  sourceLabel: string;
  status: string;
  target: string;
  targetLabel: string;
  weight: number;
}

export interface KnowledgeGraphSnapshotView {
  edgeTotal: number;
  edges: KnowledgeGraphEdgeView[];
  generatedAt: string | null;
  nodeTotal: number;
  nodes: KnowledgeGraphNodeView[];
  truncateReason: string | null;
  truncated: boolean;
}

export interface KnowledgeGraphTaskView {
  completedAt: string | null;
  createdTime: string | null;
  errorMessage: string | null;
  id: string;
  progressPercent: number;
  status: string;
  summary: string;
  taskCode: string;
  taskName: string;
  taskType: string;
}

export interface KnowledgeGraphNodeFormValue {
  description: string;
  id: string;
  metadataJson: string;
  nodeCode: string;
  nodeType: string;
  positionX: number;
  positionY: number;
  sourceId: string;
  status: string;
  tags: string;
  title: string;
  weight: number;
}

export interface KnowledgeGraphEdgeFormValue {
  description: string;
  id: string;
  metadataJson: string;
  relationCode: string;
  relationType: string;
  sourceNodeId: string;
  status: string;
  targetNodeId: string;
  title: string;
  weight: number;
}

export interface KnowledgeGraphPathDraft {
  maxDepth: number;
  relationType: string;
  sourceNodeId: string;
  targetNodeId: string;
}

export interface KnowledgeGraphImpactDraft {
  direction: KnowledgeGraphDirection;
  maxDepth: number;
  nodeId: string;
  relationType: string;
}

export interface KnowledgeGraphExchangeDraft {
  fileName: string;
  format: 'json' | 'mermaid';
  importContent: string;
  mode: 'export' | 'import';
}

export interface KnowledgeGraphPathView {
  edges: string[];
  id: string;
  nodes: string[];
  riskLevel: string;
  score: number;
  summary: string;
}

export interface KnowledgeGraphImpactView {
  affectedEdges: string[];
  affectedNodes: string[];
  blastRadius: number;
  id: string;
  recommendation: string;
  riskLevel: string;
  summary: string;
}

export interface KnowledgeGraphSelection {
  id: string;
  kind: KnowledgeGraphSelectionKind;
}

export interface KnowledgeGraphOption {
  label: string;
  value: string;
}
