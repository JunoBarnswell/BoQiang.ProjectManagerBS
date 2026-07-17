import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../shared/icons/AppIcon';

export interface CodeRuleSegment {
  dateFormat?: string;
  length?: number;
  type: string;
  value?: string;
}

interface CodeRuleSegmentEditorProps {
  value: CodeRuleSegment[];
  onChange: (value: CodeRuleSegment[]) => void;
}

export function CodeRuleSegmentEditor({ value, onChange }: CodeRuleSegmentEditorProps) {
  const rows = value.length > 0 ? value : [{ type: 'fixed', value: 'RK-' }, { dateFormat: 'yyyyMMdd', type: 'date' }, { length: 4, type: 'sequence' }];
  const preview = buildPreview(rows);

  return (
    <div className="space-y-2">
      {rows.map((row, index) => (
        <div key={index} className="grid grid-cols-[120px_1fr_92px_auto] gap-2">
          <select className={inputClass} value={row.type} onChange={(event) => updateRow(rows, index, normalizeType(event.target.value), onChange)}>
            <option value="fixed">{translateCurrentLiteral("固定值")}</option>
            <option value="date">{translateCurrentLiteral("日期")}</option>
            <option value="businessField">{translateCurrentLiteral("业务字段")}</option>
            <option value="sequence">{translateCurrentLiteral("流水号")}</option>
          </select>
          <input className={inputClass} placeholder={placeholder(row.type)} value={row.value ?? row.dateFormat ?? ''} onChange={(event) => updateRow(rows, index, updateValue(row, event.target.value), onChange)} />
          <input className={inputClass} min={1} max={12} placeholder={translateCurrentLiteral("长度")} type="number" value={row.length ?? ''} onChange={(event) => updateRow(rows, index, { ...row, length: event.target.value ? Number(event.target.value) : undefined }, onChange)} />
          <button aria-label="删除" className="h-9 rounded border border-slate-200 px-2 text-slate-500 hover:border-red-200 hover:text-red-600" type="button" onClick={() => onChange(rows.filter((_item, rowIndex) => rowIndex !== index))}>
            <AppIcon className="h-4 w-4" name="trash" />
          </button>
        </div>
      ))}
      <div className="flex items-center justify-between gap-3 rounded border border-slate-100 bg-slate-50 px-3 py-2 text-xs">
        <span className="text-slate-500">{translateCurrentLiteral("示例预览")}</span>
        <span className="font-mono font-semibold text-slate-800">{preview}</span>
      </div>
      <button className="inline-flex items-center gap-1 text-xs font-medium text-primary-700 hover:text-primary-800" type="button" onClick={() => onChange([...rows, { type: 'fixed', value: '-' }])}>
        <AppIcon className="h-3.5 w-3.5" name="plus" />{translateCurrentLiteral("添加段")}</button>
    </div>
  );
}

const inputClass = 'h-9 rounded border border-slate-300 px-2 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100';

function normalizeType(type: string): CodeRuleSegment {
  if (type === 'date') {
    return { dateFormat: 'yyyyMMdd', type };
  }

  if (type === 'sequence') {
    return { length: 4, type };
  }

  return { type, value: type === 'businessField' ? 'deptCode' : '-' };
}

function updateValue(row: CodeRuleSegment, value: string): CodeRuleSegment {
  return row.type === 'date' ? { ...row, dateFormat: value } : { ...row, value };
}

function updateRow(rows: CodeRuleSegment[], index: number, row: CodeRuleSegment, onChange: (value: CodeRuleSegment[]) => void) {
  const next = [...rows];
  next[index] = row;
  onChange(next);
}

function placeholder(type: string): string {
  if (type === 'date') {
    return 'yyyyMMdd';
  }

  if (type === 'businessField') {
    return '字段名';
  }

  if (type === 'sequence') {
    return '自动生成';
  }

  return '固定文本';
}

function buildPreview(rows: CodeRuleSegment[]): string {
  const now = new Date();
  const yyyy = String(now.getFullYear());
  const MM = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');

  return rows
    .map((row) => {
      if (row.type === 'date') {
        return (row.dateFormat || 'yyyyMMdd').replace('yyyy', yyyy).replace('MM', MM).replace('dd', dd);
      }

      if (row.type === 'sequence') {
        return '1'.padStart(row.length ?? 4, '0');
      }

      if (row.type === 'businessField') {
        return row.value ? `{${row.value}}` : '{字段}';
      }

      return row.value ?? '';
    })
    .join('');
}
