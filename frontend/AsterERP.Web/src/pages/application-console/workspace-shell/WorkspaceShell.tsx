import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';

import { AppIcon } from '../../../shared/icons/AppIcon';

import { WorkspacePanel } from './WorkspacePanel';

export interface WorkspaceShellNavItem {
  badge?: number | string | null;
  description?: string;
  icon: Parameters<typeof AppIcon>[0]['name'];
  key: string;
  partialMatch?: boolean;
  title: string;
  to: string;
}

interface WorkspaceShellProps {
  activeItemKey?: string;
  children: ReactNode;
  context?: ReactNode;
  density?: 'compact' | 'tight';
  navDescription?: string;
  navItems: WorkspaceShellNavItem[];
  navPlacement?: 'side' | 'top';
  navTitle: string;
  toolbar?: ReactNode;
}

export function WorkspaceShell({
  activeItemKey,
  children,
  context,
  density = 'compact',
  navDescription,
  navItems,
  navPlacement = 'side',
  navTitle,
  toolbar
}: WorkspaceShellProps) {
  const tight = density === 'tight';

  if (navPlacement === 'top') {
    return (
      <div className={`${tight ? 'space-y-1.5' : 'space-y-2'}`}>
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
          <div className={`bg-slate-50/85 ${tight ? 'px-2.5 py-1.5' : 'px-3 py-2'}`}>
            <nav aria-label={navDescription ? `${navTitle}：${navDescription}` : navTitle} className="flex min-w-0 flex-wrap items-center gap-1">
              {navItems.map((item) => (
                <WorkspaceTopNavLink active={activeItemKey === item.key} density={density} item={item} key={item.key} />
              ))}
            </nav>
          </div>
        </section>

        <div className={`grid min-h-0 ${tight ? 'gap-1.5' : 'gap-2'} ${context ? (tight ? 'xl:grid-cols-[minmax(0,1fr)_220px]' : 'xl:grid-cols-[minmax(0,1fr)_280px]') : ''}`}>
          <main className="min-w-0 space-y-2">
            {toolbar}
            {children}
          </main>

          {context ? <aside className="min-w-0">{context}</aside> : null}
        </div>
      </div>
    );
  }

  return (
    <div className={`workspace-shell-grid grid ${tight ? 'gap-1.5' : 'gap-2'} ${context ? (tight ? 'xl:grid-cols-[184px_minmax(0,1fr)_272px]' : 'xl:grid-cols-[204px_minmax(0,1fr)_300px]') : (tight ? 'xl:grid-cols-[184px_minmax(0,1fr)]' : 'xl:grid-cols-[204px_minmax(0,1fr)]')}`}>
      <aside className="min-h-0">
        <WorkspacePanel bodyClassName={tight ? 'space-y-1 p-1.5' : 'space-y-1 p-2'} description={navDescription} eyebrow="Workspace" title={navTitle}>
          <nav className="space-y-1">
            {navItems.map((item) => (
              <WorkspaceNavLink active={activeItemKey === item.key} density={density} item={item} key={item.key} />
            ))}
          </nav>
        </WorkspacePanel>
      </aside>

      <main className="min-w-0 space-y-2">
        {toolbar}
        {children}
      </main>

      {context ? <aside className="min-w-0">{context}</aside> : null}
    </div>
  );
}

function WorkspaceTopNavLink({ active, density, item }: { active: boolean; density: 'compact' | 'tight'; item: WorkspaceShellNavItem }) {
  const tight = density === 'tight';

  return (
    <NavLink
      aria-label={item.description ? `${item.title}：${item.description}` : item.title}
      className={({ isActive }) => [
        `group inline-flex shrink-0 items-center gap-1.5 rounded-md border transition ${tight ? 'h-8 px-2' : 'h-9 px-2.5'}`,
        isActive || active
          ? 'border-primary-300 bg-primary-50 text-primary-800 shadow-sm'
          : 'border-slate-200 bg-white text-slate-700 hover:border-primary-200 hover:text-primary-700'
      ].join(' ')}
      end={!item.partialMatch}
      title={item.description ?? item.title}
      to={item.to}
    >
      <AppIcon className={tight ? 'h-3.5 w-3.5' : 'h-4 w-4'} name={item.icon} />
      <span className={`max-w-[92px] truncate font-semibold ${tight ? 'text-xs' : 'text-[13px]'}`}>{item.title}</span>
      {item.badge == null ? null : (
        <span className={`rounded-full bg-white font-medium text-slate-600 shadow-sm ${tight ? 'px-1 py-0.5 text-[9px]' : 'px-1.5 py-0.5 text-[10px]'}`}>
          {item.badge}
        </span>
      )}
    </NavLink>
  );
}

function WorkspaceNavLink({ active, density, item }: { active: boolean; density: 'compact' | 'tight'; item: WorkspaceShellNavItem }) {
  const tight = density === 'tight';

  return (
    <NavLink
      className={({ isActive }) => [
        `group block rounded-lg border transition ${tight ? 'px-2 py-1.5' : 'px-2.5 py-2'}`,
        isActive || active
          ? 'border-primary-300 bg-primary-50/90 shadow-sm'
          : 'border-slate-200 bg-slate-50/70 hover:border-primary-200 hover:bg-white'
      ].join(' ')}
      end={!item.partialMatch}
      to={item.to}
    >
      {({ isActive }) => (
        <div className={`flex items-start ${tight ? 'gap-2' : 'gap-2.5'}`}>
          <span className={`mt-0.5 inline-flex shrink-0 items-center justify-center rounded-md border bg-white transition ${tight ? 'h-6 w-6' : 'h-7 w-7'} ${isActive || active ? 'border-primary-200 text-primary-700' : 'border-slate-200 text-slate-600 group-hover:border-primary-200 group-hover:text-primary-700'}`}>
            <AppIcon className={tight ? 'h-3 w-3' : 'h-3.5 w-3.5'} name={item.icon} />
          </span>
          <span className="min-w-0 flex-1">
            <span className="flex items-center justify-between gap-2">
              <span className={`truncate font-semibold text-slate-900 ${tight ? 'text-xs' : 'text-[13px]'}`}>{item.title}</span>
              {item.badge == null ? null : (
                <span className={`rounded-full bg-white font-medium text-slate-600 shadow-sm ${tight ? 'px-1 py-0.5 text-[9px]' : 'px-1.5 py-0.5 text-[10px]'}`}>
                  {item.badge}
                </span>
              )}
            </span>
            {item.description ? <span className={`mt-0.5 block text-slate-500 ${tight ? 'text-[10px] leading-4' : 'text-[11px] leading-[18px]'}`}>{item.description}</span> : null}
          </span>
        </div>
      )}
    </NavLink>
  );
}
