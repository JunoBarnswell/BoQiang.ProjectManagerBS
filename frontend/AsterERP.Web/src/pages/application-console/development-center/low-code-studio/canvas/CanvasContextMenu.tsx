import { ArrowDownToLine, ArrowUpToLine, Copy, Lock, LockOpen, MoveDown, MoveUp, Trash2 } from 'lucide-react';
import { useEffect, useRef } from 'react';

export type CanvasContextAction = 'bring-forward' | 'bring-to-front' | 'copy' | 'delete' | 'duplicate' | 'paste' | 'send-backward' | 'send-to-back' | 'toggle-lock';

interface CanvasContextMenuProps {
  locked: boolean;
  onAction: (action: CanvasContextAction) => void;
  onClose: () => void;
  point: { x: number; y: number };
  text: (key: string) => string;
}

export function CanvasContextMenu({ locked, onAction, onClose, point, text }: CanvasContextMenuProps) {
  const menuRef = useRef<HTMLDivElement>(null);
  const actions: ReadonlyArray<{ action: CanvasContextAction; icon: typeof Copy; label: string }> = [
    { action: 'copy', icon: Copy, label: text('contextCopy') },
    { action: 'paste', icon: Copy, label: text('contextPaste') },
    { action: 'duplicate', icon: Copy, label: text('contextDuplicate') },
    { action: 'bring-forward', icon: MoveUp, label: text('contextBringForward') },
    { action: 'bring-to-front', icon: ArrowUpToLine, label: text('contextBringToFront') },
    { action: 'send-backward', icon: MoveDown, label: text('contextSendBackward') },
    { action: 'send-to-back', icon: ArrowDownToLine, label: text('contextSendToBack') },
    { action: 'toggle-lock', icon: locked ? LockOpen : Lock, label: text(locked ? 'contextUnlock' : 'contextLock') },
    { action: 'delete', icon: Trash2, label: text('contextDelete') }
  ];
  useEffect(() => {
    menuRef.current?.querySelector<HTMLButtonElement>('[role="menuitem"]')?.focus();
    const handlePointer = (event: PointerEvent) => { if (!menuRef.current?.contains(event.target as Node)) onClose(); };
    const handleKey = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      event.preventDefault();
      event.stopPropagation();
      onClose();
    };
    window.addEventListener('pointerdown', handlePointer, true);
    window.addEventListener('keydown', handleKey, true);
    return () => {
      window.removeEventListener('pointerdown', handlePointer, true);
      window.removeEventListener('keydown', handleKey, true);
    };
  }, [onClose]);
  const left = Math.max(8, Math.min(point.x, (typeof window === 'undefined' ? point.x + 180 : window.innerWidth) - 180));
  const top = Math.max(8, Math.min(point.y, (typeof window === 'undefined' ? point.y + 280 : window.innerHeight) - 280));
  return <div ref={menuRef} aria-label={text('contextMenu')} className="page-studio__context-menu" data-canvas-interaction-control="true" role="menu" style={{ left, top }}>
    {actions.map(({ action, icon: Icon, label }) => {
      const isDivider = action === 'toggle-lock' || action === 'bring-forward';
      return (
        <div key={action}>
          {isDivider ? <div className="page-studio__menu-separator" role="separator" /> : null}
          <button className={`page-studio__menu-item ${action === 'delete' ? 'page-studio__menu-item--danger' : ''}`} role="menuitem" type="button" onClick={() => onAction(action)}>
            <Icon aria-hidden="true" className="h-3.5 w-3.5 opacity-60 group-hover:opacity-100" />
            {label}
          </button>
        </div>
      );
    })}
  </div>;
}
