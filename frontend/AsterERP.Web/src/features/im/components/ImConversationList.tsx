import type { ImConversation } from '../types/imTypes';

interface ImConversationListProps {
  activeConversationId?: string;
  conversations: ImConversation[];
  onSelect: (conversation: ImConversation) => void;
}

export function ImConversationList({ activeConversationId, conversations, onSelect }: ImConversationListProps) {
  if (conversations.length === 0) {
    return <div className="p-4 text-sm text-gray-500">暂无会话</div>;
  }

  return (
    <div className="min-h-0 overflow-auto">
      {conversations.map((conversation) => (
        <button
          className={`flex w-full items-start gap-3 border-b border-gray-100 px-3 py-3 text-left hover:bg-gray-50 ${
            activeConversationId === conversation.id ? 'bg-blue-50' : 'bg-white'
          }`}
          key={conversation.id}
          onClick={() => onSelect(conversation)}
          type="button"
        >
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded bg-blue-100 text-sm font-semibold text-blue-700">
            {conversation.peerDisplayName.slice(0, 1).toUpperCase()}
          </span>
          <span className="min-w-0 flex-1">
            <span className="flex items-center justify-between gap-2">
              <span className="truncate text-sm font-medium text-gray-900">{conversation.peerDisplayName}</span>
              {conversation.unreadCount > 0 ? (
                <span className="rounded-full bg-red-500 px-1.5 py-0.5 text-[10px] font-semibold text-white">{conversation.unreadCount}</span>
              ) : null}
            </span>
            <span className="mt-1 block truncate text-xs text-gray-500">{conversation.lastMessagePreview ?? '暂无消息'}</span>
          </span>
        </button>
      ))}
    </div>
  );
}
