import type { ChangeEvent } from 'react';

import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { DictCheckbox } from '../dict/DictCheckbox';
import { DictSelect } from '../dict/DictSelect';

import type { FormFieldRendererProps } from './formTypes';
import { PermissionTreeField } from './PermissionTreeField';

export function renderFormField<TValues extends object>({
  field,
  onValueChange,
  translate,
  value
}: FormFieldRendererProps<TValues>) {
  const commonProps = {
    disabled: field.disabled,
    placeholder: typeof field.placeholder === 'string' ? translateCurrentLiteral(field.placeholder) : field.placeholder,
    required: field.required,
    max: field.max,
    min: field.min,
    step: field.step,
    className: "w-full border border-gray-300 rounded-md px-2.5 py-1.5 outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500 transition-all text-sm bg-white"
  };

  switch (field.type) {
    case 'textarea':
      return (
        <textarea
          {...commonProps}
          rows={field.rows ?? 3}
          value={typeof value === 'string' ? value : ''}
          onChange={(event) => onValueChange(field.name, event.target.value as TValues[keyof TValues & string])}
          className={`${commonProps.className} resize-none`}
        />
      );
    case 'number':
      return (
        <input
          {...commonProps}
          type="number"
          value={typeof value === 'number' ? value : value ? Number(value) : ''}
          onChange={(event) =>
            onValueChange(
              field.name,
              (event.target.value === '' ? '' : Number(event.target.value)) as TValues[keyof TValues & string]
            )
          }
        />
      );
    case 'range':
      return (
        <div className="flex items-center gap-3">
          <input
            {...commonProps}
            className="h-2 flex-1 cursor-pointer accent-primary-600"
            type="range"
            value={typeof value === 'number' ? value : value ? Number(value) : 0}
            onChange={(event) => onValueChange(field.name, Number(event.target.value) as TValues[keyof TValues & string])}
          />
          <output className="min-w-10 text-right text-sm font-medium text-gray-700">{typeof value === 'number' ? value : 0}%</output>
        </div>
      );
    case 'date':
    case 'datetime-local':
    case 'text':
      return (
        <input
          {...commonProps}
          type={field.type}
          value={typeof value === 'string' ? value : ''}
          onChange={(event) => onValueChange(field.name, event.target.value as TValues[keyof TValues & string])}
        />
      );
    case 'select':
      return (
        <select
          {...commonProps}
          value={typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean' ? String(value) : ''}
          onChange={(event) => onValueChange(field.name, event.target.value as TValues[keyof TValues & string])}
        >
          <option value="">{typeof field.emptyOptionLabel === 'string' ? translateCurrentLiteral(field.emptyOptionLabel) : field.emptyOptionLabel ?? translate('common.select')}</option>
          {field.options?.map((option) => (
            <option key={option.value} disabled={option.disabled} value={option.value}>
              {typeof option.label === 'string' ? translateCurrentLiteral(option.label) : option.label}
            </option>
          ))}
        </select>
      );
    case 'multiselect':
      return (
        <select
          {...commonProps}
          multiple
          value={Array.isArray(value) ? value.map((item) => String(item)) : []}
          onChange={(event) =>
            onValueChange(
              field.name,
              Array.from(event.target.selectedOptions, (option) => option.value) as TValues[keyof TValues & string]
            )
          }
          className={`${commonProps.className} min-h-[88px]`}
        >
          {field.options?.map((option) => (
            <option key={option.value} disabled={option.disabled} value={option.value}>
              {typeof option.label === 'string' ? translateCurrentLiteral(option.label) : option.label}
            </option>
          ))}
        </select>
      );
    case 'dict':
      return (
        <DictSelect
          dictType={field.dictType ?? ''}
          includeEmpty
          value={typeof value === 'string' ? value : ''}
          onValueChange={(nextValue) => onValueChange(field.name, nextValue as TValues[keyof TValues & string])}
        />
      );
    case 'switch':
      return (
        <div className="switch-field">
          <input
            checked={Boolean(value)}
            type="checkbox"
            onChange={(event: ChangeEvent<HTMLInputElement>) =>
              onValueChange(field.name, event.target.checked as TValues[keyof TValues & string])
            }
          />
          <span>{typeof field.helpText === 'string' ? translateCurrentLiteral(field.helpText) : field.helpText ?? translate('common.enabled')}</span>
        </div>
      );
    case 'checkbox':
      return (
        <DictCheckbox
          dictType={field.dictType ?? ''}
          options={field.options}
          name={field.name}
          value={Array.isArray(value) ? (value as string[]) : []}
          onValueChange={(nextValue) => onValueChange(field.name, nextValue as TValues[keyof TValues & string])}
        />
      );
    case 'permissionTree':
      return (
        <PermissionTreeField
          nodes={field.permissionTreeNodes ?? []}
          value={Array.isArray(value) ? (value as string[]) : []}
          onValueChange={(nextValue) => onValueChange(field.name, nextValue as TValues[keyof TValues & string])}
        />
      );
    default:
      return null;
  }
}
