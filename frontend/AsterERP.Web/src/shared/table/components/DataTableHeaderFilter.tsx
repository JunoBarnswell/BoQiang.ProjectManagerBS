import { X } from 'lucide-react';

import { formatMessage } from '../../../core/i18n/formatMessage';
import { useI18n } from '../../../core/i18n/I18nProvider';
import {
  getColumnFilterOperators,
  getColumnFilterType,
  getDefaultFilterOperator,
  OPERATOR_OPTIONS
} from '../tableFilterUtils';
import { titleToText } from '../tableLayoutUtils';
import { valueToString, type QueryFieldType } from '../tableQueryUtils';
import type {
  DataTableColumn,
  DataTableCondition,
  DataTableFilterOption,
  DataTableFilterType,
  DataTableOperator
} from '../tableTypes';

interface DataTableHeaderFilterProps<TItem> {
  activeColumnKey: string | null;
  column: DataTableColumn<TItem>;
  draft: DataTableCondition | null;
  fieldTypeMap: Map<string, QueryFieldType>;
  onApply: (column: DataTableColumn<TItem>) => void;
  onClear: (column: DataTableColumn<TItem>) => void;
  onClose: () => void;
  onDraftChange: (patch: Partial<DataTableCondition>) => void;
}

const booleanFilterOptions: DataTableFilterOption[] = [
  { label: 'table.boolean.true', value: true },
  { label: 'table.boolean.false', value: false }
];

export function DataTableHeaderFilter<TItem>({
  activeColumnKey,
  column,
  draft,
  fieldTypeMap,
  onApply,
  onClear,
  onClose,
  onDraftChange
}: DataTableHeaderFilterProps<TItem>) {
  const { translate } = useI18n();
  if (activeColumnKey !== column.key || !draft) {
    return null;
  }

  const filterType = getColumnFilterType(column, fieldTypeMap);
  const operators = getColumnFilterOperators(column, filterType);
  const currentOperator = operators.includes(draft.operator)
    ? draft.operator
    : getDefaultFilterOperator(column, filterType);

  return (
    <div className="data-table-column-filter" onClick={(event) => event.stopPropagation()}>
      <div className="data-table-column-filter__header">
        <span>{formatMessage(translate('table.filterFor'), { name: titleToText(column.title, column.key) })}</span>
        <button aria-label={translate('table.closeFilter')} type="button" onClick={onClose}>
          <X size={14} />
        </button>
      </div>
      <label className="data-table-field">
        <span>{translate('table.condition')}</span>
        <select
          value={currentOperator}
          onChange={(event) => onDraftChange({ operator: event.target.value as DataTableOperator, value: '', valueTo: undefined })}
        >
          {operators.map((operator) => {
            const option = OPERATOR_OPTIONS.find((item) => item.value === operator);
            return (
              <option key={operator} value={operator}>
                {option ? translate(option.labelKey) : operator}
              </option>
            );
          })}
        </select>
      </label>
      <label className="data-table-field">
        <span>{translate('table.value')}</span>
        <HeaderFilterValue column={column} draft={draft} filterType={filterType} onDraftChange={onDraftChange} />
      </label>
      <div className="data-table-column-filter__actions">
        <button type="button" onClick={() => onClear(column)}>
          {translate('table.clear')}
        </button>
        <button type="button" onClick={() => onApply(column)}>
          {translate('table.apply')}
        </button>
      </div>
    </div>
  );
}

interface HeaderFilterValueProps<TItem> {
  column: DataTableColumn<TItem>;
  draft: DataTableCondition;
  filterType: DataTableFilterType;
  onDraftChange: (patch: Partial<DataTableCondition>) => void;
}

function HeaderFilterValue<TItem>({ column, draft, filterType, onDraftChange }: HeaderFilterValueProps<TItem>) {
  const { translate } = useI18n();
  if (draft.operator === 'between' && (filterType === 'number' || filterType === 'date')) {
    return (
      <div className="data-table-column-filter__range">
        <input
          onChange={(event) => onDraftChange({ value: event.target.value })}
          type={filterType === 'number' ? 'number' : 'date'}
          value={valueToString(draft.value)}
        />
        <span>{translate('table.rangeTo')}</span>
        <input
          onChange={(event) => onDraftChange({ valueTo: event.target.value })}
          type={filterType === 'number' ? 'number' : 'date'}
          value={valueToString(draft.valueTo)}
        />
      </div>
    );
  }

  if (filterType === 'select' || filterType === 'boolean') {
    const options = column.filterOptions ?? (filterType === 'boolean' ? booleanFilterOptions : []);
    return (
      <select
        onChange={(event) => onDraftChange({ value: event.target.value })}
        value={valueToString(draft.value)}
      >
        <option value="">{translate('table.all')}</option>
        {options.map((option) => (
        <option key={String(option.value)} value={String(option.value)}>
            {typeof option.label === 'string' && option.label.startsWith('table.') ? translate(option.label) : option.label}
          </option>
        ))}
      </select>
    );
  }

  return (
    <input
      onChange={(event) => onDraftChange({ value: event.target.value })}
      type={filterType === 'number' ? 'number' : filterType === 'date' ? 'date' : 'text'}
      value={valueToString(draft.value)}
    />
  );
}
