import type { ReactNode } from 'react';

interface WorkspaceEmptyStateProps {
  children: ReactNode;
  className?: string;
}

export function WorkspaceEmptyState({ children, className }: WorkspaceEmptyStateProps) {
  return (
    <div className={['grid place-items-center rounded-lg border border-dashed border-slate-300 bg-slate-50 px-4 py-10 text-center text-sm text-slate-500', className ?? ''].filter(Boolean).join(' ')}>
      {children}
    </div>
  );
}
