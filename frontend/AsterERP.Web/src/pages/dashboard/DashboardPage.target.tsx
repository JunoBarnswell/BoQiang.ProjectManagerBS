import { Boxes, LayoutDashboard, RefreshCw, Route, Warehouse } from 'lucide-react';
import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';

import type { MenuTreeNodeDto } from '../../api/system/system.types';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useMenuStore, useWorkspaceStore } from '../../core/state';

function countMenuNodes(nodes: MenuTreeNodeDto[]): number {
  return nodes.reduce((count, node) => count + 1 + countMenuNodes(node.children ?? []), 0);
}

function flattenRuntimeMenus(nodes: MenuTreeNodeDto[]): MenuTreeNodeDto[] {
  return nodes.flatMap((node) => {
    const current = node.pageCode || node.routePath?.startsWith('/pages/') ? [node] : [];
    return [...current, ...flattenRuntimeMenus(node.children ?? [])];
  });
}

export function DashboardPage() {
  const navigate = useNavigate();
  const { translate } = useI18n();
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const menus = useMenuStore((state) => state.menus);
  const availableWorkspaces = useWorkspaceStore((state) => state.availableWorkspaces);
  const visibleMenuCount = useMemo(() => countMenuNodes(menus), [menus]);
  const runtimeMenus = useMemo(() => flattenRuntimeMenus(menus).slice(0, 6), [menus]);
  const availableSystemCount = useMemo(
    () => availableWorkspaces.filter((workspace) => workspace.isAvailable !== false).length,
    [availableWorkspaces]
  );

  const appName = currentWorkspace?.systemName || currentWorkspace?.appName || translate('page.dashboard.currentApplication');
  const tenantName = currentWorkspace?.tenantName || '-';

  return (
    <main className="space-y-4">
      <section className="erp-panel p-5">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="text-sm font-semibold text-blue-600">{tenantName}</p>
            <h1 className="mt-1 text-2xl font-extrabold text-gray-900">{appName}</h1>
          </div>
          <button
            className="inline-flex items-center gap-2 rounded-md border border-gray-200 px-3 py-2 text-sm font-semibold text-gray-700 transition hover:border-blue-300 hover:text-blue-600"
            onClick={() => window.location.reload()}
            type="button"
          >
            <RefreshCw size={16} />
            {translate('page.dashboard.refresh')}
          </button>
        </div>
      </section>

      <section className="grid gap-4 md:grid-cols-3">
        <div className="erp-panel p-4">
          <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-blue-50 text-blue-600">
            <LayoutDashboard size={22} />
          </span>
          <p className="mt-4 text-sm font-semibold text-gray-500">{translate('page.dashboard.visibleMenuCount')}</p>
          <strong className="mt-1 block text-2xl font-extrabold text-gray-900">{visibleMenuCount}</strong>
        </div>
        <div className="erp-panel p-4">
          <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-emerald-50 text-emerald-600">
            <Route size={22} />
          </span>
          <p className="mt-4 text-sm font-semibold text-gray-500">{translate('page.dashboard.runtimeEntryCount')}</p>
          <strong className="mt-1 block text-2xl font-extrabold text-gray-900">{runtimeMenus.length}</strong>
        </div>
        <div className="erp-panel p-4">
          <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-amber-50 text-amber-600">
            <Warehouse size={22} />
          </span>
          <p className="mt-4 text-sm font-semibold text-gray-500">{translate('page.dashboard.currentWorkspaceCount')}</p>
          <strong className="mt-1 block text-2xl font-extrabold text-gray-900">{availableSystemCount}</strong>
        </div>
      </section>

      <section className="erp-panel p-4">
        <div className="mb-3 flex items-center gap-2">
          <Boxes size={18} className="text-blue-600" />
          <h2 className="text-base font-bold text-gray-900">{translate('page.dashboard.businessEntry')}</h2>
        </div>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {runtimeMenus.length === 0 ? (
            <p className="text-sm text-gray-500">{translate('page.dashboard.noRuntimePages')}</p>
          ) : (
            runtimeMenus.map((menu) => {
              const path = menu.pageCode ? `/pages/${menu.pageCode}` : menu.routePath || '/home';
              return (
                <button
                  className="rounded-md border border-gray-200 px-3 py-3 text-left transition hover:border-blue-300 hover:bg-blue-50"
                  key={menu.menuCode}
                  onClick={() => navigate(path)}
                  type="button"
                >
                  <span className="block text-sm font-semibold text-gray-900">{menu.menuName}</span>
                  <span className="mt-1 block text-xs text-gray-500">{path}</span>
                </button>
              );
            })
          )}
        </div>
      </section>
    </main>
  );
}
