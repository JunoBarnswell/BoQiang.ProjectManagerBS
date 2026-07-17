import type { DashboardMetric } from './dashboardModel';
export function MetricCard({ desc, icon: Icon, isLoading = false, noAccess = false, tone, title, value }: DashboardMetric) {
  const toneClassMap: Record<DashboardMetric['tone'], string> = {
    amber: 'bg-amber-50 text-amber-600',
    blue: 'bg-blue-50 text-blue-600',
    emerald: 'bg-emerald-50 text-emerald-600',
    rose: 'bg-rose-50 text-rose-600',
    teal: 'bg-teal-50 text-teal-600'
  };

  return (
    <section className="erp-panel p-4 transition-all hover:shadow-md">
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <p className="text-xs font-semibold text-gray-500">{title}</p>
          <strong className={`mt-2 block truncate text-2xl font-extrabold ${noAccess ? 'text-gray-400' : 'text-gray-900'}`}>
            {isLoading ? '...' : value}
          </strong>
        </div>
        <span className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-lg ${toneClassMap[tone]}`}>
          <Icon size={24} />
        </span>
      </div>
      <p className="mt-3 line-clamp-2 text-xs text-gray-400">{desc}</p>
    </section>
  );
}
