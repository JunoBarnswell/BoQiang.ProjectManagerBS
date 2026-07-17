import { useMemo, useState } from 'react';

import { parseJsonObject } from './configJsonCodec';

interface ConfigJsonAdvancedPanelProps {
  label: string;
  readOnly?: boolean;
  value: string;
  onChange?: (value: string) => void;
}

export function ConfigJsonAdvancedPanel({ label, readOnly, value, onChange }: ConfigJsonAdvancedPanelProps) {
  const [open, setOpen] = useState(false);
  const [editable, setEditable] = useState(false);
  const error = useMemo(() => parseJsonObject(value).error, [value]);

  return (
    <section className="rounded-md border border-slate-200 bg-white">
      <button className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm font-semibold text-slate-900" type="button" onClick={() => setOpen((current) => !current)}>
        <span>{label}</span>
        <span className="text-xs font-normal text-slate-500">{open ? '收起' : '展开'}</span>
      </button>
      {open ? (
        <div className="space-y-3 border-t border-slate-100 p-3">
          {!readOnly && !editable ? (
            <label className="flex items-start gap-2 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs leading-5 text-amber-800">
              <input className="mt-1" type="checkbox" checked={editable} onChange={(event) => setEditable(event.target.checked)} />
              我确认需要直接编辑原始 JSON。非法 JSON 将禁止保存，未识别字段会继续保留。
            </label>
          ) : null}
          <textarea
            className="min-h-[180px] w-full rounded border border-slate-300 bg-slate-950 px-3 py-2 font-mono text-xs leading-5 text-slate-100 outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 disabled:opacity-80"
            disabled={readOnly || !editable}
            spellCheck={false}
            value={value}
            onChange={(event) => onChange?.(event.target.value)}
          />
          {error ? <div className="text-xs leading-5 text-red-600">JSON 格式不正确：{error}</div> : null}
        </div>
      ) : null}
    </section>
  );
}
