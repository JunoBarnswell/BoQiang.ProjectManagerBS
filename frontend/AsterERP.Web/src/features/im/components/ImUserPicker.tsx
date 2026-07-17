import { useEffect, useState } from 'react';

import { AppIcon } from '@/shared/icons/AppIcon';

import { useImUserSearch } from '../hooks/useImUserSearch';
import { useImStore } from '../state/imStore';

import { useImContext } from './ImProvider';

export function ImUserPicker() {
  const { adapter, permissions } = useImContext();
  const { loading, search, users } = useImUserSearch();
  const mergeConversation = useImStore((state) => state.mergeConversation);
  const setActiveConversationId = useImStore((state) => state.setActiveConversationId);
  const [keyword, setKeyword] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    const timer = window.setTimeout(() => void search(keyword, controller.signal), 250);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [keyword, search]);

  const openUser = async (targetUserId: string) => {
    const conversation = await adapter.createDirectConversation(targetUserId);
    mergeConversation(conversation);
    setActiveConversationId(conversation.id);
    setKeyword('');
  };

  if (!permissions.canSearchUsers) {
    return null;
  }

  return (
    <div className="relative border-b border-gray-200 bg-white p-3">
      <div className="flex items-center gap-2 rounded border border-gray-300 px-2 py-1.5">
        <AppIcon className="text-gray-400" name="search" />
        <input
          className="min-w-0 flex-1 text-sm outline-none"
          onChange={(event) => setKeyword(event.target.value)}
          placeholder="搜索同租户用户"
          value={keyword}
        />
      </div>
      {keyword.trim() ? (
        <div className="absolute left-3 right-3 top-[48px] z-10 max-h-64 overflow-auto rounded border border-gray-200 bg-white shadow-lg">
          {loading ? <div className="p-3 text-sm text-gray-500">搜索中</div> : null}
          {!loading && users.length === 0 ? <div className="p-3 text-sm text-gray-500">未找到用户</div> : null}
          {users.map((user) => (
            <button className="flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-gray-50" key={user.userId} onClick={() => void openUser(user.userId)} type="button">
              <span className="flex h-7 w-7 items-center justify-center rounded bg-gray-100 text-xs font-semibold text-gray-700">{user.displayName.slice(0, 1).toUpperCase()}</span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm text-gray-900">{user.displayName}</span>
                <span className="block truncate text-xs text-gray-500">{user.userName}</span>
              </span>
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}
