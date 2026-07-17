import { useState, type KeyboardEvent } from 'react';

import { AppIcon } from '@/shared/icons/AppIcon';

interface ImMessageComposerProps {
  disabled?: boolean;
  onSend: (content: string) => Promise<void> | void;
}

export function ImMessageComposer({ disabled, onSend }: ImMessageComposerProps) {
  const [content, setContent] = useState('');
  const [sending, setSending] = useState(false);
  const canSend = !disabled && content.trim().length > 0 && !sending;

  const send = async () => {
    if (!canSend) return;
    const trimmedContent = content.trim();
    setSending(true);
    try {
      await onSend(trimmedContent);
      setContent('');
    } finally {
      setSending(false);
    }
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      void send();
    }
  };

  return (
    <div className="border-t border-gray-200 bg-white p-3">
      <textarea
        className="h-20 w-full resize-none rounded border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
        disabled={disabled || sending}
        onChange={(event) => setContent(event.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="输入消息"
        value={content}
      />
      <div className="mt-2 flex justify-end">
        <button
          className="inline-flex items-center gap-1.5 rounded bg-blue-600 px-3 py-1.5 text-sm font-medium text-white disabled:cursor-not-allowed disabled:bg-gray-300"
          disabled={!canSend}
          onClick={() => void send()}
          type="button"
        >
          <AppIcon name="rocket" />
          发送
        </button>
      </div>
    </div>
  );
}
