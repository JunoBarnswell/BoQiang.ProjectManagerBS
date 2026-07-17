import { create } from 'zustand';

import type { ImConversation, ImMessage, ImPresenceChanged, ImUnreadSummary } from '../types/imTypes';

interface ImState {
  activeConversationId?: string;
  connectionStatus: 'connecting' | 'connected' | 'reconnecting' | 'disconnected';
  conversations: ImConversation[];
  drawerOpen: boolean;
  messagesByConversation: Record<string, ImMessage[]>;
  presenceByUserId: Record<string, ImPresenceChanged>;
  unreadSummary: ImUnreadSummary;
  appendMessage: (message: ImMessage) => void;
  closeDrawer: () => void;
  mergeConversation: (conversation: ImConversation) => void;
  openDrawer: () => void;
  prependMessages: (conversationId: string, messages: ImMessage[]) => void;
  receiveRealtimeMessage: (message: ImMessage, currentUserId: string) => void;
  replaceSendingMessage: (conversationId: string, clientMessageId: string, message: ImMessage) => void;
  setActiveConversationId: (conversationId?: string) => void;
  setConnectionStatus: (status: ImState['connectionStatus']) => void;
  setConversations: (conversations: ImConversation[]) => void;
  setMessages: (conversationId: string, messages: ImMessage[]) => void;
  setUnreadSummary: (summary: ImUnreadSummary) => void;
  upsertSendingMessage: (message: ImMessage) => void;
  updatePresence: (presence: ImPresenceChanged) => void;
}

const emptyUnread: ImUnreadSummary = { conversationUnreadCounts: {}, totalUnread: 0 };

export const useImStore = create<ImState>((set, get) => ({
  activeConversationId: undefined,
  appendMessage: (message) => {
    const existing = get().messagesByConversation[message.conversationId] ?? [];
    if (existing.some((item) => item.id === message.id || (message.clientMessageId && item.clientMessageId === message.clientMessageId))) {
      return;
    }

    set({
      messagesByConversation: {
        ...get().messagesByConversation,
        [message.conversationId]: [...existing, message]
      }
    });
  },
  closeDrawer: () => set({ drawerOpen: false }),
  connectionStatus: 'disconnected',
  conversations: [],
  drawerOpen: false,
  mergeConversation: (conversation) => {
    const current = get().conversations.filter((item) => item.id !== conversation.id);
    set({ conversations: [conversation, ...current] });
  },
  messagesByConversation: {},
  openDrawer: () => set({ drawerOpen: true }),
  presenceByUserId: {},
  prependMessages: (conversationId, messages) => {
    const existing = get().messagesByConversation[conversationId] ?? [];
    const existingIds = new Set(existing.map((item) => item.id));
    set({
      messagesByConversation: {
        ...get().messagesByConversation,
        [conversationId]: [...messages.filter((item) => !existingIds.has(item.id)), ...existing]
      }
    });
  },
  receiveRealtimeMessage: (message, currentUserId) => {
    const state = get();
    const existing = state.messagesByConversation[message.conversationId] ?? [];
    const exists = existing.some((item) => item.id === message.id || (message.clientMessageId && item.clientMessageId === message.clientMessageId));
    const active = state.activeConversationId === message.conversationId;
    const incoming = message.senderUserId !== currentUserId;
    const shouldIncrementUnread = incoming && !active && !exists;
    const currentUnread = state.unreadSummary.conversationUnreadCounts[message.conversationId] ?? 0;
    const conversations = state.conversations.map((conversation) => (
      conversation.id === message.conversationId
        ? {
            ...conversation,
            lastMessageAt: message.sentAt,
            lastMessageId: message.id,
            lastMessagePreview: message.content,
            unreadCount: shouldIncrementUnread ? conversation.unreadCount + 1 : conversation.unreadCount
          }
        : conversation
    ));

    set({
      conversations,
      messagesByConversation: exists
        ? state.messagesByConversation
        : {
            ...state.messagesByConversation,
            [message.conversationId]: [...existing, message]
          },
      unreadSummary: shouldIncrementUnread
        ? {
            conversationUnreadCounts: {
              ...state.unreadSummary.conversationUnreadCounts,
              [message.conversationId]: currentUnread + 1
            },
            totalUnread: state.unreadSummary.totalUnread + 1
          }
        : state.unreadSummary
    });
  },
  replaceSendingMessage: (conversationId, clientMessageId, message) => {
    const existing = get().messagesByConversation[conversationId] ?? [];
    set({
      messagesByConversation: {
        ...get().messagesByConversation,
        [conversationId]: existing.map((item) => (item.clientMessageId === clientMessageId ? message : item))
      }
    });
  },
  setActiveConversationId: (conversationId) => set({ activeConversationId: conversationId }),
  setConnectionStatus: (connectionStatus) => set({ connectionStatus }),
  setConversations: (conversations) => set({ conversations }),
  setMessages: (conversationId, messages) => set({ messagesByConversation: { ...get().messagesByConversation, [conversationId]: messages } }),
  setUnreadSummary: (unreadSummary) => set({ unreadSummary }),
  unreadSummary: emptyUnread,
  updatePresence: (presence) => set({ presenceByUserId: { ...get().presenceByUserId, [presence.userId]: presence } }),
  upsertSendingMessage: (message) => {
    const existing = get().messagesByConversation[message.conversationId] ?? [];
    set({
      messagesByConversation: {
        ...get().messagesByConversation,
        [message.conversationId]: [...existing.filter((item) => item.clientMessageId !== message.clientMessageId), message]
      }
    });
  }
}));
