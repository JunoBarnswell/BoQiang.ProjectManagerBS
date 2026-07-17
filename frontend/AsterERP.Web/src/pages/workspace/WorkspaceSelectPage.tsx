import { AlertCircle, LogOut, Search, Server, ShieldCheck } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type { WorkspaceDto } from '../../api/platform/auth.types';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useAuthStore, useWorkspaceStore } from '../../core/state';
import { useMessage } from '../../shared/feedback/useMessage';
import { getErrorMessage } from '../../shared/utils/errorMessage';

function groupWorkspaces(workspaces: WorkspaceDto[]): Array<{ tenantId: string; tenantName: string; items: WorkspaceDto[] }> {
  const map = new Map<string, { tenantId: string; tenantName: string; items: WorkspaceDto[] }>();
  workspaces.forEach((workspace) => {
    const key = workspace.tenantId;
    const group = map.get(key) ?? { tenantId: workspace.tenantId, tenantName: workspace.tenantName, items: [] };
    group.items.push(workspace);
    map.set(key, group);
  });

  return Array.from(map.values());
}

function getSystemName(workspace: WorkspaceDto): string {
  return workspace.systemName || workspace.appName;
}

function getSystemCode(workspace: WorkspaceDto): string {
  return workspace.systemCode || workspace.appCode;
}

function getStatusLabel(workspace: WorkspaceDto): string {
  if (workspace.isAvailable === false) {
    return workspace.disabledReason || 'workspace.status.unavailable';
  }

  if (workspace.workspaceLevel === 'application' && !workspace.isDatabaseBound) {
    return 'workspace.status.initializationRequired';
  }

  if (workspace.isDefault) {
    return 'workspace.status.default';
  }

  return 'workspace.status.available';
}

