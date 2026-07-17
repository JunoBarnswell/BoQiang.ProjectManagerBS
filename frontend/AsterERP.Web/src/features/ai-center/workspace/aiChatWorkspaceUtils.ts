import type {
  AiChatStreamRequest,
  AiKernelFunctionDefinitionDto,
  AiMessageDto,
  AiModelConfigDto
} from '.././api/aiCenter.api';

import type { AiMessageDraft, StreamSettings, WorkMode } from './aiChatWorkspaceTypes';

export function buildLocalMessage(conversationId: string, role: AiMessageDto['role'], content: string, pending = false): AiMessageDraft {
  return {
    content,
    conversationId,
    createdTime: new Date().toISOString(),
    id: `local-${role}-${Date.now()}-${Math.random().toString(16).slice(2)}`,
    pending,
    role,
    seq: Date.now(),
    status: pending ? 'Running' : 'Succeeded',
    tokenCount: 0
  };
}

export function buildStreamRequest(
  content: string,
  settings: StreamSettings,
  workMode: WorkMode,
  taskPlanId: string | null,
  enabledToolCodes: string[]
): AiChatStreamRequest {
  const requestId = typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}`;
  const enabledToolDomains = workMode === 'Agent' ? settings.enabledToolDomains : [];
  const runtimeToolCodes = workMode === 'Agent' ? enabledToolCodes : [];
  return {
    agentProfileIds: [],
    clientMessageId: requestId,
    content,
    coordinatorAgentProfileId: null,
    enabledToolCodes: runtimeToolCodes,
    enabledToolDomains,
    extraParameters: {
      enabledToolCodes: runtimeToolCodes,
      enabledToolDomains
    },
    idempotencyKey: requestId,
    mode: 'Single',
    modelConfigId: settings.modelConfigId,
    promptTemplateId: settings.promptTemplateId || null,
    requireToolConfirmation: true,
    taskPlanId: workMode === 'Agent' ? taskPlanId : null,
    workMode
  };
}

export function resolveSelectedToolCodes(selectedDomains: string[], selectedToolCodes: string[], tools: AiKernelFunctionDefinitionDto[]) {
  const domains = new Set(selectedDomains);
  const knownCodes = new Set(tools.map((tool) => tool.toolCode));
  const codes = new Set(
    tools
      .filter((tool) => domains.has(tool.toolDomain) || domains.has(tool.toolCode.split('.')[0] ?? ''))
      .map((tool) => tool.toolCode)
  );

  for (const toolCode of selectedToolCodes) {
    if (knownCodes.has(toolCode)) {
      codes.add(toolCode);
    }
  }

  return Array.from(codes);
}

export function applyModelDefaults(
  modelConfigId: string,
  models: AiModelConfigDto[],
  setSettings: (updater: (current: StreamSettings) => StreamSettings) => void
) {
  const model = models.find((item) => item.id === modelConfigId);
  setSettings((current) => ({
    ...current,
    modelConfigId: model?.id ?? modelConfigId
  }));
}

export function readText(data: unknown, key: string): string {
  if (typeof data === 'string') {
    return data;
  }

  if (!data || typeof data !== 'object') {
    return '';
  }

  const value = (data as Record<string, unknown>)[key];
  return typeof value === 'string' ? value : '';
}

export function readNumber(data: unknown, key: string): number {
  if (!data || typeof data !== 'object') {
    return 0;
  }

  const value = (data as Record<string, unknown>)[key];
  return typeof value === 'number' ? value : Number(value) || 0;
}

export function formatTime(value?: string | null): string {
  if (!value) {
    return '';
  }

  return new Intl.DateTimeFormat('zh-CN', {
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    month: '2-digit'
  }).format(new Date(value));
}
