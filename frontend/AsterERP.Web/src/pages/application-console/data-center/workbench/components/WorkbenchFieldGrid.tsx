interface FieldItem {
  label: string;
  value?: unknown;
}

interface WorkbenchFieldGridProps {
  columns?: 2 | 3;
  fields: FieldItem[];
}

export function WorkbenchFieldGrid({ columns = 2, fields }: WorkbenchFieldGridProps) {
  return (
    <div className={`grid gap-2 ${columns === 3 ? 'lg:grid-cols-3' : 'lg:grid-cols-2'}`}>
      {fields.map((field) => (
        <div className="rounded-md border border-slate-200 bg-slate-50 px-2.5 py-1.5" key={field.label}>
          <div className="text-xs text-slate-500">{field.label}</div>
          <div className="mt-0.5 break-words text-xs font-semibold text-slate-900">{formatValue(field.value)}</div>
        </div>
      ))}
    </div>
  );
}

function formatValue(value: unknown) {
  if (value === null || value === undefined || value === '') {
    return '-';
  }

  if (typeof value === 'boolean') {
    return value ? '是' : '否';
  }

  if (typeof value === 'object') {
    return Array.isArray(value) ? `${value.length} 项` : `${Object.keys(value as Record<string, unknown>).length} 项配置`;
  }

  return String(value);
}
