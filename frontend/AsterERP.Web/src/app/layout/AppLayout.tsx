import { useEffect, useMemo } from 'react';
import { Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom';

import { findRouteMeta } from '@/app/navigation/routes';

import { findMenuNodeByPath } from '../../core/auth/menuUtils';
import { getActiveTokenSlot, hasPlatformAccessToken } from '../../core/http/tokenStorage';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useAuthStore, useMenuStore, useTabStore, useThemeStore, useWorkspaceStore } from '../../core/state';
import { ImConversationDrawer } from '../../features/im/components/ImConversationDrawer';
import { ImProvider } from '../../features/im/components/ImProvider';
import { ImUnreadEntry } from '../../features/im/components/ImUnreadEntry';
import { ProjectManagementImConversationTargetLink } from '../../features/project-management/im/ProjectManagementImConversationTargetLink';
import { ProjectManagementNotificationEntry } from '../../features/project-management/notifications/ProjectManagementNotificationEntry';
import { resolveMenuLabel } from '../navigation/menuLabels';

import { BasicLayout } from './BasicLayout';

export function AppLayout() {
  const { locale, setLocale, translate } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const openTabs = useTabStore((state) => state.openTabs);
  const addTab = useTabStore((state) => state.addTab);
  const closeTab = useTabStore((state) => state.closeTab);
  const refreshTab = useTabStore((state) => state.refreshTab);
  const resetTabs = useTabStore((state) => state.resetTabs);
  const theme = useThemeStore((state) => state.theme);
  const setTheme = useThemeStore((state) => state.setTheme);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const logout = useAuthStore((state) => state.logout);
  const switchPlatform = useAuthStore((state) => state.switchPlatform);
  const clearWorkspace = useWorkspaceStore((state) => state.clearWorkspace);
  const menus = useMenuStore((state) => state.menus);
  const user = useAuthStore((state) => state.user);
  const branding = useWorkspaceStore((state) => state.branding);
  const currentWorkspace = useWorkspaceStore((state) => state.currentWorkspace);
  const locationKey = `${location.pathname}${location.search}`;
  const menuLookupPath = useMemo(
    () => toWorkspaceLocalPath(locationKey, currentWorkspace?.tenantId, currentWorkspace?.appCode, currentWorkspace?.workspaceLevel),
    [currentWorkspace?.appCode, currentWorkspace?.tenantId, currentWorkspace?.workspaceLevel, locationKey]
  );
  const currentMenu = useMemo(() => findMenuNodeByPath(menus, menuLookupPath), [menuLookupPath, menus]);
  const currentRoute = useMemo(() => findRouteMeta(location.pathname), [location.pathname]);
  const currentMenuLabel = useMemo(
    () => (currentMenu ? resolveMenuLabel(currentMenu, translate) : null),
    [currentMenu, translate]
  );
  const workspaceHomePath = useMemo(
    () => toWorkspaceDisplayPath('/home', currentWorkspace?.tenantId, currentWorkspace?.appCode, currentWorkspace?.workspaceLevel),
    [currentWorkspace?.appCode, currentWorkspace?.tenantId, currentWorkspace?.workspaceLevel]
  );
  const canReturnPlatform = currentWorkspace?.workspaceLevel === 'application' && getActiveTokenSlot() === 'platform' && hasPlatformAccessToken();

  useEffect(() => {
    if (!isAuthenticated) {
      resetTabs([]);
      return;
    }

    const localResolvedPath = currentMenu?.routePath ?? (location.search || currentRoute.tabMode === 'detail' ? menuLookupPath : currentRoute.path);
    const resolvedPath = toWorkspaceDisplayPath(localResolvedPath, currentWorkspace?.tenantId, currentWorkspace?.appCode, currentWorkspace?.workspaceLevel);
    const nextTab = {
      cacheKey: resolvedPath,
      closable: !isDefaultWorkspacePath(resolvedPath, currentWorkspace?.workspaceLevel),
      id: resolvedPath,
      isDefault: openTabs.length === 0 && isDefaultWorkspacePath(resolvedPath, currentWorkspace?.workspaceLevel),
      label: currentMenuLabel ?? translate(currentRoute.labelKey),
      path: resolvedPath,
      refreshToken: openTabs.find((tab) => tab.path === resolvedPath)?.refreshToken ?? 0,
      title: currentMenuLabel ?? translate(currentRoute.labelKey)
    };

    if (currentRoute.cachePolicy !== 'none' || isDefaultWorkspacePath(resolvedPath, currentWorkspace?.workspaceLevel)) {
      addTab(nextTab);
    }
  }, [
    addTab,
    currentMenuLabel,
    currentMenu?.routePath,
    currentRoute.labelKey,
    currentRoute.cachePolicy,
    currentRoute.path,
    currentRoute.tabMode,
    currentWorkspace?.appCode,
    currentWorkspace?.tenantId,
    currentWorkspace?.workspaceLevel,
    isAuthenticated,
    location.search,
    locationKey,
    menuLookupPath,
    openTabs,
    resetTabs,
    translate
  ]);

  const breadcrumbItems = useMemo(
    () => [currentMenuLabel ?? translate(currentRoute.breadcrumbKey)],
    [currentMenuLabel, currentRoute.breadcrumbKey, translate]
  );

  const tabs = useMemo(
    () =>
      openTabs.map((tab) => {
        const menuPath = toWorkspaceLocalPath(tab.path, currentWorkspace?.tenantId, currentWorkspace?.appCode, currentWorkspace?.workspaceLevel);
        const menuNode = findMenuNodeByPath(menus, menuPath);
        if (menuNode) {
          return {
            closable: tab.closable,
            label: resolveMenuLabel(menuNode, translate),
            path: tab.path
          };
        }

        const routeMeta = findRouteMeta(tab.path.split('?')[0] ?? tab.path);
        const label = tab.label.startsWith('nav.') || tab.label.startsWith('breadcrumbs.')
          ? translate(tab.label)
          : translate(routeMeta.labelKey);

        return {
          closable: tab.closable,
          label,
          path: tab.path
        };
      }),
    [currentWorkspace?.appCode, currentWorkspace?.tenantId, currentWorkspace?.workspaceLevel, menus, openTabs, translate]
  );

  const activeTab = useMemo(() => openTabs.find((tab) => tab.path === locationKey), [locationKey, openTabs]);

  if (!isAuthenticated) {
    return <Navigate replace to="/login" />;
  }

  return (
    <ImProvider>
      <BasicLayout
      activePath={locationKey}
      breadcrumbItems={breadcrumbItems}
      contentKey={`${locationKey}:${activeTab?.refreshToken ?? 0}`}
      currentUserName={user?.displayName ?? user?.userName}
      headerExtra={<ImUnreadEntry />}
      notificationEntry={<ProjectManagementNotificationEntry />}
      locale={locale}
      onCloseCurrent={() => {
        if (locationKey === workspaceHomePath) {
          return;
        }

        const currentIndex = openTabs.findIndex((tab) => tab.path === locationKey);
        const fallbackPath = openTabs[currentIndex - 1]?.path ?? openTabs[currentIndex + 1]?.path ?? workspaceHomePath;
        closeTab(locationKey);
        navigate(fallbackPath, { replace: true });
      }}
      homePath={workspaceHomePath}
      onLocaleChange={(nextLocale) => setLocale(nextLocale)}
      onThemeChange={(nextTheme) => setTheme(nextTheme)}
      onLogout={() => {
        logout();
        navigate('/login', { replace: true });
      }}
      onReturnPlatform={canReturnPlatform
        ? async () => {
            try {
              const response = await switchPlatform({ target: 'application-center' });
              navigate(response.defaultRoutePath || response.currentWorkspace.defaultRoutePath || '/platform/applications', { replace: true });
            } catch {
              logout();
              navigate('/login', { replace: true });
            }
          }
        : undefined}
      onSwitchSystem={() => {
        clearWorkspace();
        navigate('/workspace', { replace: true });
      }}
      onRefreshCurrent={() => refreshTab(locationKey)}
      onTabClick={(path) => navigate(path)}
      onTabClose={(path) => {
        const nextTabs = openTabs.filter((tab) => tab.path !== path);
        closeTab(path);

        if (path !== locationKey) {
          return;
        }

        const fallbackPath = nextTabs[0]?.path ?? workspaceHomePath;
        navigate(fallbackPath, { replace: true });
      }}
      onCloseOthers={() => {
        const currentTab = openTabs.find((tab) => tab.path === locationKey);
        const homeTab = openTabs.find((tab) => tab.isDefault || tab.path === workspaceHomePath);
        const nextTabs = [];
        if (homeTab) nextTabs.push(homeTab);
        if (currentTab && currentTab.path !== workspaceHomePath) nextTabs.push(currentTab);
        resetTabs(nextTabs);
      }}
      onCloseAll={() => {
        resetTabs([]);
        navigate(workspaceHomePath);
      }}
      menuTree={menus}
      tabs={tabs}
      subtitle={currentWorkspace ? `${currentWorkspace.tenantName} / ${currentWorkspace.systemName || currentWorkspace.appName}` : currentMenuLabel ?? translate(currentRoute.labelKey)}
      theme={theme}
      title={branding?.systemName ?? translate('app.title')}
      workspaceAppCode={currentWorkspace?.appCode}
      workspaceLevel={currentWorkspace?.workspaceLevel}
      workspaceTenantId={currentWorkspace?.tenantId}
    >
      <Outlet />
      </BasicLayout>
      <ImConversationDrawer
        renderConversationContext={(conversation) => conversation.conversationType === 'Group' && conversation.conversationKey.startsWith('pm:')
          ? <ProjectManagementImConversationTargetLink conversationId={conversation.id} />
          : null}
      />
    </ImProvider>
  );
}

