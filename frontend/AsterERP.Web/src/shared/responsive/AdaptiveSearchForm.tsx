import type { FormEvent } from 'react';
import { useState } from 'react';

import { useI18n , translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { getResponsiveSearchRows } from '../../core/responsive/breakpoint';
import { useBreakpoint } from '../../core/responsive/useBreakpoint';
import { useViewportSize } from '../../core/responsive/useViewportSize';
import { renderFormField } from '../forms/formFieldRenderer';
import type { FormFieldConfig } from '../forms/formTypes';

import { ResponsiveFormGrid, type ResponsiveGridColumns } from './ResponsiveFormGrid';

interface AdaptiveSearchFormProps<TValues extends object> {
  fields: FormFieldConfig<TValues>[];
  loading?: boolean;
  onReset?: () => void;
  onSubmit: (value: TValues) => void;
  onValueChange: (value: TValues) => void;
  value: TValues;
  columns?: ResponsiveGridColumns;
  defaultCollapsed?: boolean;
  maxRows?: number;
}

export function AdaptiveSearchForm<TValues extends object>({
  columns,
  defaultCollapsed = true,
  fields,
  loading = false,
  maxRows,
  onReset,
  onSubmit,
  onValueChange,
  value
}: AdaptiveSearchFormProps<TValues>) {
  const { translate } = useI18n();
  const { breakpoint } = useBreakpoint();
  const { width } = useViewportSize();
  const resolvedRows = maxRows ?? getResponsiveSearchRows(width);
  const resolvedColumns =
    typeof columns === 'number'
      ? columns
      : columns?.[breakpoint] ?? Math.max(1, Math.min(4, Math.floor(width / 420)));
  const visibleCount = resolvedRows * resolvedColumns;
  const [collapsed, setCollapsed] = useState(defaultCollapsed);
  const displayedFields = collapsed ? fields.slice(0, visibleCount) : fields;
  const hasOverflow = fields.length > visibleCount;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSubmit(value);
  };

  return (
    <form className="search-form" onSubmit={handleSubmit}>
      <ResponsiveFormGrid columns={columns} dense className="!gap-y-3">
        {displayedFields.map((field) => (
          <label key={field.name} className="form-field flex-row items-center gap-2 m-0">
            <span className="form-field-label w-20 text-right shrink-0">{typeof field.label === 'string' ? translateCurrentLiteral(field.label) : field.label}</span>
            <div className="flex-1 min-w-0">
              {renderFormField({
                field,
                onValueChange: (name, nextValue) =>
                  onValueChange({
                    ...value,
                    [name]: nextValue
                  }),
                translate,
                value: value[field.name]
              })}
            </div>
          </label>
        ))}
        
        <div className="search-form-actions flex items-center justify-start gap-2 pl-22">
          <button className="primary-button h-8 px-3 text-sm" disabled={loading} type="submit">
            {translate('common.query')}
          </button>
          <button
            className="ghost-button h-8 px-3 text-sm"
            type="button"
            onClick={() => {
              onReset?.();
              setCollapsed(defaultCollapsed);
            }}
          >
            {translate('common.reset')}
          </button>
          {hasOverflow ? (
            <button className="ghost-button h-8 px-3 text-sm text-primary-600" type="button" onClick={() => setCollapsed((currentValue) => !currentValue)}>
              {collapsed ? (breakpoint === 'xs' ? translate('common.more') : translate('toolbar.moreActions')) : translate('common.close')}
            </button>
          ) : null}
        </div>
      </ResponsiveFormGrid>
    </form>
  );
}
