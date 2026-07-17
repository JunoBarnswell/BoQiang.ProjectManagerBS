import type { FlowiseExecutionDto } from './shared.types';

export interface FlowisePredictionRequest {
  chatId?: string | null;
  form?: Record<string, unknown>;
  question: string;
  resourceId: string;
  sessionId?: string | null;
  uploads?: FlowisePredictionUpload[];
}

export interface FlowisePredictionListQuery {
  chatId?: string | null;
  pageIndex?: number;
  pageSize?: number;
  resourceId: string;
}

export interface FlowisePredictionUpload {
  data: string;
  mime: string;
  name: string;
  type: string;
}

export interface FlowiseSourceDocumentDto {
  content: string;
  metadataJson: string;
  score?: number | null;
  sourceId?: string | null;
}

export interface FlowiseUsedToolDto {
  inputJson: string;
  outputJson: string;
  tool: string;
}

export interface FlowiseAgentReasoningDto {
  agentName: string;
  artifactsJson: string;
  instructions: string;
  messages: string[];
  nextAgent?: string | null;
  nodeName: string;
  sourceDocuments: FlowiseSourceDocumentDto[];
  stateJson: string;
  usedTools: FlowiseUsedToolDto[];
}

export interface FlowiseAgentExecutedNodeDto {
  dataJson: string;
  nodeId: string;
  nodeLabel: string;
  previousNodeIds: string[];
  status: string;
}

export interface FlowiseChatMessageDto {
  actionJson?: string | null;
  agentExecutedData: FlowiseAgentExecutedNodeDto[];
  agentReasoning: FlowiseAgentReasoningDto[];
  artifactsJson: string;
  chatId?: string | null;
  createdTime: string;
  executionId?: string | null;
  feedback?: FlowiseFeedbackDto | null;
  fileUploads: FlowisePredictionUpload[];
  followUpPrompts: string[];
  id: string;
  message: string;
  role: 'user' | 'assistant' | 'system';
  sourceDocuments: FlowiseSourceDocumentDto[];
  usedTools: FlowiseUsedToolDto[];
}

export interface FlowiseFeedbackDto {
  id: string;
  messageId: string;
  rating: 'up' | 'down';
  reason?: string | null;
}

export interface FlowiseLeadDto {
  contactJson: string;
  createdTime: string;
  id: string;
  resourceId: string;
}

export interface FlowisePredictionResponse {
  execution: FlowiseExecutionDto;
  message: FlowiseChatMessageDto;
}

export interface FlowisePredictionStreamErrorPayload {
  errorCode?: string | null;
  message?: string | null;
  traceId?: string | null;
}

export interface FlowisePredictionStreamEvent {
  data?: unknown;
  event:
    | 'abort'
    | 'action'
    | 'agentFlowEvent'
    | 'agentFlowExecutedData'
    | 'agentReasoning'
    | 'artifacts'
    | 'end'
    | 'error'
    | 'metadata'
    | 'nextAgentFlow'
    | 'sourceDocuments'
    | 'start'
    | 'token'
    | 'usedTools';
}
