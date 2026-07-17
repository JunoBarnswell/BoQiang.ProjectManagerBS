import type { ReactNode } from 'react';

import { translateCurrentLiteral, useI18n } from '../../core/i18n/I18nProvider';
import { AppIcon } from '../icons/AppIcon';

import { renderFormField } from './formFieldRenderer';
import type { FormActionConfig, FormFieldConfig, FormSectionConfig } from './formTypes';

interface FormRendererProps<TValues extends object> {
  actions?: FormActionConfig[];
  className?: string;
  fields: FormFieldConfig<TValues>[];
  footer?: ReactNode;
  layout?: 'grid' | 'stack';
  onValueChange: (name: keyof TValues & string, value: TValues[keyof TValues & string]) => void;
  value: TValues;
}

export function FormRenderer<TValues extends object>({
  actions,
  className,
  fields,
  footer,
  layout = 'grid',
  onValueChange,
  value
}: FormRendererProps<TValues>) {
  const { translate } = useI18n();
  const layoutClassName = layout === 'stack' ? 'grid grid-cols-1 gap-3' : 'grid grid-cols-2 gap-3';

  // Group fields by section
  const sections: { name: string; fields: FormFieldConfig<TValues>[] }[] = [];
  fields.forEach((field) => {
    const sectionName = field.section || 'default';
    let section = sections.find((s) => s.name === sectionName);
    if (!section) {
      section = { name: sectionName, fields: [] };
      sections.push(section);
    }
    section.fields.push(field);
  });

  const renderSectionHeader = (sectionName: string) => {
    if (sectionName === 'default') return null;
    let icon = 'app-window';
    if (sectionName.includes('基础') || sectionName.includes('基本')) icon = 'identification-card';
    else if (sectionName.includes('权限') || sectionName.includes('组织')) icon = 'shield';
    else if (sectionName.includes('配置') || sectionName.includes('设置')) icon = 'gear';

    return (
      <h4 className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2 pb-1.5 border-b border-gray-100 flex items-center gap-1.5 pt-2">
        <AppIcon className="text-gray-400" name={icon} /> {translateCurrentLiteral(sectionName)}
      </h4>
    );
  };

  return (
    <form className={`space-y-3 ${className ?? ''}`.trim()} onSubmit={(e) => e.preventDefault()}>
      {sections.map((section) => (
        <div key={section.name}>
          {renderSectionHeader(section.name)}
          <div className={layoutClassName}>
            {section.fields.map((field) => {
              const span = field.span ?? 1;
              const colSpanClass = span === 2 ? 'col-span-2' : 'col-span-1';

              // 针对“状态”这种横排设计的特殊处理可以在 formFieldRenderer 里做
              // 这里统一输出外层结构
              return (
                <div key={field.name} className={colSpanClass}>
                  <label className="block text-gray-700 font-medium mb-1 text-xs">
                    {typeof field.label === 'string' ? translateCurrentLiteral(field.label) : field.label} {field.required && <span className="text-red-500">*</span>}
                  </label>
                  {renderFormField({
                    field,
                    onValueChange,
                    translate,
                    value: value[field.name]
                  })}
                  {field.helpText && (
                    <p className="text-[11px] text-gray-400 mt-1 flex items-center gap-1">
                      <AppIcon name="info" /> {typeof field.helpText === 'string' ? translateCurrentLiteral(field.helpText) : field.helpText}
                    </p>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      ))}
      {actions && actions.length > 0 ? (
        <div className="form-action-bar">
          {actions.map((action) => (
            <button
              key={action.label}
              className={action.variant === 'primary' ? 'primary-button' : 'ghost-button'}
              disabled={action.disabled || action.loading}
              onClick={action.onClick}
              type={action.type ?? 'button'}
            >
              {translateCurrentLiteral(action.label)}
            </button>
          ))}
        </div>
      ) : null}
      {footer}
    </form>
  );
}

export function FormSection({ children, description, title }: FormSectionConfig) {
  return (
    <section className="pt-2">
      <h4 className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2 pb-1.5 border-b border-gray-100 flex items-center gap-1.5">
        <AppIcon className="text-gray-400" name="app-window" /> {translateCurrentLiteral(title)}
      </h4>
      {description && <p className="text-gray-500 mb-2">{typeof description === 'string' ? translateCurrentLiteral(description) : description}</p>}
      {children}
    </section>
  );
}
