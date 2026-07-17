import type { ReactNode } from 'react';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';

export function hasChoiceRuntimeRenderer(type: string): boolean {
  return type === 'select.dropdown' || type === 'select.multi' || type === 'select.radio' || type === 'select.checkbox' || type === 'select.datalist';
}

export function renderChoiceRuntime(context: RuntimeComponentRenderContext): ReactNode {
  const options = normalizeChoiceOptions(readChoiceOptions(context));
  if (context.componentType === 'select.radio' || context.componentType === 'select.checkbox') {
    const type = context.componentType === 'select.radio' ? 'radio' : 'checkbox';
    const values = Array.isArray(context.value) ? context.value.map(String) : [String(context.value ?? '')];
    return applyRuntimeNodePresentation(context, <div className="flex flex-wrap gap-3 text-sm">{options.map((option) => <label className="inline-flex items-center gap-1" key={option.value}><input checked={values.includes(option.value)} disabled={context.disabled || context.readOnly} name={type === 'radio' ? context.element.id : undefined} type={type} value={option.value} onChange={(event) => { if (type === 'radio') context.onChange(option.value, context.changeAction); else context.onChange(event.target.checked ? [...values.filter(Boolean), option.value] : values.filter((value) => value !== option.value), context.changeAction); }} />{option.label}</label>)}</div>);
  }
  return applyRuntimeNodePresentation(context, <select className="form-input h-9 w-full" disabled={context.disabled || context.readOnly} multiple={context.componentType === 'select.multi' || Boolean(context.props.multiple)} value={Array.isArray(context.value) ? context.value.map(String) : String(context.value ?? '')} onChange={(event) => context.onChange(event.currentTarget.multiple ? [...event.currentTarget.selectedOptions].map((option) => option.value) : event.currentTarget.value, context.changeAction)}><option value="">{context.title}</option>{options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select>);
}

function readChoiceOptions(context: RuntimeComponentRenderContext): unknown {
  const boundOptions = context.bindings?.options;
  if (isChoiceCollection(boundOptions)) return boundOptions;

  const boundData = context.bindings?.data;
  if (isChoiceCollection(boundData)) return boundData;

  return context.props.options;
}

function isChoiceCollection(value: unknown): boolean {
  if (Array.isArray(value)) return true;
  if (typeof value !== 'string') return false;
  try {
    return Array.isArray(JSON.parse(value));
  } catch {
    return false;
  }
}

export function normalizeChoiceOptions(value: unknown, labelPath?: string | null, valuePath?: string | null): Array<{ label: string; value: string }> {
  const parsed = typeof value === 'string' ? parseJson(value) : value;
  return (Array.isArray(parsed) ? parsed : []).map((item, index) => {
    if (item && typeof item === 'object' && !Array.isArray(item)) {
      const record = item as Record<string, unknown>;
      const optionValue = String(valuePath ? readOptionPath(record, valuePath) ?? record.value ?? record.key ?? record.id ?? index + 1 : record.value ?? record.key ?? record.id ?? index + 1);
      return { label: String(labelPath ? readOptionPath(record, labelPath) ?? record.label ?? record.name ?? record.title ?? optionValue : record.label ?? record.name ?? record.title ?? optionValue), value: optionValue };
    }
    return { label: String(item ?? index + 1), value: String(item ?? index + 1) };
  });
}
function parseJson(value: string): unknown { try { return JSON.parse(value); } catch { return []; } }
function readOptionPath(root: Record<string, unknown>, path: string): unknown { const normalized = path.replace(/^\$\./, '').trim(); return normalized ? normalized.split('.').reduce<unknown>((current, segment) => current === null || current === undefined ? undefined : Array.isArray(current) && /^\d+$/.test(segment) ? current[Number(segment)] : typeof current === 'object' && !Array.isArray(current) ? (current as Record<string, unknown>)[segment] : undefined, root) : undefined; }
