import { useMemo } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiQuery } from '../../core/query/useApiQuery';
import { DataTable } from '../../shared/table/DataTable';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import { aiChatApi, type AiSkCapabilityDto } from './api/aiCenter.api';

import './styles/ai-center.css';

export function AiSkCapabilitiesPanel() {
  const { translate } = useI18n();
  const capabilitiesQuery = useApiQuery({
    queryKey: ['ai', 'sk-capabilities'],
    queryFn: ({ signal }) => aiChatApi.capabilities.list(signal)
  });

  const rows = capabilitiesQuery.data?.data ?? [];
  const columns = useMemo<DataTableColumn<AiSkCapabilityDto>[]>(() => [
    { key: 'capabilityCode', title: translate('ai.skCapabilities.column.capabilityCode'), render: (row) => row.capabilityCode },
    { key: 'status', title: translate('ai.skCapabilities.column.status'), render: (row) => renderCapabilityStatus(row.status, translate) },
    { key: 'frameworkType', title: translate('ai.skCapabilities.column.frameworkType'), render: (row) => row.frameworkType },
    { key: 'implementationSymbol', title: translate('ai.skCapabilities.column.implementationSymbol'), render: (row) => row.implementationSymbol },
    { key: 'reason', title: translate('ai.skCapabilities.column.reason'), render: (row) => row.reason }
  ], [translate]);

  return (
    <section className="ai-tool-section">
      <h3>{translate('ai.skCapabilities.title')}</h3>
      <DataTable
        className="ai-admin-grid"
        columns={columns}
        fitScreen
        loading={capabilitiesQuery.isFetching}
        rowKey={(row) => row.capabilityCode}
        rows={rows}
      />
    </section>
  );
}

function renderCapabilityStatus(value: string, translate: (key: string) => string) {
  const className =
    value === 'Implemented'
      ? 'bg-emerald-50 text-emerald-700'
      : value === 'FrameworkUnavailable'
        ? 'bg-amber-50 text-amber-700'
        : value === 'Blocked'
          ? 'bg-red-50 text-red-700'
          : 'bg-gray-100 text-gray-700';
  const label =
    value === 'Implemented'
      ? translate('ai.skCapabilities.status.implemented')
      : value === 'FrameworkUnavailable'
        ? translate('ai.skCapabilities.status.frameworkUnavailable')
        : value === 'Blocked'
          ? translate('ai.skCapabilities.status.blocked')
          : translate('ai.skCapabilities.status.unknown');
  return <span className={`inline-flex rounded px-2 py-1 text-xs font-medium ${className}`}>{label}</span>;
}
