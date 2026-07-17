import type { ReactNode } from 'react';

interface TableToolbarProps {
  extra?: ReactNode;
  title?: ReactNode;
}

export function TableToolbar({ extra, title }: TableToolbarProps) {
  return (
    <div className="table-toolbar">
      <div className="table-toolbar-title">{title}</div>
      {extra ? <div className="table-toolbar-extra">{extra}</div> : null}
    </div>
  );
}
