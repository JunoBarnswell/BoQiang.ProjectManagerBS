import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';


import { getApplicationConsoleSummary } from '../../api/application-console/applicationConsole.api';
import type { ApplicationConsoleSummaryDto } from '../../api/application-console/applicationConsole.types';
import { getActiveTokenSlot, hasPlatformAccessToken } from '../../core/http/tokenStorage';
import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useAuthStore, useWorkspaceStore } from '../../core/state';
import { AppIcon } from '../../shared/icons/AppIcon';

import { getApplicationConsoleNavItem, type ApplicationConsolePageKey } from './applicationConsoleCatalog';
import {
  buildRecentVisitStorageKey,
  loadRecentVisits,
  recordRecentVisit,
  recordDetailedRecentVisit,
  type ApplicationConsoleRecentVisit,
  type ApplicationConsoleRecentVisitInput
} from './applicationConsoleRecentVisits';

interface ApplicationConsolePageFrameProps {
  children: (context: ApplicationConsolePageContext) => ReactNode;
  density?: 'compact' | 'normal';
  hideDescription?: boolean;
  pageKey: ApplicationConsolePageKey;
  surface?: 'ide' | 'page';
}

export interface ApplicationConsolePageContext {
  recentVisits: ApplicationConsoleRecentVisit[];
  refreshSummary: () => Promise<unknown>;
  returnToPlatform: () => Promise<void>;
  summary: ApplicationConsoleSummaryDto;
}

