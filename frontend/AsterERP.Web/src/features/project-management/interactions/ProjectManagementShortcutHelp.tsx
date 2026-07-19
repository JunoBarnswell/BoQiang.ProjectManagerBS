import { useProjectManagementEscapeLayer } from './ProjectManagementEscapeStack';
import { projectManagementShortcutHelpItems } from './projectManagementShortcutDefinitions';

interface ProjectManagementShortcutHelpProps {
  open: boolean;
  onClose: () => void;
}

export function ProjectManagementShortcutHelp({ onClose, open }: ProjectManagementShortcutHelpProps) {
  useProjectManagementEscapeLayer(open, onClose);
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-slate-900/35 px-4 py-6" role="presentation" onMouseDown={onClose}>
      <section aria-label="项目管理快捷键" aria-modal="true" className="w-full max-w-xl overflow-hidden rounded-lg border border-slate-200 bg-white shadow-2xl" role="dialog" onMouseDown={(event) => event.stopPropagation()}>
        <header className="flex items-center justify-between border-b border-slate-100 px-5 py-4">
          <h2 className="m-0 text-base font-semibold text-slate-900">项目管理快捷键</h2>
          <button aria-label="关闭快捷键帮助" className="x" type="button" onClick={onClose}>×</button>
        </header>
        <ul className="m-0 divide-y divide-slate-100 p-0" aria-label="快捷键清单">
          {projectManagementShortcutHelpItems.map((item, index) => (
            <li className="flex items-center justify-between gap-4 px-5 py-3 text-sm text-slate-700" key={`${item.id}-${index}`}>
              <span>{item.label}</span><kbd className="rounded border border-slate-200 bg-slate-50 px-2 py-1 font-mono text-xs text-slate-600">{item.keys}</kbd>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
