import { getApplicationDatabaseBindingStatus } from '../../api/application-console/applicationConsole.api';
import type { ApplicationDatabaseBindingStatusDto } from '../../api/application-console/applicationConsole.types';
import { isHttpError } from '../../core/http/httpError';
import { queryKeys } from '../../core/query/queryKeys';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state/workspaceStore';

export type ApplicationDatabaseGateState =
  | 'loading'
  | 'ready'
  | 'notConfigured'
  | 'forbidden'
  | 'invalid'
  | 'unavailable';

export interface ApplicationDatabaseGateResult {
  error: unknown;
  refetch: () => Promise<unknown>;
  state: ApplicationDatabaseGateState;
  status: ApplicationDatabaseBindingStatusDto | null;
}

export function useApplicationDatabaseGate(): ApplicationDatabaseGateResult {
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const enabled = workspace?.workspaceLevel === 'application';
  const query = useApiQuery({
    enabled,
    queryFn: ({ signal }) => getApplicationDatabaseBindingStatus(signal).then((response) => response.data),
    queryKey: queryKeys.applicationConsole.databaseBindingStatus(workspace?.tenantId, workspace?.appCode),
    retry: (failureCount, error) => {
      if (isHttpError(error) && (error.status === 401 || error.status === 403)) {
        return false;
      }

      return failureCount < 1;
    },
    staleTimeMs: 30_000
  });

  if (!enabled) {
    return { error: null, refetch: query.refetch, state: 'ready', status: null };
  }

  if (query.isLoading) {
    return { error: null, refetch: query.refetch, state: 'loading', status: null };
  }

  if (query.isError || !query.data) {
    const status = isHttpError(query.error) && (query.error.status === 401 || query.error.status === 403)
      ? 'forbidden'
      : 'unavailable';
    return { error: query.error, refetch: query.refetch, state: status, status: null };
  }

  const status = query.data;
  switch (status.status) {
    case 'Ready':
      return { error: null, refetch: query.refetch, state: 'ready', status };
    case 'NotConfigured':
      return { error: null, refetch: query.refetch, state: 'notConfigured', status };
    case 'PermissionDenied':
      return { error: null, refetch: query.refetch, state: 'forbidden', status };
    case 'Unavailable':
      return { error: null, refetch: query.refetch, state: 'unavailable', status };
    case 'InvalidConfiguration':
    case 'MigrationRequired':
    default:
      return { error: null, refetch: query.refetch, state: 'invalid', status };
  }
}
