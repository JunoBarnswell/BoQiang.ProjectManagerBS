import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { httpClient } from '../../core/http/httpClient';
import { buildQueryString } from '../queryString';

import type {
  RuntimeExpressionFunctionCatalogResponse,
  RuntimeExpressionFunctionScope
} from './runtimeExpressionFunctions.types';

export function getRuntimeExpressionFunctionCatalog(
  scope: RuntimeExpressionFunctionScope,
  signal?: AbortSignal
): Promise<ApiEnvelope<RuntimeExpressionFunctionCatalogResponse>> {
  return httpClient.get<RuntimeExpressionFunctionCatalogResponse>(
    `/application-data-center/expression-functions${buildQueryString({ scope })}`,
    undefined,
    signal
  );
}
