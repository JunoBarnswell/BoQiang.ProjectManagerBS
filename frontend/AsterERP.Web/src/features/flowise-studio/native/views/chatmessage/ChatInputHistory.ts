const maxHistoryItems = 50;

export type ChatInputHistoryDirection = 'next' | 'previous';

export interface ChatInputHistoryNavigation {
  cursor: number | null;
  value: string;
}

export function readChatInputHistory(resourceId: string): string[] {
  try {
    const parsed = JSON.parse(localStorage.getItem(chatInputHistoryStorageKey(resourceId)) ?? '[]');
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === 'string' && item.trim().length > 0).slice(-maxHistoryItems) : [];
  } catch {
    return [];
  }
}

export function persistChatInputHistory(resourceId: string, question: string, current: string[]): string[] {
  const trimmed = question.trim();
  if (!trimmed) {
    return current;
  }

  const next = [...current.filter((item) => item !== trimmed), trimmed].slice(-maxHistoryItems);
  localStorage.setItem(chatInputHistoryStorageKey(resourceId), JSON.stringify(next));
  return next;
}

export function resolveChatInputHistoryNavigation(history: string[], cursor: number | null, direction: ChatInputHistoryDirection): ChatInputHistoryNavigation {
  if (history.length === 0) {
    return { cursor: null, value: '' };
  }

  const currentIndex = cursor ?? history.length;
  const nextIndex = direction === 'previous' ? Math.max(0, currentIndex - 1) : Math.min(history.length, currentIndex + 1);

  return {
    cursor: nextIndex === history.length ? null : nextIndex,
    value: nextIndex === history.length ? '' : history[nextIndex]
  };
}

function chatInputHistoryStorageKey(resourceId: string): string {
  return `flowise:chat:${resourceId}:inputHistory`;
}
