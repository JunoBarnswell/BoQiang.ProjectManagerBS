import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../shared/icons/AppIcon';

export interface MappingRow {
  dataType?: string;
  fieldCode?: string;
  fieldName?: string;
  sourceField?: string;
  targetField?: string;
  transform?: string;
}

interface MappingEditorProps {
  variant?: 'mapping' | 'fields';
  value: MappingRow[];
  onChange: (value: MappingRow[]) => void;
}

export function MappingEditor({ variant = 'mapping', value, onChange }: MappingEditorProps) {
  const rows = value.length > 0 ? value : [createEmptyRow(variant)];

  return (
    <div className="space-y-2">
      <div className={`grid gap-2 text-[11px] font-medium text-slate-500 ${variant === 'fields' ? 'grid-cols-[1fr_1fr_110px_auto]' : 'grid-cols-[1fr_1fr_1fr_auto]'}`}>
        {variant === 'fields' ? (
          <>
            <span>{translateCurrentLiteral("字段编码")}</span>
            <span>{translateCurrentLiteral("字段名称")}</span>
            <span>{translateCurrentLiteral("类型")}</span>
            <span />
          </>
        ) : (
          <>
            <span>{translateCurrentLiteral("来源字段")}</span>
            <span>{translateCurrentLiteral("目标字段")}</span>
            <span>{translateCurrentLiteral("转换规则")}</span>
            <span />
          </>
        )}
      </div>
      {rows.map((row, index) => (
        <div key={index} className={`grid gap-2 ${variant === 'fields' ? 'grid-cols-[1fr_1fr_110px_auto]' : 'grid-cols-[1fr_1fr_1fr_auto]'}`}>
          {variant === 'fields' ? (
            <>
              <input className={inputClass} placeholder="code" value={row.fieldCode ?? ''} onChange={(event) => updateRow(rows, index, { ...row, fieldCode: event.target.value }, onChange)} />
              <input className={inputClass} placeholder={translateCurrentLiteral("名称")} value={row.fieldName ?? ''} onChange={(event) => updateRow(rows, index, { ...row, fieldName: event.target.value }, onChange)} />
              <select className={inputClass} value={row.dataType ?? 'Text'} onChange={(event) => updateRow(rows, index, { ...row, dataType: event.target.value }, onChange)}>
                <option value="Text">{translateCurrentLiteral("文本")}</option>
                <option value="Number">{translateCurrentLiteral("数字")}</option>
                <option value="Date">{translateCurrentLiteral("日期")}</option>
                <option value="Boolean">{translateCurrentLiteral("布尔")}</option>
              </select>
            </>
          ) : (
            <>
              <input className={inputClass} placeholder="source" value={row.sourceField ?? ''} onChange={(event) => updateRow(rows, index, { ...row, sourceField: event.target.value }, onChange)} />
              <input className={inputClass} placeholder="target" value={row.targetField ?? ''} onChange={(event) => updateRow(rows, index, { ...row, targetField: event.target.value }, onChange)} />
              <input className={inputClass} placeholder="trim / map / script" value={row.transform ?? ''} onChange={(event) => updateRow(rows, index, { ...row, transform: event.target.value }, onChange)} />
            </>
          )}
          <button aria-label="删除" className="h-9 rounded border border-slate-200 px-2 text-slate-500 hover:border-red-200 hover:text-red-600" type="button" onClick={() => onChange(rows.filter((_item, rowIndex) => rowIndex !== index))}>
            <AppIcon className="h-4 w-4" name="trash" />
          </button>
        </div>
      ))}
      <button className="inline-flex items-center gap-1 text-xs font-medium text-primary-700 hover:text-primary-800" type="button" onClick={() => onChange([...rows, createEmptyRow(variant)])}>
        <AppIcon className="h-3.5 w-3.5" name="plus" />{translateCurrentLiteral("添加一行")}</button>
    </div>
  );
}

const inputClass = 'h-9 rounded border border-slate-300 px-2 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100';

function createEmptyRow(variant: 'mapping' | 'fields'): MappingRow {
  return variant === 'fields'
    ? { dataType: 'Text', fieldCode: '', fieldName: '' }
    : { sourceField: '', targetField: '', transform: '' };
}

function updateRow(rows: MappingRow[], index: number, row: MappingRow, onChange: (value: MappingRow[]) => void) {
  const next = [...rows];
  next[index] = row;
  onChange(next);
}
