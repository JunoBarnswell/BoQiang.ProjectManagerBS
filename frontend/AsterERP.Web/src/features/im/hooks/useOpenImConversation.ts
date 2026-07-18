import { useCallback } from 'react';

import { useImContext } from '../components/ImProvider';
import { useImStore } from '../state/imStore';

/**
 * Refreshes the IM projection before selecting a conversation created by another bounded context.
 * Project management only owns the link; the IM adapter remains the source of conversation data.
 */
export function useOpenImConversation() {
  const { adapter } = useImContext();
  const openDrawer = useImStore((state) => state.openDrawer);
  const setActiveConversationId = useImStore((state) => state.setActiveConversationId);
  const setConversations = useImStore((state) => state.setConversations);
  const setUnreadSummary = useImStore((state) => state.setUnreadSummary);

  return useCallback(async (conversationId: string) => {
    const [conversations, unread] = await Promise.all([
      adapter.getConversations(),
      adapter.getUnreadSummary(),
    ]);
    if (!conversations.some((item) => item.id === conversationId)) {
      throw new Error('关联会话当前不可访问，请刷新项目成员后重试');
    }
    setConversations(conversations);
    setUnreadSummary(unread);
    setActiveConversationId(conversationId);
    openDrawer();
  }, [adapter, openDrawer, setActiveConversationId, setConversations, setUnreadSummary]);
}
