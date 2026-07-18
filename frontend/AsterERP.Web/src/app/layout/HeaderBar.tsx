import type { ReactNode } from 'react';

import type { AppLocale, ThemeMode } from '../../core/config/env';
import { useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../../shared/icons/AppIcon';

import { DisplayPreferenceControl } from './DisplayPreferenceControl';

interface HeaderBarProps {
  currentUserName?: string;
  headerExtra?: ReactNode;
  notificationEntry?: ReactNode;
  locale: AppLocale;
  onLocaleChange: (nextLocale: AppLocale) => void;
  onThemeChange: (nextTheme: ThemeMode) => void;
  onLogout: () => void;
  onReturnPlatform?: () => void;
  onSwitchSystem: () => void;
  onToggleSidebar: () => void;
  showSidebarToggle: boolean;
  subtitle: string;
  theme: ThemeMode;
  title: string;
  workspaceLevel?: 'application' | 'platform';
}

export function HeaderBar({
  currentUserName,
  headerExtra,
  notificationEntry,
  locale,
  onLocaleChange,
  onLogout,
  onReturnPlatform,
  onSwitchSystem,
  subtitle,
  title,
  workspaceLevel
}: HeaderBarProps) {
  const { translate } = useI18n();
  return (
    <header className="app-header-bar bg-primary-700 text-white flex items-center justify-between gap-2 px-2 sm:px-3 shrink-0 shadow-sm z-20">
      {/* Logo 区域 */}
      <div className="flex min-w-0 items-center gap-2 md:w-44">
        <AppIcon className="shrink-0 text-lg text-primary-200" name="cube" />
        <span className="max-w-[128px] truncate text-sm font-bold tracking-wide sm:max-w-none" title={title}>
          {title}
        </span>
      </div>

      {/* 顶部搜索与全局功能 */}
      <div className="flex min-w-0 flex-1 items-center justify-end gap-2 pr-1 md:justify-between md:pl-4 md:pr-2">
        <div className="flex shrink-0 items-center gap-2 text-primary-100 sm:gap-2.5 lg:gap-3">
          {workspaceLevel === 'application' && onReturnPlatform ? (
            <button
              className="inline-flex items-center gap-1.5 rounded border border-primary-500 bg-primary-800 px-2 py-1 text-xs font-medium text-white transition-colors hover:bg-primary-600"
              aria-label={translate('layout.returnPlatform')}
              title={translate('layout.returnPlatform')}
              type="button"
              onClick={onReturnPlatform}
            >
              <AppIcon className="text-sm" name="arrow-left" />
              <span className="hidden sm:inline">{translate('layout.returnPlatform')}</span>
            </button>
          ) : null}

          <DisplayPreferenceControl />

          <button
            className="hover:text-white transition-colors cursor-pointer"
            title={translate('layout.language')}
            onClick={() => onLocaleChange(locale === 'zh-CN' ? 'en-US' : 'zh-CN')}
          >
            <AppIcon className="text-lg" name="translate" />
          </button>
          {notificationEntry}
          {headerExtra}
          <button className="hidden hover:text-white transition-colors cursor-pointer sm:block" title={translate('layout.settings')}>
            <AppIcon className="text-lg" name="gear" />
          </button>

          <div className="hidden h-4 w-px bg-primary-600 sm:block"></div>

          <button
            className="inline-flex items-center gap-1.5 rounded border border-primary-500 bg-primary-600 px-2 py-1 text-xs font-medium text-white transition-colors hover:bg-primary-500"
            aria-label={translate('layout.switchSystem')}
            title={translate('layout.switchSystem')}
            type="button"
            onClick={onSwitchSystem}
          >
            <AppIcon className="text-sm" name="squares-four" />
            <span className="hidden sm:inline">{translate('layout.switchSystem')}</span>
          </button>

          <div className="hidden items-center gap-2 px-1.5 py-0.5 rounded transition-colors sm:flex" title={subtitle}>
            <div className="w-6 h-6 bg-primary-500 rounded text-xs flex items-center justify-center font-bold border border-primary-400">
              {currentUserName?.charAt(0).toUpperCase() || 'U'}
            </div>
            <div className="flex flex-col leading-none">
              <span className="text-xs text-white">{currentUserName || translate('layout.user.fallback')}</span>
              <span className="text-[10px] text-primary-300 mt-0.5">{subtitle}</span>
            </div>
          </div>

          <button className="hover:text-white transition-colors cursor-pointer" title={translate('layout.logout')} type="button" onClick={onLogout}>
            <AppIcon className="text-lg" name="sign-out" />
          </button>
        </div>
      </div>
    </header>
  );
}
