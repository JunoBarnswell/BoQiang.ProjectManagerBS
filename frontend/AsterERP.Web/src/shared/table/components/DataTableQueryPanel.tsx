import type { ReactNode } from 'react';

import { translateCurrentLiteral, useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../icons/AppIcon';
import { OPERATOR_OPTIONS } from '../tableFilterUtils';
import type {
  DataTableCondition,
  DataTableOperator,
  DataTableQueryFormField
} from '../tableTypes';

interface DataTableQueryPanelProps {
  conditions: DataTableCondition[];
  fields: DataTableQueryFormField[];
  matchMode: 'and' | 'or';
  onAddCondition: () => void;
  onApply: () => void;
  onClear: () => void;
  onConditionFieldChange: (index: number, field: string) => void;
  onConditionOperatorChange: (index: number, operator: DataTableOperator) => void;
  onMatchModeChange: (matchMode: 'and' | 'or') => void;
  onRemoveCondition: (index: number) => void;
  renderConditionInput: (condition: DataTableCondition, index: number) => ReactNode;
  showHint: boolean;
}

export function DataTableQueryPanel({
  conditions,
  fields,
  matchMode,
  onAddCondition,
  onApply,
  onClear,
  onConditionFieldChange,
  onConditionOperatorChange,
  onMatchModeChange,
  onRemoveCondition,
  renderConditionInput,
  showHint
}: DataTableQueryPanelProps) {
  const { translate } = useI18n();
  return (
    <section className="grid gap-[10px] border border-[var(--app-border)] rounded-[16px] p-[12px] bg-[color-mix(in_srgb,var(--app-card)_90%,transparent)]">
      <div className="flex gap-[12px] items-center justify-between flex-wrap max-[1023px]:items-stretch max-[1023px]:flex-col">
        <div className="flex gap-[10px] items-center max-[1023px]:items-stretch max-[1023px]:flex-col">
          <span>{translate('table.advancedQuery')}</span>
          <select value={matchMode} onChange={(event) => onMatchModeChange(event.target.value === 'or' ? 'or' : 'and')}>
            <option value="and">{translate('table.matchMode.and')}</option>
            <option value="or">{translate('table.matchMode.or')}</option>
          </select>
        </div>
        <div className="flex gap-[10px] items-center max-[1023px]:items-stretch max-[1023px]:flex-col">
          <button className="ghost-button" disabled={fields.length === 0} type="button" onClick={onAddCondition}>
            {translate('table.addCondition')}
          </button>
          <button className="ghost-button" type="button" onClick={onApply}>
            {translate('common.query')}
          </button>
          <button className="ghost-button" type="button" onClick={onClear}>
            {translate('table.clear')}
          </button>
        </div>
      </div>

      {showHint ? <p className="m-0 text-[var(--app-muted)]">{translate('table.queryHint')}</p> : null}

      {conditions.length > 0 ? (
        conditions.map((condition, index) => (
          <div className="grid grid-cols-[220px_170px_1fr_auto] gap-[8px] items-center max-[1023px]:grid-cols-1" key={`query-${index}`}>
            <select
              className="rounded-[8px] py-[0.38rem] px-[0.55rem] bg-[var(--app-card)] border border-[var(--app-border)]"
              value={condition.field}
              onChange={(event) => onConditionFieldChange(index, event.target.value)}
            >
              {fields.map((field) => (
                <option key={field.key} value={field.key}>
                  {typeof field.label === 'string' ? translateCurrentLiteral(field.label) : field.label}
                </option>
              ))}
            </select>

            <select
              className="rounded-[8px] py-[0.38rem] px-[0.55rem] bg-[var(--app-card)] border border-[var(--app-border)]"
              value={condition.operator}
              onChange={(event) => onConditionOperatorChange(index, event.target.value as DataTableOperator)}
            >
              {OPERATOR_OPTIONS.map((operator) => (
                <option key={operator.value} value={operator.value}>
                  {translate(operator.labelKey)}
                </option>
              ))}
            </select>

            {renderConditionInput(condition, index)}

            <button
              aria-label={translate('table.clear')}
              className="border border-[var(--app-border)] rounded-[10px] w-[28px] h-[28px] inline-grid place-items-center"
              title={translate('table.clear')}
              type="button"
              onClick={() => onRemoveCondition(index)}
            >
              <AppIcon name="default" size={14} />
            </button>
          </div>
        ))
      ) : (
        <p className="m-0 text-[var(--app-muted)]">{translate('table.queryEmptyHint')}</p>
      )}
    </section>
  );
}
