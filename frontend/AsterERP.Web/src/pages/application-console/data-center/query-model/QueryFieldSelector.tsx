import { memo } from 'react';

import type { QueryModelAggregate, QueryModelSelection } from './queryModelTypes';

export interface QueryFieldSelectorOption {
  fieldResourceId: string;
  label: string;
  nodeId: string;
}

interface QueryFieldSelectorProps {
  fields: QueryFieldSelectorOption[];
  onChange: (selections: QueryModelSelection[]) => void;
  selections: QueryModelSelection[];
  t: (key: string) => string;
}

export const QueryFieldSelector = memo(function QueryFieldSelector({ fields, onChange, selections, t }: QueryFieldSelectorProps) {
  const update = (index: number, patch: Partial<QueryModelSelection>) => onChange(selections.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item));
  const move = (index: number, direction: -1 | 1) => {
    const target = index + direction;
    if (target < 0 || target >= selections.length) return;
    const next = [...selections];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(next);
  };
  const remove = (index: number) => onChange(selections.filter((_, itemIndex) => itemIndex !== index));

  return (
    <section className="rounded border bg-white p-3">
      <div className="flex justify-between">
        <h2 className="text-sm font-semibold">{t('applicationConsole.dataCenter.queryModel.selectFields')}</h2>
        <button className="secondary-button h-8 text-xs" type="button" onClick={() => onChange([...selections, { id: `selection:${Date.now()}:${selections.length}`, nodeId: '', fieldResourceId: '', alias: '', aggregate: 'none' }])}>
          {t('applicationConsole.dataCenter.queryModel.addField')}
        </button>
      </div>
      {selections.map((selection, index) => {
        const selectedField = fields.find((field) => field.nodeId === selection.nodeId && field.fieldResourceId === selection.fieldResourceId);
        return (
          <div className="mt-2 grid gap-2 rounded border border-slate-200 p-2 md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)_auto]" key={selection.id}>
            <div>
              <label className="text-xs" htmlFor={`selection-field-${selection.id}`}>{t('applicationConsole.dataCenter.queryModel.field')}</label>
              <select
                aria-label={`${t('applicationConsole.dataCenter.queryModel.field')} ${index + 1}`}
                className="form-input mt-1 w-full"
                id={`selection-field-${selection.id}`}
                value={selectedField ? encodeFieldReference(selectedField.nodeId, selectedField.fieldResourceId) : ''}
                onChange={(event) => {
                  const reference = decodeFieldReference(event.target.value);
                  update(index, reference ? { nodeId: reference.nodeId, fieldResourceId: reference.fieldResourceId } : { nodeId: '', fieldResourceId: '' });
                }}
              >
                <option value="">Select</option>
                {fields.map((field) => <option key={`${field.nodeId}:${field.fieldResourceId}`} value={encodeFieldReference(field.nodeId, field.fieldResourceId)}>{field.label}</option>)}
              </select>
            </div>
            <div>
              <label className="text-xs" htmlFor={`selection-alias-${selection.id}`}>{t('applicationConsole.dataCenter.queryModel.alias')}</label>
              <input className="form-input mt-1 w-full" id={`selection-alias-${selection.id}`} value={selection.alias} onChange={(event) => update(index, { alias: event.target.value })} />
            </div>
            <div>
              <label className="text-xs" htmlFor={`selection-aggregate-${selection.id}`}>{t('applicationConsole.dataCenter.queryModel.aggregate')}</label>
              <select className="form-input mt-1 w-full" id={`selection-aggregate-${selection.id}`} value={selection.aggregate} onChange={(event) => update(index, { aggregate: event.target.value as QueryModelAggregate })}>
                <option value="none">{t('applicationConsole.dataCenter.queryModel.none')}</option>
                <option value="count">COUNT</option><option value="sum">SUM</option><option value="avg">AVG</option><option value="min">MIN</option><option value="max">MAX</option>
              </select>
            </div>
            <div className="flex items-end gap-1">
              <button aria-label={`Move field ${index + 1} up`} className="text-xs" disabled={index === 0} type="button" onClick={() => move(index, -1)}>↑</button>
              <button aria-label={`Move field ${index + 1} down`} className="text-xs" disabled={index === selections.length - 1} type="button" onClick={() => move(index, 1)}>↓</button>
              <button aria-label={`${t('applicationConsole.dataCenter.queryModel.remove')} field ${index + 1}`} className="text-xs text-red-600" type="button" onClick={() => remove(index)}>{t('applicationConsole.dataCenter.queryModel.remove')}</button>
            </div>
          </div>
        );
      })}
    </section>
  );
});

function encodeFieldReference(nodeId: string, fieldResourceId: string): string { return JSON.stringify([nodeId, fieldResourceId]); }

function decodeFieldReference(value: string): { fieldResourceId: string; nodeId: string } | undefined {
  try {
    const parsed: unknown = JSON.parse(value);
    if (Array.isArray(parsed) && parsed.length === 2 && typeof parsed[0] === 'string' && typeof parsed[1] === 'string') return { nodeId: parsed[0], fieldResourceId: parsed[1] };
  } catch {
    return undefined;
  }
  return undefined;
}
