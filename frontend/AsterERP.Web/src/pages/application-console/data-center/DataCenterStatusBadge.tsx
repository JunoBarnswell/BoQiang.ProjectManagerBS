interface DataCenterStatusBadgeProps {
  status?: string | null;
}

const statusClassMap: Record<string, string> = {
  Archived: 'border-slate-200 bg-slate-50 text-slate-600',
  Disabled: 'border-gray-200 bg-gray-50 text-gray-500',
  Draft: 'border-amber-200 bg-amber-50 text-amber-700',
  Enabled: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  Failed: 'border-red-200 bg-red-50 text-red-700',
  Passed: 'border-emerald-200 bg-emerald-50 text-emerald-700',
  Published: 'border-blue-200 bg-blue-50 text-blue-700',
  Unknown: 'border-slate-200 bg-slate-50 text-slate-600'
};

const statusTextMap: Record<string, string> = {
  Archived: '已归档',
  Disabled: '停用',
  Draft: '草稿',
  Enabled: '启用',
  Failed: '失败',
  Passed: '通过',
  Published: '已发布',
  Unknown: '未知'
};

export function DataCenterStatusBadge({ status }: DataCenterStatusBadgeProps) {
  const normalized = status || 'Unknown';
  const className = statusClassMap[normalized] ?? statusClassMap.Unknown;

  return (
    <span className={`inline-flex h-6 shrink-0 items-center whitespace-nowrap rounded border px-2 text-xs font-medium ${className}`}>
      {statusTextMap[normalized] ?? normalized}
    </span>
  );
}
