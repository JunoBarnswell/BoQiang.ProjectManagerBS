import { useState, type ReactNode } from 'react';

import type { MenuTreeNodeDto } from '../../api/system/system.types';
import { appEnv, type AppLocale, type ThemeMode } from '../../core/config/env';
import { formatMessage } from '../../core/i18n/formatMessage';
import { useI18n } from '../../core/i18n/I18nProvider';

import { HeaderBar } from './HeaderBar';
import { SidebarMenu } from './SidebarMenu';
import { TabsView } from './TabsView';

interface BasicLayoutProps {
  activePath: string;
  breadcrumbItems: string[];
  children: ReactNode;
  contentKey?: string;
  currentUserName?: string;
  headerExtra?: ReactNode;
  homePath?: string;
  locale: AppLocale;
  onCloseCurrent?: () => void;
  onLocaleChange: (nextLocale: AppLocale) => void;
  onThemeChange: (nextTheme: ThemeMode) => void;
  onLogout: () => void;
  onReturnPlatform?: () => void;
  onSwitchSystem: () => void;
  onRefreshCurrent?: () => void;
  onTabClose?: (path: string) => void;
  onTabClick: (path: string) => void;
  onCloseOthers?: () => void;
  onCloseAll?: () => void;
  menuTree: MenuTreeNodeDto[];
  tabs: Array<{
    closable?: boolean;
    label: string;
    path: string;
  }>;
  subtitle: string;
  theme: ThemeMode;
  title: string;
  workspaceAppCode?: string | null;
  workspaceLevel?: 'application' | 'platform';
  workspaceTenantId?: string | null;
}

export function BasicLayout({
  activePath,
  children,
  contentKey,
  currentUserName,
  headerExtra,
  homePath,
  locale,
  onCloseCurrent,
  onLocaleChange,
  onThemeChange,
  onLogout,
  onReturnPlatform,
  onSwitchSystem,
  onRefreshCurrent,
  onTabClose,
  onTabClick,
  onCloseOthers,
  onCloseAll,
  menuTree,
  tabs,
  subtitle,
  theme,
  title,
  workspaceAppCode,
  workspaceLevel,
  workspaceTenantId
}: BasicLayoutProps) {
  const { translate } = useI18n();
  const [sidebarOpen, setSidebarOpen] = useState(true);

  return (
    <>
      <HeaderBar
        currentUserName={currentUserName}
        headerExtra={headerExtra}
        locale={locale}
        onLocaleChange={onLocaleChange}
        onThemeChange={onThemeChange}
        onLogout={onLogout}
        onReturnPlatform={onReturnPlatform}
        onSwitchSystem={onSwitchSystem}
        onToggleSidebar={() => setSidebarOpen((current) => !current)}
        showSidebarToggle={true}
        subtitle={subtitle}
        theme={theme}
        title={title}
        workspaceLevel={workspaceLevel}
      />
      <div className="flex min-h-0 min-w-0 flex-1 overflow-hidden">
        <SidebarMenu
          menuTree={menuTree}
          onClose={() => setSidebarOpen(false)}
          onNavigate={() => {}}
          open={sidebarOpen}
          pinned={true}
          subtitle={subtitle}
          title={title}
          activePath={activePath}
          onToggle={() => setSidebarOpen((current) => !current)}
          workspaceAppCode={workspaceAppCode}
          workspaceLevel={workspaceLevel}
          workspaceTenantId={workspaceTenantId}
        />
        <main className="relative flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden bg-gray-100">
          <TabsView
            activePath={activePath}
            homePath={homePath}
            onTabClose={onTabClose}
            onTabClick={onTabClick}
            onCloseCurrent={onCloseCurrent}
            onCloseOthers={onCloseOthers}
            onCloseAll={onCloseAll}
            onRefreshCurrent={onRefreshCurrent}
            tabs={tabs}
          />
          <div className="app-content-region flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden" key={contentKey}>
            {children}
          </div>
          <div className="app-status-bar bg-gray-200 border-t border-gray-300 text-gray-500 flex items-center justify-between gap-2 px-2 sm:px-3 shrink-0">
            <span>{translate('layout.status.ready')}</span>
            <span className="hidden sm:inline">{formatMessage(translate('layout.status.environment'), { environment: appEnv.mode })}</span>
          </div>
        </main>
      </div>
    </>
  );
}
