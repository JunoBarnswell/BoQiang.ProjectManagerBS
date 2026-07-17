import type { ChangeEvent } from 'react';

import type { InspectorEditorProps } from '../registry/InspectorEditorRegistry';

export function InspectorPrimitiveEditor({ descriptor, label, options, placeholder, value, mixed, mixedValueLabel, selectOptionLabel, onChange }: InspectorEditorProps) {
  const inputId = `inspector-${descriptor.path.replace(/[^a-zA-Z0-9_-]/g, '-')}`;
  const inputClass = 'page-studio__inspector-input';
  const displayValue = mixed ? '' : value === undefined || value === null ? '' : String(value);
  const handleChange = (event: ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const next = event.target.value;
    if (descriptor.editor === 'number') onChange(next === '' ? null : Number(next));
    else onChange(next);
  };

  if (descriptor.editor === 'boolean') {
    return <input aria-label={label} className="page-studio__inspector-checkbox" type="checkbox" checked={!mixed && value === true} onChange={(event) => onChange(event.target.checked)} />;
  }
  if (descriptor.editor === 'select') {
    return <select id={inputId} aria-label={label} className={`h-8 ${inputClass}`} value={displayValue} onChange={handleChange}>
      <option value="">{selectOptionLabel}</option>
      {options?.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select>;
  }
  if (descriptor.editor === 'textarea') {
    return <textarea id={inputId} aria-label={label} className={`min-h-16 ${inputClass}`} value={displayValue} placeholder={mixed ? mixedValueLabel : placeholder} onChange={handleChange} />;
  }
  return <input id={inputId} aria-label={label} className={`h-8 ${inputClass}`} type={descriptor.editor === 'number' ? 'number' : descriptor.editor === 'color' ? 'color' : 'text'} value={descriptor.editor === 'color' && !mixed && typeof value === 'string' && value ? value : displayValue} placeholder={mixed ? mixedValueLabel : placeholder} onChange={handleChange} />;
}
