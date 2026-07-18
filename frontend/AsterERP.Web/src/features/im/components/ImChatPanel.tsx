import { useEffect, type ReactNode } from 'react';

import { useImMessages } from '../hooks/useImMessages';
import { useImStore } from '../state/imStore';
import type { ImConversation } from '../types/imTypes';

import { ImMessageComposer } from './ImMessageComposer';
import { useImContext } from './ImProvider';

interface ImChatPanelProps {
  conversation?: ImConversation;
  conversationContext?: ReactNode;
  readonly?: boolean;
  sourceAppCode?: string;
}

export function ImChatPanel({ conversation, conversationContext, readonly, sourceAppCode }: ImChatPanelProps) {
  const { currentUser } = useImContext();
  const { hasMore, load, loading, messages, send } = useImMessages(conversation?.id);
  const setActiveConversationId = useImStore((state) => state.setActiveConversationId);

  useEffect(() => {
    if (!conversation?.id) return;
    setActiveConversationId(conversation.id);
    const controller = new AbortController();
    void load(undefined, controller.signal);
    return () => controller.abort();
  }, [conversation?.id, load, setActiveConversationId]);

  if (!conversation) {
    return <div className="flex flex-1 items-center justify-center text-sm text-gray-500">请选择会话</div>;
  }

  return (
    <section className="flex min-h-0 flex-1 flex-col bg-white">
      <div className="flex h-12 shrink-0 items-center justify-between border-b border-gray-200 px-4">
        <div className="truncate text-sm font-semibold text-gray-900">{conversation.title?.trim() || conversation.peerDisplayName}</div>
        {conversationContext}
      </div>
      <div className="min-h-0 flex-1 overflow-auto bg-gray-50 p-4">
        {hasMore ? (
          <button className="mb-3 w-full text-xs text-blue-600" disabled={loading} onClick={() => void load(messages[0]?.id)} type="button">
            加载更早消息
          </button>
          ) : null}
        <div className="space-y-3">
          {messages.map((message) => {
            const mine = message.senderUserId === currentUser.userId;
            return (
              <div className={`flex ${mine ? 'justify-end' : 'justify-start'}`} key={message.id}>
                <div className={`max-w-[72%] rounded px-3 py-2 text-sm shadow-sm ${mine ? 'bg-blue-600 text-white' : 'bg-white text-gray-800'}`}>
                  <div className="whitespace-pre-wrap break-words">{message.content}</div>
                  <div className={`mt-1 text-[10px] ${mine ? 'text-blue-100' : 'text-gray-400'}`}>{message.status === 'failed' ? '发送失败' : message.status === 'sending' ? '发送中' : new Date(message.sentAt).toLocaleString()}</div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
      <ImMessageComposer disabled={readonly} onSend={(content) => send(content, sourceAppCode)} />
    </section>
  );
}
