import { Play, RotateCcw } from 'lucide-react';
import { useMemo, useState } from 'react';

import { invokeApplicationDataApi, type ApplicationDataHttpMethod } from '../../../api/application-data/applicationData.api';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useMessage } from '../../feedback/useMessage';
import { PageError } from '../../status/PageError';
import { getErrorMessage } from '../../utils/errorMessage';

interface RuntimeApiActionProps {
  buttonText?: string | null;
  defaultPayload?: Record<string, unknown> | null;
  httpMethod?: string | null;
  parameterMappings?: unknown;
  responseMappings?: unknown;
  routePath?: string | null;
  title?: string | null;
  variables?: Record<string, unknown> | null;
}

interface RuntimeMapping {
  source: string;
  target: string;
}

export function RuntimeApiAction({
  buttonText,
  defaultPayload,
  httpMethod,
  parameterMappings,
  responseMappings,
  routePath,
  title,
  variables
}: RuntimeApiActionProps) {
  const message = useMessage();
  const mappings = useMemo(() => parseMappings(parameterMappings), [parameterMappings]);
  const responseMap = useMemo(() => parseMappings(responseMappings), [responseMappings]);
  const method = normalizeMethod(httpMethod);
  const [payloadJson, setPayloadJson] = useState(() => JSON.stringify(defaultPayload ?? buildPayloadFromMappings(mappings, variables), null, 2));
  const [result, setResult] = useState<unknown>(null);
  const mutation = useApiMutation({
    mutationFn: async () => {
      const payload = parsePayload(payloadJson);
      const mappedPayload = { ...buildPayloadFromMappings(mappings, variables), ...payload };
      const response = await invokeApplicationDataApi({
        body: method === 'GET' ? null : mappedPayload,
        method,
        query: method === 'GET' || method === 'DELETE' ? mappedPayload : null,
        routePath: String(routePath ?? '')
      });
      return response.data;
    },
    onError: (error) => message.error(getErrorMessage(error, '接口调用失败')),
    onSuccess: (data) => {
      setResult(data);
      message.success('接口调用成功');
    }
  });

  if (!routePath?.trim()) {
    return <PageError description="接口组件缺少 routePath，请在设计器交互面板选择已发布接口。" />;
  }

  return (
    <div className="grid gap-3">
      <div className="flex flex-col gap-2 rounded-md border border-slate-200 bg-slate-50/70 p-3 md:flex-row md:items-center md:justify-between">
        <div className="min-w-0">
          <div className="text-sm font-semibold text-slate-950">{title || '接口调用'}</div>
          <div className="mt-0.5 font-mono text-xs text-slate-500">{method} {routePath}</div>
        </div>
        <div className="flex shrink-0 flex-wrap items-center gap-2">
          <button className="secondary-button h-8 text-xs" type="button" onClick={() => { setPayloadJson(JSON.stringify(buildPayloadFromMappings(mappings, variables), null, 2)); setResult(null); }}>
            <RotateCcw className="h-3.5 w-3.5" />{translateCurrentLiteral("重置")}</button>
          <button className="primary-button h-8 text-xs" disabled={mutation.isPending} type="button" onClick={() => mutation.mutate()}>
            <Play className="h-3.5 w-3.5" />
            {mutation.isPending ? '调用中...' : buttonText || '调用接口'}
          </button>
        </div>
      </div>
      <textarea
        className="form-input min-h-28 font-mono text-xs"
        value={payloadJson}
        onChange={(event) => setPayloadJson(event.target.value)}
      />
      <div className="rounded-md border border-slate-200 bg-white p-3">
        <div className="mb-2 flex items-center justify-between text-xs font-semibold text-slate-700">
          <span>{translateCurrentLiteral("响应结果")}</span>
          {responseMap.length ? <span className="font-normal text-slate-400">已配置 {responseMap.length} 个返回映射</span> : null}
        </div>
        <pre className="max-h-72 overflow-auto whitespace-pre-wrap rounded bg-slate-950 p-3 text-xs text-slate-100">
          {result === null ? '暂无响应' : JSON.stringify(result, null, 2)}
        </pre>
      </div>
    </div>
  );
}

function parseMappings(value: unknown): RuntimeMapping[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map((item): RuntimeMapping | null => {
      if (!item || typeof item !== 'object') {
        return null;
      }

      const source = String((item as { source?: unknown }).source ?? '').trim();
      const target = String((item as { target?: unknown }).target ?? '').trim();
      return source && target ? { source, target } : null;
    })
    .filter((item): item is RuntimeMapping => item !== null);
}

function buildPayloadFromMappings(mappings: RuntimeMapping[], variables: Record<string, unknown> | null | undefined): Record<string, unknown> {
  const payload: Record<string, unknown> = {};
  mappings.forEach((mapping) => {
    payload[mapping.target] = resolveExpression(mapping.source, variables);
  });
  return payload;
}

function resolveExpression(expression: string, variables: Record<string, unknown> | null | undefined): unknown {
  const trimmed = expression.trim();
  const variableMatch = /^\{\{\s*vars\.([A-Za-z][A-Za-z0-9_]*)\s*\}\}$/.exec(trimmed);
  if (variableMatch) {
    return variables?.[variableMatch[1]] ?? '';
  }

  if (/^\{\{.*\}\}$/.test(trimmed)) {
    return '';
  }

  return trimmed;
}

function parsePayload(value: string): Record<string, unknown> {
  const trimmed = value.trim();
  if (!trimmed) {
    return {};
  }

  const parsed = JSON.parse(trimmed) as unknown;
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('请求参数必须是 JSON 对象');
  }

  return parsed as Record<string, unknown>;
}

function normalizeMethod(method: string | null | undefined): ApplicationDataHttpMethod {
  const normalized = String(method || 'GET').trim().toUpperCase();
  return ['DELETE', 'GET', 'PATCH', 'POST', 'PUT'].includes(normalized)
    ? normalized as ApplicationDataHttpMethod
    : 'GET';
}
