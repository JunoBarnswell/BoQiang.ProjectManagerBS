import { useEffect, useState, type ReactNode } from 'react';

import { translateCurrentLiteral, useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../icons/AppIcon';

import { FormRenderer } from './FormRenderer';
import type { FormActionConfig, FormFieldConfig } from './formTypes';

interface ModalFormProps<TValues extends object> {
  actions?: FormActionConfig[];
  children?: ReactNode;
  fields: FormFieldConfig<TValues>[];
  open: boolean;
  onClose: () => void;
  onValueChange: (name: keyof TValues & string, value: TValues[keyof TValues & string]) => void;
  title: string;
  value: TValues;
}

export function ModalForm<TValues extends object>({
  actions,
  children,
  fields,
  open,
  onClose,
  onValueChange,
  title,
  value
}: ModalFormProps<TValues>) {
  const { translate } = useI18n();
  const [isVisible, setIsVisible] = useState(false);
  const [isAnimating, setIsAnimating] = useState(false);

  useEffect(() => {
    if (open) {
      setIsVisible(true);
      requestAnimationFrame(() => {
        setIsAnimating(true);
      });
    } else {
      setIsAnimating(false);
      const timer = setTimeout(() => {
        setIsVisible(false);
      }, 300);
      return () => clearTimeout(timer);
    }
  }, [open]);

  if (!isVisible) return null;

  const resolveText = (value: string) => {
    const translated = translate(value);
    if (translated !== value) {
      return translated;
    }

    return translateCurrentLiteral(value);
  };

  const resolvedTitle = resolveText(title);
  const editLabel = translate('common.edit');
  const closeLabel = translate('common.close');
  const isEditTitle = resolvedTitle.toLowerCase().includes(editLabel.toLowerCase());

  return (
    <>
      <div 
        className={`fixed inset-0 bg-gray-900/40 z-40 transition-opacity duration-300 ${isAnimating ? 'opacity-100' : 'opacity-0'}`} 
        onClick={onClose}
      />
      <div 
        className={`fixed top-0 right-0 h-full w-full max-w-[460px] bg-white shadow-2xl z-50 transform transition-transform duration-300 ease-in-out flex flex-col ${isAnimating ? 'translate-x-0' : 'translate-x-full'}`}
      >
        <div className="h-11 border-b border-gray-200 flex items-center justify-between px-4 shrink-0 bg-gray-50/80 backdrop-blur-sm">
          <h3 className="font-bold text-gray-800 text-sm flex items-center gap-2">
            {isEditTitle ? <AppIcon className="text-primary-600 text-base" name="pencil-simple" /> : <AppIcon className="text-primary-600 text-base" name="plus-circle" />}
            <span>{resolvedTitle}</span>
          </h3>
          <button
            aria-label={closeLabel}
            className="text-gray-400 hover:text-gray-600 hover:bg-gray-200 p-1 rounded transition-colors"
            onClick={onClose}
            title={closeLabel}
            type="button"
          >
            <AppIcon className="text-base" name="x" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-4 text-sm">
          {children && <div className="mb-3 text-gray-500 text-xs">{children}</div>}
          <FormRenderer actions={undefined} fields={fields} layout="grid" onValueChange={onValueChange} value={value} />
        </div>

        <div className="form-action-bar h-12 border-t border-gray-200 px-4 shrink-0 bg-white">
          {actions?.map((action) => (
            <button
              key={resolveText(action.label)}
              type={action.type ?? 'button'}
              className={action.variant === 'primary' ? 'primary-button' : 'ghost-button'}
              disabled={action.disabled || action.loading}
              onClick={action.onClick}
            >
              {action.loading ? <AppIcon className="text-base animate-spin" name="spinner-gap" /> : action.variant === 'primary' ? <AppIcon className="text-base" name="check-circle" /> : null}
              {resolveText(action.label)}
            </button>
          ))}
        </div>
      </div>
    </>
  );
}
