import { Braces, Database, GitBranch, Table2 } from 'lucide-react';

import type { WorkbenchTab, WorkbenchTabDefinition } from '../workbenchTypes';

interface WorkbenchTabsProps {
  active: WorkbenchTab;
  tabs: WorkbenchTabDefinition[];
  onChange: (tab: WorkbenchTab) => void;
}

const icons = {
  mapping: GitBranch,
  microflows: GitBranch,
  tables: Table2,
  views: Braces
} satisfies Record<WorkbenchTab, typeof Database>;

export function WorkbenchTabs({ active, tabs, onChange }: WorkbenchTabsProps) {
  return (
    <nav className="flex gap-0.5 overflow-x-auto border-b border-slate-200 px-1">
      {tabs.map((tab) => {
        const Icon = icons[tab.key];
        const selected = active === tab.key;
        return (
          <button
            className={[
              'group relative flex min-w-28 items-center gap-1.5 rounded-t-md px-2.5 py-2 text-left text-xs transition',
              selected ? 'bg-white text-primary-700 shadow-sm ring-1 ring-slate-200 before:absolute before:inset-x-0 before:bottom-0 before:h-0.5 before:bg-primary-600' : 'text-slate-500 hover:bg-white/70 hover:text-slate-950'
            ].join(' ')}
            key={tab.key}
            type="button"
            onClick={() => onChange(tab.key)}
          >
            <Icon className="h-3.5 w-3.5 shrink-0" />
            <span className="min-w-0">
              <span className="block truncate font-medium">{tab.title}</span>
              {tab.description ? <span className="block truncate text-[11px] text-slate-400">{tab.description}</span> : null}
            </span>
            {typeof tab.badge === 'number' ? (
              <span className="ml-auto rounded-full bg-slate-100 px-1.5 py-0.5 text-[11px] text-slate-500">{tab.badge}</span>
            ) : null}
          </button>
        );
      })}
    </nav>
  );
}
