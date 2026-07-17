export type FlowiseChatflowType = 'CHATFLOW' | 'AGENTFLOW' | 'MULTIAGENT' | 'ASSISTANT';

export interface FlowiseChatflowQuery {
  category?: string | null;
  deployed?: boolean | null;
  keyword?: string | null;
  pageIndex: number;
  pageSize: number;
  type?: FlowiseChatflowType | null;
  workspaceId?: string | null;
}

export interface FlowiseChatflowDto {
  analytic: string;
  apiConfig: string;
  apikeyid?: string | null;
  category?: string | null;
  chatbotConfig: string;
  createdDate: string;
  deployed: boolean;
  flowData: string;
  followUpPrompts: string;
  id: string;
  isPublic: boolean;
  mcpServerConfig: string;
  metadataJson: string;
  name: string;
  speechToText: string;
  textToSpeech: string;
  type: FlowiseChatflowType;
  updatedDate?: string | null;
  webhookSecretConfigured: boolean;
  workspaceId?: string | null;
}

export interface FlowiseChatflowUpsertRequest {
  analytic?: string | null;
  apiConfig?: string | null;
  apikeyid?: string | null;
  category?: string | null;
  chatbotConfig?: string | null;
  deployed: boolean;
  flowData: string;
  followUpPrompts?: string | null;
  isPublic: boolean;
  mcpServerConfig?: string | null;
  metadataJson?: string | null;
  name: string;
  speechToText?: string | null;
  textToSpeech?: string | null;
  type: FlowiseChatflowType;
  webhookSecret?: string | null;
  workspaceId?: string | null;
}

export interface FlowiseMcpServerConfigDto {
  chatflowId: string;
  description: string;
  enabled: boolean;
  endpointPath: string;
  hasExistingConfig: boolean;
  token: string;
  toolName: string;
}

export interface FlowiseMcpServerUpsertRequest {
  description: string;
  enabled: boolean;
  toolName: string;
}

export interface FlowiseScheduleStatusDto {
  cronExpression?: string | null;
  defaultFormJson?: string | null;
  defaultInput?: string | null;
  enabled: boolean;
  endDate?: string | null;
  isScheduled: boolean;
  lastRunAt?: string | null;
  nextRunAt?: string | null;
  scheduleInputMode: string;
  timezone?: string | null;
}

export interface FlowiseScheduleTriggerLogDto {
  completedAt?: string | null;
  error?: string | null;
  executionId?: string | null;
  id: string;
  outputJson?: string | null;
  scheduledAt: string;
  startedAt?: string | null;
  status: string;
}
