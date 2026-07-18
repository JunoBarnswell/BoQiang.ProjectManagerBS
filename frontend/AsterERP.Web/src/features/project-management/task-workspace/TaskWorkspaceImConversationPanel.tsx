import type { ProjectManagementImConversation } from '../../../api/project-management/projectManagement.types';
import { PermissionButton } from '../../../shared/auth/PermissionButton';

interface TaskWorkspaceImConversationPanelProps {
  conversation?: ProjectManagementImConversation | null;
  error: boolean;
  loading: boolean;
  opening: boolean;
  scope: 'project' | 'task';
  onOpen: () => void;
}

export function TaskWorkspaceImConversationPanel({
  conversation,
  error,
  loading,
  opening,
  scope,
  onOpen,
}: TaskWorkspaceImConversationPanelProps) {
  const label = scope === 'project' ? '项目协作会话' : '任务协作会话';
  const exists = Boolean(conversation?.conversationId && conversation.status === 'Active');

  return (
    <section className="rounded-lg border border-gray-200 p-3">
      <div className="text-sm font-semibold">{label}</div>
      {loading ? <div className="mt-1 text-sm text-gray-500">正在读取关联状态…</div> : null}
      {error ? <div className="mt-1 text-sm text-amber-700">关联状态读取失败，操作时会由服务端重新校验权限。</div> : null}
      {!loading ? (
        <div className="mt-2 flex flex-wrap items-center gap-2">
          <span className="text-sm text-gray-600">{exists ? conversation?.title : '尚未创建关联会话'}</span>
          <PermissionButton
            code={exists ? 'project-management:im-conversation:view' : 'project-management:im-conversation:manage'}
            disabled={opening}
            onClick={onOpen}
          >
            {opening ? '正在打开…' : exists ? '进入会话' : '创建并进入'}
          </PermissionButton>
        </div>
      ) : null}
    </section>
  );
}
