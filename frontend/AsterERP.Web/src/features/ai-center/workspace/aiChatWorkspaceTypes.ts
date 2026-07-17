import type {
  AiMessageDto
} from '.././api/aiCenter.api';

export type AiMessageDraft = AiMessageDto & { pending?: boolean };
export type WorkMode = 'Agent' | 'Ask' | 'Plan';

export interface StreamSettings {
  enabledToolCodes: string[];
  enabledToolDomains: string[];
  modelConfigId: string;
  promptTemplateId: string;
}

export const defaultSettings: StreamSettings = {
  enabledToolCodes: [],
  enabledToolDomains: ['workflow'],
  modelConfigId: '',
  promptTemplateId: ''
};
