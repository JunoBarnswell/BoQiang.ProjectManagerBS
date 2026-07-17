import { useState } from 'react';

interface WorkbenchJsonVisualViewerProps {
  data: unknown;
  emptyText?: string;
  title?: string;
}

export function WorkbenchJsonVisualViewer({ data, emptyText = '暂无配置', title = '配置' }: WorkbenchJsonVisualViewerProps) {
  const [showRaw, setShowRaw] = useState(false);
  const normalized = normalize(data);
  const entries = normalized && !Array.isArray(normalized) ? Object.entries(normalized) : [];

  if (!normalized || (Array.isArray(normalized) && normalized.length === 0) || (!Array.isArray(normalized) && entries.length === 0)) {
    return <div className="rounded-md border border-dashed border-slate-200 px-4 py-6 text-center text-sm text-slate-500">{emptyText}</div>;
  }

  return (
    <div className="rounded-md border border-slate-200 bg-white">
      <div className="flex items-center justify-between border-b border-slate-200 px-3 py-2">
        <span className="text-sm font-medium text-slate-900">{title}</span>
        <button className="text-xs text-primary-600 hover:text-primary-700" type="button" onClick={() => setShowRaw((value) => !value)}>
          {showRaw ? '隐藏原始配置' : '高级原始配置'}
        </button>
      </div>
      {!showRaw ? (
        Array.isArray(normalized) ? (
          <div className="divide-y divide-slate-100">
            {normalized.map((item, index) => <JsonRow key={index} label={`#${index + 1}`} value={item} />)}
          </div>
        ) : (
          <div className="divide-y divide-slate-100">
            {entries.map(([key, value]) => <JsonRow key={key} label={key} value={value} />)}
          </div>
        )
      ) : (
        <pre className="max-h-80 overflow-auto bg-slate-950 p-3 text-xs leading-5 text-slate-100">{JSON.stringify(normalized, null, 2)}</pre>
      )}
    </div>
  );
}

function JsonRow({ label, value }: { label: string; value: unknown }) {
  return (
    <div className="grid gap-2 px-3 py-2 text-sm md:grid-cols-[160px_1fr]">
      <span className="font-medium text-slate-600">{label}</span>
      <span className="break-words text-slate-900">{formatValue(value)}</span>
    </div>
  );
}

function normalize(data: unknown) {
  if (typeof data === 'string') {
    try {
      return JSON.parse(data) as unknown;
    } catch {
      return data ? { value: data } : {};
    }
  }

  return data;
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined || value === '') {
    return '-';
  }

  if (typeof value === 'object') {
    return Array.isArray(value) ? `${value.length} 项` : `${Object.keys(value as Record<string, unknown>).length} 个字段`;
  }

  return String(value);
}
