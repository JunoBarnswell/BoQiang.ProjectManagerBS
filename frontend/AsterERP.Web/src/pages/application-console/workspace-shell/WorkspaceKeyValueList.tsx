import type { ReactNode } from 'react';

interface WorkspaceKeyValueItem {
  label: ReactNode;
  value: ReactNode;
}

interface WorkspaceKeyValueListProps {
  items: WorkspaceKeyValueItem[];
}

export function WorkspaceKeyValueList({ items }: WorkspaceKeyValueListProps) {
  return (
    <div className="space-y-1.5">
      {items.map((item, index) => (
        <div className="flex items-start justify-between gap-3 rounded-md border border-slate-100 bg-slate-50 px-2.5 py-2 text-[11px]" key={`${index}-${String(item.label)}`}>
          <span className="text-slate-500">{item.label}</span>
          <span className="text-right font-medium text-slate-900">{item.value}</span>
        </div>
      ))}
    </div>
  );
}
