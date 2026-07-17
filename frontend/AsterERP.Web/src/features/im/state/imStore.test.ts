import { beforeEach, describe, expect, it } from 'vitest';

import type { ImConversation, ImMessage } from '../types/imTypes';

import { useImStore } from './imStore';

describe('imStore', () => {
  beforeEach(() => {
    useImStore.setState({
      activeConversationId: undefined,
      connectionStatus: 'disconnected',
      conversations: [],
      drawerOpen: false,
      messagesByConversation: {},
      presenceByUserId: {},
      unreadSummary: { conversationUnreadCounts: {}, totalUnread: 0 }
    });
  });

  it('does not increase unread when realtime message belongs to current conversation', () => {
    useImStore.setState({
      activeConversationId: 'conversation-1',
      conversations: [createConversation('conversation-1')]
    });

    useImStore.getState().receiveRealtimeMessage(createMessage('message-1', 'conversation-1', 'user-b'), 'user-a');

    const state = useImStore.getState();
    expect(state.messagesByConversation['conversation-1']).toHaveLength(1);
    expect(state.unreadSummary.totalUnread).toBe(0);
    expect(state.conversations[0]?.unreadCount).toBe(0);
    expect(state.conversations[0]?.lastMessagePreview).toBe('hello');
  });

  it('increases unread once for non-current incoming message and ignores duplicates', () => {
    useImStore.setState({
      activeConversationId: 'conversation-1',
      conversations: [createConversation('conversation-2')]
    });

    const message = createMessage('message-2', 'conversation-2', 'user-b');
    useImStore.getState().receiveRealtimeMessage(message, 'user-a');
    useImStore.getState().receiveRealtimeMessage(message, 'user-a');

    const state = useImStore.getState();
    expect(state.messagesByConversation['conversation-2']).toHaveLength(1);
    expect(state.unreadSummary.totalUnread).toBe(1);
    expect(state.unreadSummary.conversationUnreadCounts['conversation-2']).toBe(1);
    expect(state.conversations[0]?.unreadCount).toBe(1);
  });

  it('does not increase unread for messages sent by current user', () => {
    useImStore.setState({
      activeConversationId: 'conversation-1',
      conversations: [createConversation('conversation-2')]
    });

    useImStore.getState().receiveRealtimeMessage(createMessage('message-3', 'conversation-2', 'user-a'), 'user-a');

    const state = useImStore.getState();
    expect(state.unreadSummary.totalUnread).toBe(0);
    expect(state.conversations[0]?.unreadCount).toBe(0);
  });
});

function createConversation(id: string): ImConversation {
  return {
    conversationKey: `direct:tenant-a:${id}`,
    createdTime: '2026-07-05T00:00:00.000Z',
    id,
    peerDisplayName: 'User B',
    peerUserId: 'user-b',
    tenantId: 'tenant-a',
    unreadCount: 0
  };
}

function createMessage(id: string, conversationId: string, senderUserId: string): ImMessage {
  return {
    content: 'hello',
    conversationId,
    id,
    messageType: 'Text',
    receiverUserId: senderUserId === 'user-a' ? 'user-b' : 'user-a',
    senderUserId,
    sentAt: '2026-07-05T00:01:00.000Z',
    status: 'sent'
  };
}
