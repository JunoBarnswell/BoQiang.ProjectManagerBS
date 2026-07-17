import type { ReactNode } from 'react';

interface TableActionsProps {
  children: ReactNode;
}

export function TableActions({ children }: TableActionsProps) {
  return (
    <div
      className="table-actions flex items-center gap-1 flex-nowrap"
      onClick={(event) => event.stopPropagation()}
      onDoubleClick={(event) => event.stopPropagation()}
    >
      {children}
    </div>
  );
}