export function WorkspaceSelectPage() {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const message = useMessage();
  const availableWorkspaces = useWorkspaceStore((state) => state.availableWorkspaces);
  const switchWorkspace = useAuthStore((state) => state.switchWorkspace);
  const logout = useAuthStore((state) => state.logout);
  const [keyword, setKeyword] = useState('');
  const [switchingWorkspaceId, setSwitchingWorkspaceId] = useState<string | null>(null);

  const filteredWorkspaces = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();
    if (!normalizedKeyword) {
      return availableWorkspaces;
    }

    return availableWorkspaces.filter((workspace) =>
      `${workspace.tenantName} ${workspace.appCode} ${workspace.appName} ${workspace.systemName} ${workspace.description ?? ''}`.toLowerCase().includes(normalizedKeyword)
    );
  }, [availableWorkspaces, keyword]);

  const workspaceGroups = useMemo(() => groupWorkspaces(filteredWorkspaces), [filteredWorkspaces]);
  const availableCount = useMemo(() => availableWorkspaces.filter((workspace) => workspace.isAvailable !== false).length, [availableWorkspaces]);

  const handleEnterWorkspace = async (workspace: WorkspaceDto) => {
    if (workspace.isAvailable === false) {
      return;
    }

    if (workspace.workspaceLevel === 'application') {
      navigate(`/tenants/${encodeURIComponent(workspace.tenantId)}/apps/${workspace.appCode.toUpperCase()}/login`);
      return;
    }

    setSwitchingWorkspaceId(workspace.workspaceId);
    try {
      const response = await switchWorkspace({ appCode: workspace.appCode, tenantId: workspace.tenantId });
      const targetRoute = response.defaultRoutePath || response.currentWorkspace.defaultRoutePath || workspace.defaultRoutePath || '/platform/applications';
      navigate(targetRoute, { replace: true });
    } catch (error) {
      message.error(getErrorMessage(error, translate('workspace.switchFailed')));
    } finally {
      setSwitchingWorkspaceId(null);
    }
  };

  return (
    <main className="workspace-select-page flex min-h-0 flex-col bg-gray-50 text-gray-900">
      <header className="shrink-0 border-b border-gray-200 bg-white px-4 py-4 sm:px-8 sm:py-5 flex items-center justify-between">
        <div>
          <div className="text-xs font-semibold text-primary-600 uppercase">{translate('workspace.system')}</div>
          <h1 className="text-xl font-semibold mt-1">{translate('workspace.title')}</h1>
        </div>
        <button className="inline-flex items-center gap-1.5 rounded border border-gray-200 bg-white px-3 py-1.5 text-sm text-gray-600 hover:border-gray-300 hover:text-gray-900" type="button" onClick={logout}>
          <LogOut size={15} />
          {translate('layout.logout')}
        </button>
      </header>

      <section className="min-h-0 flex-1 overflow-y-auto px-6 py-8">
        <div className="mx-auto max-w-5xl pb-8">
        <div className="mb-4 flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2 className="text-base font-semibold text-gray-800">{translate('workspace.available')}</h2>
            <p className="mt-1 text-sm text-gray-500">
              {translate('workspace.description')}
            </p>
          </div>
          <span className="text-xs text-gray-500">
            {formatMessage(translate('workspace.count'), { available: availableCount, total: availableWorkspaces.length })}
          </span>
        </div>

        <div className="flex items-center gap-3 rounded-md border border-gray-200 bg-white px-3 py-2 shadow-sm">
          <Search size={18} className="text-gray-400" />
          <input
            className="w-full bg-transparent text-sm outline-none"
            placeholder={translate('workspace.searchPlaceholder')}
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
          />
        </div>

        {availableWorkspaces.length === 0 ? (
          <div className="mt-8 rounded-md border border-gray-200 bg-white p-10 text-center">
            <ShieldCheck className="mx-auto text-gray-300" size={38} />
            <h2 className="mt-4 text-base font-semibold">{translate('workspace.noneAvailable')}</h2>
            <p className="mt-2 text-sm text-gray-500">{translate('workspace.noneAvailableDescription')}</p>
          </div>
        ) : workspaceGroups.length === 0 ? (
          <div className="mt-8 rounded-md border border-gray-200 bg-white p-10 text-center">
            <Search className="mx-auto text-gray-300" size={38} />
            <h2 className="mt-4 text-base font-semibold">{translate('workspace.noneMatched')}</h2>
            <p className="mt-2 text-sm text-gray-500">{translate('workspace.noneMatchedDescription')}</p>
          </div>
        ) : (
          <div className="mt-6 space-y-6">
            {workspaceGroups.map((group) => (
              <section key={group.tenantId}>
                <h2 className="text-sm font-semibold text-gray-600">{group.tenantName}</h2>
                <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {group.items.map((workspace) => (
                    <button
                      key={workspace.workspaceId}
                      className={`rounded-md border bg-white p-4 text-left shadow-sm transition ${
                        workspace.isAvailable === false
                          ? 'border-gray-200 opacity-75'
                          : 'border-gray-200 hover:border-primary-400 hover:shadow'
                      }`}
                      disabled={switchingWorkspaceId !== null || workspace.isAvailable === false}
                      type="button"
                      onClick={() => void handleEnterWorkspace(workspace)}
                    >
                      <div className="flex items-start gap-3">
                        <span className={`flex h-10 w-10 items-center justify-center rounded-md ${workspace.isAvailable === false ? 'bg-gray-100 text-gray-400' : 'bg-primary-50 text-primary-600'}`}>
                          {workspace.isAvailable === false ? <AlertCircle size={20} /> : <Server size={20} />}
                        </span>
                        <span className="min-w-0">
                          <span className="block truncate text-base font-semibold">{getSystemName(workspace)}</span>
                          <span className="mt-1 block text-xs text-gray-500">
                            {workspace.tenantName} / {getSystemCode(workspace)}
                          </span>
                        </span>
                      </div>
                      <p className="mt-3 min-h-8 text-xs leading-4 text-gray-500">
                        {workspace.description || formatMessage(translate('workspace.descriptionFallback'), { appName: workspace.appName, appCode: workspace.appCode })}
                      </p>
                      <div className="mt-4 flex items-center justify-between text-xs text-gray-500">
                        <span className={workspace.isAvailable === false ? 'text-amber-600' : 'text-gray-500'}>
                          {translate(getStatusLabel(workspace))}
                        </span>
                        <span className={workspace.isAvailable === false ? 'text-gray-400' : 'text-primary-600'}>
                          {switchingWorkspaceId === workspace.workspaceId
                            ? translate('workspace.state.entering')
                            : workspace.isAvailable === false
                              ? translate('workspace.state.unavailable')
                              : workspace.workspaceLevel === 'application' && !workspace.isDatabaseBound
                                ? translate('workspace.state.initialize')
                                : translate('workspace.state.enter')}
                        </span>
                      </div>
                    </button>
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
        </div>
      </section>
    </main>
  );
}

export default WorkspaceSelectPage;
