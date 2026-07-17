import { useCallback, useEffect, useState } from 'react';

import { useImContext } from '../components/ImProvider';
import { useImStore } from '../state/imStore';

export function useImConversations() {
  const { adapter } = useImContext();
  const conversations = useImStore((state) => state.conversations);
  const setConversations = useImStore((state) => state.setConversations);
  const setUnreadSummary = useImStore((state) => state.setUnreadSummary);
  const [loading, setLoading] = useState(false);

  const refresh = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    try {
      const [nextConversations, unread] = await Promise.all([
        adapter.getConversations(signal),
        adapter.getUnreadSummary(signal)
      ]);
      setConversations(nextConversations);
      setUnreadSummary(unread);
    } finally {
      setLoading(false);
    }
  }, [adapter, setConversations, setUnreadSummary]);

  useEffect(() => {
    const controller = new AbortController();
    void refresh(controller.signal);
    return () => controller.abort();
  }, [refresh]);

  return { conversations, loading, refresh };
}
