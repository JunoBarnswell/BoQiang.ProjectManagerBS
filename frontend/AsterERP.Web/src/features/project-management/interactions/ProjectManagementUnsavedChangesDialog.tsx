import { useProjectManagementEscapeLayer } from './ProjectManagementEscapeStack';
import type { ProjectManagementUnsavedChangesGuard } from './useProjectManagementUnsavedChangesGuard';

interface ProjectManagementUnsavedChangesDialogProps {
  guard: ProjectManagementUnsavedChangesGuard;
}

export function ProjectManagementUnsavedChangesDialog({ guard }: ProjectManagementUnsavedChangesDialogProps) {
  useProjectManagementEscapeLayer(guard.isOpen, guard.cancel);
  if (!guard.isOpen) return null;

  return (
    <div className="fixed inset-0 z-[80] grid place-items-center bg-slate-900/35 px-4 py-6" role="presentation" onMouseDown={guard.cancel}>
      <section aria-label="未保存的更改" aria-modal="true" className="w-full max-w-[440px] overflow-hidden rounded-lg border border-slate-200 bg-white shadow-2xl" role="dialog" onMouseDown={(event) => event.stopPropagation()}>
        <header className="border-b border-slate-100 px-5 py-4">
          <h2 className="m-0 text-base font-semibold text-slate-900">未保存的更改</h2>
          <p className="mt-1 text-sm leading-6 text-slate-600">离开前是否保存当前编辑内容？</p>
        </header>
        <footer className="flex flex-wrap justify-end gap-2 border-t border-slate-100 bg-slate-50 px-5 py-3">
          <button className="ghost-button" disabled={guard.isSaving} type="button" onClick={guard.cancel}>取消</button>
          <button className="ghost-button" disabled={guard.isSaving} type="button" onClick={() => void guard.discard()}>不保存</button>
          <button className="primary-button" disabled={guard.isSaving} type="button" onClick={() => void guard.save()}>{guard.isSaving ? '保存中…' : '保存并离开'}</button>
        </footer>
      </section>
    </div>
  );
}
