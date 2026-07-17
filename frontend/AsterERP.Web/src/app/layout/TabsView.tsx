import { useEffect, useRef, useState } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../../shared/icons/AppIcon';

interface TabItem {
  closable?: boolean;
  label: string;
  path: string;
}

interface TabsViewProps {
  activePath: string;
  homePath?: string;
  onCloseAll?: () => void;
  onCloseCurrent?: () => void;
  onCloseOthers?: () => void;
  onRefreshCurrent?: () => void;
  onTabClick: (path: string) => void;
  onTabClose?: (path: string) => void;
  tabs: TabItem[];
}

export function TabsView({
  activePath,
  homePath = '/home',
  onCloseAll,
  onCloseCurrent,
  onCloseOthers,
  onRefreshCurrent,
  onTabClick,
  onTabClose,
  tabs
}: TabsViewProps) {
  const { translate } = useI18n();
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setIsMenuOpen(false);
      }
    }
    if (isMenuOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isMenuOpen]);

  return (
    <div className="app-tabs-view bg-[#e2e8f0] flex min-w-0 items-end gap-1 overflow-x-auto px-1.5 pt-1 border-b border-gray-300 shrink-0">
      <div
        className={`erp-tab px-3 py-1 flex items-center gap-1.5 cursor-pointer text-xs whitespace-nowrap ${activePath === homePath || activePath === '/' ? 'active shadow-[0_-1px_2px_rgba(0,0,0,0.02)]' : ''}`}
        onClick={() => onTabClick(homePath)}
      >
        <AppIcon name="house" />
      </div>

      <div className="flex min-w-0 flex-1 items-end overflow-x-auto">
        {tabs.filter((tab) => tab.path !== '/' && tab.path !== homePath).map((tab) => {
          const isActive = activePath === tab.path;
          return (
            <div
              key={tab.path}
              className={`erp-tab px-3 py-1 flex items-center gap-1.5 cursor-pointer text-xs whitespace-nowrap ${isActive ? 'active shadow-[0_-1px_2px_rgba(0,0,0,0.02)]' : ''}`}
              onClick={() => onTabClick(tab.path)}
            >
              {isActive ? <span className="w-1.5 h-1.5 rounded-full bg-primary-500 shrink-0" /> : null}
              <span className="whitespace-nowrap">{tab.label}</span>
              {tab.closable !== false ? (
                <AppIcon
                  className={`rounded p-0.5 ml-1 -mr-1 transition-colors shrink-0 ${isActive ? 'hover:text-red-500 hover:bg-gray-100' : 'hover:text-red-500 hover:bg-gray-200'}`}
                  name="x"
                  onClick={(event) => {
                    event.stopPropagation();
                    onTabClose?.(tab.path);
                  }}
                />
              ) : null}
            </div>
          );
        })}
      </div>

      <div className="mb-0.5 flex shrink-0 items-center relative" ref={menuRef}>
        <button
          className="text-gray-500 hover:text-gray-700 bg-white border border-gray-300 rounded px-2 py-0.5 text-xs flex items-center gap-1 shadow-sm whitespace-nowrap"
          onClick={() => setIsMenuOpen(!isMenuOpen)}
        >
          {translate('tabs.actions')} <AppIcon name="caret-down" />
        </button>

        {isMenuOpen ? (
          <div className="absolute right-0 top-full mt-1 w-32 bg-white border border-gray-200 rounded shadow-lg z-50 overflow-hidden py-1">
            <button
              className="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 flex items-center gap-2 text-gray-700"
              onClick={() => {
                onRefreshCurrent?.();
                setIsMenuOpen(false);
              }}
            >
              <AppIcon name="arrows-clockwise" /> {translate('tabs.refreshCurrent')}
            </button>
            <button
              className="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 flex items-center gap-2 text-gray-700"
              onClick={() => {
                onCloseCurrent?.();
                setIsMenuOpen(false);
              }}
            >
              <AppIcon name="x" /> {translate('tabs.closeCurrent')}
            </button>
            <button
              className="w-full text-left px-3 py-1.5 text-sm hover:bg-gray-100 flex items-center gap-2 text-gray-700"
              onClick={() => {
                onCloseOthers?.();
                setIsMenuOpen(false);
              }}
            >
              <AppIcon name="corners-out" /> {translate('tabs.closeOthers')}
            </button>
            <div className="h-px bg-gray-200 my-1" />
            <button
              className="w-full text-left px-3 py-1.5 text-sm hover:bg-red-50 flex items-center gap-2 text-red-600"
              onClick={() => {
                onCloseAll?.();
                setIsMenuOpen(false);
              }}
            >
              <AppIcon name="trash" /> {translate('tabs.closeAll')}
            </button>
          </div>
        ) : null}
      </div>
    </div>
  );
}