export function ApplicationConsolePageFrame({ children, density = 'normal', hideDescription = false, pageKey, surface = 'page' }: ApplicationConsolePageFrameProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const switchPlatform = useAuthStore((state) => state.switchPlatform);
  const user = useAuthStore((state) => state.user);
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const navItem = getApplicationConsoleNavItem(pageKey);
  const canReturnPlatform = workspace?.workspaceLevel === 'application' && getActiveTokenSlot() === 'platform' && hasPlatformAccessToken();
  const storageKey = useMemo(
    () => buildRecentVisitStorageKey(user?.userId, workspace?.tenantId, workspace?.appCode),
    [user?.userId, workspace?.tenantId, workspace?.appCode]
  );
  const [recentVisits, setRecentVisits] = useState<ApplicationConsoleRecentVisit[]>(() => loadRecentVisits(storageKey));
  const summaryQuery = useApiQuery({
    enabled: workspace?.workspaceLevel === 'application',
    queryFn: ({ signal }) => getApplicationConsoleSummary(signal).then((response) => response.data),
    queryKey: queryKeys.applicationConsole.summary(workspace?.tenantId, workspace?.appCode),
    retry: shouldRetrySummaryQuery,
    staleTimeMs: 30_000
  });
  const summary = summaryQuery.data ?? null;

  useEffect(() => {
    setRecentVisits(loadRecentVisits(storageKey));
  }, [storageKey]);

  useEffect(() => {
    if (!storageKey || workspace?.workspaceLevel !== 'application') {
      return;
    }

    const stateVisit = resolveLocationVisitState(location.state, `${location.pathname}${location.search}`);
    if (stateVisit) {
      setRecentVisits(recordDetailedRecentVisit(storageKey, stateVisit));
      return;
    }

    setRecentVisits(recordRecentVisit(storageKey, navItem, `${location.pathname}${location.search}`));
  }, [location.pathname, location.search, location.state, navItem, storageKey, workspace?.workspaceLevel]);

  async function handleReturnPlatform() {
    try {
      const response = await switchPlatform({ target: 'application-center' });
      navigate(response.defaultRoutePath || response.currentWorkspace.defaultRoutePath || '/platform/applications', { replace: true });
    } catch {
      navigate('/login', { replace: true });
    }
  }

  function handleReturnConsole() {
    const tenantId = workspace?.tenantId;
    const appCode = workspace?.appCode?.toUpperCase();
    if (tenantId && appCode) {
      navigate(`/tenants/${encodeURIComponent(tenantId)}/apps/${appCode}/admin/console`);
    }
  }

  if (summaryQuery.isLoading) {
    return (
      <div className="flex min-h-[360px] items-center justify-center text-sm text-gray-500">
        <AppIcon className="mr-2 h-4 w-4 animate-spin" name="refresh" />{translateCurrentLiteral("加载应用控制台")}</div>
    );
  }

  if (summaryQuery.isError || !summary) {
    return (
      <div className="flex min-h-[360px] flex-col items-center justify-center gap-3 text-sm text-gray-500">
        <AppIcon className="h-9 w-9 text-red-500" name="warning" />
        <div className="text-base font-semibold text-gray-900">{translateCurrentLiteral("应用控制台不可用")}</div>
        <button
          type="button"
          onClick={() => void summaryQuery.refetch()}
          className="rounded-md border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:border-primary-300 hover:text-primary-600"
        >{translateCurrentLiteral("重试")}</button>
      </div>
    );
  }

  if (!summary) {
    return null;
  }

  const compact = density === 'compact';
  const pageContext = {
    recentVisits,
    refreshSummary: () => summaryQuery.refetch(),
    returnToPlatform: handleReturnPlatform,
    summary
  };

  if (surface === 'ide') {
    return (
      <div className="h-full min-h-0 overflow-hidden bg-gray-50">
        {children(pageContext)}
      </div>
    );
  }

  return (
    <div className={`h-full min-h-0 overflow-y-auto overflow-x-hidden bg-gray-50 ${compact ? 'px-1.5 py-1.5 sm:px-2.5' : 'px-3 py-3 sm:px-4 lg:px-5'}`}>
      <div className={`${compact ? 'mb-1.5 gap-1 pb-1.5' : 'mb-3 gap-2 pb-3'} flex flex-col border-b border-gray-200 lg:flex-row lg:items-start lg:justify-between`}>
        <div>
          <div className={`${compact ? 'mb-0.5 gap-1 text-[11px]' : 'mb-2 gap-2 text-xs'} flex flex-wrap items-center text-gray-500`}>
            <span>{translateCurrentLiteral("平台管理")}</span>
            <span>/</span>
            <span>{translateCurrentLiteral("应用中心")}</span>
            <span>/</span>
            <span>{summary.application.appCode}</span>
            <span>/</span>
            <span>{navItem.title}</span>
          </div>
          <h1 className={`${compact ? 'text-base' : 'text-xl'} font-semibold leading-tight text-gray-950`}>{navItem.title}</h1>
          {!hideDescription ? <p className={`${compact ? 'text-[11px]' : 'text-xs'} mt-0.5 max-w-3xl text-gray-600`}>{navItem.description}</p> : null}
        </div>
        <div className="flex shrink-0 items-center gap-2">
          {pageKey !== 'console' && pageKey !== 'home' ? (
            <button
              type="button"
              onClick={handleReturnConsole}
              className={`inline-flex items-center gap-1.5 rounded-md border border-gray-300 bg-white font-medium text-gray-700 shadow-sm hover:border-primary-300 hover:text-primary-600 ${compact ? 'min-h-7 px-2 py-1 text-[11px]' : 'min-h-8 px-2.5 py-1 text-xs'}`}
            >
              <AppIcon className="h-4 w-4" name="arrow-left" />{translateCurrentLiteral("返回应用控制台")}</button>
          ) : null}
          {canReturnPlatform ? (
            <button
              type="button"
              onClick={() => void handleReturnPlatform()}
              className={`inline-flex items-center gap-1.5 rounded-md border border-gray-300 bg-white font-medium text-gray-700 shadow-sm hover:border-primary-300 hover:text-primary-600 ${compact ? 'min-h-7 px-2 py-1 text-[11px]' : 'min-h-8 px-2.5 py-1 text-xs'}`}
            >
              <AppIcon className="h-4 w-4" name="arrow-right" />{translateCurrentLiteral("返回平台级")}</button>
          ) : null}
        </div>
      </div>
      {children(pageContext)}
    </div>
  );
}

function resolveLocationVisitState(state: unknown, path: string): ApplicationConsoleRecentVisitInput | null {
  if (!state || typeof state !== 'object' || !('recentVisit' in state)) {
    return null;
  }

  const candidate = (state as { recentVisit?: Partial<ApplicationConsoleRecentVisitInput> }).recentVisit;
  if (!candidate || typeof candidate !== 'object') {
    return null;
  }

  if (typeof candidate.title !== 'string' || candidate.title.trim().length === 0) {
    return null;
  }

  return {
    description: typeof candidate.description === 'string' ? candidate.description : null,
    kind: typeof candidate.kind === 'string' ? candidate.kind : undefined,
    pageId: typeof candidate.pageId === 'string' ? candidate.pageId : null,
    path,
    section: typeof candidate.section === 'string' ? candidate.section : null,
    targetTitle: typeof candidate.targetTitle === 'string' ? candidate.targetTitle : null,
    title: candidate.title
  };
}

function shouldRetrySummaryQuery(failureCount: number, error: unknown): boolean {
  if (error instanceof DOMException && error.name === 'AbortError') {
    return failureCount < 4;
  }

  return failureCount < 1;
}
