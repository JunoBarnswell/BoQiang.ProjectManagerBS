import { useEffect, useMemo, useState } from 'react';

import { AppIcon } from '@/shared/icons/AppIcon';

import type { ImConversation, ImDirectoryDepartment, ImDirectoryUser, ImPresenceChanged } from '../types/imTypes';

interface ImDirectoryPanelProps {
  activeConversationId?: string;
  conversations: ImConversation[];
  directory: ImDirectoryDepartment[];
  keyword: string;
  loading: boolean;
  presenceByUserId: Record<string, ImPresenceChanged>;
  refreshing: boolean;
  onKeywordChange: (keyword: string) => void;
  onOpenUser: (user: ImDirectoryUser) => void;
  onRefresh: () => void;
}

export function ImDirectoryPanel({
  activeConversationId,
  conversations,
  directory,
  keyword,
  loading,
  onKeywordChange,
  onOpenUser,
  onRefresh,
  presenceByUserId,
  refreshing
}: ImDirectoryPanelProps) {
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const conversationByUserId = useMemo(() => (
    conversations.reduce<Record<string, ImConversation>>((acc, conversation) => {
      acc[conversation.peerUserId] = conversation;
      return acc;
    }, {})
  ), [conversations]);
  const departmentIds = useMemo(() => collectDepartmentIds(directory), [directory]);

  useEffect(() => {
    setExpanded((current) => {
      const next = new Set(current);
      for (const id of departmentIds) {
        if (keyword.trim() || current.size === 0) {
          next.add(id);
        }
      }
      return next;
    });
  }, [departmentIds, keyword]);

  const toggle = (deptId: string) => {
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(deptId)) {
        next.delete(deptId);
      } else {
        next.add(deptId);
      }
      return next;
    });
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="border-b border-gray-200 bg-white p-3">
        <div className="flex items-center gap-2 rounded border border-gray-300 px-2 py-1.5">
          <AppIcon className="text-gray-400" name="search" />
          <input
            className="min-w-0 flex-1 text-sm outline-none"
            onChange={(event) => onKeywordChange(event.target.value)}
            placeholder="搜索部门、岗位、用户"
            value={keyword}
          />
          {keyword ? (
            <button className="text-xs text-gray-500 hover:text-gray-900" onClick={() => onKeywordChange('')} type="button">清空</button>
          ) : null}
        </div>
      </div>
      <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2 text-xs text-gray-500">
        <span>{loading ? '加载通讯录中' : '部门通讯录'}</span>
        <button className="text-blue-600 disabled:text-gray-400" disabled={refreshing} onClick={onRefresh} type="button">刷新</button>
      </div>
      <div className="min-h-0 flex-1 overflow-auto bg-white py-2">
        {directory.length === 0 && !loading ? <div className="px-4 py-6 text-sm text-gray-500">暂无可联系用户</div> : null}
        {directory.map((department) => (
          <DepartmentNode
            activeConversationId={activeConversationId}
            conversationByUserId={conversationByUserId}
            department={department}
            expanded={expanded}
            key={department.deptId}
            level={0}
            presenceByUserId={presenceByUserId}
            onOpenUser={onOpenUser}
            onToggle={toggle}
          />
        ))}
      </div>
    </div>
  );
}

interface DepartmentNodeProps {
  activeConversationId?: string;
  conversationByUserId: Record<string, ImConversation>;
  department: ImDirectoryDepartment;
  expanded: Set<string>;
  level: number;
  presenceByUserId: Record<string, ImPresenceChanged>;
  onOpenUser: (user: ImDirectoryUser) => void;
  onToggle: (deptId: string) => void;
}

function DepartmentNode({
  activeConversationId,
  conversationByUserId,
  department,
  expanded,
  level,
  onOpenUser,
  onToggle,
  presenceByUserId
}: DepartmentNodeProps) {
  const open = expanded.has(department.deptId);
  const count = countUsers(department);

  return (
    <div>
      <button
        className="flex w-full items-center gap-2 px-3 py-1.5 text-left text-xs font-semibold text-gray-700 hover:bg-gray-50"
        onClick={() => onToggle(department.deptId)}
        style={{ paddingLeft: 12 + level * 14 }}
        type="button"
      >
        <span className="w-3 text-gray-400">{open ? 'v' : '>'}</span>
        <span className="min-w-0 flex-1 truncate">{department.deptName}</span>
        <span className="text-[11px] font-normal text-gray-400">{count}</span>
      </button>
      {open ? (
        <>
          {department.users.map((user) => (
            <DirectoryUserRow
              activeConversationId={activeConversationId}
              conversation={conversationByUserId[user.userId]}
              key={`${user.employmentId}:${user.userId}`}
              level={level + 1}
              presence={presenceByUserId[user.userId]}
              user={user}
              onOpen={onOpenUser}
            />
          ))}
          {department.children.map((child) => (
            <DepartmentNode
              activeConversationId={activeConversationId}
              conversationByUserId={conversationByUserId}
              department={child}
              expanded={expanded}
              key={child.deptId}
              level={level + 1}
              presenceByUserId={presenceByUserId}
              onOpenUser={onOpenUser}
              onToggle={onToggle}
            />
          ))}
        </>
      ) : null}
    </div>
  );
}

interface DirectoryUserRowProps {
  activeConversationId?: string;
  conversation?: ImConversation;
  level: number;
  presence?: ImPresenceChanged;
  user: ImDirectoryUser;
  onOpen: (user: ImDirectoryUser) => void;
}

function DirectoryUserRow({ activeConversationId, conversation, level, onOpen, presence, user }: DirectoryUserRowProps) {
  const conversationId = conversation?.id ?? user.conversationId ?? undefined;
  const active = Boolean(conversationId && conversationId === activeConversationId);
  const online = presence?.isOnline ?? user.isOnline;
  const lastMessage = conversation?.lastMessagePreview ?? user.lastMessagePreview ?? '暂无消息';
  const unreadCount = conversation?.unreadCount ?? user.unreadCount;

  return (
    <button
      className={`flex w-full items-start gap-2 px-3 py-2 text-left hover:bg-gray-50 ${active ? 'bg-blue-50' : ''}`}
      onClick={() => onOpen(user)}
      style={{ paddingLeft: 12 + level * 14 }}
      type="button"
    >
      <span className="relative flex h-8 w-8 shrink-0 items-center justify-center rounded bg-blue-100 text-xs font-semibold text-blue-700">
        {user.displayName.slice(0, 1).toUpperCase()}
        <span className={`absolute -bottom-0.5 -right-0.5 h-2.5 w-2.5 rounded-full border border-white ${online ? 'bg-emerald-500' : 'bg-gray-300'}`} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-2">
          <span className="truncate text-sm font-medium text-gray-900">{user.displayName}</span>
          {unreadCount > 0 ? <span className="rounded-full bg-red-500 px-1.5 py-0.5 text-[10px] font-semibold text-white">{unreadCount}</span> : null}
        </span>
        <span className="mt-0.5 block truncate text-[11px] text-gray-500">{user.positionName ?? user.employmentName}</span>
        <span className="mt-0.5 block truncate text-xs text-gray-500">{lastMessage}</span>
      </span>
    </button>
  );
}

function collectDepartmentIds(departments: ImDirectoryDepartment[]): string[] {
  return departments.flatMap((department) => [department.deptId, ...collectDepartmentIds(department.children)]);
}

function countUsers(department: ImDirectoryDepartment): number {
  return department.users.length + department.children.reduce((sum, child) => sum + countUsers(child), 0);
}
