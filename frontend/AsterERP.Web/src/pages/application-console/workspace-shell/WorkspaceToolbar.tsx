import type { ReactNode } from 'react';

interface WorkspaceToolbarProps {
  actions?: ReactNode;
  description?: ReactNode;
  density?: 'compact' | 'tight';
  icon?: ReactNode;
  leading?: ReactNode;
  subtitle?: ReactNode;
  title: ReactNode;
}

export function WorkspaceToolbar({ actions, density = 'compact', description, icon, leading, subtitle, title }: WorkspaceToolbarProps) {
  const tight = density === 'tight';

  return (
    <section className={`flex flex-wrap items-center justify-between rounded-lg border border-slate-200 bg-white shadow-sm ${tight ? 'gap-1.5 px-2.5 py-1.5' : 'gap-2 px-3 py-2'}`}>
      <div className={`flex min-w-0 items-center ${tight ? 'gap-2' : 'gap-2.5'}`}>
        {leading}
        {icon ? <span className={`grid shrink-0 place-items-center rounded-md bg-slate-950 text-white ${tight ? 'h-7 w-7' : 'h-8 w-8'}`}>{icon}</span> : null}
        <div className="min-w-0">
          <div className={`truncate font-semibold text-slate-950 ${tight ? 'text-[13px]' : 'text-sm'}`}>{title}</div>
          {subtitle ? <div className={`truncate text-slate-500 ${tight ? 'text-[11px]' : 'text-xs'}`}>{subtitle}</div> : null}
          {!subtitle && description ? <div className={`truncate text-slate-500 ${tight ? 'text-[11px]' : 'text-xs'}`}>{description}</div> : null}
        </div>
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
    </section>
  );
}
