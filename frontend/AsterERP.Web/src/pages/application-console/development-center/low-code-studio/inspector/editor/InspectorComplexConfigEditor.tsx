import { useState } from 'react';

import type { InspectorEditorProps } from '../registry/InspectorEditorRegistry';

export function InspectorComplexConfigEditor({ descriptor, label, value, mixed, complexLabels, onChange }: InspectorEditorProps) {
  const initialValue = mixed ? (descriptor.valueType === 'array' ? [] : {}) : value;
  return <div className="page-studio__inspector-complex-editor" data-editor-category="complex">
    <StructuredValueNode
      label={label}
      value={initialValue}
      onChange={onChange}
      mixed={mixed}
      labels={complexLabels}
    />
  </div>;
}

interface StructuredValueNodeProps {
  label: string;
  value: unknown;
  onChange: (value: unknown) => void;
  mixed?: boolean;
  onRemove?: () => void;
  labels: InspectorEditorProps['complexLabels'];
}

function StructuredValueNode({ label, value, onChange, mixed = false, onRemove, labels }: StructuredValueNodeProps) {
  const [newKey, setNewKey] = useState('');
  if (Array.isArray(value)) {
    return <div className="page-studio__complex-value" aria-label={label}>
      <div className="page-studio__complex-value-header"><span>{label}</span><button type="button" className="page-studio__complex-action" onClick={() => onChange([...value, ''])}>{labels.addItem}</button></div>
      {mixed ? <p className="page-studio__complex-hint">{labels.noItems}</p> : null}
      {value.length === 0 ? <p className="page-studio__complex-empty">{labels.noItems}</p> : value.map((item, index) => <div className="page-studio__complex-row" key={`${label}-${index}`}>
        <StructuredValueNode labels={labels} label={`${label}[${index}]`} value={item} onChange={(next) => onChange(value.map((candidate, candidateIndex) => candidateIndex === index ? next : candidate))} onRemove={() => onChange(value.filter((_, candidateIndex) => candidateIndex !== index))} />
      </div>)}
    </div>;
  }
  if (isRecord(value)) {
    return <div className="page-studio__complex-value" aria-label={label}>
      <div className="page-studio__complex-value-header"><span>{label}</span><button type="button" className="page-studio__complex-action" onClick={() => {
        const key = newKey.trim();
        if (!key || key in value) return;
        setNewKey('');
        onChange({ ...value, [key]: '' });
      }}>{labels.addProperty}</button></div>
      {mixed ? <p className="page-studio__complex-hint">{labels.noProperties}</p> : null}
      <div className="page-studio__complex-add-row"><input aria-label={`${label} ${labels.propertyName}`} className="page-studio__inspector-input" value={newKey} placeholder={labels.propertyName} onChange={(event) => setNewKey(event.target.value)} /></div>
      {Object.keys(value).length === 0 ? <p className="page-studio__complex-empty">{labels.noProperties}</p> : Object.entries(value).map(([key, child]) => <div className="page-studio__complex-row" key={`${label}.${key}`}>
        <StructuredValueNode labels={labels} label={key} value={child} onChange={(next) => onChange({ ...value, [key]: next })} onRemove={() => {
          const next = { ...value };
          delete next[key];
          onChange(next);
        }} />
      </div>)}
    </div>;
  }
  return <div className="page-studio__complex-leaf">
    <label>{label}</label>
    <StructuredLeaf value={value} mixed={mixed} onChange={onChange} />
    {onRemove ? <button type="button" className="page-studio__complex-remove" aria-label={labels.remove(label)} onClick={onRemove}>×</button> : null}
  </div>;
}

function StructuredLeaf({ value, mixed, onChange }: { value: unknown; mixed: boolean; onChange: (value: unknown) => void }) {
  const displayValue = mixed || value === null || value === undefined ? '' : String(value);
  if (typeof value === 'boolean') return <input className="page-studio__inspector-checkbox" type="checkbox" checked={!mixed && value} onChange={(event) => onChange(event.target.checked)} />;
  return <input className="page-studio__inspector-input" type={typeof value === 'number' ? 'number' : 'text'} value={displayValue} placeholder={mixed ? '...' : undefined} onChange={(event) => onChange(typeof value === 'number' ? (event.target.value === '' ? null : Number(event.target.value)) : event.target.value)} />;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}
