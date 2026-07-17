import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import type { AiConversationDto } from '../../../../features/ai-center/api/aiCenter.api';

interface AssistantHistoryPanelProps {
  activeConversationId?: string | null;
  conversations: AiConversationDto[];
  loading?: boolean;
  onSelect: (conversationId: string) => void;
}

export function AssistantHistoryPanel({ activeConversationId, conversations, loading, onSelect }: AssistantHistoryPanelProps) {
  return (
    <div className="shrink-0 border-b border-slate-200 bg-slate-50 px-3 py-2">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-xs font-semibold text-slate-700">{translateCurrentLiteral("数据中心会话历史")}</span>
        <span className="text-[11px] text-slate-400">{loading ? '加载中' : `${conversations.length} 条`}</span>
      </div>
      <div className="max-h-48 space-y-1 overflow-y-auto">
        {conversations.length === 0 ? (
          <div className="rounded-lg border border-dashed border-slate-200 bg-white px-3 py-4 text-center text-xs text-slate-500">{translateCurrentLiteral("暂无历史会话")}</div>
        ) : conversations.map((conversation) => (
          <button
            className={[
              'block w-full rounded-lg px-3 py-2 text-left transition',
              activeConversationId === conversation.id ? 'bg-primary-50 text-primary-700' : 'bg-white text-slate-700 hover:bg-slate-100'
            ].join(' ')}
            key={conversation.id}
            type="button"
            onClick={() => onSelect(conversation.id)}
          >
            <div className="truncate text-xs font-medium">{conversation.title}</div>
            <div className="mt-1 truncate text-[11px] text-slate-400">{conversation.lastMessageAt ?? conversation.createdTime}</div>
          </button>
        ))}
      </div>
    </div>
  );
}
