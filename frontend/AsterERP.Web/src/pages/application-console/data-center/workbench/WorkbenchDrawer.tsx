import type { ReactNode } from 'react';

import { AppIcon } from '../../../../shared/icons/AppIcon';

interface WorkbenchDrawerProps {
  children: ReactNode;
  footer?: ReactNode;
  open: boolean;
  title: string;
  onClose: () => void;
}

export function WorkbenchDrawer({ children, footer, open, title, onClose }: WorkbenchDrawerProps) {
  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/40">
      <aside className="flex h-full w-full max-w-[640px] flex-col border-l border-slate-200 bg-white shadow-2xl">
        <header className="flex h-14 shrink-0 items-center justify-between border-b border-slate-200 px-5">
          <h2 className="text-base font-semibold text-slate-950">{title}</h2>
          <button className="rounded-md p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-900" type="button" onClick={onClose}>
            <AppIcon className="h-4 w-4" name="x" />
          </button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">{children}</div>
        {footer ? <footer className="flex shrink-0 justify-end gap-2 border-t border-slate-200 px-5 py-3">{footer}</footer> : null}
      </aside>
    </div>
  );
}
