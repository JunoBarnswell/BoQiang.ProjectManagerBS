import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';

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
  const tabRefs = useRef<Record<string, HTMLButtonElement | null>>({});
  const visibleTabs = useMemo(() => tabs.filter((tab) => tab.path !== '/' && tab.path !== homePath), [homePath, tabs]);
  const navigationTabs = useMemo(() => [{ closable: false, label: translate('nav.home'), path: homePath }, ...visibleTabs], [homePath, translate, visibleTabs]);

  const onTabKeyDown = (event: KeyboardEvent<HTMLButtonElement>, index: number, tab: TabItem) => {
    if ((event.key === 'Delete' || event.key === 'Backspace') && tab.closable !== false) {
      event.preventDefault();
      onTabClose?.(tab.path);
      return;
    }
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const targetIndex = event.key === 'Home'
      ? 0
      : event.key === 'End'
        ? navigationTabs.length - 1
        : (index + (event.key === 'ArrowRight' ? 1 : -1) + navigationTabs.length) % navigationTabs.length;
    const target = navigationTabs[targetIndex];
    if (!target) return;
    tabRefs.current[target.path]?.focus();
    onTabClick(target.path);
  };

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
    <div aria-label="已打开页签" className="app-tabs-view bg-[#e2e8f0] flex min-w-0 items-end gap-1 overflow-x-auto px-1.5 pt-1 border-b border-gray-300 shrink-0" role="tablist">
      <button
        aria-label={translate('nav.home')}
        aria-selected={activePath === homePath || activePath === '/'}
        className={`erp-tab px-3 py-1 flex items-center gap-1.5 cursor-pointer text-xs whitespace-nowrap ${activePath === homePath || activePath === '/' ? 'active shadow-[0_-1px_2px_rgba(0,0,0,0.02)]' : ''}`}
        onClick={() => onTabClick(homePath)}
        onKeyDown={(event) => onTabKeyDown(event, 0, navigationTabs[0])}
        ref={(element) => { tabRefs.current[homePath] = element; }}
        role="tab"
        type="button"
      >
        <AppIcon name="house" />
      </button>

      <div className="flex min-w-0 flex-1 items-end overflow-x-auto">
        {visibleTabs.map((tab, index) => {
          const isActive = activePath === tab.path;
          return (
            <div
              key={tab.path}
              className={`erp-tab px-2 py-1 flex items-center gap-1.5 text-xs whitespace-nowrap ${isActive ? 'active shadow-[0_-1px_2px_rgba(0,0,0,0.02)]' : ''}`}
            >
              <button
                aria-selected={isActive}
                className="flex min-w-0 items-center gap-1.5"
                onClick={() => onTabClick(tab.path)}
                onKeyDown={(event) => onTabKeyDown(event, index + 1, tab)}
                ref={(element) => { tabRefs.current[tab.path] = element; }}
                role="tab"
                type="button"
              >
                {isActive ? <span className="w-1.5 h-1.5 rounded-full bg-primary-500 shrink-0" /> : null}
                <span className="whitespace-nowrap">{tab.label}</span>
              </button>
              {tab.closable !== false ? (
                <button
                  aria-label={`关闭页签 ${tab.label}`}
                  className={`rounded p-0.5 ml-1 -mr-1 transition-colors shrink-0 ${isActive ? 'hover:text-red-500 hover:bg-gray-100' : 'hover:text-red-500 hover:bg-gray-200'}`}
                  onClick={(event) => {
                    event.stopPropagation();
                    onTabClose?.(tab.path);
                  }}
                  type="button"
                >
                  <AppIcon name="x" />
                </button>
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