function toWorkspaceLocalPath(path: string, tenantId?: string | null, appCode?: string | null, workspaceLevel?: 'application' | 'platform'): string {
  if (workspaceLevel !== 'application' || !tenantId || !appCode) {
    return path;
  }

  const prefix = `/tenants/${encodeURIComponent(tenantId)}/apps/${appCode.toUpperCase()}/admin`;
  const pathOnly = path.split('?')[0] ?? path;
  if (!pathOnly.toUpperCase().startsWith(prefix.toUpperCase())) {
    return path;
  }

  const suffix = path.slice(prefix.length);
  return suffix || '/home';
}

function toWorkspaceDisplayPath(path: string, tenantId?: string | null, appCode?: string | null, workspaceLevel?: 'application' | 'platform'): string {
  if (workspaceLevel !== 'application' || !tenantId || !appCode || path.startsWith('/apps/')) {
    return path;
  }

  const routePatternPrefix = '/tenants/:tenantId/apps/:appCode/admin';
  if (path.startsWith(routePatternPrefix)) {
    const suffix = path.slice(routePatternPrefix.length) || '/home';
    return `/tenants/${encodeURIComponent(tenantId)}/apps/${appCode.toUpperCase()}/admin${suffix}`;
  }

  if (path.startsWith('/tenants/')) {
    return path;
  }

  const normalized = path.startsWith('/') ? path : `/${path}`;
  return `/tenants/${encodeURIComponent(tenantId)}/apps/${appCode.toUpperCase()}/admin${normalized}`;
}

function isDefaultWorkspacePath(path: string, workspaceLevel?: 'application' | 'platform'): boolean {
  if (workspaceLevel === 'application') {
    return /\/tenants\/[^/]+\/apps\/[^/]+\/admin\/home$/i.test(path);
  }

  return path === '/home' || path === '/platform/applications';
}
