import { X } from 'lucide-react';
import type { ReactNode } from 'react';

interface WorkbenchDrawerFormProps {
  children: ReactNode;
  description?: ReactNode;
  footer?: ReactNode;
  open: boolean;
  title: string;
  width?: 'md' | 'lg' | 'xl';
  onClose: () => void;
}

export function WorkbenchDrawerForm({ children, description, footer, open, title, width = 'lg', onClose }: WorkbenchDrawerFormProps) {
  if (!open) {
    return null;
  }

  const widthClass = width === 'xl' ? 'max-w-[760px]' : width === 'md' ? 'max-w-[480px]' : 'max-w-[620px]';
  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/40 backdrop-blur-[2px]">
      <aside className={`flex h-full w-full ${widthClass} animate-[slideIn_.18s_ease-out] flex-col overflow-hidden rounded-l-lg bg-slate-50 shadow-2xl ring-1 ring-slate-950/10`}>
        <header className="sticky top-0 z-10 flex items-start justify-between gap-3 border-b border-slate-200 bg-white px-4 py-3">
          <div className="min-w-0">
            <h2 className="truncate text-base font-semibold text-slate-950">{title}</h2>
            {description ? <div className="mt-0.5 text-xs leading-5 text-slate-500">{description}</div> : null}
          </div>
          <button className="icon-button h-8 w-8 rounded-md border border-slate-200 bg-white text-slate-500 hover:bg-slate-50 hover:text-slate-900" type="button" onClick={onClose} aria-label="关闭">
            <X className="h-4 w-4" />
          </button>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">{children}</div>
        {footer ? <footer className="sticky bottom-0 flex justify-end gap-2 border-t border-slate-200 bg-white/95 px-4 py-3 shadow-[0_-8px_20px_rgba(15,23,42,0.05)] backdrop-blur">{footer}</footer> : null}
      </aside>
    </div>
  );
}
