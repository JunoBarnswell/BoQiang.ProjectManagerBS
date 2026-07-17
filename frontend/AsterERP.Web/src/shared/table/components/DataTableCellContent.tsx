import type { ReactNode } from 'react';

interface DataTableCellContentProps {
  children: ReactNode;
}

function getCellTitle(value: ReactNode): string | undefined {
  if (typeof value === 'string') {
    return value;
  }

  if (typeof value === 'number') {
    return String(value);
  }

  return undefined;
}

export function DataTableCellContent({ children }: DataTableCellContentProps) {
  return (
    <div className="data-table__cell-content" title={getCellTitle(children)}>
      {children}
    </div>
  );
}
