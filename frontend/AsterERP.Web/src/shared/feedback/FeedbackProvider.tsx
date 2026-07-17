import { createContext, useCallback, useEffect, useState, type ReactNode } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';import { AppIcon } from '../icons/AppIcon';


export type MessageType = 'success' | 'error' | 'info';

export interface Message {
  id: string;
  type: MessageType;
  content: string;
}

export interface ConfirmOptions {
  title: string;
  content: ReactNode;
  confirmText?: string;
  cancelText?: string;
  onConfirm: () => void | Promise<void>;
  onCancel?: () => void;
}

interface FeedbackContextValue {
  showMessage: (content: string, type?: MessageType) => void;
  showConfirm: (options: ConfirmOptions) => void;
}

export const FeedbackContext = createContext<FeedbackContextValue | null>(null);

let messageIdCounter = 0;

export function FeedbackProvider({ children }: { children: ReactNode }) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [confirmState, setConfirmState] = useState<{ open: boolean; options?: ConfirmOptions }>({ open: false });
  const [isConfirming, setIsConfirming] = useState(false);
  const { translate } = useI18n();

  const showMessage = useCallback((content: string, type: MessageType = 'info') => {
    const id = String(++messageIdCounter);
    setMessages((prev) => [...prev, { id, type, content }]);
    setTimeout(() => {
      setMessages((prev) => prev.filter((m) => m.id !== id));
    }, 3000);
  }, []);

  const showConfirm = useCallback((options: ConfirmOptions) => {
    setConfirmState({ open: true, options });
    setIsConfirming(false);
  }, []);

  const handleConfirmClose = useCallback(() => {
    if (isConfirming) {
      return;
    }

    if (confirmState.options?.onCancel) {
      confirmState.options.onCancel();
    }
    setConfirmState({ open: false });
  }, [confirmState, isConfirming]);

  const handleConfirmOk = useCallback(async () => {
    if (!confirmState.options?.onConfirm || isConfirming) {
      return;
    }

    try {
      setIsConfirming(true);
      await confirmState.options.onConfirm();
      setConfirmState({ open: false });
    } finally {
      setIsConfirming(false);
    }
  }, [confirmState, isConfirming]);

  useEffect(() => {
    if (!confirmState.open) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        handleConfirmClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [confirmState.open, handleConfirmClose]);

  return (
    <FeedbackContext.Provider value={{ showMessage, showConfirm }}>
      {children}
      
      {/* Toast Messages */}
      <div className="fixed top-4 right-4 z-50 flex flex-col gap-2 pointer-events-none">
        {messages.map((m) => (
          <div
            key={m.id}
            className={`px-4 py-2 rounded shadow-lg text-white pointer-events-auto transition-all ${
              m.type === 'success' ? 'bg-green-600' : m.type === 'error' ? 'bg-red-600' : 'bg-blue-600'
            }`}
          >
            {m.content}
          </div>
        ))}
      </div>

      {confirmState.options && confirmState.open && (
        <div className="fixed inset-0 z-[80] grid place-items-center bg-slate-900/35 px-4 py-6" role="presentation" onClick={handleConfirmClose}>
          <section
            aria-label={confirmState.options.title}
            aria-modal="true"
            className="w-full max-w-[440px] overflow-hidden rounded-lg border border-slate-200 bg-white shadow-2xl"
            role="dialog"
            onClick={(event) => event.stopPropagation()}
          >
            <header className="flex items-start gap-3 border-b border-slate-100 px-5 py-4">
              <span className="mt-0.5 flex h-9 w-9 flex-none items-center justify-center rounded-md bg-amber-50 text-amber-600">
                <AppIcon className="text-lg" name="warning-circle" />
              </span>
              <div className="min-w-0 flex-1">
                <h3 className="m-0 text-base font-semibold text-slate-900">{confirmState.options.title}</h3>
                <p className="mt-1 text-xs leading-5 text-slate-500">{translate('common.confirmDescription')}</p>
              </div>
              <button className="x flex-none" disabled={isConfirming} type="button" onClick={handleConfirmClose}>
                ×
              </button>
            </header>

            <div className="px-5 py-4 text-sm leading-6 text-slate-700">{confirmState.options.content}</div>

            <footer className="flex justify-end gap-2 border-t border-slate-100 bg-slate-50 px-5 py-3">
              <button className="ghost-button" disabled={isConfirming} type="button" onClick={handleConfirmClose}>
                {confirmState.options.cancelText || translate('common.cancel')}
              </button>
              <button className="primary-button" disabled={isConfirming} type="button" onClick={() => void handleConfirmOk()}>
                {isConfirming ? translate('common.loading') : confirmState.options.confirmText || translate('common.confirm')}
              </button>
            </footer>
          </section>
        </div>
      )}
    </FeedbackContext.Provider>
  );
}
