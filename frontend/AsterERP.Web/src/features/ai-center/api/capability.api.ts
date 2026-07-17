import { aiChatApi } from './aiCenter.api';

export const aiCapabilityApi = {
  agents: aiChatApi.agents,
  capabilities: aiChatApi.capabilities,
  knowledge: aiChatApi.knowledge,
  models: aiChatApi.models,
  prompts: aiChatApi.prompts,
  providers: aiChatApi.providers,
  tools: aiChatApi.tools
};
