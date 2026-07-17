import { getRuntimeExpressionFunctionCatalog } from '../../../api/runtime/runtimeExpressionFunctions.api';
import type {
  RuntimeExpressionFunctionCatalogResponse,
  RuntimeExpressionFunctionScope
} from '../../../api/runtime/runtimeExpressionFunctions.types';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiQuery } from '../../../core/query/useApiQuery';

export function useRuntimeExpressionFunctionCatalog(scope: RuntimeExpressionFunctionScope = 'all') {
  return useApiQuery<RuntimeExpressionFunctionCatalogResponse>({
    queryFn: ({ signal }) => getRuntimeExpressionFunctionCatalog(scope, signal).then((response) => response.data),
    queryKey: queryKeys.runtime.expressionFunctions(scope),
    staleTimeMs: 10 * 60 * 1000
  });
}
