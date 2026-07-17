import { create } from 'zustand';

interface AiChatStoreState {
  activeConversationId: string | null;
  draftByConversation: Record<string, string>;
  expandedTaskIds: Record<string, boolean>;
  rightPanelOpen: boolean;
  streamController: AbortController | null;
  streamingRunId: string | null;
  workMode: 'Agent' | 'Ask' | 'Plan';
  setActiveConversationId: (conversationId: string | null) => void;
  setDraft: (conversationId: string, draft: string) => void;
  setWorkMode: (workMode: 'Agent' | 'Ask' | 'Plan') => void;
  toggleTaskExpanded: (taskId: string) => void;
  setRightPanelOpen: (open: boolean) => void;
  setStreamController: (controller: AbortController | null) => void;
  setStreamingRunId: (runId: string | null) => void;
}

export const useAiChatStore = create<AiChatStoreState>((set) => ({
  activeConversationId: null,
  draftByConversation: {},
  expandedTaskIds: {},
  rightPanelOpen: true,
  streamController: null,
  streamingRunId: null,
  workMode: 'Ask',
  setActiveConversationId: (conversationId) => set({ activeConversationId: conversationId }),
  setDraft: (conversationId, draft) =>
    set((state) => ({
      draftByConversation: {
        ...state.draftByConversation,
        [conversationId]: draft
      }
    })),
  setWorkMode: (workMode) => set({ workMode }),
  toggleTaskExpanded: (taskId) =>
    set((state) => ({
      expandedTaskIds: {
        ...state.expandedTaskIds,
        [taskId]: !state.expandedTaskIds[taskId]
      }
    })),
  setRightPanelOpen: (rightPanelOpen) => set({ rightPanelOpen }),
  setStreamController: (streamController) => set({ streamController }),
  setStreamingRunId: (streamingRunId) => set({ streamingRunId })
}));
