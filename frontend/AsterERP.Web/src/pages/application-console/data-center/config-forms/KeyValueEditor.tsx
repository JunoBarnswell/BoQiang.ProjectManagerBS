import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../shared/icons/AppIcon';

export interface KeyValueRow {
  description?: string;
  key: string;
  value: string;
}

interface KeyValueEditorProps {
  value: KeyValueRow[];
  onChange: (value: KeyValueRow[]) => void;
}

export function KeyValueEditor({ value, onChange }: KeyValueEditorProps) {
  const rows = value.length > 0 ? value : [{ key: '', value: '' }];

  return (
    <div className="space-y-2">
      {rows.map((row, index) => (
        <div key={index} className="grid grid-cols-[1fr_1fr_auto] gap-2">
          <input
            className="h-9 rounded border border-slate-300 px-2 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
            placeholder={translateCurrentLiteral("键")}
            value={row.key}
            onChange={(event) => updateRow(rows, index, { ...row, key: event.target.value }, onChange)}
          />
          <input
            className="h-9 rounded border border-slate-300 px-2 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
            placeholder={translateCurrentLiteral("值")}
            value={row.value}
            onChange={(event) => updateRow(rows, index, { ...row, value: event.target.value }, onChange)}
          />
          <button
            aria-label="删除"
            className="h-9 rounded border border-slate-200 px-2 text-slate-500 hover:border-red-200 hover:text-red-600"
            type="button"
            onClick={() => onChange(rows.filter((_item, rowIndex) => rowIndex !== index))}
          >
            <AppIcon className="h-4 w-4" name="trash" />
          </button>
        </div>
      ))}
      <button className="inline-flex items-center gap-1 text-xs font-medium text-primary-700 hover:text-primary-800" type="button" onClick={() => onChange([...rows, { key: '', value: '' }])}>
        <AppIcon className="h-3.5 w-3.5" name="plus" />{translateCurrentLiteral("添加一项")}</button>
    </div>
  );
}

function updateRow(rows: KeyValueRow[], index: number, row: KeyValueRow, onChange: (value: KeyValueRow[]) => void) {
  const next = [...rows];
  next[index] = row;
  onChange(next);
}
