import type { ReactNode } from 'react';

import { useWorkspaceStore } from '../../core/state/workspaceStore';
import { buildWorkspaceFallbackSummary } from '../../pages/application-console/applicationConsoleFallbackSummary';
import { ApplicationDatabaseBindingPanel } from '../../pages/application-console/ApplicationDatabaseBindingPanel';
import { AppIcon } from '../../shared/icons/AppIcon';

import { useApplicationDatabaseGate } from './applicationDatabaseGate';

interface ApplicationDatabaseRequiredRouteProps {
  children: ReactNode;
}

export function ApplicationDatabaseRequiredRoute({ children }: ApplicationDatabaseRequiredRouteProps) {
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const gate = useApplicationDatabaseGate();

  if (!workspace || workspace.workspaceLevel !== 'application') {
    return children;
  }

  if (gate.state === 'loading') {
    return <GateMessage icon="refresh" title="检查应用数据库" loading />;
  }

  if (gate.state === 'ready') {
    return children;
  }

  if (gate.state === 'forbidden') {
    return <GateMessage icon="warning" title="当前账号无权访问应用数据库" />;
  }

  if (gate.status) {
    return (
      <div className="h-full min-h-0 overflow-y-auto overflow-x-hidden bg-gray-50 px-4 py-4 sm:px-6 lg:px-8">
        <ApplicationDatabaseBindingPanel
          summary={buildWorkspaceFallbackSummary(workspace, gate.status)}
          onReload={() => gate.refetch()}
        />
      </div>
    );
  }

  return (
    <GateMessage
      icon="warning"
      title={gate.state === 'unavailable' ? '应用数据库状态不可用' : '应用数据库配置无效'}
      onRetry={() => gate.refetch()}
    />
  );
}

function GateMessage({
  icon,
  loading = false,
  onRetry,
  title
}: {
  icon: string;
  loading?: boolean;
  onRetry?: () => Promise<unknown>;
  title: string;
}) {
  return (
    <div className="flex min-h-[360px] flex-col items-center justify-center gap-3 text-sm text-gray-500">
      <div className="flex items-center">
        <AppIcon className={loading ? 'mr-2 h-4 w-4 animate-spin' : 'h-9 w-9 text-red-500'} name={icon} />
      </div>
      <div className="text-base font-semibold text-gray-900">{title}</div>
      {onRetry ? (
        <button
          type="button"
          onClick={() => void onRetry()}
          className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:border-primary-300 hover:text-primary-600"
        >
          重试
        </button>
      ) : null}
    </div>
  );
}
