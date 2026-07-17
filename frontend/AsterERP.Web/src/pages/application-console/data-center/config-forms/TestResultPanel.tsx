import type { ApplicationDataCenterActionResult } from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { DataCenterStatusBadge } from '../DataCenterStatusBadge';

interface TestResultPanelProps {
  result: ApplicationDataCenterActionResult;
}

export function TestResultPanel({ result }: TestResultPanelProps) {
  return (
    <section className="rounded-md border border-slate-200 bg-white p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="text-sm font-semibold text-slate-900">{translateCurrentLiteral("操作结果")}</div>
        <DataCenterStatusBadge status={result.status} />
      </div>
      <div className="mt-2 text-xs leading-5 text-slate-600">{result.message}</div>
      <div className="mt-1 text-xs text-slate-400">{result.durationMs} ms</div>
      {result.detailJson ? <pre className="mt-3 max-h-40 overflow-auto rounded bg-slate-950 p-2 text-xs leading-5 text-slate-100">{result.detailJson}</pre> : null}
    </section>
  );
}
