import type { ReactNode } from 'react';

interface WorkspacePanelProps {
  actions?: ReactNode;
  bodyClassName?: string;
  children: ReactNode;
  className?: string;
  description?: ReactNode;
  eyebrow?: ReactNode;
  title?: ReactNode;
}

export function WorkspacePanel({
  actions,
  bodyClassName,
  children,
  className,
  description,
  eyebrow,
  title
}: WorkspacePanelProps) {
  const hasHeader = Boolean(actions || description || eyebrow || title);

  return (
    <section className={['flex flex-col overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm', className ?? ''].filter(Boolean).join(' ')}>
      {hasHeader ? (
        <header className="flex items-start justify-between gap-3 border-b border-slate-200 bg-slate-50/85 px-3 py-2">
          <div className="min-w-0">
            {eyebrow ? <div className="text-[10px] font-semibold uppercase tracking-[0.16em] text-slate-500">{eyebrow}</div> : null}
            {title ? <div className="truncate text-sm font-semibold text-slate-950">{title}</div> : null}
            {description ? <div className="mt-0.5 text-[11px] leading-5 text-slate-500">{description}</div> : null}
          </div>
          {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
        </header>
      ) : null}
      <div className={bodyClassName ?? 'p-3'}>{children}</div>
    </section>
  );
}
