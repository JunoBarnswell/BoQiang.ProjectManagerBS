import { useI18n } from '../../../core/i18n/I18nProvider';

interface RuntimeSummaryProps {
  appCode?: unknown;
  modelCode?: unknown;
  pageCode?: unknown;
  pageType?: unknown;
  permissionCode?: unknown;
  tenantId?: unknown;
}

function formatValue(value: unknown): string {
  return typeof value === 'string' && value.trim() ? value : '-';
}

export function RuntimeSummary(props: RuntimeSummaryProps) {
  const { translate } = useI18n();
  const items: Array<[string, unknown]> = [
    [translate('runtime.summary.pageCode'), props.pageCode],
    [translate('runtime.summary.pageType'), props.pageType],
    [translate('runtime.summary.tenantId'), props.tenantId],
    [translate('runtime.summary.appCode'), props.appCode],
    [translate('runtime.summary.modelCode'), props.modelCode],
    [translate('runtime.summary.permissionCode'), props.permissionCode]
  ];

  return (
    <section className="bg-white border border-gray-200 rounded-lg shadow-sm p-4">
      <div className="grid gap-3 md:grid-cols-3">
        {items.map(([label, value]) => (
          <div key={label} className="min-w-0">
            <div className="text-xs text-gray-500">{label}</div>
            <div className="mt-1 truncate text-sm font-medium text-gray-900">{formatValue(value)}</div>
          </div>
        ))}
      </div>
    </section>
  );
}
