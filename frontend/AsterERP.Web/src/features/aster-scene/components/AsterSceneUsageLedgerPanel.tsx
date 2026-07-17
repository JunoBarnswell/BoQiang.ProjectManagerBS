import { useQuery } from '@tanstack/react-query';

import { useI18n } from '@/core/i18n/I18nProvider';

import { asterSceneApi } from '../api/asterScene.api';

import { AsterSceneState } from './AsterSceneState';


export function AsterSceneUsageLedgerPanel() {
  const { translate: t } = useI18n();
  const ledgerQuery = useQuery({ queryFn: ({ signal }) => asterSceneApi.usage.ledger({ pageSize: 100 }, signal), queryKey: ['asterscene', 'usage-ledger'] });
  const entries = ledgerQuery.data?.data.items ?? [];
  return <section className="as-band"><div className="as-panel"><h2>{t('asterscene.admin.ledger')}</h2>{ledgerQuery.isLoading ? <AsterSceneState title={t('asterscene.admin.ledgerLoading')} /> : null}{ledgerQuery.isError ? <AsterSceneState title={t('asterscene.admin.ledgerFailed')} /> : null}{!ledgerQuery.isLoading && !ledgerQuery.isError && entries.length === 0 ? <AsterSceneState title={t('asterscene.admin.noLedger')} /> : null}<div className="as-table">{entries.map((entry) => <div className="as-table__row" key={entry.id}><span>{entry.usageType}</span><span>{entry.direction} {entry.quantity} {entry.unit}</span><span>{entry.sourceType}/{entry.sourceId}</span><span>{new Date(entry.occurredAt).toLocaleString()}</span></div>)}</div></div></section>;
}
