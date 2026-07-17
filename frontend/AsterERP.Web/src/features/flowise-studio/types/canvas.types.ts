import type { Edge, Node, Viewport } from '@xyflow/react';

import type { FlowiseNodeAnchor, FlowiseNodeDefinitionDto, FlowiseNodeInputParam } from './node.types';

export type FlowiseCanvasMode = 'chatflow' | 'agentflow' | 'agentflow-v2' | 'marketplace' | 'marketplace-template';

export interface FlowiseCanvasNodeData extends Record<string, unknown> {
  category?: string;
  config?: Record<string, unknown>;
  description?: string;
  displayName: string;
  icon?: string | null;
  inputAnchors?: FlowiseNodeAnchor[];
  inputs?: Record<string, unknown>;
  inputParams?: FlowiseNodeInputParam[];
  nodeDefinition?: FlowiseNodeDefinitionDto | null;
  nodeType: string;
  outputAnchors?: FlowiseNodeAnchor[];
  status?: 'ERROR' | 'FINISHED' | 'INPROGRESS' | 'TERMINATED' | string;
  error?: string | null;
  stickyNote?: boolean;
  onStickyTextChange?: (nodeId: string, value: string) => void;
  version?: number;
}

export interface FlowiseCanvasEdgeData extends Record<string, unknown> {
  conditionLabel?: string | null;
  config?: Record<string, unknown>;
  edgeLabel?: string | null;
  humanInputLabel?: string | null;
  isHumanInput?: boolean;
  isWithinIterationNode?: boolean;
  label?: string | null;
  onDeleteEdge?: (edgeId: string) => void;
  sourceColor?: string | null;
  targetColor?: string | null;
}

export type FlowiseCanvasNode = Node<FlowiseCanvasNodeData>;
export type FlowiseCanvasEdge = Edge<FlowiseCanvasEdgeData>;

export interface FlowiseFlowData {
  edges: FlowiseCanvasEdge[];
  nodes: FlowiseCanvasNode[];
  viewport?: Viewport;
}

export interface FlowiseCanvasDto {
  createdTime?: string;
  flowData: string;
  flowType: string;
  id: string;
  resourceId: string;
  updatedTime?: string | null;
  validation?: FlowiseCanvasValidationResult | null;
}

export interface FlowiseCanvasUpsertRequest {
  flowData: string;
  flowType?: string | null;
  resourceId: string;
}

export interface FlowiseCanvasValidationIssue {
  code: string;
  edgeId?: string | null;
  message: string;
  nodeId?: string | null;
  severity: 'error' | 'warning';
}

export interface FlowiseCanvasValidationResult {
  issues: FlowiseCanvasValidationIssue[];
  valid: boolean;
}
