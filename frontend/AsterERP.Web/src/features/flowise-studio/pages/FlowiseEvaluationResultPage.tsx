import { useParams } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useApiQuery } from '../../../core/query/useApiQuery';
import { PermissionButton } from '../../../shared/auth/PermissionButton';
import { flowisePermissions } from '../../../shared/auth/permissionCodes';
import { useMessage } from '../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { evaluationsApi } from '../api/evaluations.api';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { translateFlowiseStatus } from '../i18n/flowiseTranslate';
import { MainCard } from '../native/ui-component/cards/MainCard';
import { FlowListTable, type FlowListTableColumn } from '../native/ui-component/table/FlowListTable';

interface EvaluationMetricView {
  key: string;
  value: string;
}

interface EvaluationResultRowView {
  actualOutput: string;
  error: string;
  expectedOutput: string;
  id: string;
  input: string;
  latencyMs: string;
  raw: string;
  score: string;
  status: string;
}

function parseJson(value: string | null | undefined): unknown {
  if (!value?.trim()) {
    return null;
  }

  try {
    return JSON.parse(value) as unknown;
  } catch {
    return null;
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}

function readText(record: Record<string, unknown>, keys: string[]): string {
  for (const key of keys) {
    const value = record[key];
    if (typeof value === 'string' && value.trim()) {
      return value;
    }
    if (typeof value === 'number' || typeof value === 'boolean') {
      return String(value);
    }
  }

  return '-';
}

function toDisplayValue(value: unknown): string {
  if (value === null || value === undefined || value === '') {
    return '-';
  }

  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}

function parseMetrics(metricsJson: string | null | undefined): EvaluationMetricView[] {
  const parsed = parseJson(metricsJson);
  if (!isRecord(parsed)) {
    return [];
  }

  return Object.entries(parsed).map(([key, value]) => ({ key, value: toDisplayValue(value) }));
}

function parseResultRows(resultRowsJson: string | null | undefined): EvaluationResultRowView[] {
  const parsed = parseJson(resultRowsJson);
  const rows = Array.isArray(parsed) ? parsed : [];

  return rows.map((item, index) => {
    const record = isRecord(item) ? item : {};
    return {
      actualOutput: readText(record, ['actualOutput', 'actual', 'output', 'response']),
      error: readText(record, ['error', 'errorMessage', 'reason']),
      expectedOutput: readText(record, ['expectedOutput', 'expected', 'expectedAnswer']),
      id: readText(record, ['id', 'rowId', 'datasetRowId']).replace(/^-$/, '') || `row-${index + 1}`,
      input: readText(record, ['input', 'question', 'prompt']),
      latencyMs: readText(record, ['latencyMs', 'durationMs', 'elapsedMs']),
      raw: JSON.stringify(item),
      score: readText(record, ['score', 'passedScore', 'grade']),
      status: readText(record, ['status', 'passed', 'result'])
    };
  });
}

export function FlowiseEvaluationResultPage() {
  const { id = '' } = useParams();
  const { translate } = useI18n();
  const message = useMessage();
  const resultQuery = useApiQuery({
    enabled: Boolean(id),
    queryKey: ['flowise', 'evaluation-result', id],
    queryFn: ({ signal }) => evaluationsApi.evaluations.result(id, signal)
  });
  const runAgainMutation = useApiMutation({
    mutationFn: () => evaluationsApi.evaluations.runAgain(id),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.runAgainFailed))),
    onSuccess: async () => {
      message.success(translate(flowiseI18nKeys.messages.evaluationRunCreated));
      await resultQuery.refetch();
    }
  });
  const result = resultQuery.data?.data;
  const metrics = parseMetrics(result?.metricsJson);
  const rows = parseResultRows(result?.resultRowsJson);
  const rowColumns: FlowListTableColumn<EvaluationResultRowView>[] = [
    { key: 'status', title: translate(flowiseI18nKeys.fields.status), width: '110px' },
    { key: 'score', title: 'Score', width: '90px' },
    { key: 'latencyMs', title: translate(flowiseI18nKeys.fields.latency), width: '110px' },
    { key: 'input', title: 'Input', width: '220px', render: (item) => <span className="flowise-cell-ellipsis">{item.input}</span> },
    { key: 'expectedOutput', title: 'Expected', width: '220px', render: (item) => <span className="flowise-cell-ellipsis">{item.expectedOutput}</span> },
    { key: 'actualOutput', title: 'Actual', width: '220px', render: (item) => <span className="flowise-cell-ellipsis">{item.actualOutput}</span> },
    { key: 'error', title: 'Error', width: '180px', render: (item) => <span className="flowise-cell-ellipsis">{item.error}</span> }
  ];

  return (
    <MainCard
      actions={
        <PermissionButton className="btn-primary" code={[flowisePermissions.run, flowisePermissions.retry]} disabled={runAgainMutation.isPending} type="button" onClick={() => runAgainMutation.mutate()}>
          {translate(flowiseI18nKeys.actions.runAgain)}
        </PermissionButton>
      }
      description={result ? translate(flowiseI18nKeys.detail.versionStatus).replace('{version}', String(result.versionNo)).replace('{status}', translateFlowiseStatus(result.status, translate)) : ''}
      title={translate(flowiseI18nKeys.detail.evaluationResult)}
    >
      <div className="flowise-metrics-grid">
        <div><span>{translate(flowiseI18nKeys.detail.passRate)}</span><strong>{Math.round((result?.passRate ?? 0) * 100)}%</strong></div>
        <div><span>{translate(flowiseI18nKeys.fields.latency)}</span><strong>{result?.averageLatencyMs ?? 0} ms</strong></div>
        <div><span>{translate(flowiseI18nKeys.fields.tokens)}</span><strong>{result?.totalTokens ?? 0}</strong></div>
        <div><span>{translate(flowiseI18nKeys.fields.status)}</span><strong>{result ? translateFlowiseStatus(result.status, translate) : '-'}</strong></div>
      </div>
      <div className="flowise-detail-panel">
        <h3>{translate(flowiseI18nKeys.detail.metrics)}</h3>
        {metrics.length > 0 ? (
          <dl className="flowise-detail-list">
            {metrics.map((metric) => <div key={metric.key}><dt>{metric.key}</dt><dd>{metric.value}</dd></div>)}
          </dl>
        ) : (
          <pre>{result?.metricsJson ?? '{}'}</pre>
        )}
      </div>
      <div className="flowise-detail-panel">
        <h3>{translate(flowiseI18nKeys.detail.rows)}</h3>
        {rows.length > 0 ? (
          <FlowListTable columns={rowColumns} emptyText="No rows" getRowKey={(item) => item.id} loading={resultQuery.isLoading} rows={rows} />
        ) : (
          <pre>{result?.resultRowsJson ?? '[]'}</pre>
        )}
      </div>
    </MainCard>
  );
}
