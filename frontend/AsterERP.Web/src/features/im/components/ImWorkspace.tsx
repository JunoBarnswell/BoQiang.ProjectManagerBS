import { useEffect, useMemo, useState, type ReactNode } from 'react';

import { useImConversations } from '../hooks/useImConversations';
import { useImDirectory } from '../hooks/useImDirectory';
import { useImStore } from '../state/imStore';
import type { ImConversation, ImDirectoryUser } from '../types/imTypes';

import { ImChatPanel } from './ImChatPanel';
import { ImConnectionStatus } from './ImConnectionStatus';
import { ImDirectoryPanel } from './ImDirectoryPanel';
import { useImContext } from './ImProvider';

interface ImWorkspaceProps {
  defaultConversationId?: string;
  defaultTargetUserId?: string;
  mode?: 'full' | 'drawer';
  renderConversationContext?: (conversation: ImConversation) => ReactNode;
  sourceAppCode?: string;
}

export function ImWorkspace({ defaultConversationId, defaultTargetUserId, mode = 'full', renderConversationContext, sourceAppCode }: ImWorkspaceProps) {
  const { adapter, permissions } = useImContext();
  const [keyword, setKeyword] = useState('');
  const [refreshing, setRefreshing] = useState(false);
  const { conversations, loading, refresh } = useImConversations();
  const { directory, loading: directoryLoading, refresh: refreshDirectory } = useImDirectory(keyword);
  const activeConversationId = useImStore((state) => state.activeConversationId);
  const mergeConversation = useImStore((state) => state.mergeConversation);
  const presenceByUserId = useImStore((state) => state.presenceByUserId);
  const setActiveConversationId = useImStore((state) => state.setActiveConversationId);
  const activeConversation = useMemo(
    () => conversations.find((item) => item.id === activeConversationId),
    [activeConversationId, conversations]
  );

  useEffect(() => {
    if (defaultConversationId) {
      setActiveConversationId(defaultConversationId);
    }
  }, [defaultConversationId, setActiveConversationId]);

  useEffect(() => {
    if (!defaultTargetUserId || defaultConversationId) return;
    void adapter.createDirectConversation(defaultTargetUserId).then((conversation) => {
      mergeConversation(conversation);
      setActiveConversationId(conversation.id);
    });
  }, [adapter, defaultConversationId, defaultTargetUserId, mergeConversation, setActiveConversationId]);

  if (!permissions.canView) {
    return <div className="flex flex-1 items-center justify-center text-sm text-gray-500">无 IM 访问权限</div>;
  }

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await Promise.all([refresh(), refreshDirectory()]);
    } finally {
      setRefreshing(false);
    }
  };

  const handleOpenUser = async (user: ImDirectoryUser) => {
    if (!permissions.canCreateConversation) {
      return;
    }

    const existing = conversations.find((conversation) => conversation.conversationType !== 'Group' && conversation.peerUserId === user.userId);
    if (existing) {
      setActiveConversationId(existing.id);
      return;
    }

    const conversation = await adapter.createDirectConversation(user.userId);
    mergeConversation(conversation);
    setActiveConversationId(conversation.id);
    await refreshDirectory();
  };

  return (
    <div className={`flex min-h-0 min-w-0 flex-1 overflow-hidden bg-white ${mode === 'full' ? 'h-full' : 'h-[70vh]'}`}>
      <aside className="flex w-80 shrink-0 flex-col border-r border-gray-200 bg-white">
        <div className="flex h-12 items-center justify-between border-b border-gray-200 px-3">
          <div className="text-sm font-semibold text-gray-900">站内信</div>
          <div className="flex items-center gap-2">
            <ImConnectionStatus />
            <button className="text-xs text-blue-600" disabled={loading || directoryLoading || refreshing} onClick={() => void handleRefresh()} type="button">刷新</button>
          </div>
        </div>
        <ImDirectoryPanel
          activeConversationId={activeConversationId}
          conversations={conversations}
          directory={directory.departments}
          keyword={keyword}
          loading={directoryLoading}
          presenceByUserId={presenceByUserId}
          refreshing={refreshing}
          onKeywordChange={setKeyword}
          onOpenUser={(user) => void handleOpenUser(user)}
          onRefresh={() => void handleRefresh()}
        />
      </aside>
      <ImChatPanel conversation={activeConversation} conversationContext={activeConversation ? renderConversationContext?.(activeConversation) : undefined} sourceAppCode={sourceAppCode} />
    </div>
  );
}
