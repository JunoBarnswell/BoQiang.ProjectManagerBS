import { useQueryClient } from '@tanstack/react-query';
import { useMemo } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';

import { getRuntimePageSchema } from '../../api/runtime/runtime.api';
import { isHttpError } from '../../core/http/httpError';
import { useI18n } from '../../core/i18n/I18nProvider';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useAuthStore, usePermissionStore } from '../../core/state';
import { usePrintLauncher } from '../../features/print-center/hooks/usePrintLauncher';
import { ComponentRuntimeHost, createRuntimeManifestRegistry } from '../../runtime-kernel/ComponentRuntimeHost';
import { parseRuntimePageArtifact, toRuntimeArtifact } from '../../runtime-kernel/RuntimeArtifactCodec';
import { Page403 } from '../../shared/status/Page403';
import { Page404 } from '../../shared/status/Page404';
import { PageError } from '../../shared/status/PageError';
import { PageLoading } from '../../shared/status/PageLoading';

const PUBLISHED_PAGE_SCHEMA_STALE_TIME_MS = 5 * 60 * 1000;
const PREVIEW_PAGE_SCHEMA_STALE_TIME_MS = 15 * 1000;

export function RuntimePage() {
  const { translate } = useI18n();
  const { appCode = '', pageCode = '', tenantId = '' } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const printLauncher = usePrintLauncher();
  const permissionCodes = usePermissionStore((state) => state.permissionCodes);
  const currentUser = useAuthStore((state) => state.user);
  const previewPageId = searchParams.get('previewPageId')?.trim() ?? '';
  const decodedPageCode = useMemo(() => {
    try { return decodeURIComponent(pageCode); } catch { return pageCode; }
  }, [pageCode]);
  const runtimePageQueryKey = queryKeys.runtime.pageSchemaScoped(tenantId, appCode, decodedPageCode, previewPageId || '');
  const schemaQuery = useApiQuery({
    enabled: decodedPageCode.trim().length > 0,
    keepPreviousData: true,
    queryFn: ({ signal }) => getRuntimePageSchema(decodedPageCode, previewPageId || null, signal),
    queryKey: runtimePageQueryKey,
    refetchOnMount: false,
    refetchOnReconnect: false,
    retry: false,
    staleTimeMs: previewPageId ? PREVIEW_PAGE_SCHEMA_STALE_TIME_MS : PUBLISHED_PAGE_SCHEMA_STALE_TIME_MS
  });
  const runtimeEnvelope = useMemo(() => schemaQuery.data?.data.artifactJson ? parseRuntimePageArtifact(schemaQuery.data.data.artifactJson) : null, [schemaQuery.data?.data.artifactJson]);
  const runtimeArtifact = useMemo(() => runtimeEnvelope ? toRuntimeArtifact(runtimeEnvelope) : null, [runtimeEnvelope]);

  if (!decodedPageCode.trim()) return <Page404 />;
  if (schemaQuery.isLoading) return <PageLoading />;
  if (schemaQuery.isError) {
    const error = schemaQuery.error;
    if (isHttpError(error) && error.status === 403) return <Page403 />;
    if (isHttpError(error) && error.status === 404) {
      return <PageError action={<RetryButton onClick={() => void schemaQuery.refetch()} label={translate('common.retry')} />} description={translate('status.404.description')} />;
    }
    return <PageError action={<RetryButton onClick={() => void schemaQuery.refetch()} label={translate('common.retry')} />} description={translate('runtime.pageLoadFailed')} />;
  }
  if (!runtimeEnvelope) return <PageError description={translate('runtime.invalidSchema')} />;

  return (
    <div className="runtime-page-shell">
      {runtimeArtifact
        ? <>
          <ComponentRuntimeHost
            artifact={runtimeArtifact}
            manifests={createRuntimeManifestRegistry()}
            onNavigate={navigate}
            onOpenPageInvocation={(invocation, row, pageInputs) => navigate(resolveRuntimeInvocationPath(invocation, tenantId, appCode, row, pageInputs))}
            onOpenPrint={printLauncher.open}
            onRefreshModel={async () => { await queryClient.invalidateQueries({ exact: true, queryKey: runtimePageQueryKey }); }}
            permissions={{
              granted: new Set([...permissionCodes, ...(currentUser?.permissionCodes ?? [])]),
              isSystemAdmin: Boolean(currentUser?.isAdmin || currentUser?.isTenantAdmin || currentUser?.isPlatformAdmin)
            }}
            state={{ scopes: { system: { currentUser } }, variables: {} }}
          />
          {printLauncher.dialog}
        </>
        : <PageError description="Runtime artifact metadata is invalid." />}
    </div>
  );
}

function RetryButton({ label, onClick }: { label: string; onClick: () => void }) {
  return <button className="rounded border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50" onClick={onClick} type="button">{label}</button>;
}

function resolveRuntimeInvocationPath(invocation: Record<string, unknown>, tenantId: string, appCode: string, row?: Record<string, unknown> | null, pageInputs?: Record<string, unknown>): string {
  const explicitPath = typeof invocation.path === 'string' ? invocation.path.trim() : '';
  if (explicitPath.startsWith('/')) return explicitPath;
  const pageCode = String(invocation.targetPageCode ?? invocation.pageCode ?? '').trim();
  if (!pageCode) throw new Error('Runtime page invocation requires targetPageCode or path.');
  const prefix = tenantId && appCode ? `/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin` : '';
  const query = new URLSearchParams();
  if (row && Object.keys(row).length > 0) query.set('runtimeRow', JSON.stringify(row));
  if (pageInputs && Object.keys(pageInputs).length > 0) query.set('pageInputs', JSON.stringify(pageInputs));
  const queryString = query.toString();
  return `${prefix}/pages/${encodeURIComponent(pageCode)}${queryString ? `?${queryString}` : ''}`;
}
