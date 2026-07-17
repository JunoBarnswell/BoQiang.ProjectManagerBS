import { useCallback, useState } from 'react';

import { useImContext } from '../components/ImProvider';
import { useImStore } from '../state/imStore';
import type { ImMessage } from '../types/imTypes';

const emptyMessages: ImMessage[] = [];

export function useImMessages(conversationId?: string) {
  const { adapter, currentUser } = useImContext();
  const appendMessage = useImStore((state) => state.appendMessage);
  const messages = useImStore((state) => (conversationId ? state.messagesByConversation[conversationId] ?? emptyMessages : emptyMessages));
  const prependMessages = useImStore((state) => state.prependMessages);
  const replaceSendingMessage = useImStore((state) => state.replaceSendingMessage);
  const setMessages = useImStore((state) => state.setMessages);
  const upsertSendingMessage = useImStore((state) => state.upsertSendingMessage);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(false);

  const load = useCallback(async (cursor?: string, signal?: AbortSignal) => {
    if (!conversationId) return;
    setLoading(true);
    try {
      const page = await adapter.getMessages(conversationId, cursor, signal);
      if (cursor) {
        prependMessages(conversationId, page.items);
      } else {
        setMessages(conversationId, page.items);
      }
      setHasMore(page.hasMore);
    } finally {
      setLoading(false);
    }
  }, [adapter, conversationId, prependMessages, setMessages]);

  const send = useCallback(async (content: string, sourceAppCode?: string) => {
    if (!conversationId || !content.trim()) return;
    const clientMessageId = crypto.randomUUID();
    const sending: ImMessage = {
      clientMessageId,
      content: content.trim(),
      conversationId,
      id: clientMessageId,
      messageType: 'Text',
      receiverUserId: '',
      senderUserId: currentUser.userId,
      sentAt: new Date().toISOString(),
      sourceAppCode,
      status: 'sending'
    };
    upsertSendingMessage(sending);
    try {
      const sent = await adapter.sendMessage(conversationId, {
        clientMessageId,
        content,
        messageType: 'Text',
        sourceAppCode
      });
      replaceSendingMessage(conversationId, clientMessageId, sent);
      appendMessage(sent);
    } catch {
      replaceSendingMessage(conversationId, clientMessageId, { ...sending, status: 'failed' });
    }
  }, [adapter, appendMessage, conversationId, currentUser.userId, replaceSendingMessage, upsertSendingMessage]);

  return { hasMore, load, loading, messages, send };
}
